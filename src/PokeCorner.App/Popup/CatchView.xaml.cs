using PokeCorner.App.Imaging;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PokeCorner.Core;
using PokeCorner.Core.Domain;

namespace PokeCorner.App.Popup;

public partial class CatchView : System.Windows.Controls.UserControl
{
    private readonly AppState _appState;
    private readonly Func<Task<CatchReveal?>> _tryPerformCatch;
    private readonly Func<Guid, Task<CatchReveal?>>? _tryPerformEncounter;
    private readonly Func<int, Task<byte[]?>> _fetchSprite;
    private readonly Func<int, bool, Task<byte[]?>>? _fetchVariantSprite;
    private readonly Func<Guid?, byte[]?>? _fetchScene;
    private readonly Func<int> _catchReadyCount;
    private readonly Action _onStateChanged;
    private readonly Func<int, string> _resolveSpeciesName;
    private readonly Action? _onResultContinued;
    private readonly Func<Task>? _activateLuckyCharm;
    private readonly Func<bool> _animationEnabled;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private DispatcherTimer? _liveRefreshTimer;
    private bool _busy, _showingResult, _suspended, _listening;
    private int _generation, _artGeneration;
    private string? _error, _sceneKey;
    private Guid? _selectedEncounterId;
    private int? _knownSpeciesId;
    private CatchReveal? _pendingReveal, _spriteFetchedFor;
    private CancellationTokenSource? _revealCancellation;

    public CatchView(AppState appState, Func<CatchReveal?>? tryPerformCatch, Func<int, Task<byte[]?>> fetchSprite,
        Func<int> catchReadyCount, Action onStateChanged, Func<int, string> resolveSpeciesName,
        Func<Task<CatchReveal?>>? tryPerformCatchAsync = null, Action? onResultContinued = null,
        Func<Guid, Task<CatchReveal?>>? tryPerformEncounterAsync = null,
        Func<int, bool, Task<byte[]?>>? fetchVariantSprite = null, Func<Task>? activateLuckyCharmAsync = null,
        Func<bool>? animationEnabled = null, Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        Func<Guid?, byte[]?>? fetchScene = null, string? luckyCharmArtPath = null)
    {
        InitializeComponent();
        _appState = appState;
        _tryPerformCatch = tryPerformCatchAsync ?? (() => Task.FromResult(tryPerformCatch?.Invoke()));
        _tryPerformEncounter = tryPerformEncounterAsync;
        _fetchSprite = fetchSprite;
        _fetchVariantSprite = fetchVariantSprite;
        _fetchScene = fetchScene;
        _catchReadyCount = catchReadyCount;
        _onStateChanged = onStateChanged;
        _resolveSpeciesName = resolveSpeciesName;
        _onResultContinued = onResultContinued;
        _activateLuckyCharm = activateLuckyCharmAsync;
        _animationEnabled = animationEnabled ?? (() => SystemParameters.ClientAreaAnimation);
        _delay = delayAsync ?? Task.Delay;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        try {
            if (luckyCharmArtPath is not null && System.IO.File.Exists(luckyCharmArtPath))
                CharmImage.Source = SpriteImages.Decode(System.IO.File.ReadAllBytes(luckyCharmArtPath));
        }
        catch (System.IO.IOException) { }
        Refresh();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _suspended = false;
        if (!_listening) { SystemParameters.StaticPropertyChanged += AnimationPreferenceChanged; _listening = true; }
        if (_liveRefreshTimer is null) {
            _liveRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _liveRefreshTimer.Tick += LiveRefresh;
            _liveRefreshTimer.Start();
        }
        if (_pendingReveal is { } reveal && _revealCancellation is null) ShowResult(reveal);
        else Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _suspended = true;
        _generation++; _artGeneration++;
        _sceneKey = null; _spriteFetchedFor = null;
        if (_liveRefreshTimer is not null) {
            _liveRefreshTimer.Stop(); _liveRefreshTimer.Tick -= LiveRefresh; _liveRefreshTimer = null;
        }
        if (_listening) { SystemParameters.StaticPropertyChanged -= AnimationPreferenceChanged; _listening = false; }
        _revealCancellation?.Cancel();
        StopAnimations();
        if (_pendingReveal is { } reveal) ShowResult(reveal);
    }

    private void LiveRefresh(object? sender, EventArgs e) => Refresh();

    private EncounterState? CurrentEncounter() => _selectedEncounterId is { } id
        ? _appState.Encounters.FirstOrDefault(e => e.Id == id && CatchBallQueue.IsReady(e))
        : CatchBallQueue.NextReady(_appState.Encounters);

    // Entry pins an already resolved expedition; it never invokes the grant producer.
    public bool SelectEncounter(Guid id)
    {
        if (_busy || _pendingReveal is not null || _revealCancellation is not null) return false;
        if (_appState.Encounters.FirstOrDefault(e => e.Id == id) is not
            { Source: "expedition", Phase: "resolved", KnownSpeciesId: not null, Individual: not null }) return false;
        _selectedEncounterId = id; _sceneKey = null; _error = null;
        Refresh();
        return true;
    }

    public void Refresh()
    {
        CatchProgressBar.Value = ProgressCalculator.Compute(_appState.Activity, _appState.Collection.GetActive(), _appState.Feeding).CatchFraction;
        var stock = _appState.Shop.Items.GetValueOrDefault("lucky-charm");
        var active = _appState.Shop.CatchLuckyCharmActive;
        var encounter = CurrentEncounter();
        CatchCharmText.Text = encounter?.Source switch
        {
            "gift" => "Подарочный покебол уже закрыт: талисман не меняет его результат.",
            "expedition" => "Результат встречи уже определён: талисман не меняет его.",
            _ => active
                ? $"Талисман зарезервирован на пригодную попытку. В запасе: {stock}. Если шайни-форма недоступна, он остаётся активным."
                : $"Талисман помогает встретить шайни при пригодном виде. В запасе: {stock}."
        };
        CharmButton.Content = active ? "Талисман активен" : stock > 0 ? "Активировать" : "Нет в запасе";
        CharmButton.IsEnabled = !_busy && !_suspended && !_showingResult && _activateLuckyCharm is not null && stock > 0 && !active;
        CatchButton.IsEnabled = !_busy && _revealCancellation is null && (_showingResult || encounter is not null ||
            _selectedEncounterId is null && _catchReadyCount() > 0);
        if (_showingResult || _busy || _suspended) return;
        PresentClosedScene(encounter);
        StatusText.Text = _error ?? (encounter?.Source switch {
            "expedition" => "Встреча сохранена. Бросьте покебол, когда будете готовы.",
            "gift" => $"Готово подарочных покеболов: {CatchBallQueue.GiftReadyCount(_appState.Encounters)}.",
            _ => _catchReadyCount() > 0 ? $"Покеболов за активность готово: {_catchReadyCount()}." :
                "Копи активность за ПК — следующий покебол ещё не готов."
        });
    }

    private string SpeciesName(int id)
    {
        try { return _resolveSpeciesName(id); }
        catch { return $"№{id}"; }
    }

    private void PresentClosedScene(EncounterState? encounter)
    {
        var key = encounter?.Id.ToString() ?? "empty";
        if (_sceneKey == key) return;
        _sceneKey = key; _artGeneration++;
        StopAnimations();
        RevealSpriteImage.Source = null;
        RevealSpriteImage.Visibility = RevealNameText.Visibility = RevealRarityText.Visibility = ResultDetailText.Visibility = Visibility.Collapsed;
        RevealNameText.Text = RevealRarityText.Text = ResultDetailText.Text = "";
        CatchBallImage.Source = (ImageSource)FindResource("UiBallClosedImage");
        PokeBallCanvas.Visibility = Visibility.Visible;
        PokeBallCanvas.Margin = new Thickness(0);
        CatchButton.Content = "Бросить покебол";
        SceneHeadingText.Visibility = CharmPanel.Visibility = ProgressPanel.Visibility = Visibility.Visible;
        SceneHeadingText.Text = "Твой ещё один друг";
        DetailPanel.Visibility = Visibility.Visible; System.Windows.Controls.Grid.SetColumnSpan(VisualPanel, 1);
        SkipButton.Visibility = Visibility.Collapsed;
        var known = encounter is { Source: "expedition", Phase: "resolved", KnownSpeciesId: not null, Individual: not null };
        EncounterTitleText.Text = known ? "Встреча с " + SpeciesName(encounter!.KnownSpeciesId!.Value) : "Кто внутри?";
        SubtitleText.Text = known ? "Этот покемон встретился команде в походе." : "Ещё один друг ждёт знакомства.";
        SceneImage.Visibility = known ? Visibility.Visible : Visibility.Collapsed;
        _knownSpeciesId = known ? encounter!.KnownSpeciesId : null;
        if (!known) return;
        LoadScene(encounter!.Id);
        SceneHeadingText.Visibility = Visibility.Collapsed;
        RevealNameText.Text = SpeciesName(encounter.KnownSpeciesId!.Value);
        RevealNameText.Visibility = RevealSpriteImage.Visibility = Visibility.Visible;
        PokeBallCanvas.Visibility = Visibility.Collapsed;
        _ = LoadSpriteAsync(encounter.KnownSpeciesId.Value, encounter.Individual!.IsShiny, null, _artGeneration);
    }

    private void LoadScene(Guid? id)
    {
        try { SceneImage.Source = _fetchScene?.Invoke(id) is { } bytes ? SpriteImages.Decode(bytes) : null; }
        catch { SceneImage.Source = null; }
    }

    private async void CharmButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _suspended || _showingResult || _activateLuckyCharm is null || _appState.Shop.CatchLuckyCharmActive ||
            _appState.Shop.Items.GetValueOrDefault("lucky-charm") < 1) return;
        _busy = true; _error = null; Refresh();
        try { await _activateLuckyCharm(); _onStateChanged(); }
        catch (Exception error) { _error = "Не удалось активировать талисман: " + error.Message; }
        finally { _busy = false; Refresh(); }
    }

    private async void CatchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _suspended || _revealCancellation is not null) return;
        if (_showingResult) {
            _showingResult = false; _pendingReveal = null; _spriteFetchedFor = null; _selectedEncounterId = null;
            _sceneKey = null; _error = null;
            _onResultContinued?.Invoke();
            Refresh();
            return;
        }
        var encounter = CurrentEncounter();
        if (_selectedEncounterId is not null && encounter is null) { _error = "Выбранная встреча уже недоступна."; Refresh(); return; }
        _selectedEncounterId ??= encounter?.Id;
        var generation = _generation;
        _busy = true; _error = null; Refresh();
        try {
            var reveal = _selectedEncounterId is { } id && _tryPerformEncounter is not null
                ? await _tryPerformEncounter(id) : await _tryPerformCatch();
            if (reveal is null) { _error = "Пока нет готового покебола."; return; }
            _pendingReveal = reveal;
            _onStateChanged();
            // A commit may finish after navigation or after this instance has reloaded.
            if (_suspended || generation != _generation) { RestoreReveal(reveal); return; }
            await PlayReveal(reveal);
        }
        catch (Exception error) { _error = "Не удалось открыть покебол: " + error.Message; }
        finally { _busy = false; Refresh(); }
    }

    public async Task PlayReveal(CatchReveal reveal)
    {
        if (_revealCancellation is not null) return;
        _pendingReveal = reveal; _showingResult = true;
        if (_suspended) { ShowResult(reveal); return; }
        var generation = _generation;
        var cancellation = _revealCancellation = new CancellationTokenSource();
        CatchButton.IsEnabled = false;
        SkipButton.Visibility = Visibility.Visible;
        DetailPanel.Visibility = Visibility.Collapsed; System.Windows.Controls.Grid.SetColumnSpan(VisualPanel, 2);
        EncounterTitleText.Text = _knownSpeciesId is { } knownId ? "Ловим " + SpeciesName(knownId) : "Новая встреча";
        SubtitleText.Text = "Пора познакомиться с новым другом.";
        SceneImage.Visibility = Visibility.Visible; LoadScene(_selectedEncounterId ?? CurrentEncounter()?.Id);
        PokeBallCanvas.Visibility = Visibility.Visible; PokeBallCanvas.Margin = new Thickness(0);
        CatchBallImage.Source = (ImageSource)FindResource("UiBallClosedImage");
        // Only a known expedition's already public sprite may remain visible before capture.
        if (_knownSpeciesId is null) {
            _artGeneration++; RevealSpriteImage.Source = null; RevealSpriteImage.Visibility = Visibility.Collapsed;
            RevealNameText.Visibility = RevealRarityText.Visibility = Visibility.Collapsed;
        }
        try {
            if (!_animationEnabled()) {
                StatusText.Text = "Пойман!";
                await _delay(TimeSpan.FromMilliseconds(250), cancellation.Token);
            }
            else {
                StatusText.Text = "Покебол летит…";
                PokeBallThrow.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-160, 0, TimeSpan.FromMilliseconds(1100)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
                PokeBallThrow.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(70, -50, TimeSpan.FromMilliseconds(1100)));
                await _delay(TimeSpan.FromMilliseconds(1100), cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                StatusText.Text = "Почти получилось…";
                CatchBallImage.Source = (ImageSource)FindResource("UiBallOpenImage");
                RevealSpriteImage.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(650)));
                PetScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(650)));
                PetScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(650)));
                await _delay(TimeSpan.FromMilliseconds(650), cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                StatusText.Text = "Мягкое приземление…";
                CatchBallImage.Source = (ImageSource)FindResource("UiBallClosedImage");
                PokeBallThrow.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-50, 45, TimeSpan.FromMilliseconds(800)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
                await _delay(TimeSpan.FromMilliseconds(800), cancellation.Token);
                for (var wobble = 0; wobble < 3; wobble++) {
                    cancellation.Token.ThrowIfCancellationRequested();
                    StatusText.Text = "Покебол покачивается…";
                    var shake = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(1000) };
                    shake.KeyFrames.Add(new EasingDoubleKeyFrame(-12, KeyTime.FromPercent(.12)));
                    shake.KeyFrames.Add(new EasingDoubleKeyFrame(12, KeyTime.FromPercent(.28)));
                    shake.KeyFrames.Add(new EasingDoubleKeyFrame(-7, KeyTime.FromPercent(.42)));
                    shake.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(.55)));
                    shake.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromPercent(1)));
                    PokeBallRotate.BeginAnimation(RotateTransform.AngleProperty, shake);
                    await _delay(TimeSpan.FromMilliseconds(1000), cancellation.Token);
                }
                cancellation.Token.ThrowIfCancellationRequested();
                StatusText.Text = "Пойман!";
                await _delay(TimeSpan.FromMilliseconds(700), cancellation.Token);
            }
            cancellation.Token.ThrowIfCancellationRequested();
            if (generation == _generation && ReferenceEquals(_pendingReveal, reveal)) ShowResult(reveal);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally {
            if (ReferenceEquals(_revealCancellation, cancellation)) _revealCancellation = null;
            cancellation.Dispose();
            Refresh();
        }
    }

    public void RestoreReveal(CatchReveal reveal)
    {
        _revealCancellation?.Cancel();
        _pendingReveal = reveal; _showingResult = true;
        ShowResult(reveal);
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        if (_suspended || _pendingReveal is not { } reveal) return;
        _revealCancellation?.Cancel();
        ShowResult(reveal);
    }

    private void AnimationPreferenceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SystemParameters.ClientAreaAnimation) || SystemParameters.ClientAreaAnimation) return;
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => AnimationPreferenceChanged(sender, e)); return; }
        if (_suspended) return;
        if (_pendingReveal is { } reveal) { _revealCancellation?.Cancel(); ShowResult(reveal); }
    }

    private void StopAnimations()
    {
        PokeBallThrow.BeginAnimation(TranslateTransform.XProperty, null); PokeBallThrow.X = 0;
        PokeBallThrow.BeginAnimation(TranslateTransform.YProperty, null); PokeBallThrow.Y = 0;
        PokeBallRotate.BeginAnimation(RotateTransform.AngleProperty, null); PokeBallRotate.Angle = 0;
        PetScale.BeginAnimation(ScaleTransform.ScaleXProperty, null); PetScale.ScaleX = 1;
        PetScale.BeginAnimation(ScaleTransform.ScaleYProperty, null); PetScale.ScaleY = 1;
        RevealSpriteImage.BeginAnimation(OpacityProperty, null); RevealSpriteImage.Opacity = 1;
    }

    private void ShowResult(CatchReveal reveal)
    {
        StopAnimations();
        _pendingReveal = reveal; _showingResult = true;
        DetailPanel.Visibility = Visibility.Visible; System.Windows.Controls.Grid.SetColumnSpan(VisualPanel, 1);
        SceneImage.Visibility = SceneHeadingText.Visibility = CharmPanel.Visibility = ProgressPanel.Visibility = SkipButton.Visibility = Visibility.Collapsed;
        PokeBallCanvas.Visibility = Visibility.Visible; PokeBallCanvas.Margin = new Thickness(0, 150, 0, 0);
        CatchBallImage.Source = (ImageSource)FindResource("UiBallOpenImage");
        EncounterTitleText.Text = "Теперь вы знакомы!";
        SubtitleText.Text = SpeciesName(reveal.Result.SpeciesId) + " уже добавлен в коллекцию.";
        RevealNameText.Text = SpeciesName(reveal.Result.SpeciesId) + (reveal.Result.IsShiny ? " ✨" : "");
        RevealRarityText.Text = reveal.Rarity switch { RarityTier.Common => "Обычный", RarityTier.Uncommon => "Необычный", RarityTier.Rare => "Редкий", _ => "Легендарный" };
        ResultDetailText.Text = reveal.Result.WasDuplicateSpecies ? "Ещё один питомец знакомого вида." : "Новый вид в твоей коллекции!";
        RevealNameText.Visibility = RevealRarityText.Visibility = ResultDetailText.Visibility = RevealSpriteImage.Visibility = Visibility.Visible;
        CatchButton.Content = "Продолжить";
        CatchButton.IsEnabled = !_busy || _suspended;
        StatusText.Text = "Покемон уже добавлен в коллекцию.";
        if (!_suspended && !ReferenceEquals(_spriteFetchedFor, reveal)) {
            _spriteFetchedFor = reveal; RevealSpriteImage.Source = null;
            _ = LoadSpriteAsync(reveal.Result.SpeciesId, reveal.Result.IsShiny, reveal, ++_artGeneration);
        }
    }

    private async Task LoadSpriteAsync(int speciesId, bool shiny, CatchReveal? reveal, int generation)
    {
        try {
            // A shiny result cannot silently use the legacy normal-only art callback.
            var bytes = _fetchVariantSprite is not null ? await _fetchVariantSprite(speciesId, shiny)
                : shiny ? null : await _fetchSprite(speciesId);
            if (!_suspended && generation == _artGeneration && (reveal is null || ReferenceEquals(_pendingReveal, reveal)) && bytes is not null)
                RevealSpriteImage.Source = SpriteImages.Decode(bytes);
        }
        catch { /* The saved identity stays visible if its exact art is unavailable. */ }
    }
}
