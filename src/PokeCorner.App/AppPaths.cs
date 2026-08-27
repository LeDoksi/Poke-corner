using System;
using System.IO;

namespace PokeCorner.App;

public static class AppPaths
{
    /// <summary>
    /// Everything the app owns at runtime — save file, sprite cache, animated-GIF cache and
    /// the species catalog — lives in a single "Data" folder beside the executable, i.e.
    /// &lt;InstallDir&gt;\Data. The app is deliberately self-contained on disk: nothing of its
    /// state lives in %APPDATA%, so the install folder is the whole app.
    ///
    /// The installer pre-populates this folder (sprites, animated GIFs, catalog.json), which
    /// is what makes a fresh install work fully offline on first launch with no copy step and
    /// no network call. The MSI grants the Users group write access to it, because the app
    /// itself runs unelevated and must be able to write state.json here.
    ///
    /// overrideRoot IS the data directory (not a parent to append a folder name to) — tests
    /// point it straight at a temp directory.
    /// </summary>
    public static string GetAppDataDirectory(string? overrideRoot = null) =>
        overrideRoot ?? Path.Combine(AppContext.BaseDirectory, "Data");

    public static string GetSaveFilePath(string? overrideRoot = null) =>
        Path.Combine(GetAppDataDirectory(overrideRoot), "state.json");

    public static string GetSpriteCacheDirectory(string? overrideRoot = null) =>
        Path.Combine(GetAppDataDirectory(overrideRoot), "sprites");

    /// Animated-GIF sprites, kept in their own folder rather than alongside the static PNGs
    /// because SpriteCache's on-disk naming is always "{speciesId}.png" — a GIF stored there
    /// would collide with the static sprite for the same species.
    public static string GetAnimatedSpriteCacheDirectory(string? overrideRoot = null) =>
        Path.Combine(GetAppDataDirectory(overrideRoot), "animated");
}
