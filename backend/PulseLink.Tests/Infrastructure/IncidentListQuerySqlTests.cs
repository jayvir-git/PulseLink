using Microsoft.EntityFrameworkCore;
using PulseLink.Api.Services;
using PulseLink.Infrastructure.Data;
using Xunit.Abstractions;

namespace PulseLink.Tests.Infrastructure;

public class IncidentListQuerySqlTests(ITestOutputHelper output)
{
    [Fact]
    public void Sqlite_OrdersAndPagesInSql()
    {
        using var db = new SqlitePulseLinkDbContext(
            new DbContextOptionsBuilder<SqlitePulseLinkDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options);

        var sql = IncidentListQuery
            .ApplyOrderAndPage(db.Incidents.AsNoTracking(), page: 1, pageSize: 50)
            .ToQueryString();

        output.WriteLine(sql);
        Assert.Contains("\"UpdatedAtUtc\"", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LIMIT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DateTimeOffset", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlServer_OrdersAndPagesInSql()
    {
        using var db = new SqlServerPulseLinkDbContext(
            new DbContextOptionsBuilder<SqlServerPulseLinkDbContext>()
                .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PulseLink_QueryShape;Trusted_Connection=True")
                .Options);

        var sql = IncidentListQuery
            .ApplyOrderAndPage(db.Incidents.AsNoTracking(), page: 1, pageSize: 50)
            .ToQueryString();

        output.WriteLine(sql);
        Assert.Contains("[UpdatedAtUtc]", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OFFSET", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FETCH NEXT", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SqlServer_ParamedicFilter_UsesUnionNotUnionAll()
    {
        using var db = new SqlServerPulseLinkDbContext(
            new DbContextOptionsBuilder<SqlServerPulseLinkDbContext>()
                .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PulseLink_QueryShape;Trusted_Connection=True")
                .Options);

        var sql = IncidentRoleQueries
            .ForParamedic(db.Incidents.AsNoTracking(), Guid.NewGuid(), "user-1")
            .ToQueryString();

        output.WriteLine(sql);
        Assert.Contains("UNION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UNION ALL", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sqlite_ParamedicFilter_UsesUnionNotUnionAll()
    {
        using var db = new SqlitePulseLinkDbContext(
            new DbContextOptionsBuilder<SqlitePulseLinkDbContext>()
                .UseSqlite("Data Source=:memory:")
                .Options);

        var sql = IncidentRoleQueries
            .ForParamedic(db.Incidents.AsNoTracking(), Guid.NewGuid(), "user-1")
            .ToQueryString();

        output.WriteLine(sql);
        Assert.Contains("UNION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UNION ALL", sql, StringComparison.OrdinalIgnoreCase);
    }
}
