using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PulseLink.Infrastructure.Data;

internal static class DesignTimeConfiguration
{
    public static IConfigurationRoot Load()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }
}

public class SqlitePulseLinkDbContextFactory : IDesignTimeDbContextFactory<SqlitePulseLinkDbContext>
{
    public SqlitePulseLinkDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfiguration.Load();
        var connectionString = configuration.GetConnectionString("Sqlite")
            ?? configuration.GetConnectionString("Default")
            ?? "Data Source=pulselink.db";

        var options = new DbContextOptionsBuilder<SqlitePulseLinkDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new SqlitePulseLinkDbContext(options);
    }
}

public class SqlServerPulseLinkDbContextFactory : IDesignTimeDbContextFactory<SqlServerPulseLinkDbContext>
{
    public SqlServerPulseLinkDbContext CreateDbContext(string[] args)
    {
        var configuration = DesignTimeConfiguration.Load();
        var connectionString = configuration.GetConnectionString("SqlServer")
            ?? throw new InvalidOperationException(
                "SQL Server migrations require ConnectionStrings:SqlServer (see appsettings.Development.json).");

        var options = new DbContextOptionsBuilder<SqlServerPulseLinkDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new SqlServerPulseLinkDbContext(options);
    }
}
