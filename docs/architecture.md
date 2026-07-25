# PulseLink Architecture

## Purpose

PulseLink is an EMS-to-hospital care continuity application. Paramedics document a lightweight patient care report (PCR) in the field, advance transport status, and deliver a structured handoff summary to a receiving hospital.

## Stack

| Layer | Choice | Notes |
|-------|--------|-------|
| API | ASP.NET Core 8 (C#) | Controllers + JWT auth |
| Domain | `PulseLink.Core` | Entities + status state machine |
| Data | EF Core + SQLite (local) | Swap connection string to Azure SQL for cloud |
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

## Security model

- **Paramedic**: create/update incidents for their agency; record vitals/interventions; advance status
- **HospitalStaff**: view incidents destined for their hospital once transport begins
- **Admin**: full read access + organization lookup

Every create/update/status/export writes an `AuditEvent`.

## Integration surface

`GET /api/incidents/{id}/export` returns a FHIR-inspired JSON Bundle (Encounter, Condition, Observation, Procedure). This is intentionally a stub adapter pattern for hospital system integration—not a full FHIR server.

## Azure deployment notes

1. Provision Azure SQL Database (or keep SQLite only for local demos)
2. Deploy API to Azure App Service (Linux/.NET 8)
3. Store JWT signing key in Azure Key Vault / App Settings
4. Deploy frontend to Azure Static Web Apps (or App Service static site)
5. Set `VITE_API_URL` to the App Service URL
6. Update CORS origins in API configuration

Local default connection string:

```json
"ConnectionStrings": {
  "Default": "Data Source=pulselink.db"
}
```

Azure SQL example:

```json
"ConnectionStrings": {
  "Default": "Server=tcp:<server>.database.windows.net,1433;Database=pulselink;User ID=...;Password=...;Encrypt=True;"
}
```

When moving to Azure SQL, add `Microsoft.EntityFrameworkCore.SqlServer` and switch `UseSqlite` → `UseSqlServer` in `DependencyInjection.cs`.
