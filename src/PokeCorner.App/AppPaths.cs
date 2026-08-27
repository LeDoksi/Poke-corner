using System;
using System.IO;

namespace PokeCorner.App;

public static class AppPaths
{
    /// <summary>
    /// Shared, machine-wide assets — sprite cache, animated-GIF cache, the species catalog —
    /// live in a "Data" folder beside the executable, i.e. &lt;InstallDir&gt;\Data. These are
    /// the same for every account on the machine and ship with (or get refreshed alongside)
    /// the app itself, so they belong with the install rather than in a user profile.
    ///
    /// The installer pre-populates this folder (sprites, animated GIFs, catalog.json), which
    /// is what makes a fresh install work fully offline on first launch with no copy step and
    /// no network call. The MSI grants the Users group write access to it, because the app
    /// itself runs unelevated and must be able to write here (an incrementally-cached sprite,
    /// a periodic catalog refresh).
    ///
    /// Per-user SAVE progress does NOT live here — see GetSaveFilePath.
    ///
    /// overrideRoot IS the data directory (not a parent to append a folder name to) — tests
    /// point it straight at a temp directory.
    /// </summary>
    public static string GetAppDataDirectory(string? overrideRoot = null) =>
        overrideRoot ?? Path.Combine(AppContext.BaseDirectory, "Data");

    /// Save progress is personal to the Windows account playing, unlike the shared assets in
    /// GetAppDataDirectory — it lives in the standard per-user %APPDATA% location instead, so
    /// two accounts on the same machine don't share (or overwrite) each other's collection, and
    /// reinstalling/updating the app never touches it (an MSI never manages %APPDATA%).
    public static string GetSaveFilePath(string? overrideRoot = null)
    {
        var root = overrideRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PokeCorner");
        return Path.Combine(root, "state.json");
    }

    public static string GetSpriteCacheDirectory(string? overrideRoot = null) =>
        Path.Combine(GetAppDataDirectory(overrideRoot), "sprites");

    /// Animated-GIF sprites, kept in their own folder rather than alongside the static PNGs
    /// because SpriteCache's on-disk naming is always "{speciesId}.png" — a GIF stored there
    /// would collide with the static sprite for the same species.
    public static string GetAnimatedSpriteCacheDirectory(string? overrideRoot = null) =>
        Path.Combine(GetAppDataDirectory(overrideRoot), "animated");
}
