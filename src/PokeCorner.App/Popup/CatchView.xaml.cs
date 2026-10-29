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
    // Set when an expedition sent us here: its result button leads back instead of stranding the player on an empty catch tab.
    private bool _returnAfterResult;
    private readonly Action? _returnFromEncounter;
    private int? _knownSpeciesId;
    private CatchReveal? _pendingReveal, _spriteFetchedFor;
    private CancellationTokenSource? _revealCancellation;

    /// <summary>The page's own way back to the corner; the shell decides where that is.</summary>
    public event Action? BackRequested;

    private void BackToCornerButton_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke();

    public CatchView(AppState appState, Func<CatchReveal?>? tryPerformCatch, Func<int, Task<byte[]?>> fetchSprite,
        Func<int> catchReadyCount, Action onStateChanged, Func<int, string> resolveSpeciesName,
        Func<Task<CatchReveal?>>? tryPerformCatchAsync = null, Action? onResultContinued = null,
        Func<Guid, Task<CatchReveal?>>? tryPerformEncounterAsync = null,
        Func<int, bool, Task<byte[]?>>? fetchVariantSprite = null, Func<Task>? activateLuckyCharmAsync = null,
        Func<bool>? animationEnabled = null, Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        Func<Guid?, byte[]?>? fetchScene = null, string? luckyCharmArtPath = null, Action? returnFromEncounter = null)
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
        _returnFromEncounter = returnFromEncounter;
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
        _returnAfterResult = _returnFromEncounter is not null;
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
        BackToCornerButton.IsEnabled = !_busy && _revealCancellation is null; // not mid-throw: the throw commits first
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
        PokeBallThrow.Y = GroundY; // an earned or gift ball already lies on the ground, waiting to be opened
        SceneHeadingText.Visibility = CharmPanel.Visibility = ProgressPanel.Visibility = Visibility.Visible;
        SceneHeadingText.Text = "Твой ещё один друг";
        DetailPanel.Visibility = Visibility.Visible; System.Windows.Controls.Grid.SetColumnSpan(VisualPanel, 1);
        SkipButton.Visibility = Visibility.Collapsed;
        var known = encounter is { Source: "expedition", Phase: "resolved", KnownSpeciesId: not null, Individual: not null };
        // A met Pokémon is caught with a throw; a ball earned at the PC or gifted already holds one, so it is opened.
        CatchButton.Content = known ? "Бросить покебол" : "Открыть покебол";
        EncounterTitleText.Text = known ? "Встреча с " + SpeciesName(encounter!.KnownSpeciesId!.Value) : "Кто внутри?";
        SubtitleText.Text = known ? "Этот покемон встретился команде в походе." : "Ещё один друг ждёт знакомства.";
        SceneImage.Visibility = Visibility.Visible;
        LoadScene(encounter?.Id);
        _knownSpeciesId = known ? encounter!.KnownSpeciesId : null;
        if (!known) return;
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
            var goBack = _returnAfterResult; _returnAfterResult = false;
            _onResultContinued?.Invoke();
            Refresh();
            if (goBack) _returnFromEncounter?.Invoke();
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
        catch (Exception error) { _error = "Не удалось открыть покебол, он остался у вас. Попробуйте ещё раз.\nПричина: " + error.Message; }
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
        PokeBallCanvas.Visibility = Visibility.Visible;
        CatchBallImage.Source = (ImageSource)FindResource("UiBallClosedImage");
        var thrown = _knownSpeciesId is not null;
        // Only a known expedition's already public sprite may remain visible before capture.
        if (!thrown) {
            _artGeneration++; RevealSpriteImage.Source = null; RevealSpriteImage.Visibility = Visibility.Collapsed;
            RevealNameText.Visibility = RevealRarityText.Visibility = Visibility.Collapsed;
        }
        try {
            if (!_animationEnabled()) {
                StatusText.Text = "Пойман!";
                await _delay(TimeSpan.FromMilliseconds(250), cancellation.Token);
            }
            else if (thrown) await PlayThrow(cancellation.Token);
            else await PlayOpening(cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (generation == _generation && ReferenceEquals(_pendingReveal, reveal)) ShowResult(reveal, popIn: _animationEnabled());
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally {
            if (ReferenceEquals(_revealCancellation, cancellation)) _revealCancellation = null;
            cancellation.Dispose();
            Refresh();
        }
    }

    // Ball centre offsets inside the 250 px scene: resting on the ground, and where it meets a Pokémon standing on it.
    private const double GroundY = 72, HitY = -28;

    // Expedition encounter, ~3.5 s: arc with spin, opens on contact and takes the Pokémon in, falls, bounces, rocks three times, clicks.
    private async Task PlayThrow(CancellationToken token)
    {
        StatusText.Text = "Покебол летит…";
        Animate(PokeBallThrow, TranslateTransform.XProperty, 600, K(0, -300), K(1, 0));
        Animate(PokeBallThrow, TranslateTransform.YProperty, 600, K(0, 90), K(.55, -95, EasingMode.EaseOut), K(1, HitY, EasingMode.EaseIn));
        Animate(PokeBallSpin, RotateTransform.AngleProperty, 600, K(0, 0), K(1, 540, EasingMode.EaseOut));
        await _delay(TimeSpan.FromMilliseconds(600), token);
        token.ThrowIfCancellationRequested();
        StatusText.Text = "Покебол раскрылся…";
        CatchBallImage.Source = (ImageSource)FindResource("UiBallOpenImage");
        Sparks(HitY, 8, 34, NdBrush("NdLavenderBrush"));
        Animate(RevealSpriteImage, OpacityProperty, 450, K(0, 1), K(1, 0, EasingMode.EaseIn));
        Animate(PetScale, ScaleTransform.ScaleXProperty, 450, K(0, 1), K(1, 0, EasingMode.EaseIn));
        Animate(PetScale, ScaleTransform.ScaleYProperty, 450, K(0, 1), K(1, 0, EasingMode.EaseIn));
        await _delay(TimeSpan.FromMilliseconds(450), token);
        token.ThrowIfCancellationRequested();
        StatusText.Text = "Мягкое приземление…";
        CatchBallImage.Source = (ImageSource)FindResource("UiBallClosedImage");
        // Falls with gravity (ease-in), lands squashed, then two small bounces.
        Animate(PokeBallThrow, TranslateTransform.YProperty, 450, K(0, HitY), K(.5, GroundY, EasingMode.EaseIn),
            K(.68, GroundY - 14, EasingMode.EaseOut), K(.84, GroundY, EasingMode.EaseIn), K(.93, GroundY - 5, EasingMode.EaseOut), K(1, GroundY, EasingMode.EaseIn));
        Animate(BallSquash, ScaleTransform.ScaleYProperty, 450, K(0, 1), K(.5, 1), K(.56, .8), K(.68, 1), K(.84, 1), K(.88, .92), K(1, 1));
        Animate(BallSquash, ScaleTransform.ScaleXProperty, 450, K(0, 1), K(.5, 1), K(.56, 1.16), K(.68, 1), K(.84, 1), K(.88, 1.06), K(1, 1));
        await _delay(TimeSpan.FromMilliseconds(450), token);
        for (var wobble = 0; wobble < 3; wobble++) {
            token.ThrowIfCancellationRequested();
            StatusText.Text = "Покебол покачивается…";
            Rock(550, 14 - wobble * 3);
            await _delay(TimeSpan.FromMilliseconds(550), token);
        }
        token.ThrowIfCancellationRequested();
        StatusText.Text = "Пойман!";
        Sparks(GroundY, 4, 26, NdBrush("NdApricotBrush"));
        await _delay(TimeSpan.FromMilliseconds(350), token);
    }

    // Earned or gift ball, ~2.1 s with the pop-in: it rustles, rocks twice, springs open; ShowResult lets the Pokémon hop out.
    private async Task PlayOpening(CancellationToken token)
    {
        PokeBallThrow.Y = GroundY;
        StatusText.Text = "Кто-то шуршит внутри…";
        Rock(500, 5);
        await _delay(TimeSpan.FromMilliseconds(500), token);
        for (var wobble = 0; wobble < 2; wobble++) {
            token.ThrowIfCancellationRequested();
            StatusText.Text = "Покебол покачивается…";
            Rock(450, 12);
            await _delay(TimeSpan.FromMilliseconds(450), token);
        }
        token.ThrowIfCancellationRequested();
        StatusText.Text = "Открывается!";
        Animate(BallSquash, ScaleTransform.ScaleYProperty, 300, K(0, 1), K(.4, .82), K(1, 1, EasingMode.EaseOut));
        Animate(BallSquash, ScaleTransform.ScaleXProperty, 300, K(0, 1), K(.4, 1.14), K(1, 1, EasingMode.EaseOut));
        CatchBallImage.Source = (ImageSource)FindResource("UiBallOpenImage");
        Sparks(GroundY, 8, 40, NdBrush("NdLavenderBrush"));
        await _delay(TimeSpan.FromMilliseconds(300), token);
    }

    // Rocks the resting ball on its contact point: left, right, a smaller left, settle.
    private void Rock(int ms, double degrees) => Animate(PokeBallRotate, RotateTransform.AngleProperty, ms,
        K(0, 0), K(.2, -degrees, EasingMode.EaseOut), K(.5, degrees, EasingMode.EaseInOut), K(.75, -degrees / 2, EasingMode.EaseInOut), K(1, 0, EasingMode.EaseOut));

    private static EasingDoubleKeyFrame K(double at, double value, EasingMode ease = EasingMode.EaseInOut) =>
        new(value, KeyTime.FromPercent(at), new QuadraticEase { EasingMode = ease });

    private static void Animate(IAnimatable target, DependencyProperty property, int ms, params EasingDoubleKeyFrame[] frames)
    {
        var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(ms) };
        foreach (var frame in frames) animation.KeyFrames.Add(frame);
        target.BeginAnimation(property, animation);
    }

    private System.Windows.Media.Brush NdBrush(string key) => TryFindResource(key) as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.White;

    // Square pixel sparks flying out of the ball and fading: no glow, Night Diary keeps light effects out of the window (D-22).
    private void Sparks(double centreY, int count, double reach, System.Windows.Media.Brush brush)
    {
        SparkLayer.Children.Clear();
        var cx = CaptureScene.ActualWidth / 2; var cy = CaptureScene.ActualHeight / 2 + centreY;
        for (var i = 0; i < count; i++) {
            var angle = Math.PI * 2 * i / count - Math.PI / 2;
            var spark = new System.Windows.Shapes.Rectangle { Width = 5, Height = 5, Fill = brush, Opacity = 0 };
            var move = new TranslateTransform();
            spark.RenderTransform = move;
            System.Windows.Controls.Canvas.SetLeft(spark, cx - 2.5); System.Windows.Controls.Canvas.SetTop(spark, cy - 2.5);
            SparkLayer.Children.Add(spark);
            Animate(move, TranslateTransform.XProperty, 420, K(0, Math.Cos(angle) * reach * .4, EasingMode.EaseOut), K(1, Math.Cos(angle) * reach, EasingMode.EaseOut));
            Animate(move, TranslateTransform.YProperty, 420, K(0, Math.Sin(angle) * reach * .4, EasingMode.EaseOut), K(1, Math.Sin(angle) * reach, EasingMode.EaseOut));
            Animate(spark, OpacityProperty, 420, K(0, 1, EasingMode.EaseOut), K(.6, 1), K(1, 0, EasingMode.EaseIn));
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
        PokeBallSpin.BeginAnimation(RotateTransform.AngleProperty, null); PokeBallSpin.Angle = 0;
        BallSquash.BeginAnimation(ScaleTransform.ScaleXProperty, null); BallSquash.ScaleX = 1;
        BallSquash.BeginAnimation(ScaleTransform.ScaleYProperty, null); BallSquash.ScaleY = 1;
        PetHop.BeginAnimation(TranslateTransform.YProperty, null); PetHop.Y = 0;
        SparkLayer.Children.Clear();
    }

    private void ShowResult(CatchReveal reveal, bool popIn = false)
    {
        StopAnimations();
        _pendingReveal = reveal; _showingResult = true;
        DetailPanel.Visibility = Visibility.Visible; System.Windows.Controls.Grid.SetColumnSpan(VisualPanel, 1);
        SceneHeadingText.Visibility = CharmPanel.Visibility = ProgressPanel.Visibility = SkipButton.Visibility = Visibility.Collapsed;
        // The new friend stands on the same scene; the empty ball would only sit under its feet.
        if (SceneImage.Source is null) LoadScene(_selectedEncounterId);
        SceneImage.Visibility = Visibility.Visible;
        PokeBallCanvas.Visibility = Visibility.Collapsed;
        if (popIn) {
            Animate(PetScale, ScaleTransform.ScaleXProperty, 400, K(0, .2), K(.7, 1.08, EasingMode.EaseOut), K(1, 1));
            Animate(PetScale, ScaleTransform.ScaleYProperty, 400, K(0, .2), K(.7, 1.08, EasingMode.EaseOut), K(1, 1));
            Animate(PetHop, TranslateTransform.YProperty, 400, K(0, GroundY - 16), K(.6, -14, EasingMode.EaseOut), K(1, 0, EasingMode.EaseIn));
        }
        EncounterTitleText.Text = "Теперь вы знакомы!";
        SubtitleText.Text = SpeciesName(reveal.Result.SpeciesId) + " уже добавлен в коллекцию.";
        RevealNameText.Text = SpeciesName(reveal.Result.SpeciesId) + (reveal.Result.IsShiny ? " ✨" : "");
        RevealRarityText.Text = reveal.Rarity switch { RarityTier.Common => "Обычный", RarityTier.Uncommon => "Необычный", RarityTier.Rare => "Редкий", _ => "Легендарный" };
        ResultDetailText.Text = reveal.Result.WasDuplicateSpecies ? "Ещё один питомец знакомого вида." : "Новый вид в твоей коллекции!";
        RevealNameText.Visibility = RevealRarityText.Visibility = ResultDetailText.Visibility = RevealSpriteImage.Visibility = Visibility.Visible;
        CatchButton.Content = _returnAfterResult ? "Вернуться к экспедициям" : "Продолжить";
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
