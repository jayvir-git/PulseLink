using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PulseLink.Core.Entities;
using PulseLink.Core.Enums;
using PulseLink.Infrastructure.Data;
using PulseLink.Tests.Infrastructure;

namespace PulseLink.Tests.Api;

// Each factory owns an in-memory SQLite database or unique disposable LocalDB
// catalog. Request contexts use separate connections.
internal sealed class IncidentApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection? keeper;
    private readonly string? sqlCatalog;
    private readonly IInterceptor[] interceptors;
    private int databaseDisposed;

    public Guid AgencyA { get; } = Guid.NewGuid();
    public Guid AgencyB { get; } = Guid.NewGuid();
    public Guid HospitalA { get; } = Guid.NewGuid();
    public Guid HospitalB { get; } = Guid.NewGuid();

    public IncidentApiFactory(bool? sqlServer = null, params IInterceptor[] interceptors)
    {
        this.interceptors = interceptors;
        if (sqlServer ?? LocalDb.Required)
        {
            sqlCatalog = $"PulseLink_ConcurrencyTest_{Guid.NewGuid():N}";
        }
        else
        {
            keeper = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = $"pulselink-http-{Guid.NewGuid():N}",
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared,
                Pooling = false
            }.ToString());
            keeper.Open();
        }
        try
        {
            using var db = CreateDatabaseContext();
            db.Database.Migrate();
            db.Agencies.AddRange(
                new Agency { Id = AgencyA, Name = "Test EMS A", Region = "Test" },
                new Agency { Id = AgencyB, Name = "Test EMS B", Region = "Test" });
            db.Hospitals.AddRange(
                new Hospital { Id = HospitalA, Name = "Test Hospital A", City = "Test" },
                new Hospital { Id = HospitalB, Name = "Test Hospital B", City = "Test" });
            db.SaveChanges();
        }
        catch
        {
            DisposeDatabase();
            throw;
        }
    }

    public PulseLinkDbContext CreateDatabaseContext() => sqlCatalog is not null
        ? new SqlServerPulseLinkDbContext(new DbContextOptionsBuilder<SqlServerPulseLinkDbContext>()
            .UseSqlServer(LocalDb.ConnectionString(sqlCatalog)).AddInterceptors(interceptors).Options)
        : new SqlitePulseLinkDbContext(new DbContextOptionsBuilder<SqlitePulseLinkDbContext>()
            .UseSqlite(keeper!.ConnectionString).AddInterceptors(interceptors).Options);

    public async Task<Guid> SeedIncidentAsync(
        IncidentStatus status, Guid? hospitalId, Guid? agencyId = null,
        string createdBy = "paramedic-a")
    {
        await using var db = CreateDatabaseContext();
        var incident = IncidentControllerFactory.Incident(
            agencyId ?? AgencyA, createdBy, DateTimeOffset.UtcNow,
            $"TEST-{Guid.NewGuid():N}", status, hospitalId);
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();
        return incident.Id;
    }

    public HttpClient CreateClientAs(string actor)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.ActorHeader, actor);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["Database:MigrateOnStartup"] = "false",
                ["Database:SeedLoadTest"] = "false",
                ["ConnectionStrings:Default"] = keeper?.ConnectionString ?? "Data Source=:memory:",
                ["ConnectionStrings:Sqlite"] = keeper?.ConnectionString ?? "Data Source=:memory:",
                ["ConnectionStrings:SqlServer"] = "",
                ["Jwt:Key"] = "Synthetic-Test-Signing-Key-Not-For-Production-12345"
            }));
        builder.ConfigureTestServices(services =>
        {
            // Replace the base registration used by controllers AND Identity.
            // Construct options ourselves so production provider callbacks cannot run.
            services.RemoveAll<PulseLinkDbContext>();
            services.RemoveAll<SqlitePulseLinkDbContext>();
            services.RemoveAll<SqlServerPulseLinkDbContext>();
            services.AddScoped<PulseLinkDbContext>(_ => CreateDatabaseContext());
            services.AddSingleton(new TestActors(new Dictionary<string, Claim[]>
            {
                ["paramedic-a"] = Claims("paramedic-a", AppRoles.Paramedic, "agencyId", AgencyA.ToString()),
                ["paramedic-b"] = Claims("paramedic-b", AppRoles.Paramedic, "agencyId", AgencyB.ToString()),
                ["hospital-a"] = Claims("hospital-a", AppRoles.HospitalStaff, "hospitalId", HospitalA.ToString()),
                ["hospital-b"] = Claims("hospital-b", AppRoles.HospitalStaff, "hospitalId", HospitalB.ToString()),
                ["hospital-missing"] = Claims("hospital-missing", AppRoles.HospitalStaff),
                ["hospital-invalid"] = Claims("hospital-invalid", AppRoles.HospitalStaff, "hospitalId", "invalid"),
                ["admin"] = Claims("admin", AppRoles.Admin)
            }));
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SchemeName, _ => { });
        });
    }

    private static Claim[] Claims(string id, string role, string? affiliation = null, string? value = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id), new(ClaimTypes.Role, role) };
        if (affiliation is not null) claims.Add(new Claim(affiliation, value!));
        return claims.ToArray();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) DisposeDatabase();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        DisposeDatabase();
    }

    private void DisposeDatabase()
    {
        if (Interlocked.Exchange(ref databaseDisposed, 1) != 0) return;
        keeper?.Dispose();
        if (sqlCatalog is not null) LocalDb.DropDatabaseAsync(sqlCatalog).GetAwaiter().GetResult();
    }
}

internal sealed record TestActors(IReadOnlyDictionary<string, Claim[]> Identities);

// Exercises HTTP authorization with controlled identities, NOT JWT validation.
internal sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, TestActors actors)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string ActorHeader = "X-Test-Actor";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ActorHeader, out var actor))
            return Task.FromResult(AuthenticateResult.NoResult());
        if (!actors.Identities.TryGetValue(actor.ToString(), out var claims))
            return Task.FromResult(AuthenticateResult.Fail("Unknown test actor."));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
