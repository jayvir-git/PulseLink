namespace PulseLink.Tests.Infrastructure;

public class LocalDbAvailabilityTests
{
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
        if (OperatingSystem.IsWindows() && LocalDb.IsAvailable)
        {
            Assert.True(string.IsNullOrEmpty(attr.Skip));
        }
        else
        {
            Assert.False(string.IsNullOrEmpty(attr.Skip));
        }
    }
}
