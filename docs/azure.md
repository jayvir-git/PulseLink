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

### Serverless database startup

Azure SQL serverless can return error 40613 while resuming. Startup opens the SQL connection with a bounded transient retry before seeding roles and demo data. Only connection opening is retried; seed writes and clinical requests are not replayed by this startup policy. The connection is disposed with the seed scope. Database migration remains controlled by `Database:MigrateOnStartup`.

## Custom domain through Cloudflare

The demonstration uses `https://pulselink.jayvir.dev` through a Cloudflare Worker
while the application and database remain on Azure. The Worker source is
[`infra/cloudflare/worker.mjs`](../infra/cloudflare/worker.mjs). Its origin is fixed
to the Azure app; request paths, queries, authorization headers, and bodies pass
through. Redirects to the Azure origin are rewritten to the incoming hostname. The
proxy contains no application secrets.

Caching is an allowlist, and everything not on it keeps reaching the App Service.
Content-hashed bundles under `/assets` are cached at the edge and by the browser for
a year; unhashed files copied from `public/` (`.svg`, `.png`, `.ico`, `.webmanifest`,
`.woff`, `.woff2`) get an hour. The document, SPA routes and every `/api` path stay
`no-store`, as does any request carrying an `Authorization` header and any response
outside the 2xx range, so a failed deployment cannot pin a 404 at the edge. This
matters on the free tiers: without it every asset request spends App Service CPU.

To reproduce the setup using the dashboards:

1. Add the domain to Cloudflare's Free plan, preserve existing DNS records, and set
   the registrar's nameservers to the pair assigned by Cloudflare.
2. In Workers & Pages, create a Worker and replace its starter code with
   `worker.mjs`. Deploy the source and check its `workers.dev` URL.
3. Under the Worker's Domains tab, add a custom domain, selecting the zone and
   entering `pulselink` as the subdomain. Cloudflare manages that DNS record and
   its HTTPS certificate. Wait for DNS and certificate activation before testing.
4. Verify the landing page, sign-in, and incident retrieval on the custom domain.
   Azure's direct URL remains available for troubleshooting.

Run `node --test infra/cloudflare/worker.test.mjs` to check authenticated write
forwarding, redirect handling, and network failures. CI runs these tests too.
Keep the Worker on its Free plan and monitor its request allowance in Cloudflare;
the proxy does not remove Azure's own free-tier quotas or idle startup delay.
Use only fictional patient information in this demonstration.
