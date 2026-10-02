
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

    [Fact]
    public void GetCryDirectory_WithOverrideRoot_ReturnsCriesDirectlyUnderIt()
    {
        var path = AppPaths.GetCryDirectory(overrideRoot: @"C:\FakeRoot");

        Assert.Equal(Path.Combine(@"C:\FakeRoot", "cries"), path);
    }

    /// Save progress is personal to the Windows account playing, unlike the shared sprite/
    /// catalog assets beside the executable — it lives in the standard per-user %APPDATA%
    /// location so two accounts on one machine never share or overwrite each other's
    /// collection, and an app update/reinstall never touches it.
    [Fact]
    public void GetSaveFilePath_WithoutOverride_LivesUnderApplicationData()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var path = AppPaths.GetSaveFilePath();

        Assert.StartsWith(appData, path);
        Assert.EndsWith(Path.Combine("PokeCorner", "state.json"), path);
    }

    [Fact]
    public void GetSaveFilePath_WithoutOverride_IsNotBesideTheExecutable()
    {
        Assert.DoesNotContain(AppContext.BaseDirectory.TrimEnd('\\'), AppPaths.GetSaveFilePath());
    }
}
