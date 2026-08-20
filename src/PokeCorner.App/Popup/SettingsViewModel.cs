using PokeCorner.Core;

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
}
