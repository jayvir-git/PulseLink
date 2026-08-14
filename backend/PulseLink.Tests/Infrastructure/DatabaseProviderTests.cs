using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PulseLink.Infrastructure.Data;

namespace PulseLink.Tests.Infrastructure;

public class DatabaseProviderTests
{
    [Fact]
    public void Read_MissingValue_DefaultsToSqlite()
    {
        Assert.Equal(DatabaseProvider.Sqlite, DatabaseProvider.Read(Config()));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Read_BlankOrWhitespace_DefaultsToSqlite(string value)
    {
        Assert.Equal(DatabaseProvider.Sqlite, DatabaseProvider.Read(Config(("Database:Provider", value))));
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("Sqlite")]
    [InlineData("SQLITE")]
    public void Read_Sqlite_IsCaseInsensitive(string value)
    {
        Assert.Equal(DatabaseProvider.Sqlite, DatabaseProvider.Read(Config(("Database:Provider", value))));
    }

    [Theory]
    [InlineData("sqlserver")]
    [InlineData("SqlServer")]
    [InlineData("SQLSERVER")]
    public void Read_SqlServer_IsCaseInsensitive(string value)
    {
        Assert.Equal(DatabaseProvider.SqlServer, DatabaseProvider.Read(Config(("Database:Provider", value))));
    }

    [Fact]
    public void Read_UnknownValue_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => DatabaseProvider.Read(Config(("Database:Provider", "postgres"))));

        Assert.Contains("postgres", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShouldMigrateOnStartup_Development_IsTrue()
    {
        Assert.True(DatabaseProvider.ShouldMigrateOnStartup(
            Env(Environments.Development),
            Config()));
    }

    [Fact]
    public void ShouldMigrateOnStartup_ProductionWithoutFlag_IsFalse()
    {
        Assert.False(DatabaseProvider.ShouldMigrateOnStartup(
            Env(Environments.Production),
            Config()));
    }

    [Fact]
    public void ShouldMigrateOnStartup_ProductionWithFlag_IsTrue()
    {
        Assert.True(DatabaseProvider.ShouldMigrateOnStartup(
            Env(Environments.Production),
            Config(("Database:MigrateOnStartup", "true"))));
    }

    [Fact]
    public void ShouldMigrateOnStartup_ProductionWithFlagFalse_IsFalse()
    {
        Assert.False(DatabaseProvider.ShouldMigrateOnStartup(
            Env(Environments.Production),
            Config(("Database:MigrateOnStartup", "false"))));
    }

    private static IConfiguration Config(params (string Key, string Value)[] values)
    {
        var pairs = values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value));
        return new ConfigurationBuilder().AddInMemoryCollection(pairs).Build();
    }

    private static IHostEnvironment Env(string name) =>
        new FakeHostEnvironment { EnvironmentName = name };

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "PulseLink.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
