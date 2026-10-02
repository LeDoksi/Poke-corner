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

    /// Replaces the old one-shot `_catchReady` bool: activity kept accumulating past the
    /// catch-check threshold while a catch was already pending used to silently discard those
    /// extra rolls. Now they queue, up to MaxPendingCatches.
    private int _catchReadyCount;
    private const int MaxPendingCatches = 3;
    /// Every decoded frame of the active individual's sprite, in order — the same list the
    /// overlay animates from. This replaces the previous single-frame `_activeSpriteBytes`
    /// field: the collection popup only ever received frame 0 through it, which is why its
    /// hero sprite sat perfectly still while the desktop pet animated. A single-element list
    /// means "this species has no animated sprite", which the hero renders statically; null
    /// means "nothing cached yet".
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

    /// Decoded PNG frames per display species id (null = "no usable animation"), in front of
    /// the on-disk GIF cache. Decoding a GIF is NOT cheap: measured ~22ms per species, ~350ms for
    /// one 16-card Pokédex page, which was the page-flip stutter of POKE-18.
    private readonly Dictionary<int, IReadOnlyList<byte[]>?> _decodedFramesCache = new();

    protected override void OnStartup(StartupEventArgs e)
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

        _saveStore = new JsonSaveStore(AppPaths.GetSaveFilePath());
        _saveWriter = new BackgroundSaveWriter(_saveStore);
        _spriteCache = new SpriteCache(AppPaths.GetSpriteCacheDirectory());
        _pokeApiClient = new PokeApiClient(new System.Net.Http.HttpClientHandler());
        var catalogPath = Path.Combine(AppPaths.GetAppDataDirectory(), "catalog.json");
        _speciesCatalog = new SpeciesCatalog(_pokeApiClient, _spriteCache, catalogPath);

        // The refresh crawls ~650 species (1000+ sequential requests against the free public
        // PokeAPI), so firing it on every launch of an autostart-with-Windows app is both
        // wasteful and rude to that API. Species data changes rarely — a weekly refresh is
        // plenty. Fire-and-forget when it does run; today's rolls still use whatever
        // LoadCachedOrFallback returns right now.
        //
        // IsCacheSchemaCurrent is part of the staleness test, not just the age: a catalog.json
        // written before the crawler captured display names is "fresh" by any clock but
        // resolves every species to "Pokemon #{id}". LoadCachedOrFallback already refuses to
        // SERVE such a file (it returns the fully-named bundled catalog instead); this is what
        // makes the file on disk actually get rewritten, rather than being ignored forever.
        var catalogIsStale = !File.Exists(catalogPath)
            || File.GetLastWriteTimeUtc(catalogPath) < DateTime.UtcNow.AddDays(-7)
            || !_speciesCatalog.IsCacheSchemaCurrent();
        if (catalogIsStale)
        {
            // On the thread pool, not the dispatcher: each pokemon/{id}/ response is a few hundred
            // KB of JSON, and ~1600 of them parsed on the UI thread over ~5 minutes was the
            // after-launch stutter of POKE-18. Delayed because this app autostarts with Windows,
            // usually before the network is up — the first request then failed and took the
            // whole crawl down on every boot, so the file never actually refreshed.
            // ponytail: one attempt per launch, no retry; add a retry loop if a 2-minute head
            // start still loses to slow networks.
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(2));
                await _speciesCatalog.RefreshFromNetworkAsync();
            });
        }
        _catchRoller = new CatchRoller();
        _eggIncubationService = new EggIncubationService();
        _careModeService = new CareModeService();
        _feedingService = new FeedingService();
        _shopService = new ShopService();

        var loaded = _saveStore.Load();
        _appState = new AppState();
        if (loaded is not null) _appState.Hydrate(loaded);
        _catchReadyCount = Math.Clamp(loaded?.PendingCatchCount ?? 0, 0, MaxPendingCatches);

        if (FirstRunFlow.NeedsOnboarding(loaded))
        {
            // Reads the SAME catalog as every other catch-pool call site here. It used to
            // deliberately bypass SpeciesCatalog and read the narrow curated offline pool
            // directly, because only that pool was guaranteed to work with zero network on a
            // first launch. LoadCachedOrFallback's own fallback is now the complete bundled
            // Gen 1-5 catalog, so it carries that same zero-network guarantee — and offers
            // dozens of Common starters to choose between instead of two or three.
            var allChains = _speciesCatalog.LoadCachedOrFallback();
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

            var starter = FirstRunFlow.HatchChosen(chosenChain, _catchRoller, DateTimeOffset.UtcNow);
            _appState.Collection.Add(starter);

            new OnboardingWindow().ShowDialog();
        }

        _appState.CatchRollDue += OnCatchRollDue; // now just flips "ready" state — see OnCatchRollDue

        _inputCounter = new InputEventCounter();

        // Unconditional: "your pet grows from your PC usage" is the app's premise, not an
        // option, so there is no setting gating this any more. Started here on the dispatcher
        // thread — which is exactly why OnGrowthTick must stay cheap; see its comment.
        _inputHook = new GlobalInputHook(_inputCounter);
        _inputHook.Start();

        // Same counter, same "unconditional" rule — a controller is just another way of using
        // the PC. Unlike the hook above this one polls on a thread-pool timer, deliberately
        // nowhere near the dispatcher thread. No-ops if the machine has no XInput DLL.
        _gamepadHook = new GamepadInputHook(_inputCounter);
        _gamepadHook.Start();

        // One second, not five: this tick is what refreshes the progress bars, and at 5s they
        // visibly lagged behind the activity the player had just generated. Everything else on
        // the tick is cheap enough to run 5x more often — except the autosave, which is
        // throttled back to its original cadence inside OnGrowthTick.
        _growthTickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _growthTickTimer.Tick += (_, _) => OnGrowthTick();
        _growthTickTimer.Start();

        _overlayWindow = new OverlayWindow();
        _overlayWindow.PetClicked += PetActiveIndividual;
        _overlayWindow.MenuRequested += () => OpenMenu(CollectionTabIndex);
        // Re-top the pet the instant another app is brought to the front, rather than up to a
        // second later on the growth tick — a visible lag now that HWND_TOP (not the topmost
        // band) is what keeps it above ordinary windows.
        _overlayWindow.ForegroundWindowChanged += ReassertOverlayTopmost;
        // Remember where the player actually leaves the pet, so relaunch/reboot restores that
        // spot instead of always snapping back to the configured corner.
        _overlayWindow.PositionChanged += () =>
        {
            _appState.Settings.PetPositionX = (int)_overlayWindow.Left;
            _appState.Settings.PetPositionY = (int)_overlayWindow.Top;
            PersistState();
        };

        // Keeps the progress panel glued to the pet while it is being dragged. WPF raises
        // LocationChanged continuously throughout DragMove()'s modal move loop, which is the
        // only signal available during a drag: OverlayWindow.SpriteImage_MouseMove hands
        // control to DragMove() and doesn't get it back until the mouse is released, so
        // nothing in the App layer runs until then. Repositioning used to happen only on
        // discrete events (settings changes, the growth tick), so the panel visibly trailed
        // the sprite by up to a second and then snapped into place.
        _overlayWindow.LocationChanged += (_, _) =>
        {
            if (_progressPanelWindow is null) return;
            _progressPanelWindow.PositionLeftOf(_overlayWindow);
            ReassertOverlayTopmost(); // PositionLeftOf re-tops the panel; put the pet back on top of it
        };

        // The callbacks now mean "a fullscreen app is covering the pet's OWN monitor", not
        // "something, somewhere, went fullscreen" — a fullscreen window on another screen no
        // longer hides a pet it cannot possibly overlap. The handle is resolved per poll, not
        // captured, so a pet dragged between monitors is judged against where it actually is.
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

        // MUST come after _trayIconService exists, not before it. This is async void, and its
        // first await only suspends when the sprite actually has to be read from disk — after
        // onboarding the starter's frames are already in _decodedFramesCache (the starter picker
        // put it there), so the await completes synchronously and the method runs straight
        // through to _trayIconService.SetSpriteIcon in the same call. Sited above the tray
        // service's construction, that dereferenced null and crashed the app on first run
        // right after the tutorial — intermittently, since a species whose sprite does have to
        // be read from disk suspends instead and resumes safely after OnStartup returns.
        RefreshOverlayForActiveIndividual();

        ReconcileSettingsState();

        _singleInstance.ActivationRequested += () => Dispatcher.BeginInvoke(() => OpenMenu(CollectionTabIndex));
        // Windows logging off must not be blocked by the pet window refusing to close.
        SessionEnding += (_, _) => _overlayWindow.AllowClose = true;
        Exit += (_, _) =>
        {
            SaveState();
            _singleInstance.Dispose();
        };
    }

    /// EVERYTHING ON THIS TICK MUST STAY CHEAP — this is not a style preference, it is a
    /// system-wide correctness constraint. This runs on the WPF dispatcher thread, which is the
    /// thread that called SetWindowsHookEx for WH_MOUSE_LL/WH_KEYBOARD_LL (see GlobalInputHook,
    /// started in OnStartup). Windows cannot deliver low-level input to ANY application while
    /// the hook's owning thread is busy, so N milliseconds spent here freezes the mouse cursor
    /// for N milliseconds across the whole desktop, once per tick.
    ///
    /// That is not hypothetical: the Claude Code token-usage poll used to run inline here,
    /// walking ~/.claude/projects and JSON-parsing every session log written today. Measured at
    /// 176-196ms per tick on a real install, it froze the cursor system-wide once a second, in
    /// every application. (That feature is gone now — but the constraint it violated is not.)
    /// Anything that touches the disk, the network, or a large collection belongs on a
    /// background thread with the result handed back here, not in this method.
    private void OnGrowthTick()
    {
        ReconcileSettingsState();

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
        if (units > 0)
        {
            _appState.ApplyActivityUnits(units);
            _feedingService.EarnTreatsFromActivity(_appState.Feeding, units);
            _shopService.EarnCoinsFromActivity(_appState.Shop, units);
        }

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

        // Piggybacks on this timer so a crash or Task Manager kill never loses more than a
        // few seconds of session state. JsonSaveStore.Save already writes atomically (temp
        // file + rename), so calling it repeatedly here is safe. Deliberately kept on its
        // original ~5-second cadence rather than following the timer down to 1s: this writes
        // the entire save file to disk, and the finer tick exists for the progress bars, not
        // for the autosave. Uses PersistState() (write-only), NOT SaveState() — SaveState()
        // also disposes the input hook/fullscreen monitor/tray icon, which must stay alive.
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

    /// Hardcore care mode's "убегает" mechanic (spec: "Уход за питомцем") only has a
    /// visible effect if something polls for it — this is that poll, riding the same
    /// 5-second timer as growth so it doesn't need a timer of its own.
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

    /// The evolution-ready counterpart to OnCatchRollDue's one-shot toast. AppState.IsReadyToEvolve
    /// is a pure query with no "already told them" state of its own, so the same one-shot guard
    /// _catchReady gives the catch path lives here instead — keyed by individual id, so evolving
    /// (readiness drops) or switching pets both re-arm it for the next threshold crossing.
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
        // The input hook and the Claude Code token provider used to be started/stopped here from
        // their settings toggles. Token tracking is gone entirely, and PC activity tracking is
        // unconditional now — the hook is started once in OnStartup — so there is nothing left
        // to reconcile for either.
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
            // Prime the bars immediately. Without this the panel spends its first seconds
            // showing three empty tracks — OnGrowthTick only pushes values every 5 seconds,
            // so a freshly enabled panel looked broken right at the moment the user turned
            // it on to look at it.
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
            _progressPanelWindow.Close();
            _progressPanelWindow = null;
        }

        _progressPanelWindow?.PositionLeftOf(_overlayWindow);
    }

    /// Applies the corner/monitor/size settings to the overlay (and drags the progress panel
    /// along with it), but only when something actually changed — repositioning on every call
    /// would fight the user dragging the pet around.
    ///
    /// Extracted from ReconcileSettingsState so the settings screen can call it the instant
    /// the size slider moves. It used to run only on the 5-second growth tick, which meant
    /// dragging the slider changed the stored number immediately but the sprite on screen
    /// only caught up seconds later — unusable as a live preview.
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
            // Genuinely asynchronous now — no sync-over-async blocking on the WPF
            // dispatcher thread, which previously deadlocked forever on any uncached
            // sprite fetch (SpriteCache/PokeApiClient await without ConfigureAwait(false),
            // so their continuations were posted back to this same dispatcher thread that
            // .GetAwaiter().GetResult() was blocking).
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

        // GetOrFetchAsync returns null on a network failure (e.g. offline first launch
        // with nothing cached yet) rather than throwing — this is exactly the case the
        // spec's "работает без сети с первого раза" requirement covers. Skip the frame
        // update and leave whatever the overlay was already showing (or nothing, on a
        // true cold start) rather than crashing; RefreshOverlayForActiveIndividual will
        // be called again on the next state change or growth tick and can retry then.
        if (spriteBytes is not null)
        {
            // A single static frame for now — true Gen-V-style animated sprites come from
            // PokeAPI's separate animated-GIF sprite set, not the static front_default PNG
            // FetchSpriteAsync fetches; decoding multi-frame GIFs into FrameSequencer input
            // is Task 2 of docs/superpowers/plans/2026-08-19-pokecorner-full-experience-and-installer.md,
            // deliberately sequenced after this plan ships and is verified. SpriteAnimator/
            // FrameSequencer both already work correctly with a one-frame list (no visible
            // cycling, just a static sprite) so nothing here is broken in the meantime.
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

    /// Fetches and decodes one species' animated sprite into a list of PNG frames, or null
    /// when the species has no animated sprite (plenty don't) or the fetch/decode failed —
    /// every caller falls back to the static PNG in that case.
    ///
    /// Extracted from RefreshOverlayForActiveIndividual so the Pokédex cards and the starter
    /// picker animate from exactly the same memoized source. Without the shared memo the
    /// Pokédex would re-download a GIF per owned species every time the popup rebuilt its
    /// grid, which is once a second.
    ///
    /// The decoded frames are memoized too (_decodedFramesCache) and decoded on a worker
    /// thread: a page of the Pokédex used to decode all of its GIFs on the UI thread on every
    /// open. The returned list is shared between callers — it is read-only and nobody writes
    /// into the frame arrays.
    private async Task<IReadOnlyList<byte[]>?> GetAnimatedFramesAsync(int speciesId)
    {
        if (_decodedFramesCache.TryGetValue(speciesId, out var memoized)) return memoized;

        byte[]? animatedBytes = null;
        // Disk before network, mirroring SpriteCache's static-PNG contract. This is what
        // lets the GIFs the installer ships into Data\animated render on a fresh machine
        // with no network at all, and it stops a re-download on every app launch.
        var gifPath = Path.Combine(AppPaths.GetAnimatedSpriteCacheDirectory(), $"{speciesId}.gif");
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

        if (animatedBytes is null)
        {
            try
            {
                animatedBytes = await _pokeApiClient.FetchAnimatedSpriteAsync(speciesId);
            }
            catch
            {
                // Network errors already degrade to null inside PokeApiClient's normal paths;
                // this guards the unexpected ones rather than letting them reach
                // DispatcherUnhandledException, which deliberately doesn't suppress crashes.
                animatedBytes = null;
            }

            // Persist a freshly fetched GIF so the next launch reads it off disk. Best-effort:
            // a failed write only costs a re-fetch later, so it must never break rendering.
            if (animatedBytes is { Length: > 0 })
            {
                try
                {
                    Directory.CreateDirectory(AppPaths.GetAnimatedSpriteCacheDirectory());
                    await File.WriteAllBytesAsync(gifPath, animatedBytes);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
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


    /// Activity has crossed the threshold for a catch — per explicit feedback, this
    /// should NOT catch automatically (that took the moment away from the player).
    /// Instead it queues one up (capped at MaxPendingCatches — extra rolls past the cap are
    /// still discarded, a hard cap rather than unbounded banking) so the player triggers the
    /// actual catch themselves from the Catch tab — the tray's "Поймать!" item now just opens
    /// the menu there (see the CatchRequested subscription in OnStartup) and the tab's button
    /// calls TryPerformCatch.
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
        if (_catchReadyCount <= 0) return null;

        // PokemonCollection.Add() auto-activates via ActiveIndividualId ??= individual.Id
        // whenever nothing is currently active — correct for onboarding, but the only way
        // ActiveIndividualId is null with a non-empty collection here is a deliberate
        // Hardcore run-away, and nobody should auto-appear on the desk after that.
        var hadNoActiveInNonEmptyCollection = _appState.Collection.ActiveIndividualId is null && _appState.Collection.Individuals.Count > 0;

        var availableChains = _speciesCatalog.LoadCachedOrFallback();
        var shinyMultiplier = _shopService.ConsumeCatchLuckyCharm(_appState.Shop);
        var result = _catchRoller.RollCatch(availableChains, _appState.Collection.Individuals, shinyMultiplier);

        _appState.Collection.Add(result.ToIndividual(DateTimeOffset.UtcNow));

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

    /// No tray toast here on purpose: PlayPetReaction is already a visible on-screen response
    /// to every click, and pet/feed get clicked often enough that a notification per click was
    /// pure noise. Evolution no longer happens inline either — see EvolveActiveIndividual.
    private void PetActiveIndividual()
    {
        var active = _appState.Collection.GetActive();
        if (active is null) return;

        var careState = _appState.GetOrCreateCareState(active.Id);
        if (!_feedingService.Pet(active, careState, DateTimeOffset.UtcNow)) return; // on cooldown

        _overlayWindow.PlayPetReaction();
        PlayCry(ResolveDisplaySpeciesId(active));
    }

    /// The one player every cry goes through, created lazily on first use and then reused.
    ///
    /// This used to be `new System.Media.SoundPlayer(path).Play()` per click. SoundPlayer has
    /// no volume of its own — it always plays at full system volume — so the "Громкость крика
    /// питомца" setting could not exist while it was in charge. MediaPlayer is WPF's own player,
    /// takes a 0.0-1.0 Volume, and plays a local PCM WAV through Media Foundation with no codec
    /// to install (which is the whole reason the crawl transcodes the upstream Ogg cries to WAV
    /// — see PcmWavWriter). Play() is still non-blocking, so the pet animation is untouched.
    ///
    /// One instance rather than one per click: each MediaPlayer holds an unmanaged audio
    /// session, and there is no Dispose to release it deterministically, so constructing one
    /// per pet click would leak sessions for as long as the app runs.
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
            var path = Path.Combine(AppPaths.GetCryDirectory(), $"{speciesId}.wav");
            if (!File.Exists(path)) return;

            if (_cryPlayer is null)
            {
                _cryPlayer = new System.Windows.Media.MediaPlayer();
                _cryPlayer.MediaFailed += (_, _) => { /* no sound this click; see above */ };
            }

            // Read fresh every play, so dragging the settings slider mid-session is heard on
            // the very next click with no restart. 0 maps to 0.0, which is real silence.
            _cryPlayer.Volume = CryVolume.ToPlayerVolume(_appState.Settings.CrySoundVolume);

            // Re-Open every time. Unlike SoundPlayer, whose Play() re-reads its SoundLocation
            // per call, MediaPlayer holds an open media and would otherwise replay the PREVIOUS
            // species (or sit at the end of it) when the active pet changes. Opening also
            // rewinds, which is what makes two pets of the same species in a row both audible.
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
            // This one stays: unlike a successful feed, "you're out of treats" has no on-screen
            // animation of its own, so without the toast the click would look like it did nothing.
            _trayIconService.ShowNotification("PokéCorner", "Не хватает лакомств — накопи ещё активности.");
        }
    }

    /// Evolution is now an explicit player action rather than something that fires the instant
    /// a threshold is crossed — same shape as hatching a ready egg. Returns false when the
    /// active individual isn't actually ready (or there is none), so a stale click is inert.
    private bool EvolveActiveIndividual()
    {
        var active = _appState.Collection.GetActive();
        if (active is null || !_appState.TryEvolve(active)) return false;

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
        var egg = _appState.PendingEggs.FirstOrDefault(e => e.Id == eggId);
        if (egg is null || !_eggIncubationService.IsReady(egg, DateTimeOffset.UtcNow)) return false;

        // A save written before the shop's tier gating (or hand-edited) can hold an egg of a tier
        // the catalog can't fill yet: keep the egg — and the lucky charm — for a later retry.
        if (!_catchRoller.TryRollCatchFromTier(_speciesCatalog.LoadCachedOrFallback(), egg.Tier, _appState.Collection.Individuals,
                () => _shopService.ConsumeCatchLuckyCharm(_appState.Shop), out var result))
        {
            _trayIconService.ShowNotification("PokéCorner", "Пока нет доступных покемонов этой редкости — попробуй позже, когда каталог обновится.");
            return false;
        }

        _appState.Collection.Add(result.ToIndividual(DateTimeOffset.UtcNow));
        _appState.PendingEggs.Remove(egg);

        _menuWindow?.ShowCatchReveal(new CatchReveal(result, egg.Tier));
        return true;
    }

    /// Whether the species catalog currently in use (same source as BuyEgg/HatchEgg) has at
    /// least one chain in this rarity tier, so the shop can't sell an egg that can never
    /// hatch — spending the coins for good and crashing on the hatch attempt.
    ///
    /// The old 7-chain offline fallback had no Uncommon and no Legendary chains at all, which
    /// is what made this guard load-bearing offline. The bundled full catalog covers all four
    /// tiers, so it now only really fires against a hand-edited or partially-written cache —
    /// still worth keeping as the cheap defense it always was.
    private bool IsRarityTierAvailable(RarityTier tier) =>
        _speciesCatalog.LoadCachedOrFallback().Any(c => RarityClassifier.Classify(c.CaptureRate) == tier);

    private bool BuyEgg(RarityTier tier)
    {
        if (!_shopService.TrySpendForEgg(_appState.Shop, tier)) return false;
        _appState.PendingEggs.Add(_eggIncubationService.StartIncubating(tier, DateTimeOffset.UtcNow));
        return true;
    }

    private void OpenMenu(int tabIndex)
    {
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

    /// Re-inserts the overlay (and the panel anchored to it) directly behind the taskbar, so
    /// it stays above ordinary app windows but below every shell surface. Neither window sets
    /// Topmost="True" any more — that put them in the flat topmost band, where they could and
    /// did render over the taskbar and tray flyouts. See OverlayWindow.KeepAboveAppsBelowShell
    /// for why the obvious non-topmost alternative does not work either.
    ///
    /// ORDER MATTERS, and it used to be the wrong way round. KeepAboveAppsBelowShell inserts
    /// its window DIRECTLY behind the taskbar, so whichever window is re-topped LAST ends up
    /// on top of the other. Doing the overlay first and the panel second put the panel above
    /// the pet — which is why the pet's pet/feed bounce appeared clipped wherever it grew into
    /// the panel's rectangle. The panel goes first now, so the sprite is always the upper of
    /// the two and can scale up over it.
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

    /// Deletes the save file and relaunches, so the new process takes OnStartup's first-run
    /// branch — FirstRunFlow.NeedsOnboarding gets a null SaveData when JsonSaveStore.Load
    /// finds no file — and runs starter selection plus onboarding as if freshly installed.
    ///
    /// Only state.json goes. The sprite cache and catalog.json are deliberately kept: they
    /// are asset caches, not player progress, and rebuilding the catalog means ~650 species
    /// worth of PokeAPI requests, which would turn a reset into a multi-minute wait.
    /// The only way out: lets the pet window close (it refuses otherwise, see OverlayWindow.AllowClose).
    private void ShutdownApp()
    {
        _overlayWindow.AllowClose = true;
        Shutdown();
    }

    private void ResetSaveDataAndRestart()
    {
        _isWipingSaveData = true;
        _saveWriter.Delete(); // also cancels an autosave still queued on the thread pool

        Process.Start(Environment.ProcessPath!, AfterRestartArg);

        // Shutdown(), matching the tray's "Выход" item, rather than Environment.Exit: it runs
        // the Exit handler, so the global input hook, the fullscreen monitor and the tray icon
        // are all disposed properly instead of being abandoned while the replacement process
        // is already starting up. Its SaveState() call reaches a write that _isWipingSaveData
        // now suppresses.
        ShutdownApp();
    }

    private void SaveState()
    {
        _inputHook?.Dispose();
        _gamepadHook?.Dispose();
        _fullscreenMonitor?.Dispose();
        _trayIconService?.Dispose();

        PersistState(flush: true);
    }

    /// Autosaves (flush: false) only queue the disk write on the thread pool — they run on the
    /// growth tick, whose thread owns the input hooks. Exit and the crash handler flush
    /// synchronously, so the process can't end before the final state is on disk.
    private void PersistState(bool flush = false)
    {
        // The one case where not writing is correct: ResetSaveDataAndRestart has just deleted
        // state.json, and every remaining write path leads back here — the autosave tick, the
        // Exit handler's SaveState(), the crash handler. Without this the shutdown that
        // follows the delete would immediately write the in-memory collection straight back
        // out and the relaunched process would find a save file exactly where it left one.
        if (_isWipingSaveData) return;

        var data = _appState.ToSaveData(_catchReadyCount);
        if (flush) _saveWriter.Flush(data);
        else _saveWriter.Post(data);
    }
}
