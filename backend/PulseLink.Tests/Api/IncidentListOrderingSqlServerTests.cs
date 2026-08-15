using Microsoft.EntityFrameworkCore;
using PulseLink.Core.Enums;
using PulseLink.Infrastructure.Data;
using PulseLink.Tests.Infrastructure;

namespace PulseLink.Tests.Api;

public class IncidentListOrderingSqlServerTests
{
    [LocalDbFact]
    public async Task List_OrdersByUpdatedAtUtcThenIdDescending()
    {
        var catalog = "PulseLink_OrderTest_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<SqlServerPulseLinkDbContext>()
            .UseSqlServer(LocalDb.ConnectionString(catalog))
            .Options;

        try
        {
            await using var db = new SqlServerPulseLinkDbContext(options);
            await db.Database.MigrateAsync();
            var expected = await IncidentListEndpointTests.SeedOrderingAsync(db);
            var admin = IncidentControllerFactory.Create(db, AppRoles.Admin, "admin-1");

            var page = await IncidentControllerFactory.ListAsync(admin, pageSize: 10);

            Assert.Equal(expected, page.Items.Select(i => i.Id));
            IncidentListEndpointTests.AssertMonotonicOrder(page.Items);
        }
        finally
        {
            await LocalDb.DropDatabaseAsync(catalog);
        }
    }
}
