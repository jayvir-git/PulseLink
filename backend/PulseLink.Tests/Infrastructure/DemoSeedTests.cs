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
