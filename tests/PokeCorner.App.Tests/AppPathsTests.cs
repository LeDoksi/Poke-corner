using PokeCorner.App;
using Xunit;

namespace PokeCorner.App.Tests;

public class AppPathsTests
{
    [Fact]
    public void GetSaveFilePath_WithOverrideRoot_ReturnsStateJsonDirectlyUnderIt()
    {
        var path = AppPaths.GetSaveFilePath(overrideRoot: @"C:\FakeRoot");

        Assert.Equal(Path.Combine(@"C:\FakeRoot", "state.json"), path);
    }

    [Fact]
    public void GetSpriteCacheDirectory_WithOverrideRoot_ReturnsSpritesDirectlyUnderIt()
    {
        var path = AppPaths.GetSpriteCacheDirectory(overrideRoot: @"C:\FakeRoot");

        Assert.Equal(Path.Combine(@"C:\FakeRoot", "sprites"), path);
    }

    [Fact]
    public void GetAnimatedSpriteCacheDirectory_WithOverrideRoot_ReturnsAnimatedDirectlyUnderIt()
    {
        var path = AppPaths.GetAnimatedSpriteCacheDirectory(overrideRoot: @"C:\FakeRoot");

        Assert.Equal(Path.Combine(@"C:\FakeRoot", "animated"), path);
    }

    /// The app keeps ALL of its state next to its own executable rather than in %APPDATA%,
    /// so that an install directory is the entire app. This is the test that fails if anyone
    /// reintroduces a per-user location.
    [Fact]
    public void GetSaveFilePath_WithoutOverride_LivesInDataFolderBesideTheExecutable()
    {
        var path = AppPaths.GetSaveFilePath();

        Assert.StartsWith(AppContext.BaseDirectory, path);
        Assert.EndsWith(Path.Combine("Data", "state.json"), path);
    }

    [Fact]
    public void GetSaveFilePath_WithoutOverride_IsNotUnderApplicationData()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        Assert.DoesNotContain(appData, AppPaths.GetSaveFilePath());
    }
}
