using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PulseLink.Infrastructure;
using PulseLink.Infrastructure.Data;
using PulseLink.Infrastructure.Identity;

namespace PulseLink.Tests.Infrastructure;

public class DemoSeedTests
{
    [Fact]
    public async Task Production_RequiresExplicitDemoPasswordBeforeCreatingOrganizations()
    {
        await using var fixture = new SeedFixture(null);
        await fixture.MigrateAsync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => DbSeeder.SeedAsync(fixture.Services));
        Assert.Contains("Demo:Password", error.Message);
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PulseLinkDbContext>();
        Assert.Empty(await db.Agencies.ToListAsync());
        Assert.Empty(await db.Users.ToListAsync());
    }

    [Fact]
    public async Task Production_SeedsOnlyWithConfiguredPassword_AndCanRestartWithoutIt()
    {
        const string password = "Synthetic-Cloud-Password-Only-123!";
        await using var fixture = new SeedFixture(password);
        await fixture.MigrateAsync();
        await DbSeeder.SeedAsync(fixture.Services);
        using var scope = fixture.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await manager.FindByEmailAsync("admin@pulselink.demo");
        Assert.NotNull(user);
        Assert.True(await manager.CheckPasswordAsync(user, password));
        Assert.False(await manager.CheckPasswordAsync(user, "Demo123!"));
        fixture.Configuration["Demo:Password"] = null;
        await DbSeeder.SeedAsync(fixture.Services);
        Assert.Equal(3, await manager.Users.CountAsync());
    }

    [LocalDbFact]
    public async Task SqlServer_StartupRecoversFromTransientConnectionFailureBeforeSeeding()
    {
        var catalog = "PulseLink_Startup_" + Guid.NewGuid().ToString("N");
        var failure = new FirstOpenFailure();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "SqlServer",
            ["ConnectionStrings:SqlServer"] = LocalDb.ConnectionString(catalog),
            ["Demo:Password"] = "Synthetic-Startup-Password-123!"
        }).Build();
        await using var services = new ServiceCollection().AddLogging()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton<IHostEnvironment>(new ProductionEnvironment())
            .AddInfrastructure(configuration)
            .AddScoped<SqlServerPulseLinkDbContext>(_ => new SqlServerPulseLinkDbContext(
                new DbContextOptionsBuilder<SqlServerPulseLinkDbContext>()
                    .UseSqlServer(LocalDb.ConnectionString(catalog)).AddInterceptors(failure).Options))
            .BuildServiceProvider();
        try
        {
            using (var scope = services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<PulseLinkDbContext>().Database.MigrateAsync();
            failure.Armed = true;
            await DbSeeder.SeedAsync(services);
            Assert.True(failure.OpenAttempts >= 2);
            using var check = services.CreateScope();
            var db = check.ServiceProvider.GetRequiredService<PulseLinkDbContext>();
            Assert.Equal(3, await db.Users.CountAsync());
            Assert.Equal(1, await db.Agencies.CountAsync());
        }
        finally { await LocalDb.DropDatabaseAsync(catalog); }
    }

    private sealed class FirstOpenFailure : DbConnectionInterceptor
    {
        public bool Armed { get; set; }
        public int OpenAttempts { get; private set; }
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            if (Armed && ++OpenAttempts == 1)
                throw new TimeoutException("Synthetic database resume delay");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SeedFixture : IAsyncDisposable
    {
        private readonly SqliteConnection keeper;
        public ServiceProvider Services { get; }
        public IConfigurationRoot Configuration { get; }
        public SeedFixture(string? password)
        {
            keeper = new SqliteConnection($"Data Source=seed-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            keeper.Open();
            Configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Sqlite"] = keeper.ConnectionString,
                ["Demo:Password"] = password
            }).Build();
            Services = new ServiceCollection().AddLogging()
                .AddSingleton<IConfiguration>(Configuration)
                .AddSingleton<IHostEnvironment>(new ProductionEnvironment())
                .AddInfrastructure(Configuration).BuildServiceProvider();
        }
        public async Task MigrateAsync()
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<PulseLinkDbContext>().Database.MigrateAsync();
        }
        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            await keeper.DisposeAsync();
        }
    }
    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "PulseLink.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
