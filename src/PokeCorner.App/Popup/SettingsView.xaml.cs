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
    private bool _pending;
    private bool _suspended;
    private int _generation;
    private readonly Func<bool> _getAutostart;
    private readonly Action<bool> _setAutostart;
    private readonly Action? _showOnboarding;
    private readonly Func<string, string, string, Task<bool>> _confirm;
    private TaskCompletionSource<bool>? _localConfirmation;

    public SettingsView(SettingsViewModel viewModel, Action onPetSizeChanged, Action? onResetSaveData = null, Action? onRecoverSaveData = null,
        Func<string, string, string, Task<bool>>? confirmAsync = null, Action? showOnboarding = null,
        Func<bool>? getAutostart = null, Action<bool>? setAutostart = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _onPetSizeChanged = onPetSizeChanged;
        _onResetSaveData = onResetSaveData;
        _onRecoverSaveData = onRecoverSaveData;
        _getAutostart = getAutostart ?? AutostartRegistry.IsEnabled;
        _setAutostart = setAutostart ?? (enabled =>
        {
            if (enabled) AutostartRegistry.Enable(Environment.ProcessPath!);
            else AutostartRegistry.Disable();
        });
        _showOnboarding = showOnboarding;
        _confirm = confirmAsync ?? ConfirmLocally;
        LocalConfirmationHost.Completed += CompleteLocalConfirmation;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Escape || _localConfirmation is null) return;
            CompleteLocalConfirmation(false); e.Handled = true;
        };
        Loaded += (_, _) => _suspended = false;
        Unloaded += (_, _) => { _suspended = true; _generation++; CompleteLocalConfirmation(false); };
        LoadFromSettings();
        _isLoadingInitialState = false;
    }

    private void LoadFromSettings()
    {
        var settings = _viewModel.Settings;
        FullscreenHideCheck.IsChecked = settings.AutoHideInFullscreen;
        AutostartCheck.IsChecked = _getAutostart();
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
        try { _setAutostart(AutostartCheck.IsChecked == true); FeedbackText.Text = "Настройка запуска сохранена."; }
        catch (Exception error)
        {
            _isLoadingInitialState = true;
            try { AutostartCheck.IsChecked = _getAutostart(); }
            finally { _isLoadingInitialState = false; }
            FeedbackText.Text = "Не удалось изменить запуск: " + error.Message;
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
        if (_pending) return;
        if (_showOnboarding is not null) _showOnboarding();
        else new PokeCorner.App.Onboarding.OnboardingWindow().Show();
    }

    private void RecoverDataButton_Click(object sender, System.Windows.RoutedEventArgs e) => _onRecoverSaveData?.Invoke();

    private Task<bool> ConfirmLocally(string title, string body, string caption)
    {
        _localConfirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        LocalConfirmationHost.Present(title, body, caption);
        return _localConfirmation.Task;
    }

    private void CompleteLocalConfirmation(bool result)
    {
        var pending = _localConfirmation; _localConfirmation = null;
        LocalConfirmationHost.Visibility = System.Windows.Visibility.Collapsed;
        pending?.TrySetResult(result);
    }

    private async void ResetDataButton_Click(object sender, System.Windows.RoutedEventArgs e) => await ResetAsync();

    private async Task ResetAsync()
    {
        if (_pending || _suspended || _onResetSaveData is null) return;
        _pending = true;
        var generation = _generation;
        try
        {
            if (!await _confirm("Начать заново?", "Текущая коллекция и прогресс будут заменены. Данные останутся в истории сохранений.", "Продолжить")
                || _suspended || generation != _generation) return;
            if (!await _confirm("Последнее предупреждение", "Новая игра начнётся с выбора стартового покемона. Приложение перезапустится.", "Начать новую игру")
                || _suspended || generation != _generation) return;
            _onResetSaveData();
        }
        catch (Exception error) { if (!_suspended) FeedbackText.Text = error.Message; }
        finally { _pending = false; }
    }
}
