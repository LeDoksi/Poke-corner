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
    private SaveProfileLease? _profileLease;
    private string _profileDirectory = null!;

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
    private EncounterService _encounterService = null!;
    private CareModeService _careModeService = null!;
    private FeedingService _feedingService = null!;
    private bool _isFullscreenHiding;
    private bool _isFullscreenMonitorRunning;
    private int _overlayRefreshGeneration;
    private AppMenuWindow? _menuWindow;
    private readonly HashSet<string> _pendingHintKeys = [];
    private CatchReveal? _pendingCatchReveal;
    private ShopService _shopService = null!;
    private ScreenCorner _appliedCorner;
    private int _appliedMonitorIndex;
    private int _appliedPetSizePixels;

    /// Earned activity balls waiting to be opened.
    private int _catchReadyCount;
    private Task _activityReservationTask = Task.CompletedTask;
    private Task _expeditionProgressTask = Task.CompletedTask;
    private readonly ExpeditionActivityBuffer _expeditionActivity = new();
    /// Every decoded frame of the active individual's sprite — the list the overlay animates
    /// from. One element means a static sprite; null means nothing is cached yet.
    private IReadOnlyList<byte[]>? _activeSpriteFrames;
    private ProgressPanelWindow? _progressPanelWindow;

    /// POKE-9 diagnostics: %APPDATA%\PokeCorner\zorder.log, next to the save file, so a user can
    /// find and send it. Lines only on z-order state changes (see ZOrderLog).
    private ZOrderLog _zOrderLog = null!;
    private static readonly TimeSpan AutosaveInterval = TimeSpan.FromSeconds(5);
    private DateTimeOffset _lastAutosaveUtc = DateTimeOffset.MinValue;
    private Guid? _evolutionReadyNotifiedFor;

    /// Suppresses old-process writes after an explicit replacement and before restart.
    private bool _isWipingSaveData;
    private bool _hasLoadedState;

    /// Decoded PNG frames per display species id (null = no usable animation), in front of the
    /// on-disk GIF cache. A GIF costs ~22 ms to decode, so each species is decoded once.
    private readonly Dictionary<(int SpeciesId, bool IsShiny), IReadOnlyList<byte[]>?> _decodedFramesCache = new();

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

        try
        {
            WindowsProfile.EnsurePersistent();
            _profileDirectory = Path.GetDirectoryName(AppPaths.GetSaveFilePath())!;
            _profileLease = SaveProfileLease.Acquire(_profileDirectory);
            _saveStore = new JsonSaveStore(AppPaths.GetV2SaveFilePath(_profileDirectory));
            _saveWriter = CreateSaveWriter();
            _zOrderLog = new ZOrderLog(Path.Combine(_profileDirectory, "zorder.log"));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            System.Windows.MessageBox.Show(ex.Message + "\nПрофиль недоступен или уже открыт другим процессом. Игра не загружена.", "PokéCorner — профиль недоступен");
            Shutdown(); return;
        }
        _spriteCache = new SpriteCache(AppPaths.GetSpriteCacheDirectory());
        _pokeApiClient = new PokeApiClient(new System.Net.Http.HttpClientHandler());
        var catalogPath = Path.Combine(AppPaths.GetAppDataDirectory(), "catalog.json");
        _speciesCatalog = new SpeciesCatalog(_pokeApiClient, _spriteCache, catalogPath);
        _catalogPackage = CatalogPackage.Load(AppPaths.GetAppDataDirectory());

        _catchRoller = new CatchRoller();
        _careModeService = new CareModeService();
        _feedingService = new FeedingService();
        _shopService = new ShopService(_catalogPackage.DataRoot);

        SaveData? loaded;
        var isNewGame = false;
        while (true)
        {
            try
            {
                var dataRoot = AppPaths.GetAppDataDirectory();
                var preserved = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PokeCorner", LegacyLookup.PreservedLookupFileName);
                _legacyLookup = File.Exists(AppPaths.GetV2SaveFilePath(_profileDirectory))
                    ? LegacyLookup.Load(dataRoot)
                    : LegacyLookup.Load(dataRoot, catalogPath, preserved);
                loaded = await SaveMigration.LoadAsync(AppPaths.GetSaveFilePath(_profileDirectory), AppPaths.GetV2SaveFilePath(_profileDirectory), _legacyLookup, _catalogPackage, _saveStore);
                isNewGame = loaded is null || loaded.StarterSelectionPending && loaded.Individuals.Count == 0;
                if (loaded is null)
                {
                    loaded = SaveMigration.Migrate(new SaveData { Individuals = [], Settings = new() }, _legacyLookup, _catalogPackage);
                    loaded.StarterSelectionPending = true;
                }
                _appState = new AppState { Achievements = new AchievementService(_catalogPackage) };
                _appState.Hydrate(loaded);
                break;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
            {
                var choice = System.Windows.MessageBox.Show(
                    ex.Message + "\n\nДа — повторить загрузку. Нет — выбрать исправную резервную копию. Отмена — закрыть приложение.",
                    "PokéCorner — сохранение недоступно", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (choice == MessageBoxResult.Yes) continue;
                if (choice == MessageBoxResult.No)
                {
                    try { RestoreFromHistory(); }
                    catch (Exception restoreError) when (restoreError is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
                    { System.Windows.MessageBox.Show(restoreError.Message, "PokéCorner — резервная копия недоступна"); }
                    continue;
                }
                Shutdown();
                return;
            }
        }
        _catchReadyCount = loaded?.PendingCatchCount ?? 0;
        _encounterService = new EncounterService(_catalogPackage);
        // Older saves carry earned balls as a count only; give them stable queue IDs in memory.
        for (var missing = _catchReadyCount - _appState.Encounters.Count(e => e.Source == "activity" && e.Phase == "sealed"); missing > 0; missing--)
            _encounterService.Reserve(_appState.Encounters, "activity", Guid.NewGuid());

        if (FirstRunFlow.NeedsStarter(loaded!))
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
            if (selectionWindow.ShowDialog() != true)
            {
                Shutdown(); return;
            }
            var chosenChain = selectionWindow.Chosen!;

            try
            {
                var starter = FirstRunFlow.HatchChosen(chosenChain, _catchRoller, DateTimeOffset.UtcNow, _catalogPackage);
                starter.AcquisitionSource = "starter";
                SaveMigration.InitializeIndividual(starter, _legacyLookup, _catalogPackage);
                await _appState.CommitAsync(_saveWriter, next => FirstRunFlow.CompleteStarter(next, starter, isNewGame, _catalogPackage), () => _catchReadyCount);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                System.Windows.MessageBox.Show(ex.Message + "\nНовая игра не записана. Данные защищены; при следующем запуске выберите восстановление.", "PokéCorner — ошибка сохранения");
                Shutdown(); return;
            }
        }
        else if (loaded!.ActiveIndividualId is null || loaded.Gift.Gifts.Count == 0 || FirstRunFlow.HasUnqueuedGift(loaded))
        {
            var companion = FirstRunFlow.AvailableCompanion(loaded, _catalogPackage);
            try
            {
                await _appState.CommitAsync(_saveWriter, next => FirstRunFlow.RestoreCompanionAndGift(next, _catalogPackage), () => _catchReadyCount);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                System.Windows.MessageBox.Show(ex.Message + "\nКоллекция не изменена. Повторите запуск или восстановите сохранение.", "PokéCorner — ошибка сохранения");
                Shutdown(); return;
            }
            if (companion is null && _appState.Collection.GetActive() is null)
                System.Windows.MessageBox.Show("Все сохранённые питомцы сейчас недоступны. Коллекция сохранена без замены; питомца можно выбрать позже.", "PokéCorner — питомец недоступен");
        }

        if (FirstRunFlow.NeedsTour(_appState.ToSaveData(_catchReadyCount)))
        {
            try { await ShowUpdateTourAsync(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                System.Windows.MessageBox.Show(ex.Message + "\nШаг обучения не сохранён. При следующем запуске обучение продолжится.", "PokéCorner — ошибка сохранения");
                Shutdown(); return;
            }
        }

        _hasLoadedState = true;
        _appState.CatchRollDue += OnCatchRollDue;

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
        _overlayWindow.MenuRequested += () => OpenMenu(MenuSection.Corner);
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
        _trayIconService.OpenCollectionRequested += () => OpenMenu(MenuSection.Pokedex);
        _trayIconService.OpenSettingsRequested += () => OpenMenu(MenuSection.Settings);
        _trayIconService.OpenShopRequested += () => OpenMenu(MenuSection.Shop);
        _trayIconService.CatchRequested += () => OpenMenu(MenuSection.Catch);
        _trayIconService.ExitRequested += ShutdownApp;
        // Restore the tray's ready glyph if a catch was already queued when the app last closed
        // (see _catchReadyCount's hydration above) — the AppMenuWindow badge doesn't need the
        // same treatment since it's created lazily and reads the live count when it is.
        _trayIconService.SetCatchReady(ReadyBallCount() > 0);

        // MUST come after _trayIconService exists. This async void runs straight through to
        // _trayIconService.SetSpriteIcon when the sprite is already memoized — which it is right after
        // onboarding — so placed earlier it dereferenced null on first run.
        RefreshOverlayForActiveIndividual();

        ReconcileSettingsState();

        _singleInstance.ActivationRequested += () => Dispatcher.BeginInvoke(() => OpenMenu(MenuSection.Corner));
        // Windows logging off must not be blocked by the pet window refusing to close.
        SessionEnding += (_, args) =>
        {
            if (_appState.IsCommitting) { args.Cancel = true; ShutdownApp(); }
            else AllowDeskWindowsToClose();
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
        if (_appState.PendingActivityEncounterCount > 0) StartActivityReservationCommit();
        StartExpeditionProgressCommit();
        if (_appState.IsCommitting) return;

        _progressPanelWindow?.UpdateProgress(ProgressCalculator.Compute(_appState.Activity, _appState.Collection.GetActive(), _appState.Feeding));
        UpdateActiveThought();
        if (_cryPlayer is not null) _cryPlayer.Volume = CryVolume.ToPlayerVolume(_appState.Settings.CrySoundVolume);
        if (_effectPlayer is not null) _effectPlayer.Volume = CryVolume.ToPlayerVolume(_appState.Settings.EffectsSoundVolume);

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

    /// One-shot "ready to evolve" toast per individual; re-armed when readiness drops or the
    /// active pet changes.
    private void CheckEvolutionReady()
    {
        var active = _appState.Collection.GetActive();
        if (active is null || !IsActiveIndividualReadyToEvolve())
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

        UpdateActiveThought();

        var displaySpeciesId = ResolveDisplaySpeciesId(active);

        var pngFrames = await GetAnimatedFramesAsync(displaySpeciesId, active.IsShiny);

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
    private Task<IReadOnlyList<byte[]>?> GetAnimatedFramesAsync(int speciesId) => GetAnimatedFramesAsync(speciesId, false);

    private async Task<IReadOnlyList<byte[]>?> GetAnimatedFramesAsync(int speciesId, bool isShiny)
    {
        var key = (speciesId, isShiny);
        if (_decodedFramesCache.TryGetValue(key, out var memoized)) return memoized;

        byte[]? animatedBytes = null;
        // The selected package provides verified local GIFs; an unavailable species has no animation.
        var gifPath = _catalogPackage.GetAssetPath(speciesId, isShiny)
            ?? (isShiny ? _catalogPackage.GetAssetPath(speciesId, false) : null);
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
        if (animatedBytes is not null) _decodedFramesCache[key] = decoded;
        return decoded;
    }

    // LoadForLookup, not LoadCachedOrFallback: an individual migrated off the old colliding
    // offline pool sits on a reserved legacy chain id that only the lookup view contains.
    private int ResolveDisplaySpeciesId(PokemonIndividual individual) =>
        individual.ResolveDisplaySpeciesId(_speciesCatalog.LoadForLookup());


    /// Activity crossed the catch threshold: queue a ball for later opening.
    private void OnCatchRollDue()
    {
        // shortcut: the persisted counter is int-sized; widen it if a profile can earn 2 billion balls.
        if ((long)_catchReadyCount + _appState.PendingActivityEncounterCount == int.MaxValue) return;
        _appState.QueueActivityEncounter(Guid.NewGuid());
    }

    private void StartActivityReservationCommit()
    {
        if (_isWipingSaveData || _saveWriter.HasFailed || !_activityReservationTask.IsCompleted) return;
        _activityReservationTask = CommitActivityReservationsAsync();
    }

    private async Task CommitActivityReservationsAsync()
    {
        try
        {
            while (_appState.PendingActivityEncounterCount > 0)
            {
                var wasIdle = _catchReadyCount == 0;
                await _appState.CommitQueuedActivityEncountersAsync(_saveWriter, _encounterService,
                    () => _catchReadyCount, next => _catchReadyCount = next.PendingCatchCount);
                _trayIconService.SetCatchReady(ReadyBallCount() > 0);
                _menuWindow?.SetCatchReady(ReadyBallCount());
                if (wasIdle && _catchReadyCount > 0)
                    _trayIconService.ShowNotification("PokéCorner", "Покебол за активность готов к открытию.");
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            System.Windows.MessageBox.Show(ex.Message + "\nПокебол заработан, но сохранение не записано. Повторите после восстановления сохранения.",
                "PokéCorner — ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task<CatchReveal?> TryPerformCatchAsync()
    {
        if (_isExiting || _appState.IsCommitting) return null;
        var encounter = CatchBallQueue.NextReady(_appState.Encounters);
        if (encounter is null) return null;
        EncounterOpenResult? result = null;
        CatchReveal? reveal = null;
        await _appState.CommitAsync(_saveWriter, next => result = _encounterService.Open(next, encounter.Id),
            () => _catchReadyCount, next =>
            {
                _catchReadyCount = next.PendingCatchCount;
                reveal = PublishConfirmedCatch(result!);
            });
        _trayIconService.SetCatchReady(ReadyBallCount() > 0);
        _menuWindow?.SetCatchReady(ReadyBallCount());
        RefreshOverlayForActiveIndividual();
        return reveal;
    }

    private int ReadyBallCount() => CatchBallQueue.ReadyCount(_appState.Encounters, _catchReadyCount);

    private CatchReveal PublishConfirmedCatch(EncounterOpenResult result)
    {
        var individual = result.Individual;
        _pendingCatchReveal = new CatchReveal(new CatchResult(individual.EvolutionChainId, individual.SpeciesId, individual.Nature,
            individual.IsShiny, result.WasDuplicateSpecies, individual.ChainLength), result.Rarity);
        return _pendingCatchReveal;
    }

    /// No toast: the bounce is the feedback, and pets get clicked far too often for a
    /// notification each time.
    private async void PetActiveIndividual()
    {
        if (_isExiting) return;
        if (_appState.IsCommitting) { _appState.RunWhenReady(PetActiveIndividual); return; }
        var active = _appState.Collection.GetActive();
        if (active is null) return;

        if (_appState.CareStateByIndividualId.TryGetValue(active.Id, out var currentCare) &&
            currentCare.LastPetUtc is { } lastPet && DateTimeOffset.UtcNow - lastPet < FeedingService.PetCooldown) return;
        var id = active.Id;
        var petted = false;
        try
        {
            await _appState.CommitAsync(_saveWriter, next =>
            {
                var individual = next.Individuals.Single(i => i.Id == id);
                if (!next.CareStates.TryGetValue(id, out var care)) next.CareStates[id] = care = new CareState();
                petted = _feedingService.Pet(individual, care, DateTimeOffset.UtcNow);
                if (petted) AchievementMetrics.Record(next, "care:" + id);
            }, () => _catchReadyCount, next => _catchReadyCount = next.PendingCatchCount);
            if (!petted) return;
            RefreshOverlayForActiveIndividual();
            _overlayWindow.PlayPetReaction();
            if (_appState.Collection.Individuals.FirstOrDefault(i => i.Id == id) is { } saved)
                PlayCry(ResolveDisplaySpeciesId(saved));
        }
        catch (Exception ex) { _trayIconService.ShowNotification("PokéCorner", "Поглаживание не сохранено: " + ex.Message); }
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
            if (_appState.Settings.CrySoundVolume <= 0) { _cryPlayer?.Stop(); return; }
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
            _cryPlayer.Stop();
            _cryPlayer.Open(new Uri(path));
            _cryPlayer.Play();
        }
        catch
        {
            // no sound this click; see above
        }
    }

    private System.Windows.Media.MediaPlayer? _effectPlayer;

    private void PlayCrunch()
    {
        try
        {
            if (_appState.Settings.EffectsSoundVolume <= 0) { _effectPlayer?.Stop(); return; }
            var path = Path.Combine(_catalogPackage.DataRoot, "voice-preview", "audio", "feeding-crunch.wav");
            if (!File.Exists(path)) return;
            if (_effectPlayer is null)
            {
                _effectPlayer = new System.Windows.Media.MediaPlayer();
                _effectPlayer.MediaFailed += (_, _) => { };
            }
            _effectPlayer.Stop();
            _effectPlayer.Volume = CryVolume.ToPlayerVolume(_appState.Settings.EffectsSoundVolume);
            _effectPlayer.Open(new Uri(path));
            _effectPlayer.Play();
        }
        catch { /* cosmetic sound must not turn a committed feed into a visible failure */ }
    }

    private void UpdateActiveThought()
    {
        var now = DateTimeOffset.UtcNow;
        var active = _appState.Collection.GetActive();
        var last = active is null ? now : _appState.CareStateByIndividualId.TryGetValue(active.Id, out var care)
            ? care.LastInteractionUtc : active.CaughtAtUtc;
        _overlayWindow.UpdateThought(last, now);
    }

    private async void FeedActiveIndividual()
    {
        if (_isExiting) return;
        if (_appState.IsCommitting) { _appState.RunWhenReady(FeedActiveIndividual); return; }
        var active = _appState.Collection.GetActive();
        if (active is null) return;

        if (_appState.Feeding.Treats < 1)
        {
            // Unlike a successful feed, running out of treats has no on-screen animation, so without
            // the toast the click would look like it did nothing.
            _trayIconService.ShowNotification("PokéCorner", "Не хватает лакомств — накопи ещё активности.");
            return;
        }
        var id = active.Id;
        var fed = false;
        try
        {
            await _appState.CommitAsync(_saveWriter, next =>
            {
                var individual = next.Individuals.Single(i => i.Id == id);
                if (!next.CareStates.TryGetValue(id, out var care)) next.CareStates[id] = care = new CareState();
                var food = new FeedingInventory { Treats = next.FeedingTreats, RemainderUnits = next.FeedingRemainderUnits };
                fed = _feedingService.Feed(individual, food, care, DateTimeOffset.UtcNow);
                if (!fed) return;
                next.FeedingTreats = food.Treats;
                AchievementMetrics.Record(next, "care:" + id);
                AchievementMetrics.Record(next, "first-feeding");
            }, () => _catchReadyCount, next => _catchReadyCount = next.PendingCatchCount);
            if (fed)
            {
                RefreshOverlayForActiveIndividual();
                _overlayWindow.PlayFeedReaction();
                PlayCrunch();
            }
            else _trayIconService.ShowNotification("PokéCorner", "Не хватает лакомств — накопи ещё активности.");
        }
        catch (Exception ex) { _trayIconService.ShowNotification("PokéCorner", "Кормление не сохранено: " + ex.Message); }
    }

    private bool IsActiveIndividualReadyToEvolve()
    {
        var active = _appState.Collection.GetActive();
        if (active is null) return false;
        var preview = new EvolutionService(_catalogPackage, _appState.ToSaveData(_catchReadyCount)).Preview(active.Id);
        return _appState.IsReadyToEvolve(active, preview);
    }

    private EvolutionPreview PreviewEvolution(Guid id) =>
        new EvolutionService(_catalogPackage, _appState.ToSaveData(_catchReadyCount)).Preview(id);

    private RetrainingPreview PreviewRetraining(Guid id, int slot) =>
        new RetrainingService(_catalogPackage, _appState.ToSaveData(_catchReadyCount)).Preview(id, slot);

    private async Task<RetrainingAction> RetrainIndividualAsync(Guid id, int slot, Guid operationId)
    {
        if (_isExiting) throw new InvalidOperationException("Приложение закрывается.");
        RetrainingAction? result = null;
        await _expeditionProgressTask;
        if (_isExiting) throw new InvalidOperationException("Приложение закрывается.");
        await _appState.CommitAsync(_saveWriter, next =>
        {
            result = new RetrainingService(_catalogPackage, next).Retrain(id, slot, operationId);
            if (!result.Success) throw new InvalidOperationException(result.Message);
        }, () => _catchReadyCount, next => _catchReadyCount = next.PendingCatchCount);
        if (_appState.Collection.ActiveIndividualId == id) RefreshOverlayForActiveIndividual();
        return result!;
    }

    private RehomeQuote PreviewRehome(Guid id) =>
        new RehomingService(_catalogPackage, _appState.ToSaveData(_catchReadyCount)).Quote(id);

    private async Task<RehomeAction> RehomeIndividualAsync(Guid id, Guid operationId)
    {
        if (_isExiting) throw new InvalidOperationException("Приложение закрывается.");
        RehomeAction? result = null;
        await _expeditionProgressTask;
        if (_isExiting) throw new InvalidOperationException("Приложение закрывается.");
        await _appState.CommitAsync(_saveWriter, next =>
        {
            result = new RehomingService(_catalogPackage, next).Transfer(id, operationId);
            if (!result.Success) throw new InvalidOperationException(result.Message);
        }, () => _catchReadyCount, next => _catchReadyCount = next.PendingCatchCount);
        return result!;
    }

    private async Task<EvolutionAction> EvolveIndividualAsync(Guid id, int? branch, Guid operationId)
    {
        if (_isExiting) throw new InvalidOperationException("Приложение закрывается.");
        EvolutionAction? result = null;
        await _expeditionProgressTask;
        if (_isExiting) throw new InvalidOperationException("Приложение закрывается.");
        await _appState.CommitAsync(_saveWriter, next =>
        {
            result = new EvolutionService(_catalogPackage, next).Evolve(id, branch, operationId);
            if (!result.Success) throw new InvalidOperationException(result.Message);
        }, () => _catchReadyCount, next => _catchReadyCount = next.PendingCatchCount);
        if (_appState.Collection.ActiveIndividualId == id)
        {
            _overlayWindow.PlayEvolutionFlash();
            RefreshOverlayForActiveIndividual();
        }
        return result!;
    }

    private async Task ShowUpdateTourAsync()
    {
        var tour = _appState.ToSaveData(_catchReadyCount).Tutorial;
        if (tour.Status == "not-started")
            await _appState.CommitAsync(_saveWriter, next => FirstRunFlow.SetTourProgress(next, 1, "in-progress"), () => _catchReadyCount);
        var step = _appState.ToSaveData(_catchReadyCount).Tutorial.CurrentStep;
        new OnboardingWindow(step, (nextStep, status) =>
            _appState.CommitAsync(_saveWriter, next => FirstRunFlow.SetTourProgress(next, nextStep, status), () => _catchReadyCount)).ShowDialog();
    }

    private async void ShowContextHint(MenuSection section)
    {
        var hint = section switch
        {
            MenuSection.Pokedex => ("view:pokedex", "Покедекс: открой экземпляр, чтобы увидеть кличку, уход и условия эволюции."),
            MenuSection.Expeditions => ("view:expeditions", "Экспедиция: выбери команду и длительность; одновременно доступен один поход."),
            MenuSection.Nursery => ("view:nursery", "Питомник: сначала проверь совместимость, варианты потомства и цену."),
            MenuSection.Shop => ("view:shop", "Магазин: лакомства и шесть ягод учитываются отдельно; яйца здесь не продаются."),
            MenuSection.Achievements => ("view:achievements", "Альбом: три открытых трофея можно бесплатно выставить на полку."),
            _ => ("", "")
        };
        if (_isExiting || hint.Item1.Length == 0 || _appState.ToSaveData(_catchReadyCount).Tutorial.SeenHintKeys.Contains(hint.Item1)
            || !_pendingHintKeys.Add(hint.Item1)) return;
        try
        {
            await _appState.CommitAsync(_saveWriter, next => next.Tutorial.SeenHintKeys.Add(hint.Item1), () => _catchReadyCount);
            if (!_isExiting) _trayIconService.ShowNotification("PokéCorner — подсказка", hint.Item2);
        }
        catch (Exception ex)
        { if (!_isExiting) _trayIconService.ShowNotification("PokéCorner — ошибка сохранения", "Подсказка не сохранена: " + ex.Message); }
        finally { _pendingHintKeys.Remove(hint.Item1); }
    }

    private List<SpeciesEvolutionChain> GetDirectIssueChains() =>
        _speciesCatalog.LoadCachedOrFallback().Where(c => _catalogPackage.CanIssueDirectly(c.StarterSpeciesId, false)).ToList();

    private void OpenMenu(MenuSection section)
    {
        if (_isExiting) return;
        if (_appState.IsCommitting) { _appState.RunWhenReady(() => OpenMenu(section)); return; }
        try
        {
            if (_menuWindow is null)
            {
                // LoadForLookup so the collection/Pokédex can resolve names, sprites and rarity
                // for individuals sitting on a migrated legacy chain id, not just real ones.
                var lookupChains = _speciesCatalog.LoadForLookup();
                var levels = new LevelProgression(_catalogPackage.DataRoot);
                var collectionPresentation = new CollectionPresentation(_catalogPackage);
                var collectionVm = new CollectionViewModel(_appState.Collection, _appState.CareStateByIndividualId, _careModeService, lookupChains, _appState.PendingEggs,
                    () => _appState.BusyIndividualIds, _catalogPackage.Species, () => _appState.DiscoveredSpeciesIds,
                    _catalogPackage.GetRouteHints(), individual => levels.LevelForXp(individual.TotalXp),
                    () => _appState.NormalSpeciesIds, () => _appState.ShinySpeciesIds, collectionPresentation);
                var shopView = new ShopView(_appState, _shopService,
                    update => _appState.CommitAsync(_saveWriter, update, () => _catchReadyCount,
                        next => _catchReadyCount = next.PendingCatchCount), RefreshOverlayForActiveIndividual, _catalogPackage);
                var collectionView = new CollectionView(collectionVm, _appState, _spriteCache, PetActiveIndividual, FeedActiveIndividual, RefreshOverlayForActiveIndividual, () => _activeSpriteFrames, () => false, IsActiveIndividualReadyToEvolve,
                    (id, shiny) => _catalogPackage.CanObtain(id, shiny) ? GetAnimatedFramesAsync(id, shiny) : Task.FromResult<IReadOnlyList<byte[]>?>(null),
                    speciesId => _spriteCache.GetOrFetchAsync(speciesId, () => _pokeApiClient.FetchSpriteAsync(speciesId)),
                    (speciesId, shiny) => _catalogPackage.GetAssetPath(speciesId, shiny),
                    update => _appState.CommitAsync(_saveWriter, update, () => _catchReadyCount),
                    PreviewEvolution, EvolveIndividualAsync, section => _menuWindow?.Navigate(section),
                    (berry, required) => shopView.SelectBerryShortage(berry, required), PreviewRehome, RehomeIndividualAsync,
                    PreviewRetraining, RetrainIndividualAsync,
                    (title, body, caption) => _menuWindow?.ConfirmAsync(title, body, caption) ?? Task.FromResult(false),
                    (id, item) =>
                    {
                        var pet = _appState.Collection.Individuals.FirstOrDefault(p => p.Id == id);
                        var good = _shopService.Goods.Single(g => g.Id == item);
                        var stock = _appState.Shop.Items.GetValueOrDefault(item);
                        var reason = pet is null ? "Питомец не найден." : _appState.BusyIndividualIds.Contains(id)
                            ? "Питомец занят в походе или питомнике." : !_catalogPackage.CanObtain(pet.CurrentSpeciesId, pet.IsShiny)
                            ? "Для этой формы предмет пока недоступен." : stock < 1 ? "Предмета нет в запасе." : null;
                        var effect = item == "care-booster" ? $"+{good.Units:N0} ухода для эволюции выбранному питомцу." : "Добавляет опыт выбранному питомцу.";
                        if (pet is not null && item == "xp-potion")
                        {
                            var (useful, excess) = _shopService.PotionPreview(pet, levels);
                            effect = $"Получит {useful} XP." + (excess > 0 ? $" Излишек {excess} XP будет потерян." : "");
                            if (useful == 0) reason = "Максимальный уровень: зелье не расходуется.";
                        }
                        return new IndividualItemPreview(item, good.Name, stock, effect, reason);
                    },
                    async (id, item, operation) =>
                    {
                        PurchaseResult? result = null;
                        await _appState.CommitAsync(_saveWriter, next =>
                        {
                            var pet = next.Individuals.FirstOrDefault(p => p.Id == id) ?? throw new InvalidOperationException("Питомец не найден.");
                            if (ExpeditionService.BusyIndividualIds(next).Contains(id)) throw new InvalidOperationException("Питомец занят в походе или питомнике.");
                            if (!_catalogPackage.CanObtain(pet.CurrentSpeciesId, pet.IsShiny)) throw new InvalidOperationException("Для этой формы предмет пока недоступен.");
                            result = item == "care-booster" ? _shopService.TryUseCareBooster(next, id, operation)
                                : _shopService.TryUseXpPotion(next, id, operation, levels, _catalogPackage);
                            if (!result.Success) throw new InvalidOperationException(result.Message);
                        }, () => _catchReadyCount);
                        return result!;
                    },
                    id =>
                    {
                        var pet = _appState.Collection.Individuals.Single(p => p.Id == id);
                        var level = levels.LevelForXp(pet.TotalXp);
                        var start = levels.XpForLevel(level); var end = level == 40 ? start : levels.XpForLevel(level + 1);
                        return level == 40 ? ("Опыт · максимальный уровень", 1d)
                            : ($"Опыт · {pet.TotalXp - start} / {end - start} XP до уровня {level + 1}", (pet.TotalXp - start) / (double)(end - start));
                    },
                    (id, shiny) => collectionPresentation.WhereFind(id, shiny, shiny ? _appState.ShinySpeciesIds : _appState.NormalSpeciesIds));
                var settingsView = new SettingsView(new SettingsViewModel(_appState.Settings), ApplyPetSizeAndPosition, ResetSaveDataAndRestart, RecoverSaveDataAndRestart);
                var catchView = new CatchView(_appState, null, speciesId => _spriteCache.GetOrFetchAsync(speciesId, () => _pokeApiClient.FetchSpriteAsync(speciesId)), () => _catchReadyCount, RefreshOverlayForActiveIndividual, speciesId => _speciesCatalog.LoadForLookup().ResolveSpeciesName(speciesId), TryPerformCatchAsync, () => _pendingCatchReveal = null);
                if (_pendingCatchReveal is { } pendingCatchReveal) catchView.RestoreReveal(pendingCatchReveal);
                var expeditionView = new ExpeditionView(() => _appState.ToSaveData(_catchReadyCount),
                    PreviewExpedition, StartExpeditionAsync, CancelExpeditionAsync, ClaimExpeditionAsync,
                    OpenExpeditionEncounterAsync, _catalogPackage);
                var nurseryView = new NurseryView(() => _appState.ToSaveData(_catchReadyCount), PreviewNursery,
                    StartNurseryAsync, HatchNurseryAsync, RefreshNurseryReadyAsync, _catalogPackage,
                    (good, required) =>
                    {
                        if (good == "offspring-charm") shopView.SelectItemShortage(good, required);
                        else shopView.SelectBerryShortage(good, required);
                        _menuWindow?.Navigate(MenuSection.Shop);
                    },
                    id => { _menuWindow?.Navigate(MenuSection.Pokedex); collectionView.OpenIndividual(id); },
                    id => CommitStateAsync(next =>
                    {
                        if (!next.HatchedNurseryResults.Values.Any(child => child.Id == id) ||
                            !next.Individuals.Any(individual => individual.Id == id))
                            throw new InvalidOperationException("Малыш больше не найден в коллекции.");
                        next.ActiveIndividualId = id;
                        if (!next.CareStates.TryGetValue(id, out var care)) next.CareStates[id] = care = new CareState();
                        care.LastInteractionUtc = DateTimeOffset.UtcNow;
                    }, closeMenu: false));
                var roomArt = RoomArt.Load(_catalogPackage.DataRoot);
                var roomView = new RoomView(roomArt, () => _appState.ToSaveData(_catchReadyCount),
                    () => _activeSpriteFrames, () => _menuWindow?.Navigate(MenuSection.Achievements),
                    id => _catalogPackage.Species.FirstOrDefault(species => species.Id == id)?.DisplayName ?? $"№{id:D4}",
                    catalog: _catalogPackage, pet: PetActiveIndividual, feed: FeedActiveIndividual,
                    openIndividual: id => { _menuWindow?.Navigate(MenuSection.Pokedex); collectionView.OpenIndividual(id); },
                    openPokedex: () => _menuWindow?.Navigate(MenuSection.Pokedex));
                var achievementsView = new AchievementsView(roomArt, () => _appState.ToSaveData(_catchReadyCount),
                    update => CommitStateAsync(update, closeMenu: false), () => _menuWindow?.Navigate(MenuSection.Corner),
                    _appState.Achievements);
                _menuWindow = new AppMenuWindow(collectionView, settingsView, shopView, catchView, expeditionView,
                    nurseryView, roomView, achievementsView, () => _appState.ToSaveData(_catchReadyCount));
                _menuWindow.SectionViewed += ShowContextHint;
                _menuWindow.Closed += (_, _) => _menuWindow = null;
                _menuWindow.SetCatchReady(ReadyBallCount()); // picks up activity and gift balls queued before opening
                _menuWindow.Show();
            }
            _menuWindow.Navigate(section);
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
    public async Task CommitStateAsync(Action<SaveData> update, bool closeMenu = true)
    {
        if (!_hasLoadedState || _isExiting) throw new InvalidOperationException("Сохранение ещё не загружено.");
        if (_menuWindow is not null) _menuWindow.IsEnabled = false;
        try
        {
            await _appState.CommitAsync(_saveWriter, update, () => _catchReadyCount, next => _catchReadyCount = next.PendingCatchCount);
            if (closeMenu) _menuWindow?.Close();
            RefreshOverlayForActiveIndividual();
        }
        finally { if (_menuWindow is not null) _menuWindow.IsEnabled = true; }
    }

    public ExpeditionPreview PreviewExpedition(ExpeditionRequest request) =>
        new ExpeditionService(_catalogPackage, _appState.ToSaveData(_catchReadyCount)).Preview(request);

    public NurseryPreview PreviewNursery(IReadOnlyList<Guid> parentIds, int? selectedSpeciesId = null) =>
        new NurseryService(_catalogPackage, _appState.ToSaveData(_catchReadyCount))
            .Preview(parentIds, selectedSpeciesId);

    public Task<NurseryAction> StartNurseryAsync(NurseryRequest request) =>
        CommitNurseryAsync(service => service.Start(request));

    public Task<NurseryAction> HatchNurseryAsync(Guid id, Guid operationId) =>
        CommitNurseryAsync(service => service.Hatch(id, operationId));

    public async Task<bool> RefreshNurseryReadyAsync(DateTimeOffset now)
    {
        var nest = _appState.ToSaveData(_catchReadyCount).Nest;
        if (nest?.Status != "running" || now.ToUniversalTime() < nest.ReadyAtUtc) return false;
        var changed = false;
        await _expeditionProgressTask;
        await CommitStateAsync(next => changed = new NurseryService(_catalogPackage, next).RefreshReady(now), closeMenu: false);
        return changed;
    }

    private async Task<NurseryAction> CommitNurseryAsync(Func<NurseryService, NurseryAction> action)
    {
        NurseryAction? result = null;
        await _expeditionProgressTask;
        await CommitStateAsync(next =>
        {
            result = action(new NurseryService(_catalogPackage, next));
            if (!result.Success) throw new InvalidOperationException(result.Message);
        }, closeMenu: false);
        return result!;
    }

    public Task<ExpeditionAction> StartExpeditionAsync(ExpeditionRequest request) =>
        CommitExpeditionAsync(service => service.Start(request));

    public Task<ExpeditionAction> CancelExpeditionAsync(Guid id, DateTimeOffset now) =>
        CommitExpeditionAsync(service => service.Cancel(id, now));

    public Task<ExpeditionAction> ClaimExpeditionAsync(Guid id, Guid operationId) =>
        CommitExpeditionAsync(service => service.Claim(id, operationId));

    public async Task<EncounterOpenResult> OpenExpeditionEncounterAsync(Guid encounterId)
    {
        EncounterOpenResult? result = null;
        await CommitStateAsync(next =>
        {
            if (next.Encounters.FirstOrDefault(e => e.Id == encounterId) is not { Source: "expedition", Phase: "resolved" or "opened" })
                throw new InvalidOperationException("Встреча похода ещё не готова к открытию.");
            result = _encounterService.Open(next, encounterId, DateTimeOffset.UtcNow);
        }, closeMenu: false);
        return result!;
    }

    private async Task<ExpeditionAction> CommitExpeditionAsync(Func<ExpeditionService, ExpeditionAction> action)
    {
        ExpeditionAction? result = null;
        await _expeditionProgressTask;
        await CommitStateAsync(next =>
        {
            result = action(new ExpeditionService(_catalogPackage, next));
            if (!result.Success) throw new InvalidOperationException(result.Message);
        }, closeMenu: false);
        _expeditionActivity.Sync(_appState.RunningExpeditionId);
        return result!;
    }

    private void StartExpeditionProgressCommit()
    {
        if (_isWipingSaveData || _saveWriter.HasFailed || !_expeditionProgressTask.IsCompleted || !_appState.HasRunningExpedition) return;
        if (_expeditionActivity.PendingActions < 500 && _appState.ExpeditionReadyAtUtc > DateTimeOffset.UtcNow) return;
        _expeditionProgressTask = CommitExpeditionProgressAsync();
    }

    private async Task CommitExpeditionProgressAsync()
    {
        try
        {
            await CommitBufferedExpeditionActivityAsync();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            System.Windows.MessageBox.Show(ex.Message, "PokéCorner — поход не сохранён", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private Task CommitBufferedExpeditionActivityAsync() =>
        _expeditionActivity.CommitAsync(_appState, _saveWriter, _catalogPackage,
            () => _catchReadyCount, next => _catchReadyCount = next.PendingCatchCount, DateTimeOffset.UtcNow);

    private SaveData SnapshotIncludingPendingExpeditionActivity()
    {
        var snapshot = _appState.ToSaveDataIncludingPendingActivity(_encounterService, _catchReadyCount);
        _expeditionActivity?.ApplyTo(snapshot, _catalogPackage, DateTimeOffset.UtcNow);
        return snapshot;
    }

    private void CreditActivity(long units)
    {
        _appState.ApplyActivityUnits(units);
        _feedingService.EarnTreatsFromActivity(_appState.Feeding, units);
        _shopService.EarnCoinsFromActivity(_appState.Shop, units);
        _expeditionActivity.Credit(_appState.RunningExpeditionId, units);
        StartExpeditionProgressCommit();
        if (_appState.PendingActivityEncounterCount > 0) StartActivityReservationCommit();
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
            {
                await _appState.WhenIdle;
                if (_appState.PendingActivityEncounterCount > 0 && !_saveWriter.HasFailed) StartActivityReservationCommit();
                await _activityReservationTask;
                await _expeditionProgressTask;
                if (_saveWriter.HasFailed) _saveStore.History.Capture(SnapshotIncludingPendingExpeditionActivity());
                else
                {
                    if (_appState.PendingActivityEncounterCount > 0) throw new IOException("Заработанный покебол ещё не записан.");
                    if (_appState.HasRunningExpedition && _expeditionActivity.PendingActions > 0)
                        await CommitBufferedExpeditionActivityAsync();
                    await _appState.FlushAsync(_saveWriter, () => _catchReadyCount);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
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

    private BackgroundSaveWriter CreateSaveWriter()
    {
        var writer = new BackgroundSaveWriter(_saveStore);
        writer.SaveFailed += ex => Dispatcher.BeginInvoke(() => System.Windows.MessageBox.Show(
            ex.Message + "\nАвтосохранение остановлено. Текущая игра остаётся в памяти. Откройте историю сохранений в настройках.",
            "PokéCorner — ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Warning));
        return writer;
    }

    private bool RestoreFromHistory()
    {
        var entries = _saveStore.History.List();
        var dialog = new SaveHistoryWindow(entries, _saveStore.History.UnreadableCount);
        if (_menuWindow is not null) { dialog.Owner = _menuWindow; dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner; }
        if (dialog.ShowDialog() != true || dialog.SelectedEntry is null) return false;
        SaveMigration.RestoreHistory(_saveStore, dialog.SelectedEntry, _catalogPackage, AppPaths.GetAppDataDirectory());
        return true;
    }

    private async void RecoverSaveDataAndRestart()
    {
        if (_appState.IsCommitting) { _appState.RunWhenReady(RecoverSaveDataAndRestart); return; }
        _growthTickTimer.Stop();
        if (_menuWindow is not null) _menuWindow.IsEnabled = false;
        if (_overlayWindow is not null) _overlayWindow.IsEnabled = false;
        try
        {
            if (_saveWriter.HasFailed) _saveStore.History.Capture(SnapshotIncludingPendingExpeditionActivity());
            else
            {
                await _expeditionProgressTask;
                if (_appState.HasRunningExpedition && _expeditionActivity.PendingActions > 0)
                    await CommitBufferedExpeditionActivityAsync();
                await _appState.FlushAsync(_saveWriter, () => _catchReadyCount);
            }
            if (!RestoreFromHistory()) return;
            RestartAfterReplacement();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { System.Windows.MessageBox.Show(ex.Message, "PokéCorner — восстановление не выполнено"); }
        finally
        {
            if (!_isWipingSaveData)
            {
                if (_menuWindow is not null) _menuWindow.IsEnabled = true;
                if (_overlayWindow is not null) _overlayWindow.IsEnabled = true;
                _growthTickTimer.Start();
            }
        }
    }

    private async void ResetSaveDataAndRestart()
    {
        if (_appState.IsCommitting) { _appState.RunWhenReady(ResetSaveDataAndRestart); return; }
        _growthTickTimer.Stop();
        if (_menuWindow is not null) _menuWindow.IsEnabled = false;
        if (_overlayWindow is not null) _overlayWindow.IsEnabled = false;
        try
        {
            await _appState.FlushAsync(_saveWriter, () => _catchReadyCount);
            var reset = SaveMigration.Migrate(new SaveData { Individuals = [], Settings = new() }, _legacyLookup, _catalogPackage);
            await _appState.CommitAsync(_saveWriter, next => next.StarterSelectionPending = true, () => 0, afterPublish: _ => _catchReadyCount = 0, replacement: reset);
            _saveWriter.Stop();
            RestartAfterReplacement();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { System.Windows.MessageBox.Show(ex.Message + "\nНовая игра не начата.", "PokéCorner — ошибка сохранения"); }
        finally
        {
            if (!_isWipingSaveData)
            {
                if (_menuWindow is not null) _menuWindow.IsEnabled = true;
                if (_overlayWindow is not null) _overlayWindow.IsEnabled = true;
                _growthTickTimer.Start();
            }
        }
    }

    private void RestartAfterReplacement()
    {
        _isWipingSaveData = true;
        _hasLoadedState = false;
        _saveWriter.Stop();
        try { Process.Start(Environment.ProcessPath!, AfterRestartArg); }
        catch (System.ComponentModel.Win32Exception ex)
        { System.Windows.MessageBox.Show(ex.Message + "\nСохранение записано. Запустите PokéCorner снова вручную.", "PokéCorner — перезапуск недоступен"); }
        ShutdownApp();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { SaveState(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { System.Windows.MessageBox.Show(ex.Message + "\nПоследнее сохранение не записано.", "PokéCorner — ошибка сохранения"); }
        finally
        {
            _saveWriter?.Stop();
            _profileLease?.Dispose();
            _singleInstance?.Dispose();
            base.OnExit(e);
        }
    }

    private void SaveState()
    {
        _inputHook?.Dispose();
        _gamepadHook?.Dispose();
        _fullscreenMonitor?.Dispose();
        _trayIconService?.Dispose();

        if (!_isExiting)
        {
            if (_hasLoadedState && !_isWipingSaveData && !_appState.IsCommitting && _saveWriter.HasFailed)
                _saveStore.History.Capture(SnapshotIncludingPendingExpeditionActivity());
            else PersistState(flush: true);
        }
    }

    /// Autosaves (flush: false) only queue the disk write on the thread pool — they run on the
    /// growth tick, whose thread owns the input hooks. Exit and the crash handler flush
    /// synchronously, so the process can't end before the final state is on disk.
    private void PersistState(bool flush = false)
    {
        // Never publish unresolved startup state or an old candidate after replacement/failure.
        if (_isWipingSaveData || !_hasLoadedState || _appState.IsCommitting || _saveWriter.HasFailed || _appState.PendingActivityEncounterCount > 0) return;

        var data = _appState.ToSaveData(_catchReadyCount);
        if (flush) _saveWriter.Flush(data);
        else _saveWriter.Post(data);
    }
}
