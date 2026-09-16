# Database

PulseLink supports two EF Core providers. SQLite is the default so the local quick start needs no extra services. SQL Server is the second provider for environments that need it (local Docker or Azure SQL).

## Provider selection

`Database:Provider` in configuration:

| Value | Connection string | When to use |
| --- | --- | --- |
| `Sqlite` (default) | `ConnectionStrings:Default` (or `Sqlite`) | Local development, CI, demos |
| `SqlServer` | `ConnectionStrings:SqlServer` | Docker SQL Server, LocalDB, Azure SQL |

Unknown values fail at startup. The zero-setup path is unchanged: `dotnet run` in `backend/PulseLink.Api` uses SQLite and `Data Source=pulselink.db`.

Local SQL Server via Docker:

```bash
docker compose up -d
cd backend/PulseLink.Api
dotnet run --launch-profile http-sqlserver
```

On Windows without Docker, SQL Server Express LocalDB works instead:

```bash
sqllocaldb start MSSQLLocalDB
cd backend/PulseLink.Api
dotnet run --launch-profile http-localdb
```

`appsettings.Development.json` contains a non-secret local `sa` password for the compose file. Do not reuse it outside this machine. The LocalDB profile uses Windows authentication and does not store a password.

**Verification status:** SQL Server was verified on this machine with LocalDB (`http-localdb`). `docker-compose.yml` is documented for environments that have Docker; that path has not been run here.

## Provider SQL differences

EF Core generates different store types per provider. Two current examples:

- `DateTimeOffset` is `TEXT` on SQLite and `datetimeoffset` on SQL Server. SQLite cannot `ORDER BY` that type in SQL, which is why `Incident.UpdatedAtUtc` (`DateTime` / UTC) exists as the list-ordering column.
- `decimal` is `TEXT` on SQLite and a precision/scale numeric on SQL Server. `VitalSign.SpO2` is `decimal(5,2)` and `TemperatureC` is `decimal(4,1)` on SQL Server. Changing those annotations is a real `ALTER COLUMN` there and typically a no-op in the SQLite migration, because SQLite still stores the value as `TEXT`.

## Two migration sets

SQLite and SQL Server generate different SQL (column types, identity, DateTimeOffset storage). The sets live in separate folders and namespaces and are bound to different DbContext types so EF cannot apply the wrong one.

| Provider | Context | Folder | Namespace |
| --- | --- | --- | --- |
| SQLite (default) | `SqlitePulseLinkDbContext` | `backend/PulseLink.Infrastructure/Data/Migrations/Sqlite` | `PulseLink.Infrastructure.Data.Migrations.Sqlite` |
| SQL Server | `SqlServerPulseLinkDbContext` | `backend/PulseLink.Infrastructure/Data/Migrations/SqlServer` | `PulseLink.Infrastructure.Data.Migrations.SqlServer` |

Always pass `--context` and `--output-dir`. Run from the repository root after `dotnet tool restore`.

Add a migration:

```bash
# SQLite (default)
dotnet ef migrations add <Name> \
  --context SqlitePulseLinkDbContext \
  --project backend/PulseLink.Infrastructure \
  --startup-project backend/PulseLink.Api \
  --output-dir Data/Migrations/Sqlite

# SQL Server
dotnet ef migrations add <Name> \
  --context SqlServerPulseLinkDbContext \
  --project backend/PulseLink.Infrastructure \
  --startup-project backend/PulseLink.Api \
  --output-dir Data/Migrations/SqlServer
```

Apply to an empty database (also the deploy-time command):

```bash
# SQLite (default)
dotnet ef database update \
  --context SqlitePulseLinkDbContext \
  --project backend/PulseLink.Infrastructure \
  --startup-project backend/PulseLink.Api

# SQL Server (uses ConnectionStrings:SqlServer — Docker localhost,1433 by default)
dotnet ef database update \
  --context SqlServerPulseLinkDbContext \
  --project backend/PulseLink.Infrastructure \
  --startup-project backend/PulseLink.Api
```

For LocalDB, set `ConnectionStrings__SqlServer` first (the `http-localdb` launch profile already does this):

```bash
# PowerShell
$env:ConnectionStrings__SqlServer = "Server=(localdb)\\MSSQLLocalDB;Database=PulseLink;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
dotnet ef database update --context SqlServerPulseLinkDbContext --project backend/PulseLink.Infrastructure --startup-project backend/PulseLink.Api
```

## Startup migrate vs deploy-time migrate

`AddIncidentVersion` adds an application-managed GUID concurrency token in both
migration sets and initializes existing incidents with independent values. Apply
the matching migration before deploying the version-aware API. Its update, status,
vital, and intervention endpoints require the `If-Match` contract described in
[architecture.md](architecture.md#incident-write-versions); older clients receive
428 until they send the loaded incident version. Migration upgrades were exercised
on disposable SQLite and SQL Server LocalDB data, not on the application database.

`AddInterventionOperations` adds the persistent intervention retry records in both
providers. A unique `(ActorUserId, IncidentId, Key)` index enforces request identity;
restricting foreign keys link each record to its incident and intervention. Apply
this migration before deploying the retry-aware API and update clients to send
`Idempotency-Key`. Successful intervention responses now contain a stable operation
result rather than incident detail. Records expire for replay after 24 hours but
remain stored to reject expired-key reuse; no automatic pruning is implemented.
The synthetic load-test reset deletes these dependent records before interventions.
These migrations were exercised only in disposable test databases.

In Development, the API still calls `Database.MigrateAsync()` during seed so a deleted `pulselink.db` (or a fresh Docker database) comes up on `dotnet run`. That is the local developer path.

It is not used as the production default. Concurrent app instances can race on the history table, and schema changes then run with the application's database credentials on every process start.

Production should apply migrations as its own step, then start the API:

1. Run `dotnet ef database update` (or an equivalent migrate job / CI step) against the target database, using the matching `--context`.
2. Start the API with `Database:Provider=SqlServer` (or SQLite) and `ASPNETCORE_ENVIRONMENT` not Development.

To force startup migrate outside Development (a single-instance box, a one-off recover), set `Database:MigrateOnStartup` to `true`. Leave it unset in production.

## Load-test seed (not the demo seed)

For index and plan measurement only. Not used in CI or the normal `dotnet run` path.

```bash
cd backend/PulseLink.Api
dotnet run --launch-profile http-localdb -- --seed-load-test --capture-load-test-plans
```

The seeder bulk-inserts, so it sets `Incident.UpdatedAtUtc` explicitly (SaveChanges sync does not run). After seed it asserts no default timestamps and a spread across many days. Plans and numbers: [performance.md](performance.md).
