# PulseLink — agent / contributor guide

Read this before changing the project. Product background: [README.md](README.md) and [docs/architecture.md](docs/architecture.md).

## Product scope

EMS → hospital care continuity:

1. Paramedic creates a PCR incident
2. Adds vitals / interventions (normalized tables)
3. Advances status: `Draft → EnRoute → OnScene → Transporting → Arrived → HandedOff`
4. Hospital staff sees handoffs for their hospital
5. `GET /api/incidents/{id}/export` returns a FHIR-inspired JSON Bundle
6. Audit events on create/update/status/export

## Stack

- Backend: ASP.NET Core 8, C#, EF Core, Identity + JWT
- Frontend: React + TypeScript (Vite)
- DB: SQLite locally; docs describe Azure SQL for cloud
- Tests: xUnit (`PulseLink.Tests`)
- CI: `.github/workflows/ci.yml`

## Layout

```text
backend/PulseLink.Api|Core|Infrastructure|Tests
frontend/                 React UI
docs/                     architecture, demo-walkthrough
```

## Seeded local users (password `Demo123!`)

- `paramedic@pulselink.demo`
- `hospital@pulselink.demo`
- `admin@pulselink.demo`

## Run locally

```bash
# API — http://localhost:5080
cd backend/PulseLink.Api && dotnet run --launch-profile http

# UI — http://localhost:5173 (proxies /api)
cd frontend && npm run dev
```

## Engineering guardrails

- Prefer depth over new features (no BNPL, crypto, maps, AI diagnosis, full EHR)
- Keep status rules in `IncidentStatusMachine`; destination hospital required before transport/handoff
- Preserve role scoping (Paramedic/agency, HospitalStaff/hospital, Admin)
- Match existing patterns; don’t reinvent auth or folder structure
- After meaningful changes: `dotnet test` and `npm run build` in `frontend`
- Keep public docs product-focused; do not add personal career framing to this repository
