using PokeCorner.Core;
using PokeCorner.Core.Audio;

namespace PokeCorner.App.Popup;

public sealed class SettingsViewModel
{
    public const int MinPetSizePixels = 48;
    // 192 used to be the ceiling, but with NearestNeighbor pixel-art scaling (no more
    // blur at large sizes) that read as cramped — 192 is now roughly the comfortable
    // middle of the range instead of the top.
    public const int MaxPetSizePixels = 384;

    private readonly AppSettings _settings;

    public SettingsViewModel(AppSettings settings) => _settings = settings;

    public AppSettings Settings => _settings;

    public void SetPetSizePixels(int pixels) =>
        _settings.PetSizePixels = Math.Clamp(pixels, MinPetSizePixels, MaxPetSizePixels);

    /// Clamped on the way IN as well as on the way out (CryVolume.ToPlayerVolume clamps too):
    /// the stored setting is what the settings screen reads back on the next launch, so letting
    /// an out-of-range value settle there would show a slider pinned past its own track.
    public void SetCrySoundVolume(int percent) =>
        _settings.CrySoundVolume = Math.Clamp(percent, CryVolume.MinPercent, CryVolume.MaxPercent);
}
