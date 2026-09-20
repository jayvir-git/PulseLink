# PulseLink

EMS-to-hospital patient handoff platform.

Paramedics document a lightweight patient care report (PCR) in the field, advance transport status, and deliver a structured handoff summary to receiving hospital staff — with role-based access and an append-only audit trail.

## Live demo

**[pulselink.jayvir.dev](https://pulselink.jayvir.dev)**

A demonstration deployment. The API and the React production build are served from one Azure App Service running .NET 8, with Azure SQL as the database, reached through a Cloudflare Worker on a custom domain.

The landing page and the sign-in screen are public. **The clinical workspace requires an account, and the hosted demo credentials are not published here.** The seeded credentials below are for local runs only.

It runs on free tiers, so the first request after an idle period can take up to a minute while the app and its database resume; later requests are fast. Free quotas can interrupt the demo.

Use fictional patient information only. Deployment and configuration: [docs/azure.md](docs/azure.md).

## Stack

- **Backend:** ASP.NET Core 8, C#, EF Core, ASP.NET Identity, JWT
- **Frontend:** React, TypeScript, Vite
- **Database:** SQLite locally by default; SQL Server is a second provider (see [docs/database.md](docs/database.md))
- **Tests:** xUnit
- **CI:** GitHub Actions
- **Hosting (demo):** Azure App Service and Azure SQL, custom domain through a Cloudflare Worker (see [docs/azure.md](docs/azure.md))

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

### SQL Server (optional)

SQLite is the default. SQL Server is a second EF provider (`Database:Provider=SqlServer`) with its own migration set.

On Windows without Docker, LocalDB is the verified path:

```bash
sqllocaldb start MSSQLLocalDB
cd backend/PulseLink.Api
dotnet run --launch-profile http-localdb
```

Where Docker is available:

```bash
docker compose up -d
cd backend/PulseLink.Api
dotnet run --launch-profile http-sqlserver
```

`docker-compose.yml` is documented; it has not been run as part of the LocalDB verification. Provider settings, both `--context` migration commands, and startup-vs-deploy migrate: [docs/database.md](docs/database.md). List indexes: [docs/performance.md](docs/performance.md).

### Tests

```bash
dotnet test PulseLink.sln
```

List ordering is also run against SQL Server LocalDB when it is present. Those facts skip in CI (Ubuntu has no LocalDB).

## Seeded local users

| Role | Email | Password |
|------|-------|----------|
| Paramedic | `paramedic@pulselink.demo` | `Demo123!` |
| Hospital staff | `hospital@pulselink.demo` | `Demo123!` |
| Admin | `admin@pulselink.demo` | `Demo123!` |

See [docs/demo-walkthrough.md](docs/demo-walkthrough.md) for a guided local walkthrough.

## Screenshots

| Login | Paramedic incidents |
| --- | --- |
| ![Login](docs/images/login.png) | ![Paramedic incidents](docs/images/paramedic-incidents.png) |

| Incident detail + export | Hospital handoffs |
| --- | --- |
| ![Incident detail](docs/images/incident-detail.png) | ![Hospital handoffs](docs/images/hospital-handoffs.png) |

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
    PulseLink.Tests/           Domain, data, and API tests
  frontend/                    React + TypeScript UI
  docs/                        Architecture, walkthrough, database, performance
```

## Design highlights

- Explicit incident status state machine with hospital-required gates
- Relational vitals/interventions (not a single JSON blob)
- Role-scoped queries (agency vs destination hospital)
- Audit events on create/update/status/export
- Integration stub export for hospital systems

More detail: [docs/architecture.md](docs/architecture.md). List query indexes: [docs/performance.md](docs/performance.md).

## Deployment notes

The data model supports SQLite (local default) and SQL Server. The hosted demonstration serves the API and the React build from a single Windows App Service running .NET 8, with Azure SQL as its database and a Cloudflare Worker providing the custom HTTPS domain; `scripts/package-azure.ps1` builds the deployment ZIP. Required application settings, free-tier caveats and the custom-domain steps: [docs/azure.md](docs/azure.md). Provider settings and migrations: [docs/database.md](docs/database.md).
