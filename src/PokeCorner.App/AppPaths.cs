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
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var root = overrideRoot ?? (string.IsNullOrWhiteSpace(appData) || !Path.IsPathFullyQualified(appData)
            ? throw new IOException("Папка APPDATA недоступна. Сохранение не будет создано в рабочем каталоге.")
            : Path.Combine(appData, "PokeCorner"));
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            throw new IOException("Путь профиля должен быть абсолютным.");
        root = Path.GetFullPath(root);
        return Path.Combine(root, "state.json");
    }

    public static string GetV2SaveFilePath(string? overrideRoot = null) =>
        Path.Combine(Path.GetDirectoryName(GetSaveFilePath(overrideRoot))!, "state-v2.json");

    public static string GetSpriteCacheDirectory(string? overrideRoot = null) =>
        Path.Combine(GetAppDataDirectory(overrideRoot), "sprites");

    /// Animated-GIF sprites, kept in their own folder rather than alongside the static PNGs
    /// because SpriteCache's on-disk naming is always "{speciesId}.png" — a GIF stored there
    /// would collide with the static sprite for the same species.
    public static string GetAnimatedSpriteCacheDirectory(string? overrideRoot = null) =>
        Path.Combine(GetAppDataDirectory(overrideRoot), "animated");

    /// Species cry sounds, "{speciesId}.wav". WAV rather than the upstream Ogg, because PCM WAV
    /// is the one format that needs no codec installed on the target machine — Ogg does — so the
    /// conversion happens once, in the offline crawl that seeds this folder, exactly like sprites
    /// and GIFs are pre-fetched rather than decoded on the player's PC. (The playback API has
    /// since moved from System.Media.SoundPlayer to WPF's MediaPlayer, which is what made the
    /// volume setting possible; both play a local PCM WAV with no codec dependency, so the
    /// format decision is unchanged.) Unlike sprites there is no runtime fetch fallback: a
    /// species with no file here simply makes no sound.
    public static string GetCryDirectory(string? overrideRoot = null) =>
        Path.Combine(GetAppDataDirectory(overrideRoot), "cries");
}
