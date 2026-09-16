# API integration tests

Run the backend suite from the repository root with `dotnet test PulseLink.sln`.
Run the HTTP fixture checks alone with:

```powershell
dotnet test PulseLink.sln --filter FullyQualifiedName~IncidentHttpPipelineTests
```

Run the hospital visibility regression and related access checks with:

```powershell
dotnet test PulseLink.sln --filter FullyQualifiedName~IncidentVisibilityHttpTests
```

Run the incident-state regression checks with:

```powershell
dotnet test PulseLink.sln --filter FullyQualifiedName~IncidentStateHttpTests
```

Run stale-client, forced-race, and migration-upgrade checks with:

```powershell
dotnet test PulseLink.sln --filter FullyQualifiedName~IncidentConcurrencyHttpTests
```

Run intervention retry checks with:

```powershell
dotnet test PulseLink.sln --filter FullyQualifiedName~InterventionRetryHttpTests
```

`IncidentApiFactory` starts the real API pipeline with `WebApplicationFactory<Program>`.
By default each factory creates and migrates a uniquely named SQLite in-memory
database, keeps it alive with a dedicated connection, and disposes it with the host.
SQL Server cases instead create a unique
`PulseLink_ConcurrencyTest_<guid>` LocalDB catalog and drop it after host disposal.
Request contexts use separate connections. Tests seed synthetic agencies,
hospitals, and incidents directly into that database.

Before application startup, the factory replaces the `PulseLinkDbContext`
registration used by controllers and Identity with its isolated provider context.
It also overrides database configuration, disables startup migration and load-test
seeding, and uses the `Testing` environment. The normal startup seeder still runs:
it creates roles in the isolated database, then finds the test agencies and skips
demo organization and user creation. Merely changing the environment would not
disable that seeder. No application database connection is needed.

The test-only authentication handler recognizes a fixed set of actor names from
`X-Test-Actor`. It supplies identities for two agencies, two hospitals, an admin,
and hospital staff with missing or malformed affiliation. It is registered only
by the test project. These tests exercise routing, HTTP authorization, controller
execution, EF queries, and serialization; they do **not** prove JWT signature,
issuer, audience, expiration, or login behavior.

HTTP checks cover anonymous reads and writes, all five hospital write endpoints,
isolation between two hosts including startup seeding, and permitted paramedic/admin
writes. The visibility suite covers list/detail/export across both hospitals and
all six statuses, absent or malformed affiliation, paramedic agency-or-creator
access, admin access, missing records, and export auditing. Existing direct controller
tests remain useful but bypass authentication and authorization middleware.

Before the visibility fix, the new visibility suite produced 10 failures and 6
passes: all six early-status detail/export cases returned 200 instead of 403,
both hospital consistency cases exposed the same gap, and both invalid-affiliation
cases incorrectly listed incidents with null destinations. These were response
assertion failures after successful HTTP requests, not fixture startup failures.
The fix shares a single hospital predicate between list and item access; the
documented response contract is in [architecture.md](architecture.md).

The state suite verifies destination requirements during edits, controlled errors
for nonexistent hospitals on create/update, valid destination changes, immutability
after handoff, and the full forward lifecycle. Rejected writes compare persisted
clinical fields, timestamps, and audit/child counts before and after the request.
Before the state fix, 6 cases failed and 8 passed: two destination-clearing edits
returned 200, and four nonexistent-hospital requests threw foreign-key exceptions
from controller saves. The fixture itself started successfully in those cases.

The SQL Server ordering test uses a unique `PulseLink_OrderTest_<guid>` LocalDB
database and drops it afterward; it skips when LocalDB is unavailable. The HTTP
concurrency and intervention retry suites also use LocalDB facts and skip when it is
unavailable for an ordinary local run. Setting `PULSELINK_REQUIRE_SQLSERVER=1`
makes LocalDB mandatory and selects SQL Server for default HTTP fixtures, including
visibility, authorization, state validation, and retry-key validation. Explicit
SQLite cases remain SQLite. Required mode never skips SQL Server facts and includes
a readiness assertion with a clear failure message.
The readiness assertion was also run inside the restricted sandbox, where LocalDB
was inaccessible: it failed with the required-provider message and zero skipped
tests, confirming that an unavailable provider cannot produce a successful check.
Separate translation checks verify that both EF providers put the hospital and
status restrictions in SQL. These do not execute the SQL Server visibility query
and alone are not evidence of SQL Server HTTP visibility behavior; required-provider
HTTP runs execute those contracts on SQL Server.

## Required provider CI

The Ubuntu job runs the default SQLite HTTP fixtures, backend tests, frontend build,
and browser checks. The `sqlserver-test` job uses Windows 2022, starts LocalDB,
and runs the entire backend suite with `PULSELINK_REQUIRE_SQLSERVER=1`. Both jobs
upload TRX results even on failure. Windows 2022's [runner inventory](https://github.com/actions/runner-images/blob/main/images/windows/Windows2022-Readme.md)
includes the LocalDB runtime. No production connection string or database secret is
used: every SQL fixture creates and drops its own GUID-named catalog.

Reproduce the SQL Server job locally from the repository root in PowerShell:

```powershell
$env:PULSELINK_REQUIRE_SQLSERVER = '1'
try { dotnet test PulseLink.sln --configuration Release --verbosity minimal }
finally { Remove-Item Env:PULSELINK_REQUIRE_SQLSERVER }
```

The workflow is configured in source; a hosted Actions run requires pushing these
changes and has not been performed locally. Branch protection requiring this job
is a separate repository setting and has not been changed.

Concurrency regressions first demonstrated a versionless HTTP write returning 200
and two contexts committing a handoff followed by a stale clinical write. The
regression command failed both assertions before implementation. Tests now cover
all four versioned operations, missing/malformed versions, and stale clients whose
requests arrive after a prior save finishes.

The race tests use a test-only `SaveChangesInterceptor` to pause the first request
after its validation but before saving. A second request commits, then the test
releases the first. No sleeps or assumptions about simultaneous task scheduling
are needed. The suite tests handoff winning against edits and both child types,
and a clinical append winning against handoff, on both providers. It verifies the
loser's fields, children, and audit are absent. Separate-context tests also exercise
EF's concurrency exception directly. SQLite contention response mapping is tested
with injected raw and wrapped provider exceptions, not a measured lock timeout.

Migration tests downgrade only their disposable database to the preceding schema,
preserving synthetic incidents, then upgrade and verify distinct nonempty versions,
preserved incident data, a successful first edit after upgrade, and no pending
model changes on both providers. That first-edit check caught and corrected a
SQLite GUID text-casing mismatch in the initial migration backfill. Bulk-seed tests
also verify independent nonempty versions: bulk inserts bypass EF and must include
the token explicitly.

Intervention regressions initially demonstrated two distinct failures: retrying
with the original version returned 412 after a successful commit, while retrying
with a refreshed version inserted a second intervention. The retry suite now checks
sequential and controlled concurrent duplicates, different-content key reuse,
authorization and actor/incident scoping, validation failures, and expired keys.
A client handler discards a successful response after the API commits, then retries
with the original key and version. A replacement WebApplicationFactory host using
the same database verifies persistent replay after handoff and version changes;
this is host replacement, not an operating-system process restart test.

Both providers also execute a command interceptor that throws after an operation
INSERT executes but before commit. Assertions verify rollback of the operation,
intervention, audit, and incident version, followed by a successful same-key retry.
A direct duplicate insert verifies the database unique constraint independently
of the controller lookup. These checks use isolated synthetic data only.

## Browser conflict and retry recovery

From `frontend`:

```powershell
npm ci
npx playwright install chromium
npm run build
npm run test:browser
```

The nine browser tests serve the production build on an ephemeral loopback port
and intercept every API call with synthetic responses. They verify outgoing version
headers, pending/disabled controls, retained inputs, explicit reload, no automatic
retry, resubmission with the refreshed version, and disabled retained inputs after
handoff. They also check that export does not silently refresh the write version.
They do not exercise real JWT validation or a live API. Set `PULSELINK_BROWSER_PATH`
only to use an already installed Chromium executable instead of Playwright's default.
Two retry cases simulate a committed response being lost and a busy response. They
check identical key/payload/version on explicit retry, frozen inputs while uncertain,
and a new key plus refreshed version for a deliberate subsequent intervention.
Three list tests use 55 synthetic records to verify first/last-page navigation,
explicit retry versus empty results, and a held older response released after a
newer navigation completes. The initial pagination test failed against the previous
build because it discarded page metadata and offered no page controls.
The ninth test reproduces direct client-side navigation with an uncertain intervention:
the old retry button must disappear and the new incident gets independent state.
Before the fix, the previous incident's retry remained visible on the new detail page.

## Live API smoke check

From `frontend`, build the API and UI, install Chromium, then run the live harness:

```powershell
dotnet build ../PulseLink.sln --configuration Release
npm ci
npx playwright install chromium
npm run build
npm run test:live
```

The harness launches the built API on a dynamically assigned loopback port with
a generated temporary SQLite database and a temporary signing key. It authenticates
through real seeded-user login, verifies anonymous rejection and hospital draft
denial, stale vitals and handoff conflicts, creates more than 50 records, and checks
both list pages. It terminates and restarts the actual API process using the same
temporary database, logs in again, and replays the original intervention key and
version after handoff. The result is unchanged, with one intervention and no extra
audit or version change. It stops the process and deletes only its generated
database files on completion. Tokens and response payloads are not logged.

The same harness also serves the production frontend through a loopback proxy to
that real API and opens two Chromium tabs. It signs in through the login screen,
reproduces a stale vital append, verifies retained input after explicit reload,
discards a successful intervention response after commit, and uses **Retry same
intervention** with the identical key, payload, and version. One tab then hands off
while the other retains an older version; the losing tab reloads to disabled inputs.
Persisted vital/intervention/audit counts verify that the rejected and replayed
requests did not add effects. Finally, the UI navigates beyond page one and back.
Only the lost response is injected; API results are not mocked in this walkthrough.

This checks real login and bearer-token acceptance, not the full matrix of invalid
signatures, issuers, audiences, and expiry. The nine mocked browser checks remain
separate and exercise additional UI failures and response orderings.
CI runs this smoke check on Ubuntu after the Release build.

The live pagination setup initially encountered a create failure. A deterministic
HTTP regression filled all 8,999 values in the legacy daily random-number range;
the next create failed with the incident-number unique constraint. Creation now
uses the full incident GUID for the number, without changing existing numbers or
the schema. This regression runs on SQLite normally and SQL Server in required mode.

## Verification record (September 15, 2026)

- Baseline at `20facf0`: 42 backend tests passed, 1 LocalDB ordering test skipped;
  frontend build passed.
- After the visibility fix: 68 backend tests passed, none failed, and the same
  LocalDB ordering test skipped because LocalDB was unavailable to the test process.
- After the state-validation fix: 82 backend tests passed, none failed, and the
  same LocalDB ordering test skipped. All 14 new state HTTP cases passed.
- After the version/concurrency changes: 108 backend tests passed, none failed or
  skipped, with the suite run outside the sandbox so LocalDB was accessible.
- All 3 browser conflict-recovery tests passed. The retained-input handoff screen
  was also visually inspected.
- After intervention retry changes: 123 backend tests passed, none failed or
  skipped, including SQLite and SQL Server LocalDB. All 5 browser tests passed.
- After pagination and provider CI changes: 128 backend tests passed in default
  mode and 128 passed with `PULSELINK_REQUIRE_SQLSERVER=1`, both Release runs with
  zero failures or skips. All 8 browser tests and the frontend build passed.
  An initial required-provider run caught a SQLite-only fixture assertion; it was
  corrected to verify the selected provider and isolated catalog. Hosted CI and
  the manual live walkthrough have not been run.
- `npm run build` passed using the installed npm launcher outside the sandbox.
- Initial milestone 0–2 checks used disposable SQLite databases; milestone 3 also
  exercised disposable SQL Server LocalDB catalogs outside the sandbox, as did
  milestone 4. JWT validation remains outside this verification. Pagination is
  covered by mocked browser tests in milestone 5.
- Adding the pinned Playwright development dependency reported 3 existing high
  severity npm audit findings in `nanoid`, `react-router`, and `react-router-dom`.
  Their locked versions were unchanged by this milestone; no automatic dependency
  upgrades were applied. See `npm audit` for current advisories and affected modes.

## Follow-up verification (September 16, 2026)

- Fixed a reproduced cross-incident retry-state leak by keying detail state to
  the incident ID. All 9 browser checks passed; frontend build passed.
- Reproduced the legacy incident-number collision deterministically, then replaced
  the four-digit suffix with the full incident GUID. All 129 backend tests passed
  in Release with zero skips. The new regression also passed separately in required
  SQL Server mode; the preceding complete required-provider run had 128 passes.
- The real API smoke check passed with seeded-user login, 55 additional synthetic
  drafts, stale-write rejection, and intervention replay after process restart.
  Its temporary database was removed afterward. Hosted Actions and the manual
  two-browser walkthrough remain unexecuted; the automated browser and live API
  checks run separately.

The live harness was subsequently extended to the two-tab browser walkthrough
described above and passed against the real API. The frontend build also passed.
The complete required SQL Server Release suite then passed all 129 tests with zero
failures or skips, bringing the final provider verification up to date.
This automated walkthrough replaces the earlier verification gap; no human-operated
demo or hosted Actions run is claimed.
