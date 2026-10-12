using PokeCorner.App.Imaging;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using PokeCorner.App.Fullscreen;
using PokeCorner.App.Input;
using PokeCorner.App.Onboarding;
using PokeCorner.App.Overlay;
using PokeCorner.App.Popup;
using PokeCorner.App.Tray;
using PokeCorner.Core;
using PokeCorner.Core.Audio;
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;
using PokeCorner.Core.Persistence;

namespace PokeCorner.App;

public partial class App : System.Windows.Application
{
    private AppState _appState = null!;
    private JsonSaveStore _saveStore = null!;
    private SingleInstance _singleInstance = null!;
    private const string SingleInstanceName = @"Local\PokeCorner.SingleInstance";
    private const string AfterRestartArg = "--after-restart";
    private BackgroundSaveWriter _saveWriter = null!;
    private SpriteCache _spriteCache = null!;
    private PokeApiClient _pokeApiClient = null!;
    private SpeciesCatalog _speciesCatalog = null!;
    private CatalogPackage _catalogPackage = null!;
    private LegacyLookup _legacyLookup = null!;
    private bool _isExiting;
    private GlobalInputHook? _inputHook;
    private GamepadInputHook? _gamepadHook;
    private InputEventCounter _inputCounter = null!;
    private DispatcherTimer _growthTickTimer = null!;
    private OverlayWindow _overlayWindow = null!;
    private TrayIconService _trayIconService = null!;
    private ForegroundWindowMonitor _fullscreenMonitor = null!;
    private CatchRoller _catchRoller = null!;
    private EggIncubationService _eggIncubationService = null!;
    private CareModeService _careModeService = null!;
    private FeedingService _feedingService = null!;
    private bool _isFullscreenHiding;
    private bool _isFullscreenMonitorRunning;
    private int _overlayRefreshGeneration;
    private AppMenuWindow? _menuWindow;
    private ShopService _shopService = null!;
    private const int CollectionTabIndex = 0;
    private const int ShopTabIndex = 1;
    private const int CatchTabIndex = 2;
    private const int SettingsTabIndex = 3;
    private ScreenCorner _appliedCorner;
    private int _appliedMonitorIndex;
    private int _appliedPetSizePixels;

    /// Catches waiting to be claimed on the Catch tab, up to MaxPendingCatches; rolls past the
    /// cap are dropped.
    private int _catchReadyCount;
    private const int MaxPendingCatches = 3;
    /// Every decoded frame of the active individual's sprite — the list the overlay animates
    /// from. One element means a static sprite; null means nothing is cached yet.
    private IReadOnlyList<byte[]>? _activeSpriteFrames;
    private ProgressPanelWindow? _progressPanelWindow;

    /// POKE-9 diagnostics: %APPDATA%\PokeCorner\zorder.log, next to the save file, so a user can
    /// find and send it. Lines only on z-order state changes (see ZOrderLog).
    private readonly ZOrderLog _zOrderLog = new(Path.Combine(Path.GetDirectoryName(AppPaths.GetSaveFilePath())!, "zorder.log"));
    private static readonly TimeSpan AutosaveInterval = TimeSpan.FromSeconds(5);
    private DateTimeOffset _lastAutosaveUtc = DateTimeOffset.MinValue;
    private Guid? _evolutionReadyNotifiedFor;
    private Guid? _neglectWarningNotifiedFor;

    /// Set once by ResetSaveDataAndRestart; makes PersistState a no-op for the rest of this
    /// process's life so nothing writes the save file back after it has been deleted.
    private bool _isWipingSaveData;
    private bool _hasLoadedState;

    /// Decoded PNG frames per display species id (null = no usable animation), in front of the
    /// on-disk GIF cache. A GIF costs ~22 ms to decode, so each species is decoded once.
    private readonly Dictionary<int, IReadOnlyList<byte[]>?> _decodedFramesCache = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Before anything loads state: a second copy would autosave over this one's progress.
        // After "reset save data" the new process starts while the old one is still exiting,
        // so it waits for the name instead of bowing out.
        var singleInstance = SingleInstance.TryAcquire(SingleInstanceName,
            e.Args.Contains(AfterRestartArg) ? TimeSpan.FromSeconds(10) : TimeSpan.Zero);
        if (singleInstance is null)
        {
            SingleInstance.SignalRunningInstance(SingleInstanceName);
            Shutdown();
            return;
        }
        _singleInstance = singleInstance;

        DispatcherUnhandledException += (_, args) =>
        {
            try { SaveState(); } catch { /* best-effort: never let the save path itself throw during a crash handler */ }
            args.Handled = false; // still let it crash — we're not trying to keep a corrupted UI state running, just to not lose data
        };

        _saveStore = new JsonSaveStore(AppPaths.GetV2SaveFilePath());
        _saveWriter = new BackgroundSaveWriter(_saveStore);
        _spriteCache = new SpriteCache(AppPaths.GetSpriteCacheDirectory());
        _pokeApiClient = new PokeApiClient(new System.Net.Http.HttpClientHandler());
        var catalogPath = Path.Combine(AppPaths.GetAppDataDirectory(), "catalog.json");
        _speciesCatalog = new SpeciesCatalog(_pokeApiClient, _spriteCache, catalogPath);
        _catalogPackage = CatalogPackage.Load(AppPaths.GetAppDataDirectory());

        _catchRoller = new CatchRoller();
        _eggIncubationService = new EggIncubationService();
        _careModeService = new CareModeService();
        _feedingService = new FeedingService();
        _shopService = new ShopService();

        SaveData? loaded;
        var isNewGame = false;
        while (true)
        {
            try
            {
                var dataRoot = AppPaths.GetAppDataDirectory();
                var preserved = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PokeCorner", LegacyLookup.PreservedLookupFileName);
                _legacyLookup = File.Exists(AppPaths.GetV2SaveFilePath())
                    ? LegacyLookup.Load(dataRoot)
                    : LegacyLookup.Load(dataRoot, catalogPath, preserved);
                loaded = await SaveMigration.LoadAsync(AppPaths.GetSaveFilePath(), AppPaths.GetV2SaveFilePath(), _legacyLookup, _catalogPackage);
                isNewGame = loaded is null;
                if (loaded is null)
                {
                    loaded = SaveMigration.Migrate(new SaveData { Individuals = [], Settings = new() }, _legacyLookup, _catalogPackage);
                }
                _appState = new AppState();
                _appState.Hydrate(loaded);
                break;
            }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
            {
                var choice = System.Windows.MessageBox.Show(
                    ex.Message + "\n\nДа — повторить загрузку. Нет — выбрать исправную резервную копию. Отмена — закрыть приложение.",
                    "PokéCorner — сохранение недоступно", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (choice == MessageBoxResult.Yes) continue;
                if (choice == MessageBoxResult.No)
                {
                    var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Выберите резервную копию PokéCorner", Filter = "Сохранение JSON|*.json" };
                    if (dialog.ShowDialog() == true)
                    {
                        var target = File.Exists(AppPaths.GetV2SaveFilePath()) ? AppPaths.GetV2SaveFilePath() : AppPaths.GetSaveFilePath();
                        try { SaveMigration.RestoreBackup(dialog.FileName, target, target == AppPaths.GetV2SaveFilePath() ? 2 : 1); }
                        catch (Exception restoreError) when (restoreError is IOException or UnauthorizedAccessException)
                        { System.Windows.MessageBox.Show(restoreError.Message, "PokéCorner — резервная копия недоступна"); }
                    }
                    continue;
                }
                _singleInstance.Dispose();
                Shutdown();
                return;
            }
        }
        _catchReadyCount = loaded?.PendingCatchCount ?? 0;

        if (FirstRunFlow.NeedsOnboarding(loaded))
        {
            // The bundled full catalog makes this work with no network on a first launch.
            var allChains = GetDirectIssueChains();
            var commonCandidates = allChains
                .Where(c => RarityClassifier.Classify(c.CaptureRate) == RarityTier.Common)
                .OrderBy(_ => Random.Shared.Next())
                .Take(3)
                .ToList();
            if (commonCandidates.Count == 0)
            {
                // Defensive fallback — shouldn't happen given the bundled catalog's real
                // capture rates, but never leave the player with zero choices.
                commonCandidates = allChains.Take(3).ToList();
            }

            var selectionWindow = new StarterSelectionWindow(
                commonCandidates,
                speciesId => _spriteCache.GetOrFetchAsync(speciesId, () => _pokeApiClient.FetchSpriteAsync(speciesId)),
                GetAnimatedFramesAsync);
            var dialogChoseOne = selectionWindow.ShowDialog() == true;
            var chosenChain = dialogChoseOne ? selectionWindow.Chosen! : commonCandidates[0]; // window closed without a pick (e.g. Alt+F4) still needs a starter

            var starter = FirstRunFlow.HatchChosen(chosenChain, _catchRoller, DateTimeOffset.UtcNow, _catalogPackage);
            starter.AcquisitionSource = "starter";
            SaveMigration.InitializeIndividual(starter, _legacyLookup, _catalogPackage);
            _appState.Collection.Add(starter);
            if (isNewGame)
            {
                _appState.Shop.Coins = 20;
                _appState.Feeding.Treats = 5;
            }

            new OnboardingWindow().ShowDialog();
        }

        _hasLoadedState = true;
        _appState.CatchRollDue += OnCatchRollDue; // now just flips "ready" state — see OnCatchRollDue

        _inputCounter = new InputEventCounter();

        // Started on the dispatcher thread — which is why OnGrowthTick must stay cheap; see its comment.
        _inputHook = new GlobalInputHook(_inputCounter);
        _inputHook.Start();

        // A controller counts as PC use too. Polls on a thread-pool timer; no-op without XInput.
        _gamepadHook = new GamepadInputHook(_inputCounter);
        _gamepadHook.Start();

        // One second so the progress bars keep up with activity; the autosave is throttled
        // separately inside OnGrowthTick.
        _growthTickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _growthTickTimer.Tick += (_, _) => OnGrowthTick();
        _growthTickTimer.Start();

        _overlayWindow = new OverlayWindow();
        _overlayWindow.PetClicked += PetActiveIndividual;
        _overlayWindow.MenuRequested += () => OpenMenu(CollectionTabIndex);
        // Re-top the pet the moment another app comes to the front, not on the next tick.
        _overlayWindow.ForegroundWindowChanged += ReassertOverlayTopmost;
        // Remember where the player actually leaves the pet, so relaunch/reboot restores that
        // spot instead of always snapping back to the configured corner.
        _overlayWindow.PositionChanged += () =>
        {
            var x = (int)_overlayWindow.Left;
            var y = (int)_overlayWindow.Top;
            _appState.RunWhenReady(() =>
            {
                _appState.Settings.PetPositionX = x;
                _appState.Settings.PetPositionY = y;
                PersistState();
            });
        };

        // Keeps the progress panel glued to the pet during a drag: LocationChanged is the only
        // signal raised inside DragMove()'s modal loop.
        _overlayWindow.LocationChanged += (_, _) =>
        {
            if (_progressPanelWindow is null) return;
            _progressPanelWindow.PositionLeftOf(_overlayWindow);
            ReassertOverlayTopmost(); // PositionLeftOf re-tops the panel; put the pet back on top of it
        };

        // Fires only for a fullscreen app on the pet's OWN monitor; the pet's handle is read on
        // every poll, so a pet dragged to another monitor is judged where it actually is.
        _fullscreenMonitor = new ForegroundWindowMonitor(
            onFullscreenEntered: () => { _isFullscreenHiding = true; RefreshOverlayForActiveIndividual(); },
            onFullscreenExited: () => { _isFullscreenHiding = false; RefreshOverlayForActiveIndividual(); },
            petWindowHandle: () => new System.Windows.Interop.WindowInteropHelper(_overlayWindow).Handle);

        _trayIconService = new TrayIconService();
        _trayIconService.OpenCollectionRequested += () => OpenMenu(CollectionTabIndex);
        _trayIconService.OpenSettingsRequested += () => OpenMenu(SettingsTabIndex);
        _trayIconService.OpenShopRequested += () => OpenMenu(ShopTabIndex);
        _trayIconService.CatchRequested += () => OpenMenu(CatchTabIndex);
        _trayIconService.ExitRequested += ShutdownApp;
        // Restore the tray's ready glyph if a catch was already queued when the app last closed
        // (see _catchReadyCount's hydration above) — the AppMenuWindow badge doesn't need the
        // same treatment since it's created lazily and reads the live count when it is.
        _trayIconService.SetCatchReady(_catchReadyCount > 0);

        // MUST come after _trayIconService exists. This async void runs straight through to
        // _trayIconService.SetSpriteIcon when the sprite is already memoized — which it is right after
        // onboarding — so placed earlier it dereferenced null on first run.
        RefreshOverlayForActiveIndividual();

        ReconcileSettingsState();

        _singleInstance.ActivationRequested += () => Dispatcher.BeginInvoke(() => OpenMenu(CollectionTabIndex));
        // Windows logging off must not be blocked by the pet window refusing to close.
        SessionEnding += (_, args) =>
        {
            if (_appState.IsCommitting) { args.Cancel = true; ShutdownApp(); }
            else AllowDeskWindowsToClose();
        };
        Exit += (_, _) =>
        {
            SaveState();
            _singleInstance.Dispose();
        };
    }

    /// EVERYTHING ON THIS TICK MUST STAY CHEAP — a system-wide correctness constraint, not style.
    /// This runs on the dispatcher thread, which owns the WH_MOUSE_LL/WH_KEYBOARD_LL hooks
    /// (GlobalInputHook). Windows delivers no low-level input to ANY application while that
    /// thread is busy, so N ms spent here freezes the cursor across the desktop for N ms, once
    /// per tick (measured at ~190 ms when a disk-walking poll once ran here). Disk, network and
    /// large collections belong on a background thread, with the result handed back here.
    private void OnGrowthTick()
    {
        if (!_appState.IsCommitting) ReconcileSettingsState();

        // See FallbackActivityMonitor for why this exists: some anti-cheat drivers (Genshin
        // Impact's mhyprot2.sys is the reported case) silently stop GlobalInputHook's low-level
        // hook callbacks from firing at all, system-wide, while loaded. GetTimeSinceLastInput
        // is a single syscall reading a value the OS already maintains — cheap enough for this
        // tick's hard "no disk/network" budget, same as everything else here.
        if (FallbackActivityMonitor.ShouldCreditFallbackUnit(DateTimeOffset.UtcNow, _inputCounter.LastHookRecordedUtc, SystemIdleTime.GetTimeSinceLastInput()))
        {
            _inputCounter.RecordFallbackUnit();
        }

        var units = _inputCounter.DrainUnits();
        if (units > 0) _appState.RunWhenReady(() => CreditActivity(units));
        if (_appState.IsCommitting) return;

        _progressPanelWindow?.UpdateProgress(ProgressCalculator.Compute(_appState.Activity, _appState.Collection.GetActive(), _appState.Feeding));

        CheckHardcoreNeglect();
        CheckEvolutionReady();

        // Backstop for the ForegroundWindowChanged hook: plenty of things reorder the normal
        // z-order band without a foreground change (a window being restored, shown, or moved
        // by another app), and SetWindowPos on an already-top window is a no-op, so this is
        // cheap enough to just keep doing on the existing tick.
        ReassertOverlayTopmost();
        // After the re-raise on purpose: what gets logged is what survived it.
        _zOrderLog.Append(
            ZOrderProbe.Describe(new System.Windows.Interop.WindowInteropHelper(_overlayWindow).Handle),
            ZOrderProbe.ForegroundDescription(),
            DateTime.Now);

        // Autosave every ~5 s, so a crash or Task Manager kill loses little. PersistState only
        // queues the write; the disk work happens off this thread.
        var nowUtc = DateTimeOffset.UtcNow;
        if (nowUtc - _lastAutosaveUtc >= AutosaveInterval)
        {
            _lastAutosaveUtc = nowUtc;
            PersistState();
        }
    }

    /// Opacity floor at mood=0.0 for Hardcore/Super Hardcore's mood tint — dropped well below
    /// Chill's implicit 0.5 default so a pet at the run-away deadline genuinely reads as
    /// "fading away" rather than just slightly dimmed. See OverlayWindow.SetMoodTint.
    private const double HardcoreMoodOpacityFloor = 0.18;

    /// Hardcore's run-away mechanic, polled from the growth tick.
    private void CheckHardcoreNeglect()
    {
        if (_appState.Settings.CareMode != CareMode.Hardcore)
        {
            // No neglect consequence outside Hardcore — clear any droop/dim/mood-cloud left
            // over from a mode switch, since nothing else will call SetMoodTint to reset it.
            _overlayWindow.ResetMoodTint();
            return;
        }

        var active = _appState.Collection.GetActive();
        if (active is null) return;

        var careState = _appState.GetOrCreateCareState(active.Id);
        var now = DateTimeOffset.UtcNow;

        // Before anything reads the neglect clock: a PC left on overnight, or the app being
        // closed for hours, must not cost the player their pet. Only the active individual is
        // adjusted — the same scope everything else in this method works at.
        _careModeService.DiscountIdleGap(careState, now);

        // SuperHardcoreEnabled is a flag layered on top of Hardcore (see the check below), not
        // a separate CareMode value — both share the same recalibrated 6h mood/run-away clock.
        _overlayWindow.SetMoodTint(_careModeService.ComputeMood(careState, now, CareMode.Hardcore), HardcoreMoodOpacityFloor);

        // One-shot warning shortly before the pet actually runs away — same guard idiom as
        // CheckEvolutionReady, re-armed whenever the condition isn't currently true (so
        // petting/feeding before the deadline, or a later neglect cycle, can warn again).
        var timeUntilRunAway = careState.LastInteractionUtc + CareModeTuning.HardcoreNeglectThreshold - now;
        if (careState.RanAwayUntilUtc is null && timeUntilRunAway <= CareModeTuning.NeglectWarningLeadTime && timeUntilRunAway > TimeSpan.Zero)
        {
            if (_neglectWarningNotifiedFor != active.Id)
            {
                _neglectWarningNotifiedFor = active.Id;
                _trayIconService.ShowNotification("PokéCorner", "Ещё немного — и питомец обидится. Загляни к нему!");
            }
        }
        else
        {
            _neglectWarningNotifiedFor = null;
        }

        if (!_careModeService.ShouldRunAway(careState, now, _appState.Settings.CareMode)) return;

        _overlayWindow.PlayRanAwayReaction();

        if (_appState.Settings.SuperHardcoreEnabled)
        {
            // Супер Хардкор: neglect is permanent. Same path as the collection's "Отпустить"
            // button — Release() also drops them out of the active slot (promoting whoever is
            // next in the collection, exactly as a manual release does), so no ClearActive()
            // and no run-away cooldown to set on a care state that no longer belongs to anyone.
            _appState.Collection.Release(active.Id);
            _appState.CareStateByIndividualId.Remove(active.Id);
            RefreshOverlayForActiveIndividual();
            _trayIconService.ShowNotification("PokéCorner", "Супер Хардкор: питомец ушёл навсегда.");
            return;
        }

        _careModeService.TriggerRunAway(careState);
        _appState.Collection.ClearActive();
        RefreshOverlayForActiveIndividual();
        _trayIconService.ShowNotification("PokéCorner", "Питомец расстроен и не хочет возвращаться на стол ещё час.");
    }

    /// One-shot "ready to evolve" toast per individual; re-armed when readiness drops or the
    /// active pet changes.
    private void CheckEvolutionReady()
    {
        var active = _appState.Collection.GetActive();
        if (active is null || !_appState.IsReadyToEvolve(active))
        {
            _evolutionReadyNotifiedFor = null;
            return;
        }

        if (_evolutionReadyNotifiedFor == active.Id) return;

        _evolutionReadyNotifiedFor = active.Id;
        _trayIconService.ShowNotification("PokéCorner", "Покемон готов эволюционировать!");
    }

    private void ReconcileSettingsState()
    {
        if (_appState.Settings.AutoHideInFullscreen && !_isFullscreenMonitorRunning)
        {
            _fullscreenMonitor.Start();
            _isFullscreenMonitorRunning = true;
        }
        else if (!_appState.Settings.AutoHideInFullscreen && _isFullscreenMonitorRunning)
        {
            _fullscreenMonitor.Stop();
            _isFullscreenMonitorRunning = false;
            // The overlay may have been mid-hide from a fullscreen app when the user
            // turned auto-hide off — force it back to reflecting the real active state.
            _isFullscreenHiding = false;
            RefreshOverlayForActiveIndividual();
        }

        ApplyPetSizeAndPosition();

        if (_appState.Settings.ShowProgressPanel && _progressPanelWindow is null)
        {
            _progressPanelWindow = new ProgressPanelWindow();
            _progressPanelWindow.PositionLeftOf(_overlayWindow);
            // Prime the bars now rather than leaving them empty until the next tick.
            _progressPanelWindow.UpdateProgress(ProgressCalculator.Compute(_appState.Activity, _appState.Collection.GetActive(), _appState.Feeding));
            // Same fullscreen gate the overlay itself honours — turning the panel on from the
            // settings screen while a fullscreen app is in front must not pop it over that app.
            if (!_isFullscreenHiding)
            {
                _progressPanelWindow.Show();
            }
        }
        else if (!_appState.Settings.ShowProgressPanel && _progressPanelWindow is not null)
        {
            _progressPanelWindow.AllowClose = true;
            _progressPanelWindow.Close();
            _progressPanelWindow = null;
        }

        _progressPanelWindow?.PositionLeftOf(_overlayWindow);
    }

    /// Applies the corner/monitor/size settings to the overlay (dragging the progress panel
    /// along), only when something changed — repositioning on every call would fight the user
    /// dragging the pet. The settings screen calls it as the size slider moves, for a live preview.
    private void ApplyPetSizeAndPosition()
    {
        var desiredCorner = ParseCorner(_appState.Settings.ScreenCorner);
        if (desiredCorner == _appliedCorner
            && _appState.Settings.MonitorIndex == _appliedMonitorIndex
            && _appState.Settings.PetSizePixels == _appliedPetSizePixels)
        {
            return;
        }

        _overlayWindow.PositionAt(desiredCorner, _appState.Settings.MonitorIndex, _appState.Settings.PetSizePixels,
            _appState.Settings.PetPositionX, _appState.Settings.PetPositionY);
        _appliedCorner = desiredCorner;
        _appliedMonitorIndex = _appState.Settings.MonitorIndex;
        _appliedPetSizePixels = _appState.Settings.PetSizePixels;

        // The panel is anchored to the overlay's left edge, which just moved.
        _progressPanelWindow?.PositionLeftOf(_overlayWindow);
    }

    /// Single entry point for "what should the overlay show right now" — called after
    /// startup, after switching/releasing in the collection popup, and after every
    /// fullscreen enter/exit, so the desktop sprite never drifts out of sync with
    /// AppState.Collection.GetActive().
    ///
    /// async void is intentional here: every call site is a fire-and-forget statement
    /// from a non-async context (WPF event handlers/callbacks), and none of them await
    /// this method. A generation counter guards against re-entrancy — if the active
    /// individual changes again while a fetch is in flight, the stale fetch's result is
    /// discarded instead of clobbering the newer one.
    private async void RefreshOverlayForActiveIndividual()
    {
        var generation = ++_overlayRefreshGeneration;

        var active = _appState.Collection.GetActive();
        if (active is null)
        {
            // Drop the cached sprite too, otherwise the collection popup's hero keeps
            // rendering (and now animating) the pet that just ran away or was released.
            _activeSpriteFrames = null;
            _overlayWindow.Hide();
            _progressPanelWindow?.Hide(); // nothing on the desk to show progress for
            return;
        }

        var displaySpeciesId = ResolveDisplaySpeciesId(active);

        var pngFrames = await GetAnimatedFramesAsync(displaySpeciesId);

        if (generation != _overlayRefreshGeneration)
        {
            // A newer refresh started (and possibly already finished) while we were awaiting
            // the animated-sprite fetch above — applying our now-stale result would clobber
            // it. Mirrors the staleness check below for the static path.
            return;
        }

        if (pngFrames is not null)
        {
            _overlayWindow.SetSpriteFrames(pngFrames.Select(SpriteImages.Decode).ToList());
            _activeSpriteFrames = pngFrames;
            _trayIconService.SetSpriteIcon(pngFrames[0]);

            ApplyFullscreenVisibility();
            return;
        }

        // No animated sprite for this species (or the fetch/decode failed) — fall through to
        // the static PNG path below.

        byte[]? spriteBytes = null;
        try
        {
            spriteBytes = await _spriteCache.GetOrFetchAsync(displaySpeciesId, () => _pokeApiClient.FetchSpriteAsync(displaySpeciesId));
        }
        catch
        {
            // Network errors are already handled inside Core by returning null; this is
            // just defense in depth so an unexpected exception here doesn't take the app
            // down. Skip the frame update and leave whatever the overlay was already
            // showing, matching the existing null-handling philosophy below.
        }

        if (generation != _overlayRefreshGeneration)
        {
            // A newer refresh started (and possibly already finished) while we were
            // awaiting the fetch above — applying our now-stale result would clobber it.
            return;
        }

        // null on a network failure (offline first launch, nothing cached): keep whatever the
        // overlay already shows; the next refresh retries.
        if (spriteBytes is not null)
        {
            _overlayWindow.SetSpriteFrames(new List<System.Windows.Media.ImageSource> { SpriteImages.Decode(spriteBytes) });
            _activeSpriteFrames = new[] { spriteBytes }; // one real frame: static, but not "unknown"
            _trayIconService.SetSpriteIcon(spriteBytes);
        }

        ApplyFullscreenVisibility();
    }

    private void ApplyFullscreenVisibility()
    {
        if (_isFullscreenHiding)
        {
            _overlayWindow.Hide();
            _progressPanelWindow?.Hide();
            return;
        }

        _overlayWindow.Show();
        _progressPanelWindow?.Show();
        // Show() doesn't guarantee topmost placement — without this the pet could sit
        // freshly-shown but buried until the next growth tick (up to 1s later).
        ReassertOverlayTopmost();
    }

    /// One species' animated sprite as PNG frames, or null when it has none (plenty don't) or
    /// the fetch/decode failed — callers then fall back to the static PNG. The overlay, the
    /// Pokédex and the starter picker share this memo; each species is decoded once, on a worker
    /// thread. The returned list is shared: read-only, and nobody writes into the frame arrays.
    private async Task<IReadOnlyList<byte[]>?> GetAnimatedFramesAsync(int speciesId)
    {
        if (_decodedFramesCache.TryGetValue(speciesId, out var memoized)) return memoized;

        byte[]? animatedBytes = null;
        // The selected package provides verified local GIFs; an unavailable species has no animation.
        var gifPath = _catalogPackage.GetAssetPath(speciesId, false);
        if (gifPath is null) return null;
        try
        {
            if (File.Exists(gifPath))
            {
                animatedBytes = await File.ReadAllBytesAsync(gifPath);
            }
        }
        catch (IOException)
        {
            animatedBytes = null;
        }

        IReadOnlyList<byte[]>? decoded = null;
        if (animatedBytes is not null)
        {
            try
            {
                // A non-GIF or truncated response body makes the decode throw, and a
                // malformed-but-decodable GIF can yield zero frames — both mean "no animation".
                var gif = animatedBytes;
                var frames = await Task.Run(() => GifFrameDecoder.DecodeFramesAsPng(gif));
                decoded = frames.Count > 0 ? frames : null;
            }
            catch
            {
                decoded = null;
            }
        }

        // ponytail: unbounded, one entry per species ever shown (measured: ~57 MB if all 638 are viewed);
        // add LRU eviction if memory ever matters. A null from a failed network fetch is not
        // memoized, so it is retried on the next call.
        if (animatedBytes is not null) _decodedFramesCache[speciesId] = decoded;
        return decoded;
    }

    // LoadForLookup, not LoadCachedOrFallback: an individual migrated off the old colliding
    // offline pool sits on a reserved legacy chain id that only the lookup view contains.
    private int ResolveDisplaySpeciesId(PokemonIndividual individual) =>
        individual.ResolveDisplaySpeciesId(_speciesCatalog.LoadForLookup());


    /// Activity crossed the catch threshold: queue a catch (capped at MaxPendingCatches) for the
    /// player to claim on the Catch tab. Never catches automatically — that took the moment away.
    private void OnCatchRollDue()
    {
        if (_catchReadyCount >= MaxPendingCatches) return; // hard cap — extra rolls are discarded

        var wasIdle = _catchReadyCount == 0;
        _catchReadyCount++;
        _trayIconService.SetCatchReady(true);
        _menuWindow?.SetCatchReady(_catchReadyCount);

        // Only toast on the 0->1 transition — don't spam a toast per extra queued catch.
        if (wasIdle)
        {
            _trayIconService.ShowNotification("PokéCorner", "Встретили нового покемона, скорее поймай его!");
        }
    }

    private CatchReveal? TryPerformCatch()
    {
        if (_isExiting || _appState.IsCommitting) return null;
        if (_catchReadyCount <= 0) return null;

        // PokemonCollection.Add() auto-activates via ActiveIndividualId ??= individual.Id
        // whenever nothing is currently active — correct for onboarding, but the only way
        // ActiveIndividualId is null with a non-empty collection here is a deliberate
        // Hardcore run-away, and nobody should auto-appear on the desk after that.
        var hadNoActiveInNonEmptyCollection = _appState.Collection.ActiveIndividualId is null && _appState.Collection.Individuals.Count > 0;

        var availableChains = GetDirectIssueChains();
        if (availableChains.Count == 0) return null;
        var shinyMultiplier = _shopService.ConsumeCatchLuckyCharm(_appState.Shop);
        var result = ValidateDirectResult(_catchRoller.RollCatch(availableChains, _appState.Collection.Individuals, shinyMultiplier));

        var individual = result.ToIndividual(DateTimeOffset.UtcNow);
        individual.AcquisitionSource = "activity";
        SaveMigration.InitializeIndividual(individual, _legacyLookup, _catalogPackage);
        _appState.Collection.Add(individual);

        if (hadNoActiveInNonEmptyCollection)
        {
            _appState.Collection.ClearActive();
        }

        _catchReadyCount--;
        _trayIconService.SetCatchReady(_catchReadyCount > 0);
        _menuWindow?.SetCatchReady(_catchReadyCount);
        RefreshOverlayForActiveIndividual();

        var chain = availableChains.FirstOrDefault(c => c.ChainId == result.ChainId);
        var rarity = chain is null ? RarityTier.Common : RarityClassifier.Classify(chain.CaptureRate);
        return new CatchReveal(result, rarity);
    }

    /// No toast: the bounce is the feedback, and pets get clicked far too often for a
    /// notification each time.
    private void PetActiveIndividual()
    {
        if (_isExiting) return;
        if (_appState.IsCommitting) { _appState.RunWhenReady(PetActiveIndividual); return; }
        var active = _appState.Collection.GetActive();
        if (active is null) return;

        var careState = _appState.GetOrCreateCareState(active.Id);
        if (!_feedingService.Pet(active, careState, DateTimeOffset.UtcNow)) return; // on cooldown

        _overlayWindow.PlayPetReaction();
        PlayCry(ResolveDisplaySpeciesId(active));
    }

    /// One player for every cry, created lazily. MediaPlayer rather than SoundPlayer because it
    /// has a volume and plays the bundled PCM WAVs with no codec. A single instance because each
    /// holds an unmanaged audio session with no deterministic Dispose.
    private System.Windows.Media.MediaPlayer? _cryPlayer;

    /// Fire-and-forget cry for a species, from the WAV bundled at Data\cries\{id}.wav.
    ///
    /// Gated on a successful pet only, so a cooldown-rejected spam click stays silent rather
    /// than machine-gunning the sound.
    ///
    /// Every failure is swallowed on purpose: a species with no upstream cry, a gap in the
    /// crawl, or a machine with no audio device at all should be a click that makes no sound,
    /// never a crash or a toast. Cosmetic sound is not worth a single user-visible error. The
    /// MediaFailed subscription is part of that: MediaPlayer reports a broken file through that
    /// event rather than by throwing out of Open/Play, and an unhandled one surfaces on the
    /// dispatcher instead of staying silent.
    private void PlayCry(int speciesId)
    {
        try
        {
            var path = _catalogPackage.GetVoicePath(speciesId);
            if (path is null) return;
            if (!File.Exists(path)) return;

            if (_cryPlayer is null)
            {
                _cryPlayer = new System.Windows.Media.MediaPlayer();
                _cryPlayer.MediaFailed += (_, _) => { /* no sound this click; see above */ };
            }

            // Read fresh every play, so dragging the settings slider mid-session is heard on
            // the very next click with no restart. 0 maps to 0.0, which is real silence.
            _cryPlayer.Volume = CryVolume.ToPlayerVolume(_appState.Settings.CrySoundVolume);

            // Re-Open every time: MediaPlayer keeps the previous media open and would replay the
            // previous species; opening also rewinds, so the same species twice is audible twice.
            _cryPlayer.Open(new Uri(path));
            _cryPlayer.Play();
        }
        catch
        {
            // no sound this click; see above
        }
    }

    private void FeedActiveIndividual()
    {
        if (_isExiting) return;
        if (_appState.IsCommitting) { _appState.RunWhenReady(FeedActiveIndividual); return; }
        var active = _appState.Collection.GetActive();
        if (active is null) return;

        var careState = _appState.GetOrCreateCareState(active.Id);
        var fed = _feedingService.Feed(active, _appState.Feeding, careState, DateTimeOffset.UtcNow);
        if (fed)
        {
            _overlayWindow.PlayFeedReaction();
        }
        else
        {
            // Unlike a successful feed, running out of treats has no on-screen animation, so without
            // the toast the click would look like it did nothing.
            _trayIconService.ShowNotification("PokéCorner", "Не хватает лакомств — накопи ещё активности.");
        }
    }

    /// Evolution is an explicit player action. False when the active individual isn't ready,
    /// so a stale click is inert.
    private bool EvolveActiveIndividual()
    {
        if (_isExiting || _appState.IsCommitting) return false;
        var active = _appState.Collection.GetActive();
        if (active is null || !_appState.TryEvolve(active)) return false;

        SaveMigration.InitializeIndividual(active, _legacyLookup, _catalogPackage);
        _overlayWindow.PlayEvolutionFlash();
        RefreshOverlayForActiveIndividual();
        return true;
    }

    private bool IsActiveIndividualReadyToEvolve()
    {
        var active = _appState.Collection.GetActive();
        return active is not null && _appState.IsReadyToEvolve(active);
    }

    private bool HatchEgg(Guid eggId)
    {
        if (_isExiting || _appState.IsCommitting) return false;
        var egg = _appState.PendingEggs.FirstOrDefault(e => e.Id == eggId);
        if (egg is null || !_eggIncubationService.IsReady(egg, DateTimeOffset.UtcNow)) return false;

        // A save written before the shop's tier gating (or hand-edited) can hold an egg of a tier
        // the catalog can't fill yet: keep the egg — and the lucky charm — for a later retry.
        if (!_catchRoller.TryRollCatchFromTier(GetDirectIssueChains(), egg.Tier, _appState.Collection.Individuals,
                () => _shopService.ConsumeCatchLuckyCharm(_appState.Shop), out var result))
        {
            _trayIconService.ShowNotification("PokéCorner", "Пока нет доступных покемонов этой редкости — попробуй позже, когда каталог обновится.");
            return false;
        }

        result = ValidateDirectResult(result);
        var individual = result.ToIndividual(DateTimeOffset.UtcNow);
        individual.AcquisitionSource = "legacy-shop-egg";
        SaveMigration.InitializeIndividual(individual, _legacyLookup, _catalogPackage);
        _appState.Collection.Add(individual);
        _appState.PendingEggs.Remove(egg);

        _menuWindow?.ShowCatchReveal(new CatchReveal(result, egg.Tier));
        return true;
    }

    /// Whether the catalog in use has a chain in this rarity tier, so the shop can't sell an
    /// egg that can never hatch.
    private bool IsRarityTierAvailable(RarityTier tier) =>
        GetDirectIssueChains().Any(c => RarityClassifier.Classify(c.CaptureRate) == tier);

    private List<SpeciesEvolutionChain> GetDirectIssueChains() =>
        _speciesCatalog.LoadCachedOrFallback().Where(c => _catalogPackage.CanIssueDirectly(c.StarterSpeciesId, false)).ToList();

    private CatchResult ValidateDirectResult(CatchResult result)
    {
        if (!_catalogPackage.CanIssueDirectly(result.SpeciesId, false)) throw new InvalidDataException("Catch must be an available family root.");
        return result.IsShiny && !_catalogPackage.CanIssueDirectly(result.SpeciesId, true)
            ? result with { IsShiny = false } : result;
    }

    private bool BuyEgg(RarityTier tier)
    {
        if (_isExiting || _appState.IsCommitting) return false;
        if (!_shopService.TrySpendForEgg(_appState.Shop, tier)) return false;
        _appState.PendingEggs.Add(_eggIncubationService.StartIncubating(tier, DateTimeOffset.UtcNow));
        return true;
    }

    private void OpenMenu(int tabIndex)
    {
        if (_isExiting) return;
        if (_appState.IsCommitting) { _appState.RunWhenReady(() => OpenMenu(tabIndex)); return; }
        try
        {
            if (_menuWindow is null)
            {
                // LoadForLookup so the collection/Pokédex can resolve names, sprites and rarity
                // for individuals sitting on a migrated legacy chain id, not just real ones.
                var lookupChains = _speciesCatalog.LoadForLookup();
                var collectionVm = new CollectionViewModel(_appState.Collection, _appState.CareStateByIndividualId, _careModeService, _appState.Settings.CareMode, lookupChains, _appState.PendingEggs);
                var collectionView = new CollectionView(collectionVm, _appState, _spriteCache, PetActiveIndividual, FeedActiveIndividual, RefreshOverlayForActiveIndividual, () => _activeSpriteFrames, HatchEgg, EvolveActiveIndividual, IsActiveIndividualReadyToEvolve, GetAnimatedFramesAsync,
                    speciesId => _spriteCache.GetOrFetchAsync(speciesId, () => _pokeApiClient.FetchSpriteAsync(speciesId)));
                var settingsView = new SettingsView(new SettingsViewModel(_appState.Settings), ApplyPetSizeAndPosition, ResetSaveDataAndRestart);
                var shopView = new ShopView(_appState, _shopService, BuyEgg, IsRarityTierAvailable, RefreshOverlayForActiveIndividual);
                var catchView = new CatchView(_appState, TryPerformCatch, speciesId => _spriteCache.GetOrFetchAsync(speciesId, () => _pokeApiClient.FetchSpriteAsync(speciesId)), () => _catchReadyCount, RefreshOverlayForActiveIndividual, speciesId => _speciesCatalog.LoadForLookup().ResolveSpeciesName(speciesId));
                _menuWindow = new AppMenuWindow(collectionView, settingsView, shopView, catchView);
                _menuWindow.Closed += (_, _) => _menuWindow = null;
                _menuWindow.SetCatchReady(_catchReadyCount); // picks up a count queued before the menu ever opened
                _menuWindow.Show();
            }
            _menuWindow.SelectTab(tabIndex);
            // Activate() alone does not un-minimize a window — only guard against Minimized,
            // not stomp a Maximized state the player may have set via MaximizeButton_Click.
            if (_menuWindow.WindowState == WindowState.Minimized)
            {
                _menuWindow.WindowState = WindowState.Normal;
            }
            _menuWindow.Activate();
            ReassertOverlayTopmost();
        }
        catch (Exception ex)
        {
            _trayIconService.ShowNotification("PokéCorner — ошибка", ex.Message);
        }
    }

    /// Re-slots the progress panel and the pet behind the taskbar (see
    /// OverlayWindow.KeepAboveAppsBelowShell). Order matters: each call inserts directly behind
    /// the taskbar, so the last one ends up on top — the panel goes first so the pet's bounce
    /// can scale over it.
    private void ReassertOverlayTopmost()
    {
        if (_progressPanelWindow is not null)
        {
            OverlayWindow.KeepAboveAppsBelowShell(_progressPanelWindow);
        }
        OverlayWindow.KeepAboveAppsBelowShell(_overlayWindow);
    }

    private static ScreenCorner ParseCorner(string corner) => corner switch
    {
        "TopLeft" => ScreenCorner.TopLeft,
        "TopRight" => ScreenCorner.TopRight,
        "BottomLeft" => ScreenCorner.BottomLeft,
        _ => ScreenCorner.BottomRight
    };

    private void AllowDeskWindowsToClose()
    {
        _overlayWindow.AllowClose = true;
        if (_progressPanelWindow is not null) _progressPanelWindow.AllowClose = true;
    }

    /// Shared durable mutation entry point for the v2 gameplay services.
    public async Task CommitStateAsync(Action<SaveData> update)
    {
        if (!_hasLoadedState || _isExiting) throw new InvalidOperationException("Сохранение ещё не загружено.");
        if (_menuWindow is not null) _menuWindow.IsEnabled = false;
        try
        {
            await _appState.CommitAsync(_saveWriter, update, () => _catchReadyCount, next => _catchReadyCount = next.PendingCatchCount);
            _menuWindow?.Close();
            RefreshOverlayForActiveIndividual();
        }
        finally { if (_menuWindow is not null) _menuWindow.IsEnabled = true; }
    }

    private void CreditActivity(long units)
    {
        _appState.ApplyActivityUnits(units);
        _feedingService.EarnTreatsFromActivity(_appState.Feeding, units);
        _shopService.EarnCoinsFromActivity(_appState.Shop, units);
    }

    private async void ShutdownApp()
    {
        if (_isExiting) return;
        _isExiting = true;
        if (_menuWindow is not null) _menuWindow.IsEnabled = false;
        if (_overlayWindow is not null) _overlayWindow.IsEnabled = false;
        _growthTickTimer?.Stop();
        _inputHook?.Stop();
        _gamepadHook?.Stop();
        var units = _inputCounter?.DrainUnits() ?? 0;
        if (units > 0) _appState.RunWhenReady(() => CreditActivity(units));
        try
        {
            if (_hasLoadedState && !_isWipingSaveData)
                await _appState.FlushAsync(_saveWriter, () => _catchReadyCount);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show(ex.Message + "\nПоследнее сохранение не записано. Закрытие отменено.",
                "PokéCorner — ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Warning);
            _isExiting = false;
            if (_menuWindow is not null) _menuWindow.IsEnabled = true;
            if (_overlayWindow is not null) _overlayWindow.IsEnabled = true;
            _growthTickTimer?.Start();
            _inputHook?.Start();
            _gamepadHook?.Start();
            return;
        }
        AllowDeskWindowsToClose();
        Shutdown();
    }

    /// Deletes the save file and relaunches, so the new process runs first-run onboarding
    /// as if freshly installed. Both active save files go; the immutable v1 backup remains: the sprite cache and catalog.json are
    /// asset caches, and rebuilding the catalog would turn a reset into a multi-minute wait.
    private void ResetSaveDataAndRestart()
    {
        if (_appState.IsCommitting) { _appState.RunWhenReady(ResetSaveDataAndRestart); return; }
        _isWipingSaveData = true;
        _saveWriter.Delete();
        new JsonSaveStore(AppPaths.GetSaveFilePath()).Delete(); // explicit reset must not re-import v1 at restart

        Process.Start(Environment.ProcessPath!, AfterRestartArg);

        // Shutdown(), not Environment.Exit: it runs the Exit handler, which disposes the hooks and
        // the tray icon; the save write in it is suppressed by _isWipingSaveData.
        ShutdownApp();
    }

    private void SaveState()
    {
        _inputHook?.Dispose();
        _gamepadHook?.Dispose();
        _fullscreenMonitor?.Dispose();
        _trayIconService?.Dispose();

        if (!_isExiting) PersistState(flush: true);
    }

    /// Autosaves (flush: false) only queue the disk write on the thread pool — they run on the
    /// growth tick, whose thread owns the input hooks. Exit and the crash handler flush
    /// synchronously, so the process can't end before the final state is on disk.
    private void PersistState(bool flush = false)
    {
        // ResetSaveDataAndRestart deleted state.json; writing now would bring it straight back.
        if (_isWipingSaveData || !_hasLoadedState || _appState.IsCommitting) return;

        var data = _appState.ToSaveData(_catchReadyCount);
        if (flush) _saveWriter.Flush(data);
        else _saveWriter.Post(data);
    }
}
