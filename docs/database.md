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

In Development, the API still calls `Database.MigrateAsync()` during seed so a deleted `pulselink.db` (or a fresh Docker database) comes up on `dotnet run`. That is the local developer path.

It is not used as the production default. Concurrent app instances can race on the history table, and schema changes then run with the application's database credentials on every process start.

Production should apply migrations as its own step, then start the API:

1. Run `dotnet ef database update` (or an equivalent migrate job / CI step) against the target database, using the matching `--context`.
2. Start the API with `Database:Provider=SqlServer` (or SQLite) and `ASPNETCORE_ENVIRONMENT` not Development.

To force startup migrate outside Development (a single-instance box, a one-off recover), set `Database:MigrateOnStartup` to `true`. Leave it unset in production.
