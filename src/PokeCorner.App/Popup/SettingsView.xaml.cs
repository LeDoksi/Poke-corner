using System.Windows.Controls;
using PokeCorner.App.Autostart;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

// Qualified as System.Windows.Controls.UserControl, not bare UserControl — same
// WPF/WinForms ambiguity Task 7's CollectionView hit (System.Windows.Forms.UserControl
// also exists), just not caught here until now.
public partial class SettingsView : System.Windows.Controls.UserControl
{
    private readonly SettingsViewModel _viewModel;

    /// Applies the just-written pet size to the real desktop overlay. Without it the slider
    /// only updated the stored setting and the sprite resized whenever the app's 5-second
    /// reconcile tick next happened to run — so there was no live preview to size against.
    private readonly Action _onPetSizeChanged;
    private bool _isLoadingInitialState = true;

    public SettingsView(SettingsViewModel viewModel, Action onPetSizeChanged)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _onPetSizeChanged = onPetSizeChanged;
        LoadFromSettings();
        _isLoadingInitialState = false;
    }

    private void LoadFromSettings()
    {
        var settings = _viewModel.Settings;
        CareModeCombo.SelectedIndex = settings.CareMode == CareMode.Hardcore ? 1 : 0;
        TokenTrackingCheck.IsChecked = settings.TokenTrackingEnabled;
        ActivityTrackingCheck.IsChecked = settings.ActivityTrackingEnabled;
        FullscreenHideCheck.IsChecked = settings.AutoHideInFullscreen;
        AutostartCheck.IsChecked = AutostartRegistry.IsEnabled();
        PetSizeSlider.Value = settings.PetSizePixels;
        PetSizeValueText.Text = $"{settings.PetSizePixels} px";
        ShowProgressPanelCheck.IsChecked = settings.ShowProgressPanel;
        SuperHardcoreCheck.IsChecked = settings.SuperHardcoreEnabled;
    }

    private void CareModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingInitialState) return;
        _viewModel.Settings.CareMode = CareModeCombo.SelectedIndex == 1 ? CareMode.Hardcore : CareMode.Chill;
    }

    private void TokenTrackingCheck_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isLoadingInitialState) return;
        _viewModel.Settings.TokenTrackingEnabled = TokenTrackingCheck.IsChecked == true;
    }

    private void ActivityTrackingCheck_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isLoadingInitialState) return;
        _viewModel.Settings.ActivityTrackingEnabled = ActivityTrackingCheck.IsChecked == true;
    }

    private void FullscreenHideCheck_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isLoadingInitialState) return;
        _viewModel.Settings.AutoHideInFullscreen = FullscreenHideCheck.IsChecked == true;
    }

    private void AutostartCheck_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isLoadingInitialState) return;
        if (AutostartCheck.IsChecked == true)
        {
            AutostartRegistry.Enable(Environment.ProcessPath!);
        }
        else
        {
            AutostartRegistry.Disable();
        }
    }

    private void PetSizeSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoadingInitialState) return;
        _viewModel.SetPetSizePixels((int)e.NewValue);
        PetSizeValueText.Text = $"{_viewModel.Settings.PetSizePixels} px";
        _onPetSizeChanged();
    }

    private void ShowProgressPanelCheck_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isLoadingInitialState) return;
        _viewModel.Settings.ShowProgressPanel = ShowProgressPanelCheck.IsChecked == true;
    }

    private void SuperHardcoreCheck_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isLoadingInitialState) return;
        _viewModel.Settings.SuperHardcoreEnabled = SuperHardcoreCheck.IsChecked == true;
    }

    private void ShowOnboardingButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        new PokeCorner.App.Onboarding.OnboardingWindow().Show();
    }
}
