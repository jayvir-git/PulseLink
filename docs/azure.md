# Azure demo deployment

PulseLink can serve the React production build and API from one Windows App Service
running .NET 8, with Azure SQL as its database. The free App Service tier is suitable
for a demo; it has daily CPU limits and can sleep when idle.

## Package

Run `./scripts/package-azure.ps1` from PowerShell with Node.js and .NET 8 on PATH.
It builds both projects and writes a uniquely named ZIP under `artifacts/`.
The ZIP contains the API at its root and the React build in `wwwroot`.
SPA routes work directly; unknown `/api` routes return 404.

## Hosted configuration

Set these App Service application settings before starting the app:

- `ASPNETCORE_ENVIRONMENT=Production`
- `Database__Provider=SqlServer`
- `ConnectionStrings__SqlServer`: an encrypted Azure SQL connection string with
  certificate validation enabled and credentials scoped to the demo database.
- `Jwt__Key`: a cryptographically generated secret of at least 32 bytes.
- `Demo__Password`: a unique strong password for the three initial demo accounts.
- `Database__SeedLoadTest=false`

Apply the SQL Server EF migrations before startup, or explicitly set
`Database__MigrateOnStartup=true` for the initial single-instance demo deployment.
Disable startup migrations after initialization. Never enable load-test seeding on
the hosted app. The hosted login form starts empty and does not advertise a password.
The bootstrap password is only used when initializing a fresh database; changing
this setting does not reset existing users' passwords.

Use HTTPS-only, disable FTP and basic publishing credentials, and restrict database
firewall access to the app's outbound addresses. Keep deployment credentials and
connection strings outside source control.

## Free allowance

Verify availability on the selected subscription and region before provisioning.
Select Windows App Service F1 and the Azure SQL free offer with
`freeLimitExhaustionBehavior=AutoPause`. Do not opt into paid continuation when the
monthly SQL allowance is exhausted. Free quotas can interrupt the demo; increasing
the App Service tier or changing SQL's exhaustion behavior can incur charges.

After deployment, check `/health`, sign in, create a synthetic incident, and confirm
the receiving hospital can see it after transport begins. Use synthetic data for
this demonstration deployment.
