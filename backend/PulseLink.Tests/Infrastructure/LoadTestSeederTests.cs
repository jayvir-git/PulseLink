using Microsoft.EntityFrameworkCore;
using PulseLink.Infrastructure.Data;
using PulseLink.Tests.Api;

namespace PulseLink.Tests.Infrastructure;

public class LoadTestSeederTests
{
    [LocalDbFact]
    public async Task SqlServer_BulkSeed_PreservesIndependentVersions()
    {
        await using var factory = new IncidentApiFactory(sqlServer: true);
        await using var db = factory.CreateDatabaseContext();
        await LoadTestSeeder.SeedAsync(db, LoadTestSeedOptions.ForTests);
        var incidents = await db.Incidents.ToListAsync();
        Assert.NotEmpty(incidents);
        Assert.All(incidents, i => Assert.NotEqual(Guid.Empty, i.Version));
        Assert.Equal(incidents.Count, incidents.Select(i => i.Version).Distinct().Count());
        incidents[0].ChiefComplaint = "Updated bulk seed";
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Seed_SetsUpdatedAtUtcExplicitly_AndSpreadsAcrossDays()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pulselink-loadtest-{Guid.NewGuid():N}.db");
        try
        {
            await using var db = new SqlitePulseLinkDbContext(
                new DbContextOptionsBuilder<SqlitePulseLinkDbContext>()
                    .UseSqlite($"Data Source={path}")
                    .Options);
            await db.Database.MigrateAsync();

            var result = await LoadTestSeeder.SeedAsync(db, LoadTestSeedOptions.ForTests);

            Assert.Equal(0, result.DefaultTimestampCount);
            Assert.True(result.Incidents >= LoadTestSeedOptions.ForTests.IncidentCount);
            Assert.True(result.DistinctDays >= LoadTestSeedOptions.ForTests.MinDistinctDays);
            Assert.True(result.MinUpdatedAtUtc > new DateTime(2000, 1, 1));
            Assert.True(result.MaxUpdatedAtUtc > result.MinUpdatedAtUtc);
            Assert.True((result.MaxUpdatedAtUtc - result.MinUpdatedAtUtc).TotalDays >= 20);

            var defaults = await db.Incidents
                .Where(i => i.IncidentNumber.StartsWith(LoadTestIds.NumberPrefix))
                .CountAsync(i => i.UpdatedAtUtc <= new DateTime(2000, 1, 1));
            Assert.Equal(0, defaults);
            var versions = await db.Incidents.Select(i => i.Version).ToListAsync();
            Assert.DoesNotContain(Guid.Empty, versions);
            Assert.Equal(versions.Count, versions.Distinct().Count());
        }
        finally
        {
            try { File.Delete(path); } catch { /* temp */ }
        }
    }
}
