using System.Windows.Controls;
using PokeCorner.App.Autostart;

namespace PokeCorner.App.Popup;

// Qualified as System.Windows.Controls.UserControl: UseWindowsForms makes a bare
// UserControl ambiguous with System.Windows.Forms.UserControl.
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
    private readonly Action? _onRecoverSaveData;
    private bool _isLoadingInitialState = true;

    public SettingsView(SettingsViewModel viewModel, Action onPetSizeChanged, Action? onResetSaveData = null, Action? onRecoverSaveData = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _onPetSizeChanged = onPetSizeChanged;
        _onResetSaveData = onResetSaveData;
        _onRecoverSaveData = onRecoverSaveData;
        LoadFromSettings();
        _isLoadingInitialState = false;
    }

    private void LoadFromSettings()
    {
        var settings = _viewModel.Settings;
        FullscreenHideCheck.IsChecked = settings.AutoHideInFullscreen;
        AutostartCheck.IsChecked = AutostartRegistry.IsEnabled();
        PetSizeSlider.Value = settings.PetSizePixels;
        PetSizeValueText.Text = $"{settings.PetSizePixels} px";
        CrySoundVolumeSlider.Value = settings.CrySoundVolume;
        CrySoundVolumeValueText.Text = $"{settings.CrySoundVolume}%";
        EffectsSoundVolumeSlider.Value = settings.EffectsSoundVolume;
        EffectsSoundVolumeValueText.Text = $"{settings.EffectsSoundVolume}%";
        ShowProgressPanelCheck.IsChecked = settings.ShowProgressPanel;
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

    /// Writes the stored volume on every tick of the drag, exactly like the pet-size slider
    /// above. Nothing is played back here — the point is only that the NEXT pet click reads the
    /// new value (App.PlayCry re-reads the setting per play), so a mid-session change takes
    /// effect immediately and without a restart.
    private void CrySoundVolumeSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoadingInitialState) return;
        _viewModel.SetCrySoundVolume((int)e.NewValue);
        CrySoundVolumeValueText.Text = $"{_viewModel.Settings.CrySoundVolume}%";
    }

    private void EffectsSoundVolumeSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoadingInitialState) return;
        _viewModel.SetEffectsSoundVolume((int)e.NewValue);
        EffectsSoundVolumeValueText.Text = $"{_viewModel.Settings.EffectsSoundVolume}%";
    }

    private void ShowProgressPanelCheck_Changed(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_isLoadingInitialState) return;
        _viewModel.Settings.ShowProgressPanel = ShowProgressPanelCheck.IsChecked == true;
    }

    private void ShowOnboardingButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        new PokeCorner.App.Onboarding.OnboardingWindow().Show();
    }

    private void RecoverDataButton_Click(object sender, System.Windows.RoutedEventArgs e) => _onRecoverSaveData?.Invoke();

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
            "Начать заново с пустой коллекцией? Текущие данные останутся в истории сохранений.",
            "Удалить", owner);
        if (!confirmedOnce) return;

        var confirmedTwice = ConfirmDialog.Ask(
            "Последнее предупреждение",
            "Точно начать новую игру? Текущая коллекция и прогресс будут заменены. Приложение перезапустится и начнёт с выбора стартового покемона.",
            "Да, удалить всё", owner);
        if (!confirmedTwice) return;

        _onResetSaveData();
    }
}
