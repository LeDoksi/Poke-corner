using PokeCorner.App.Popup;
using PokeCorner.Core;
using Xunit;

namespace PokeCorner.App.Tests.Popup;

public class SettingsViewModelTests
{
    [Fact]
    public void SetPetSizePixels_WithinRange_SetsExactValue()
    {
        var settings = new AppSettings();
        var vm = new SettingsViewModel(settings);

        vm.SetPetSizePixels(120);

        Assert.Equal(120, settings.PetSizePixels);
    }

    [Fact]
    public void SetPetSizePixels_BelowMinimum_ClampsToMinimum()
    {
        var settings = new AppSettings();
        var vm = new SettingsViewModel(settings);

        vm.SetPetSizePixels(10);

        Assert.Equal(SettingsViewModel.MinPetSizePixels, settings.PetSizePixels);
    }

    [Fact]
    public void SetPetSizePixels_AboveMaximum_ClampsToMaximum()
    {
        var settings = new AppSettings();
        var vm = new SettingsViewModel(settings);

        vm.SetPetSizePixels(500);

        Assert.Equal(SettingsViewModel.MaxPetSizePixels, settings.PetSizePixels);
    }
}
