using Microsoft.EntityFrameworkCore;
using PulseLink.Api.Services;
using PulseLink.Infrastructure.Data;
using Xunit.Abstractions;

namespace PulseLink.Tests.Infrastructure;

public class IncidentListQuerySqlTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HospitalVisibility_FiltersInSql(bool sqlServer)
    {
        // Translation only: no SQL Server connection is opened.
        using PulseLinkDbContext db = sqlServer
            ? new SqlServerPulseLinkDbContext(new DbContextOptionsBuilder<SqlServerPulseLinkDbContext>()
                .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PulseLink_QueryShape;Trusted_Connection=True").Options)
            : new SqlitePulseLinkDbContext(new DbContextOptionsBuilder<SqlitePulseLinkDbContext>()
                .UseSqlite("Data Source=:memory:").Options);
        var sql = db.Incidents
            .Where(IncidentRoleQueries.HospitalVisibility(Guid.NewGuid()))
            .Select(i => i.Id)
            .ToQueryString();

        output.WriteLine(sql);
        var whereIndex = sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
        Assert.True(whereIndex >= 0, "Hospital visibility must be filtered in SQL.");
        var where = sql[whereIndex..];
        Assert.Contains("DestinationHospitalId", where, StringComparison.Ordinal);
        Assert.Contains("Status", where, StringComparison.Ordinal);
        Assert.Contains("3", where, StringComparison.Ordinal);
        Assert.Contains("4", where, StringComparison.Ordinal);
        Assert.Contains("5", where, StringComparison.Ordinal);
    }

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
