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

    /// Wipes the save file and relaunches (App.ResetSaveDataAndRestart). Optional so the
    /// theme/layout tests can keep constructing this view without an App instance behind
    /// them; a null callback just makes the button inert, which is the right behaviour for
    /// a view rendered outside a running app.
    private readonly Action? _onResetSaveData;
    private bool _isLoadingInitialState = true;

    public SettingsView(SettingsViewModel viewModel, Action onPetSizeChanged, Action? onResetSaveData = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _onPetSizeChanged = onPetSizeChanged;
        _onResetSaveData = onResetSaveData;
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
        SyncSuperHardcoreEnabled();
    }

    private void CareModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Deliberately outside the _isLoadingInitialState guard below: this only touches the
        // control's own enabled state, never the stored settings, and it has to run on the
        // initial load too (LoadFromSettings sets SelectedIndex before that flag is cleared).
        SyncSuperHardcoreEnabled();

        if (_isLoadingInitialState) return;
        _viewModel.Settings.CareMode = CareModeCombo.SelectedIndex == 1 ? CareMode.Hardcore : CareMode.Chill;
    }

    /// Супер Хардкор only has any effect in Hardcore (see App.CheckHardcoreNeglect, which
    /// returns early for Chill), so the checkbox is greyed out otherwise.
    ///
    /// It does NOT clear AppSettings.SuperHardcoreEnabled when switching to Chill: the value
    /// is simply inert while Chill is selected, so switching back to Hardcore restores the
    /// choice the player already made rather than silently discarding it.
    private void SyncSuperHardcoreEnabled() =>
        SuperHardcoreCheck.IsEnabled = CareModeCombo.SelectedIndex == 1;

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

    /// Two confirmations, not one: this throws away everything the player has, and the
    /// short-circuiting && means backing out of either one leaves the save file untouched.
    /// The second prompt is worded to make the finality explicit rather than just repeating
    /// the first, so it can't be dismissed as a duplicate click-through.
    private void ResetDataButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_onResetSaveData is null) return;

        var owner = System.Windows.Window.GetWindow(this);

        var confirmedOnce = ConfirmDialog.Ask(
            "Очистить сохранённые данные?",
            "Удалить всю коллекцию и прогресс? Это нельзя отменить.",
            "Удалить", owner);
        if (!confirmedOnce) return;

        var confirmedTwice = ConfirmDialog.Ask(
            "Последнее предупреждение",
            "Точно? Все покемоны и прогресс будут потеряны навсегда. Приложение перезапустится и начнёт с выбора стартового покемона.",
            "Да, удалить всё", owner);
        if (!confirmedTwice) return;

        _onResetSaveData();
    }
}
