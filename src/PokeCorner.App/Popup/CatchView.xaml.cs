// src/PokeCorner.App/Popup/CatchView.xaml.cs
using PokeCorner.App.Imaging;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PokeCorner.Core;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

public partial class CatchView : System.Windows.Controls.UserControl
{
    private readonly AppState _appState;
    private readonly Func<Task<CatchReveal?>> _tryPerformCatch;
    private readonly Func<int, Task<byte[]?>> _fetchSprite;
    /// Number of earned activity balls waiting to be opened.
    private readonly Func<int> _catchReadyCount;
    private readonly Action _onStateChanged;

    /// Species id -> display name, resolved against the live species catalog by the caller.
    private readonly Func<int, string> _resolveSpeciesName;
    private DispatcherTimer? _liveRefreshTimer;
    private bool _busy;
    private bool _showingResult;
    private string? _error;

    public CatchView(AppState appState, Func<CatchReveal?>? tryPerformCatch, Func<int, Task<byte[]?>> fetchSprite, Func<int> catchReadyCount, Action onStateChanged, Func<int, string> resolveSpeciesName, Func<Task<CatchReveal?>>? tryPerformCatchAsync = null)
    {
        InitializeComponent();
        _appState = appState;
        _tryPerformCatch = tryPerformCatchAsync ?? (() => Task.FromResult(tryPerformCatch?.Invoke()));
        _fetchSprite = fetchSprite;
        _catchReadyCount = catchReadyCount;
        _onStateChanged = onStateChanged;
        _resolveSpeciesName = resolveSpeciesName;
        Loaded += (_, _) => StartLiveRefresh();
        Unloaded += (_, _) => StopLiveRefresh();
        Refresh();
    }

    private void StartLiveRefresh()
    {
        _liveRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _liveRefreshTimer.Tick += (_, _) => Refresh();
        _liveRefreshTimer.Start();
    }

    private void StopLiveRefresh()
    {
        _liveRefreshTimer?.Stop();
        _liveRefreshTimer = null;
    }

    public void Refresh()
    {
        var progress = ProgressCalculator.Compute(_appState.Activity, _appState.Collection.GetActive(), _appState.Feeding);
        CatchProgressBar.Value = progress.CatchFraction;
        var activeCharm = _appState.Shop.CatchLuckyCharmActive;
        var chance = ShinyRules.Chance(ShinySource.Activity, 0, activeCharm, true);
        CatchCharmText.Text = $"Шанс shiny при пригодном виде: {chance:P3}. " +
            (activeCharm ? "Активный талисман расходуется на пригодную попытку; при недоступном shiny сохраняется."
                : "Если у выбранного вида нет shiny-ресурса, шанс 0%.");
        CatchCharmText.Visibility = Visibility.Visible;
        SetReady(_catchReadyCount());
    }

    /// count: how many catches are queued (0 = none ready). Wording goes from "не готово"
    /// through singular to plural.
    private void SetReady(int count)
    {
        CatchButton.IsEnabled = !_busy && (_showingResult || count > 0);
        if (_showingResult) return;
        CatchButton.Content = "Открыть покебол";
        StatusText.Text = count switch
        {
            <= 0 => "Копи активность за ПК — полоса поимки ещё не заполнилась.",
            1 => "Покебол за активность готов к открытию.",
            _ => $"Готово {count} покебола за активность."
        };
        if (_error is not null) StatusText.Text = _error;
    }

    private async void CatchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (_showingResult)
        {
            _showingResult = false;
            RevealSpriteImage.Visibility = Visibility.Collapsed;
            RevealNameText.Visibility = Visibility.Collapsed;
            RevealRarityText.Visibility = Visibility.Collapsed;
            Refresh();
            return;
        }
        _busy = true;
        _error = null;
        CatchButton.IsEnabled = false;
        try
        {
            var reveal = await _tryPerformCatch();
            if (reveal is null) { StatusText.Text = "Пока нет готового покебола."; return; }
            _onStateChanged();
            await PlayReveal(reveal);
        }
        catch (Exception ex)
        {
            _error = "Не удалось открыть покебол: " + ex.Message;
            StatusText.Text = _error;
        }
        finally
        {
            _busy = false;
            CatchButton.IsEnabled = _showingResult || _catchReadyCount() > 0;
        }
    }

    public async Task PlayReveal(CatchReveal reveal)
    {
        SetReady(0);
        RevealSpriteImage.Opacity = 0;
        RevealNameText.Opacity = 0;
        RevealRarityText.Opacity = 0;

        var shake = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(900) };
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(-15, KeyTime.FromPercent(0.15)));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(15, KeyTime.FromPercent(0.35)));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(-15, KeyTime.FromPercent(0.55)));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(15, KeyTime.FromPercent(0.75)));
        shake.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1.0)));

        var tcs = new TaskCompletionSource();
        shake.Completed += (_, _) => tcs.SetResult();
        PokeBallRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, shake);
        await tcs.Task;

        try
        {
            var spriteBytes = await _fetchSprite(reveal.Result.SpeciesId);
            if (spriteBytes is not null) RevealSpriteImage.Source = SpriteImages.Decode(spriteBytes);
        }
        catch { RevealSpriteImage.Source = null; } // The individual is already saved; an image failure must not imply a failed catch.

        RevealNameText.Text = _resolveSpeciesName(reveal.Result.SpeciesId) + (reveal.Result.IsShiny ? " ✨" : "");
        RevealRarityText.Text = reveal.Rarity + (reveal.Result.WasDuplicateSpecies ? " · уже был в коллекции" : " · новый вид!");

        // The reveal block starts Collapsed (not merely Opacity=0) so the idle screen has no
        // ~140px hole between the pokéball and the catch button. It takes up layout space only
        // from here on, which reads as the reveal "arriving" rather than as a permanent gap.
        RevealSpriteImage.Visibility = Visibility.Visible;
        RevealNameText.Visibility = Visibility.Visible;
        RevealRarityText.Visibility = Visibility.Visible;

        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(400));
        RevealSpriteImage.BeginAnimation(OpacityProperty, fadeIn);
        RevealNameText.BeginAnimation(OpacityProperty, fadeIn);
        RevealRarityText.BeginAnimation(OpacityProperty, fadeIn);

        _showingResult = true;
        CatchButton.Content = "Продолжить";
        StatusText.Text = "Покемон уже добавлен в коллекцию.";
    }
}
