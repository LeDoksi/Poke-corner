using PokeCorner.App;
using Xunit;

namespace PokeCorner.App.Tests;

public class AppPathsTests
{
    [Fact]
    public void GetSaveFilePath_WithOverrideRoot_ReturnsPokeCornerStateJsonUnderIt()
    {
        var path = AppPaths.GetSaveFilePath(overrideRoot: @"C:\FakeRoot");

        Assert.Equal(Path.Combine(@"C:\FakeRoot", "PokeCorner", "state.json"), path);
    }

    [Fact]
    public void GetSpriteCacheDirectory_WithOverrideRoot_ReturnsPokeCornerSpritesUnderIt()
    {
        var path = AppPaths.GetSpriteCacheDirectory(overrideRoot: @"C:\FakeRoot");

        Assert.Equal(Path.Combine(@"C:\FakeRoot", "PokeCorner", "sprites"), path);
    }

    [Fact]
    public void GetSaveFilePath_WithoutOverride_UsesRealApplicationDataFolder()
    {
        var expectedRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var path = AppPaths.GetSaveFilePath();

        Assert.StartsWith(expectedRoot, path);
        Assert.EndsWith(Path.Combine("PokeCorner", "state.json"), path);
    }
}
