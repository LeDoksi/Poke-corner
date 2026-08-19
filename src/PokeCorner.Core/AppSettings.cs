using PokeCorner.Core.Domain;

namespace PokeCorner.Core;

public sealed class AppSettings
{
    public CareMode CareMode { get; set; } = CareMode.Chill;
    public FeedingMode FeedingMode { get; set; } = FeedingMode.Simple;
    public bool TokenTrackingEnabled { get; set; } = false;
    public bool ActivityTrackingEnabled { get; set; } = true;
    public bool AutoHideInFullscreen { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public string ScreenCorner { get; set; } = "BottomRight";
    public int MonitorIndex { get; set; } = 0;
    public int PetSizePixels { get; set; } = 96;
}
