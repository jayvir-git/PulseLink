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
- DB: SQLite locally by default; SQL Server is a second provider (`Database:Provider`)
- Tests: xUnit (`PulseLink.Tests`)
- CI: `.github/workflows/ci.yml`

## Layout

```text
backend/PulseLink.Api|Core|Infrastructure|Tests
frontend/                 React UI
docs/                     architecture, demo-walkthrough, database, performance
```

## Seeded local users (password `Demo123!`)

- `paramedic@pulselink.demo`
- `hospital@pulselink.demo`
- `admin@pulselink.demo`

## Run locally

```bash
# API — http://localhost:5080 (SQLite default)
cd backend/PulseLink.Api && dotnet run --launch-profile http

# API against local SQL Server Docker (docker compose up -d first)
cd backend/PulseLink.Api && dotnet run --launch-profile http-sqlserver

# API against SQL Server LocalDB on Windows
cd backend/PulseLink.Api && dotnet run --launch-profile http-localdb

# UI — http://localhost:5173 (proxies /api)
cd frontend && npm run dev
```

Migrations: SQLite is the default set (`SqlitePulseLinkDbContext`, `Data/Migrations/Sqlite`). SQL Server is a separate set (`SqlServerPulseLinkDbContext`, `Data/Migrations/SqlServer`). Always pass `--context` and `--output-dir`. Commands and startup-vs-deploy migrate behavior: [docs/database.md](docs/database.md). List indexes and measured plans: [docs/performance.md](docs/performance.md).

CI (`.github/workflows/ci.yml`) runs backend tests, the frontend build, and browser tests on Ubuntu. A Windows job sets `PULSELINK_REQUIRE_SQLSERVER=1` to run HTTP contracts against disposable SQL Server LocalDB catalogs and fail if LocalDB is unavailable. Ordinary local runs use SQLite HTTP fixtures and skip optional SQL Server facts when LocalDB is absent. See `docs/testing.md`.

## Engineering guardrails

- Prefer depth over new features (no BNPL, crypto, maps, AI diagnosis, full EHR)
- Keep status rules in `IncidentStatusMachine`; destination hospital required before transport/handoff
- Preserve role scoping (Paramedic/agency, HospitalStaff/hospital, Admin)
- Match existing patterns; don’t reinvent auth or folder structure
- After meaningful changes: `dotnet test` and `npm run build` in `frontend`
- Keep public docs product-focused; do not add personal career framing to this repository
