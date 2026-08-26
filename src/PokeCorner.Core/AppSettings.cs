using PokeCorner.Core.Domain;

namespace PokeCorner.Core;

public sealed class AppSettings
{
    // TokenTrackingEnabled/ActivityTrackingEnabled used to live here. Claude Code token
    // tracking is gone entirely, and PC activity — the one remaining growth source — is the
    // app's core premise rather than an option, so it is now unconditional and has no setting.
    // Removing the properties is enough to drop them from the save file too: SaveData persists
    // this whole object, so the JSON simply stops carrying them, and System.Text.Json ignores
    // the stale keys still present in an existing state.json.
    public CareMode CareMode { get; set; } = CareMode.Chill;
    public bool AutoHideInFullscreen { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public string ScreenCorner { get; set; } = "BottomRight";
    public int MonitorIndex { get; set; } = 0;
    public int PetSizePixels { get; set; } = 96;
    public bool ShowProgressPanel { get; set; } = false;

    /// Null until the player drags the pet at least once — ScreenCorner/MonitorIndex still
    /// govern initial placement and ScreenCorner-changes until then. Set together, so treat
    /// them as a pair (one non-null implies the other is too).
    public int? PetPositionX { get; set; }
    public int? PetPositionY { get; set; }

    /// Only consulted in CareMode.Hardcore: neglect permanently releases the pet from the
    /// collection instead of benching it behind the usual one-hour RanAwayUntilUtc cooldown.
    public bool SuperHardcoreEnabled { get; set; } = false;
}
