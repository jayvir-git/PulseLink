using Microsoft.EntityFrameworkCore;
using PulseLink.Infrastructure.Data;

namespace PulseLink.Tests.Infrastructure;

public class LoadTestSeederTests
{
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
        }
        finally
        {
            try { File.Delete(path); } catch { /* temp */ }
        }
    }
}
