using PokeCorner.Core.Domain;

namespace PokeCorner.Core;

public sealed class AppSettings
{
    public CareMode CareMode { get; set; } = CareMode.Chill;
    public bool TokenTrackingEnabled { get; set; } = false;
    public bool ActivityTrackingEnabled { get; set; } = true;
    public bool AutoHideInFullscreen { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public string ScreenCorner { get; set; } = "BottomRight";
    public int MonitorIndex { get; set; } = 0;
    public int PetSizePixels { get; set; } = 96;
    public bool ShowProgressPanel { get; set; } = false;

    /// Only consulted in CareMode.Hardcore: neglect permanently releases the pet from the
    /// collection instead of benching it behind the usual one-hour RanAwayUntilUtc cooldown.
    public bool SuperHardcoreEnabled { get; set; } = false;
}
