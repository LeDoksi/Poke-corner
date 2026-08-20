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
    private bool _isLoadingInitialState = true;

    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
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
        ShowProgressPanelCheck.IsChecked = settings.ShowProgressPanel;
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
    }

    private void ShowProgressPanelCheck_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isLoadingInitialState) return;
        _viewModel.Settings.ShowProgressPanel = ShowProgressPanelCheck.IsChecked == true;
    }
}
