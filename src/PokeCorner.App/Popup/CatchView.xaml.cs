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
    private readonly Action? _onResultContinued;
    private DispatcherTimer? _liveRefreshTimer;
    private bool _busy;
    private bool _showingResult;
    private string? _error;
    private CatchReveal? _pendingReveal;
    private CatchReveal? _spriteFetchedFor;
    private CancellationTokenSource? _revealCancellation;

    public CatchView(AppState appState, Func<CatchReveal?>? tryPerformCatch, Func<int, Task<byte[]?>> fetchSprite, Func<int> catchReadyCount, Action onStateChanged, Func<int, string> resolveSpeciesName, Func<Task<CatchReveal?>>? tryPerformCatchAsync = null, Action? onResultContinued = null)
    {
        InitializeComponent();
        _appState = appState;
        _tryPerformCatch = tryPerformCatchAsync ?? (() => Task.FromResult(tryPerformCatch?.Invoke()));
        _fetchSprite = fetchSprite;
        _catchReadyCount = catchReadyCount;
        _onStateChanged = onStateChanged;
        _resolveSpeciesName = resolveSpeciesName;
        _onResultContinued = onResultContinued;
        Loaded += (_, _) =>
        {
            StartLiveRefresh();
            SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
        };
        Unloaded += (_, _) =>
        {
            StopLiveRefresh();
            SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;
            _revealCancellation?.Cancel();
            if (_pendingReveal is { } reveal) ShowResult(reveal, false);
        };
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
        if (GiftReadyCount() > 0)
            CatchCharmText.Text += " Подарочные покеболы уже закрыты: талисман не меняет их результат.";
        CatchCharmText.Visibility = Visibility.Visible;
        SetReady(_catchReadyCount(), GiftReadyCount());
    }

    private int GiftReadyCount() => CatchBallQueue.GiftReadyCount(_appState.Encounters);

    /// count: how many catches are queued (0 = none ready). Wording goes from "не готово"
    /// through singular to plural.
    private void SetReady(int count, int giftCount)
    {
        CatchButton.IsEnabled = !_busy && _revealCancellation is null && (_showingResult || count > 0 || giftCount > 0);
        if (_showingResult) return;
        CatchButton.Content = "Открыть покебол";
        StatusText.Text = count switch
        {
            <= 0 => "Копи активность за ПК — полоса поимки ещё не заполнилась.",
            1 => "Покебол за активность готов к открытию.",
            _ => $"Готово {count} покебола за активность."
        };
        if (giftCount > 0)
            StatusText.Text = count > 0
                ? $"Готово подарочных покеболов: {giftCount}; за активность: {count}."
                : $"Готово подарочных покеболов: {giftCount}.";
        if (_error is not null) StatusText.Text = _error;
    }

    private async void CatchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _revealCancellation is not null) return;
        if (_showingResult)
        {
            _showingResult = false;
            _pendingReveal = null;
            _onResultContinued?.Invoke();
            PokeBallCanvas.BeginAnimation(OpacityProperty, null);
            PokeBallCanvas.Opacity = 1;
            PokeBallThrow.Y = 0;
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
            CatchButton.IsEnabled = _revealCancellation is null && (_showingResult || _catchReadyCount() > 0 || GiftReadyCount() > 0);
        }
    }

    public async Task PlayReveal(CatchReveal reveal)
    {
        if (_revealCancellation is not null) return;
        _pendingReveal = reveal;
        _showingResult = true;
        CatchButton.IsEnabled = false;
        RevealSpriteImage.Source = null;
        RevealSpriteImage.BeginAnimation(OpacityProperty, null);
        RevealNameText.BeginAnimation(OpacityProperty, null);
        RevealRarityText.BeginAnimation(OpacityProperty, null);
        RevealSpriteImage.Opacity = 0;
        RevealNameText.Opacity = 0;
        RevealRarityText.Opacity = 0;
        var cancellation = _revealCancellation = new CancellationTokenSource();
        try
        {
            if (SystemParameters.ClientAreaAnimation)
            {
                StatusText.Text = "Покебол летит…";
                PokeBallThrow.Y = -36;
                PokeBallThrow.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
                    new DoubleAnimation(-36, 0, TimeSpan.FromMilliseconds(350)));
                await Task.Delay(350, cancellation.Token);
                StatusText.Text = "Покебол покачивается…";
                var shake = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(900) };
                shake.KeyFrames.Add(new EasingDoubleKeyFrame(-15, KeyTime.FromPercent(0.15)));
                shake.KeyFrames.Add(new EasingDoubleKeyFrame(15, KeyTime.FromPercent(0.35)));
                shake.KeyFrames.Add(new EasingDoubleKeyFrame(-15, KeyTime.FromPercent(0.55)));
                shake.KeyFrames.Add(new EasingDoubleKeyFrame(15, KeyTime.FromPercent(0.75)));
                shake.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1)));
                PokeBallRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, shake);
                await Task.Delay(900, cancellation.Token);
                StatusText.Text = "Покебол открывается…";
                PokeBallCanvas.BeginAnimation(OpacityProperty,
                    new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250)));
                await Task.Delay(250, cancellation.Token);
            }
            else await Task.Delay(100, cancellation.Token);
            ShowResult(reveal, SystemParameters.ClientAreaAnimation);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(_revealCancellation, cancellation)) _revealCancellation = null;
            cancellation.Dispose();
            CatchButton.IsEnabled = !_busy;
        }
    }

    public void RestoreReveal(CatchReveal reveal)
    {
        _pendingReveal = reveal;
        _showingResult = true;
        ShowResult(reveal, false);
        CatchButton.IsEnabled = true;
    }

    private void SystemParameters_StaticPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SystemParameters.ClientAreaAnimation) || SystemParameters.ClientAreaAnimation) return;
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => SystemParameters_StaticPropertyChanged(sender, e)); return; }
        _revealCancellation?.Cancel();
        if (_pendingReveal is { } reveal) ShowResult(reveal, false);
    }

    private void ShowResult(CatchReveal reveal, bool animate)
    {
        PokeBallThrow.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
        PokeBallThrow.Y = 0;
        PokeBallRotate.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
        PokeBallRotate.Angle = 0;
        PokeBallCanvas.BeginAnimation(OpacityProperty, null);
        PokeBallCanvas.Opacity = 0;
        string name;
        try { name = _resolveSpeciesName(reveal.Result.SpeciesId); }
        catch { name = $"№{reveal.Result.SpeciesId}"; }
        RevealNameText.Text = name + (reveal.Result.IsShiny ? " ✨" : "");
        RevealRarityText.Text = reveal.Rarity + (reveal.Result.WasDuplicateSpecies ? " · уже был в коллекции" : " · новый вид!");

        // The reveal block starts Collapsed (not merely Opacity=0) so the idle screen has no
        // ~140px hole between the pokéball and the catch button. It takes up layout space only
        // from here on, which reads as the reveal "arriving" rather than as a permanent gap.
        RevealSpriteImage.Visibility = Visibility.Visible;
        RevealNameText.Visibility = Visibility.Visible;
        RevealRarityText.Visibility = Visibility.Visible;

        if (animate)
        {
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(400));
            RevealSpriteImage.BeginAnimation(OpacityProperty, fadeIn);
            RevealNameText.BeginAnimation(OpacityProperty, fadeIn);
            RevealRarityText.BeginAnimation(OpacityProperty, fadeIn);
        }
        else
        {
            RevealSpriteImage.BeginAnimation(OpacityProperty, null);
            RevealNameText.BeginAnimation(OpacityProperty, null);
            RevealRarityText.BeginAnimation(OpacityProperty, null);
            RevealSpriteImage.Opacity = RevealNameText.Opacity = RevealRarityText.Opacity = 1;
        }

        _showingResult = true;
        CatchButton.Content = "Продолжить";
        StatusText.Text = "Покемон уже добавлен в коллекцию.";
        if (!ReferenceEquals(_spriteFetchedFor, reveal))
        {
            _spriteFetchedFor = reveal;
            _ = LoadSpriteAsync(reveal);
        }
    }

    private async Task LoadSpriteAsync(CatchReveal reveal)
    {
        try
        {
            var spriteBytes = await _fetchSprite(reveal.Result.SpeciesId);
            if (ReferenceEquals(_pendingReveal, reveal) && spriteBytes is not null)
                RevealSpriteImage.Source = SpriteImages.Decode(spriteBytes);
        }
        catch { /* Saved result remains visible when its art is unavailable. */ }
    }
}
