using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace PulseLink.Infrastructure.Data;

public static class DatabaseProvider
{
    public const string Sqlite = "Sqlite";
    public const string SqlServer = "SqlServer";

    public static string Read(IConfiguration configuration)
    {
        var value = configuration["Database:Provider"];
        if (string.IsNullOrWhiteSpace(value) || value.Equals(Sqlite, StringComparison.OrdinalIgnoreCase))
        {
            return Sqlite;
        }

        if (value.Equals(SqlServer, StringComparison.OrdinalIgnoreCase))
        {
            return SqlServer;
        }

        throw new InvalidOperationException(
            $"Unknown Database:Provider '{value}'. Use '{Sqlite}' (default) or '{SqlServer}'.");
    }

    public static bool ShouldMigrateOnStartup(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment() || configuration.GetValue("Database:MigrateOnStartup", false);
}
