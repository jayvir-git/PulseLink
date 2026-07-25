# PulseLink

EMS-to-hospital patient handoff platform.

Paramedics document a lightweight patient care report (PCR) in the field, advance transport status, and deliver a structured handoff summary to receiving hospital staff — with role-based access and an append-only audit trail.

## Stack

- **Backend:** ASP.NET Core 8, C#, EF Core, ASP.NET Identity, JWT
- **Frontend:** React, TypeScript, Vite
- **Database:** SQLite locally (connection string can point at Azure SQL)
- **Tests:** xUnit
- **CI:** GitHub Actions

## Quick start

### Prerequisites

- .NET 8 SDK
- Node.js 20+

### API

```bash
cd backend/PulseLink.Api
dotnet run
```

- API: http://localhost:5080
- Swagger: http://localhost:5080/swagger

### UI

```bash
cd frontend
npm install
npm run dev
```

- App: http://localhost:5173

Vite proxies `/api` to the API during local development.

### Tests

```bash
dotnet test PulseLink.sln
```

## Seeded local users

| Role | Email | Password |
|------|-------|----------|
| Paramedic | `paramedic@pulselink.demo` | `Demo123!` |
| Hospital staff | `hospital@pulselink.demo` | `Demo123!` |
| Admin | `admin@pulselink.demo` | `Demo123!` |

See [docs/demo-walkthrough.md](docs/demo-walkthrough.md) for a guided local walkthrough.

## Core workflow

1. Paramedic creates a draft incident (chief complaint, destination hospital)
2. Records vitals and interventions as normalized rows
3. Advances status: `Draft → EnRoute → OnScene → Transporting → Arrived → HandedOff`
4. Hospital staff reviews the handoff for their facility
5. Authorized users can generate a FHIR-inspired export via `GET /api/incidents/{id}/export`

## Project layout

```text
PulseLink/
  backend/
    PulseLink.Api/             HTTP + JWT + controllers
    PulseLink.Core/            Entities + status machine
    PulseLink.Infrastructure/  EF Core, Identity, seed data
    PulseLink.Tests/           Domain tests
  frontend/                    React + TypeScript UI
  docs/                        Architecture + walkthrough
```

## Design highlights

- Explicit incident status state machine with hospital-required gates
- Relational vitals/interventions (not a single JSON blob)
- Role-scoped queries (agency vs destination hospital)
- Audit events on create/update/status/export
- Integration stub export for hospital systems

More detail: [docs/architecture.md](docs/architecture.md)

## Deployment notes

The data model and connection-string configuration can target Azure SQL. Typical cloud layout: API on Azure App Service, frontend on Azure Static Web Apps, JWT signing key in App Settings or Key Vault. See the architecture doc for concrete steps.
