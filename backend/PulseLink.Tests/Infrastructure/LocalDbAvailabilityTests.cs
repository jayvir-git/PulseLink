namespace PulseLink.Tests.Infrastructure;

public class LocalDbAvailabilityTests
{
    [Fact]
    public void RequiredSqlServer_MustBeAvailable()
    {
        Assert.True(!LocalDb.Required || LocalDb.IsAvailable,
            "PULSELINK_REQUIRE_SQLSERVER=1 requires an accessible SQL Server LocalDB instance; refusing to skip provider coverage.");
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    public void RequiredProvider_IsNeverSkipped(bool required, bool available, bool expectedSkip)
        => Assert.Equal(expectedSkip, LocalDb.ShouldSkip(required, available));

    [Fact]
    public void Detect_ReturnsFalse_WithoutConnecting_WhenNotWindows()
    {
        var connected = false;
        var available = LocalDb.Detect(isWindows: false, () =>
        {
            connected = true;
            return true;
        });

        Assert.False(available);
        Assert.False(connected);
    }

    [Fact]
    public void Detect_ReturnsFalse_WhenConnectThrowsAnyException()
    {
        Assert.False(LocalDb.Detect(
            isWindows: true,
            () => throw new PlatformNotSupportedException("LocalDB is not supported on this platform")));
    }

    [Fact]
    public void Detect_ReturnsTrue_WhenConnectSucceedsOnWindows()
    {
        Assert.True(LocalDb.Detect(isWindows: true, () => true));
    }

    [Fact]
    public void FactAttribute_DoesNotThrowDuringConstruction()
    {
        var attr = new LocalDbFactAttribute();
        if (LocalDb.Required || LocalDb.IsAvailable)
        {
            Assert.True(string.IsNullOrEmpty(attr.Skip));
        }
        else
        {
            Assert.False(string.IsNullOrEmpty(attr.Skip));
        }
    }
}
