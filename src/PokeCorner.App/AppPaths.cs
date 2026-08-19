using System;
using System.IO;

namespace PokeCorner.App;

public static class AppPaths
{
    private const string AppFolderName = "PokeCorner";

    public static string GetAppDataDirectory(string? overrideRoot = null)
    {
        var root = overrideRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(root, AppFolderName);
    }

    public static string GetSaveFilePath(string? overrideRoot = null) =>
        Path.Combine(GetAppDataDirectory(overrideRoot), "state.json");

    public static string GetSpriteCacheDirectory(string? overrideRoot = null) =>
        Path.Combine(GetAppDataDirectory(overrideRoot), "sprites");
}
