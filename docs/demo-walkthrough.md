# PulseLink local demo walkthrough

Password for all seeded users: `Demo123!`

## 1) Paramedic path

1. Sign in as `paramedic@pulselink.demo`
2. Click **New incident**
3. Chief complaint: `Chest pain, onset 20 minutes`
4. Keep destination hospital selected → **Create draft**
5. Add vitals and an intervention (Aspirin)
6. Advance status: EnRoute → OnScene → Transporting → Arrived → HandedOff
7. Click **Generate FHIR-like JSON** and inspect the export payload and audit trail

## 2) Hospital view

1. Sign out, sign in as `hospital@pulselink.demo`
2. Confirm the incident appears in **Incoming handoffs**
3. Open it and review clinical summary, vitals, and interventions

## 3) Admin

1. Sign in as `admin@pulselink.demo`
2. Open **Admin** and review agencies and hospitals
3. Open Incidents and note cross-org visibility

## What this exercises

- Relational PCR model (not a single JSON blob)
- Explicit status machine with validation
- Role-based access by agency/hospital membership
- Append-only audit events
- Handoff export endpoint for receiving systems

## Reliability walkthrough with isolated synthetic data

Use a fresh database instead of an existing application database. From the repository
root in a dedicated PowerShell terminal, start a separate demo instance:

```powershell
$demoDatabase = Join-Path $env:TEMP ("pulselink-reliability-" + [guid]::NewGuid().ToString('N') + '.db')
$env:Database__Provider = 'Sqlite'
$env:ConnectionStrings__Sqlite = "Data Source=$demoDatabase"
$env:ConnectionStrings__Default = "Data Source=$demoDatabase"
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project backend/PulseLink.Api --no-launch-profile --urls http://localhost:5080
```

Stop any other listener on port 5080 first. In another terminal run `npm run dev`
from `frontend`. The fresh instance migrates and seeds only the generated temporary
database. Close the API terminal after the walkthrough to discard its environment
overrides. The temporary database remains available for inspection.

### Stale clinical write and handoff conflict

1. Sign in as the demo paramedic. Create a synthetic incident with a destination
   and advance it to Arrived. Open its detail URL in two browser tabs, A and B.
2. In B, change the vital inputs without saving. In A, add vitals. B still holds
   the older incident version.
3. Submit B's vitals. Expect a conflict, retained inputs, and disabled clinical
   write controls. **Reload latest incident** loads A's result without submitting B's
   inputs. Review before deliberately submitting again.
4. Reload both tabs. In A, mark HandedOff. Attempt another clinical append from B.
   Expect a conflict. After explicit reload, B shows HandedOff and retained inputs
   remain disabled. The rejected append creates no clinical row or successful audit.

### Lost response after a committed intervention

Use the deterministic test harness to discard the response only after a successful
commit; switching a browser offline at an arbitrary time cannot establish that order.
From the repository root:

```powershell
dotnet test PulseLink.sln --filter FullyQualifiedName~LostResponseAfterCommit
```

This commits one intervention, discards its response, then retries the original key,
payload, and version. The replay identifies the original intervention; persisted
intervention, operation, and audit counts remain one. The browser version of this
scenario is part of `npm run test:browser` in `frontend` after `npm run build`.
It shows **Retry same intervention**, checks unchanged request identity, and confirms
that another deliberate Add action uses a new key. Pending UI attempts require the
page to stay open; expired completed keys are rejected after 24 hours.

### Navigate beyond the first 50 incidents

In a third PowerShell terminal, seed 55 drafts through the isolated demo's API:

```powershell
$demoApi = 'http://localhost:5080'
$demoLogin = Invoke-RestMethod "$demoApi/api/auth/login" -Method Post -ContentType 'application/json' -Body '{"email":"paramedic@pulselink.demo","password":"Demo123!"}'
$demoHeaders = @{ Authorization = "Bearer $($demoLogin.token)" }
1..55 | ForEach-Object {
    $demoBody = @{ chiefComplaint = "Synthetic pagination incident $_" } | ConvertTo-Json
    Invoke-RestMethod "$demoApi/api/incidents" -Method Post -Headers $demoHeaders -ContentType 'application/json' -Body $demoBody | Out-Null
}
```

Return to Incidents. Verify the result count, Next, the remaining records, disabled
Next on the last page, and Previous back to the first page. An actively changing
list can shift between page requests; it is not a frozen snapshot. Automated browser
tests use exactly 55 mocked records and also verify delayed responses and list errors.
These demo commands are instructions, not a claim that a live demo was run.

For an automated live API version, build Release and run `npm run test:live` from
`frontend` (see [testing.md](testing.md#live-api-smoke-check)). It creates its own
temporary database, signs in through the real API, exercises stale writes and
pagination, and verifies intervention replay across an actual process restart.
The harness also signs in through the built frontend and executes the stale-write,
lost-response retry, handoff conflict, and pagination flows in two Chromium tabs
against that real API. Install Chromium and build the frontend as described in the
testing guide. The nine mocked browser checks cover additional failure scenarios.
