namespace PokeCorner.App.Tests;

public sealed class WindowsProfileTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(2u)]
    [InlineData(4u)]
    public void PersistentProfileAllowsStartup(uint flags) => WindowsProfile.EnsurePersistent(() => (true, flags, 0));

    [Theory]
    [InlineData(1u)]
    [InlineData(3u)]
    public void TemporaryProfileRefusesStartup(uint flags)
    {
        var error = Assert.ThrowsAny<IOException>(() => WindowsProfile.EnsurePersistent(() => (true, flags, 0)));
        Assert.Contains("временный профиль", error.Message);
    }

    [Fact]
    public void ProfileApiFailureRefusesStartup()
    {
        var error = Assert.ThrowsAny<IOException>(() => WindowsProfile.EnsurePersistent(() => (false, 0, 5)));
        Assert.Contains("5", error.Message);
    }
}
