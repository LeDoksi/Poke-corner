using PokeCorner.Core;

namespace PokeCorner.App.Popup;

public sealed class SettingsViewModel
{
    public const int MinPetSizePixels = 48;
    public const int MaxPetSizePixels = 192;

    private readonly AppSettings _settings;

    public SettingsViewModel(AppSettings settings) => _settings = settings;

    public AppSettings Settings => _settings;

    public void SetPetSizePixels(int pixels) =>
        _settings.PetSizePixels = Math.Clamp(pixels, MinPetSizePixels, MaxPetSizePixels);
}
