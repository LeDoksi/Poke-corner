using PokeCorner.Core.Domain;

namespace PokeCorner.Core;

public sealed class AppSettings
{
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

    /// Loudness of the species cry played when the player pets the active Pokémon, as a
    /// PERCENT (0-100) rather than the 0.0-1.0 the player itself takes — a percent is what the
    /// settings slider and its readout want, and CryVolume.ToPlayerVolume does the conversion
    /// at the one point of use. 0 is genuine silence; the default is deliberately below full,
    /// since a cry that fires on every pet click at 100% wears out its welcome fast.
    public int EffectsSoundVolume { get; set; } = 30;

    public int CrySoundVolume { get; set; } = 70;

    /// Legacy serialized setting; loaded saves normalize this to false.
    public bool SuperHardcoreEnabled { get; set; } = false;
}
