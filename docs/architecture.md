# PulseLink Architecture

## Purpose

PulseLink is an EMS-to-hospital care continuity application. Paramedics document a lightweight patient care report (PCR) in the field, advance transport status, and deliver a structured handoff summary to a receiving hospital.

New incident numbers use `PCR-` followed by the incident's full GUID in compact
form. The previous date plus four-digit random suffix could collide during ordinary
creation. Existing numbers remain unchanged; the new format fits the existing
40-character column and is an opaque reference, not a sequence or encoded date.

## Stack

| Layer | Choice | Notes |
|-------|--------|-------|
| API | ASP.NET Core 8 (C#) | Controllers + JWT auth |
| Domain | `PulseLink.Core` | Entities + status state machine |
| Data | EF Core + SQLite (default) or SQL Server | Provider selected by `Database:Provider`; see [database.md](database.md). List indexes: [performance.md](performance.md) |
| UI | React + TypeScript (Vite) | Role-aware screens |
| Auth | ASP.NET Identity + JWT | Roles: Paramedic, HospitalStaff, Admin |
| Tests | xUnit | Status transition rules |
| CI | GitHub Actions | Restore, build, test |

## Bounded context

```text
Agency ──< Incident >── Hospital
              │
      ┌───────┼────────┐
      ▼       ▼        ▼
  VitalSign Intervention AuditEvent
```

## Status lifecycle

`Draft → EnRoute → OnScene → Transporting → Arrived → HandedOff`

Rules enforced in `IncidentStatusMachine`:

- Only forward one step at a time
- Destination hospital required before `Transporting`, `Arrived`, or `HandedOff`
- Handed-off incidents are immutable for clinical edits

`EnsureCanTransition` checks the allowed step and delegates destination validation
to `EnsureValidState`. Ordinary incident edits call the same state validation before
changing tracked fields: `Transporting` and `Arrived` incidents cannot lose their
destination. Draft, EnRoute, and OnScene incidents may still have no destination.
Changing to another existing hospital is allowed before handoff, including during
transport and after arrival; this preserves the existing destination-edit policy.
Hospital visibility follows the new destination immediately.

Create and update requests with a nonexistent destination hospital return **400**
with `message: "Destination hospital does not exist."`. Clearing a required
destination also returns **400**, using the state machine's destination-required
message. Rejected validation leaves clinical fields, timestamps, children, and
successful-operation audit events unchanged. Handed-off incidents still reject
clinical edits and appends for paramedics and admins.

These domain checks validate the state observed by one request. The version
contract below protects against stale clients and competing incident writes. The
hospital existence check is not protection against concurrent external hospital
deletion; database foreign keys remain in place.

## Incident write versions

Incident detail responses (including create, update, vitals, and status) include an
opaque GUID `version`. PUT details and POST vitals, interventions, and status
require `If-Match: "<version>"` using the version the caller actually reviewed.
Create and export do not require that header. Lists remain summaries; fetch detail
before a write. This is an incident-state version, not a version of export audit
history. Exactly one quoted GUID is accepted; weak tags, wildcards, and tag lists
are not supported.

- Missing version: **428**, `code: "version_required"`.
- Malformed version: **400**, `code: "invalid_version"`.
- Stale version or a lost save race: **412**, `code: "incident_conflict"`.
- SQLite write busy/locked or a SQL Server deadlock victim: **503**,
  `code: "incident_busy"`. Reload and review before trying again, except intervention
  retries retain their original request identity as described below. No automatic replay.

Authorization and existence checks run before the version check. Each successful
clinical edit, status change, or child append advances the parent incident version.
Export-only auditing does not. The EF context generates a fresh application-managed
GUID when the incident is modified and marks `Version` as a concurrency token for
both SQLite and SQL Server. An UPDATE matches the ID **and the original version**;
a zero-row update becomes `DbUpdateConcurrencyException` and a controlled 412.
This protects the interval between the server read and save, in addition to the
initial comparison with the client's version. Timestamps alone cannot do that.

One `SaveChangesAsync` transaction contains the incident change, any appended child,
and the successful-operation audit. A concurrency failure rolls all of them back.
The API clears the failed context and does not retry or merge automatically. A
handoff losing a race must reload and revalidate; a clinical write losing to handoff
cannot commit, even if it passed validation earlier.

The UI sends the loaded version, retains unsaved vital/intervention inputs on
conflict, and disables writes until the user explicitly reloads. Reload does not
submit anything. If the latest incident is handed off, retained inputs remain
visible but disabled. Export does not silently refresh the form's version. The
API client preserves HTTP status and structured error details through `ApiError`.

Both migration sets initialize existing rows with independent version values.
Deploy the matching migration before the API update and update API clients to send
versions. This is a breaking write contract for older clients. Version checks alone
do not prevent duplicate child creation after a lost response.

## Intervention request identity

POST interventions additionally requires `Idempotency-Key`, a nonempty UUID generated
once per intended intervention. The key is scoped to the authenticated actor, target
incident, and intervention operation. A database unique index enforces this scope.
Authorization runs before any stored result is disclosed.

A SHA-256 fingerprint covers a versioned canonical array of name, medication, dose,
route, and notes. Only the name is trimmed, matching storage; null and empty strings
remain distinct. The incident version is not part of the fingerprint. A completed
matching request returns HTTP 200 with the stable `{ incidentId, interventionId,
performedAt }` result, even after another write or handoff. It does not return a
fresh incident snapshot or repeat the mutation, audit, or version change. Clients
fetch detail separately after success. Replay lookup precedes the version and
handoff checks; a new operation still requires the reviewed `If-Match` version.

The intervention, parent version change, audit, and completed operation record
commit in one EF transaction. There is no separately committed in-progress record.
Concurrent duplicates either obtain the winning result or receive a retryable busy
response; database failures roll back all effects. Validation failures do not consume
the key. Records live in the database and survive API host replacement.

- Missing or invalid key: **400**, `idempotency_key_required`.
- Completed key with different content: **409**, `idempotency_key_reused`.
- Completed key after its 24-hour replay window: **410**, `idempotency_key_expired`.

Expired records are retained indefinitely as tombstones; there is no cleanup job.
An expired key never becomes a new intervention. Review the incident instead of
blindly submitting the uncertain operation with a fresh key.

The UI retains the original key, payload, and version in component memory while a
network or transient server failure leaves the result uncertain. It freezes inputs
and offers an explicit **Retry same intervention** action. Keep the page open during
recovery: pending attempts are not persisted across navigation or browser closure.
Detail components are keyed by incident ID so direct navigation cannot carry a
previous incident's draft, retry identity, or late response into the new incident.
After confirmed success, another Add action creates a new key and a deliberate new
intervention. A failed detail refresh after success requires reload, not another
insert. Incident creation and vital appends do not yet support this retry contract;
this is not an unlimited exactly-once guarantee for all writes.

## Incident list navigation

The dashboard retains the API's `page`, `pageSize`, and `totalCount`, displays 50
records per page, and offers previous/next controls and result counts. Loading,
failed requests with explicit retry, and empty results have separate states. A
request completing after navigation away from its page cannot replace the current
result. If a shrinking list leaves a page empty, Previous remains available.
Pagination uses offsets over the current updated-time ordering: writes can move
records between pages, so traversal is not a snapshot and may repeat or omit records.

## Security model

- **Paramedic**: create/update incidents for their agency; record vitals/interventions; advance status
- **HospitalStaff**: view incidents destined for their hospital once transport begins
- **Admin**: full read access + organization lookup

Hospital list, detail, and export access share `IncidentRoleQueries.HospitalVisibility`:
the caller must have a valid hospital affiliation matching the destination, and
the incident must be `Transporting`, `Arrived`, or `HandedOff`. Missing or malformed
affiliation never grants access to incidents without a destination. EF translates
the predicate for list filtering in SQL; detail and export evaluate the same
expression against the loaded incident. Paramedic agency-or-creator access and
admin access are unchanged.

Authenticated requests for existing inaccessible incidents return **403**; missing
incidents return **404**. This preserves the existing API contract and does not
conceal whether an incident exists. Anonymous requests return **401**. Hospital
staff cannot create, edit, append vitals/interventions, or change status. Rejected
exports do not write a `HandoffExported` audit event. HTTP coverage and test
authentication limitations are described in [testing.md](testing.md).

Every create/update/status/export writes an `AuditEvent`.

## Integration surface

`GET /api/incidents/{id}/export` returns a FHIR-inspired JSON Bundle (Encounter, Condition, Observation, Procedure). This is intentionally a stub adapter pattern for hospital system integration—not a full FHIR server.

## Azure deployment notes

1. Provision Azure SQL Database (or keep SQLite only for local demos)
2. Set `Database:Provider` to `SqlServer` and `ConnectionStrings:SqlServer` (do not rely on startup migrate in production; apply migrations as a deploy step — [database.md](database.md))
3. Deploy API to Azure App Service (Linux/.NET 8)
4. Store JWT signing key in Azure Key Vault / App Settings
5. Deploy frontend to Azure Static Web Apps (or App Service static site)
6. Set `VITE_API_URL` to the App Service URL
7. Update CORS origins in API configuration

Local SQLite (default):

```json
"Database": { "Provider": "Sqlite" },
"ConnectionStrings": {
  "Default": "Data Source=pulselink.db"
}
```

Local SQL Server (Docker) and Azure SQL examples: [database.md](database.md).
