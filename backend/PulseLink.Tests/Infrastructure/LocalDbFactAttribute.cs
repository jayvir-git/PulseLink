using Microsoft.Data.SqlClient;

namespace PulseLink.Tests.Infrastructure;

/// <summary>
/// Runs only when SQL Server LocalDB answers. CI (Ubuntu) skips these facts.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class LocalDbFactAttribute : FactAttribute
{
    public LocalDbFactAttribute()
    {
        if (!LocalDb.IsAvailable)
        {
            Skip = "SQL Server LocalDB is not available.";
        }
    }
}

public static class LocalDb
{
    public const string Server = @"(localdb)\MSSQLLocalDB";

    private static readonly Lazy<bool> Available = new(Probe, LazyThreadSafetyMode.ExecutionAndPublication);

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

    private static bool Probe()
    {
        try
        {
            using var conn = new SqlConnection(ConnectionString("master"));
            conn.Open();
            return true;
        }
        catch (SqlException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
