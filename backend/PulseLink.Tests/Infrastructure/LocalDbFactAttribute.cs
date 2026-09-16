using Microsoft.Data.SqlClient;

namespace PulseLink.Tests.Infrastructure;

/// <summary>
/// Optional locally; required in the SQL Server CI job even if the server is unavailable.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class LocalDbFactAttribute : FactAttribute
{
    public LocalDbFactAttribute()
    {
        if (LocalDb.ShouldSkip(LocalDb.Required, LocalDb.IsAvailable))
        {
            Skip = "SQL Server LocalDB is not available.";
        }
    }
}

public static class LocalDb
{
    public const string Server = @"(localdb)\MSSQLLocalDB";
    public static bool Required => Environment.GetEnvironmentVariable("PULSELINK_REQUIRE_SQLSERVER") == "1";
    internal static bool ShouldSkip(bool required, bool available) => !required && !available;

    private static readonly Lazy<bool> Available = new(
        () => Detect(OperatingSystem.IsWindows(), ConnectMaster),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool IsAvailable => Available.Value;

    public static string ConnectionString(string database) =>
        $"Server={Server};Database={database};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=8";

    public static async Task DropDatabaseAsync(string database)
    {
        await using var conn = new SqlConnection(ConnectionString("master"));
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"""
            IF DB_ID(N'{database}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{database}];
            END
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Capability check used by <see cref="IsAvailable"/>. Must never throw:
    /// the fact attribute constructor runs during xUnit discovery.
    /// </summary>
    internal static bool Detect(bool isWindows, Func<bool> connect)
    {
        if (!isWindows)
        {
            return false;
        }

        try
        {
            return connect();
        }
        catch
        {
            return false;
        }
    }

    private static bool ConnectMaster()
    {
        using var conn = new SqlConnection(ConnectionString("master"));
        conn.Open();
        return true;
    }
}
