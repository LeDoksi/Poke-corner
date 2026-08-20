using System.IO;
using System.Windows;
using System.Windows.Threading;
using PokeCorner.App.Autostart;
using PokeCorner.App.Fullscreen;
using PokeCorner.App.Input;
using PokeCorner.App.Onboarding;
using PokeCorner.App.Overlay;
using PokeCorner.App.Popup;
using PokeCorner.App.Tray;
using PokeCorner.Core;
using PokeCorner.Core.ActivitySources;
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;
using PokeCorner.Core.Persistence;

namespace PokeCorner.App;

public partial class App : System.Windows.Application
{
    private AppState _appState = null!;
    private JsonSaveStore _saveStore = null!;
    private SpriteCache _spriteCache = null!;
    private PokeApiClient _pokeApiClient = null!;
    private ClaudeCodeUsageProvider? _tokenUsageProvider;
    private GlobalInputHook? _inputHook;
    private InputEventCounter _inputCounter = null!;
    private DispatcherTimer _growthTickTimer = null!;
    private OverlayWindow _overlayWindow = null!;
    private TrayIconService _trayIconService = null!;
    private ForegroundWindowMonitor _fullscreenMonitor = null!;
    private CatchRoller _catchRoller = null!;
    private CareModeService _careModeService = null!;
    private FeedingService _feedingService = null!;
    private bool _isFullscreenHiding;
    private bool _isFullscreenMonitorRunning;
    private int _overlayRefreshGeneration;
    private Window? _collectionWindow;
    private Window? _settingsWindow;
    private ScreenCorner _appliedCorner;
    private int _appliedMonitorIndex;
    private int _appliedPetSizePixels;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            try { SaveState(); } catch { /* best-effort: never let the save path itself throw during a crash handler */ }
            args.Handled = false; // still let it crash — we're not trying to keep a corrupted UI state running, just to not lose data
        };

        _saveStore = new JsonSaveStore(AppPaths.GetSaveFilePath());
        _spriteCache = new SpriteCache(AppPaths.GetSpriteCacheDirectory());
        _pokeApiClient = new PokeApiClient(new System.Net.Http.HttpClientHandler());
        _catchRoller = new CatchRoller();
        _careModeService = new CareModeService();
        _feedingService = new FeedingService();

        var loaded = _saveStore.Load();
        _appState = BuildAppState(loaded);

        if (FirstRunFlow.NeedsOnboarding(loaded))
        {
            var starter = FirstRunFlow.HatchStarter(_catchRoller, _spriteCache.LoadOfflineStarters(), DateTimeOffset.UtcNow);
            _appState.Collection.Add(starter);
        }

        _appState.CatchRollDue += OnCatchRollDue;

        _inputCounter = new InputEventCounter();

        _growthTickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _growthTickTimer.Tick += (_, _) => OnGrowthTick();
        _growthTickTimer.Start();

        _overlayWindow = new OverlayWindow();
        _overlayWindow.PetClicked += TryFeedActiveIndividual;
        RefreshOverlayForActiveIndividual();

        _fullscreenMonitor = new ForegroundWindowMonitor(
            onFullscreenEntered: () => { _isFullscreenHiding = true; RefreshOverlayForActiveIndividual(); },
            onFullscreenExited: () => { _isFullscreenHiding = false; RefreshOverlayForActiveIndividual(); });

        _trayIconService = new TrayIconService();
        _trayIconService.OpenCollectionRequested += OpenCollectionWindow;
        _trayIconService.OpenSettingsRequested += OpenSettingsWindow;
        _trayIconService.ExitRequested += () => Shutdown();

        ReconcileSettingsState();

        Exit += (_, _) => SaveState();
    }

    private AppState BuildAppState(SaveData? loaded)
    {
        var state = new AppState();
        if (loaded is not null)
        {
            state.Hydrate(loaded);
        }
        return state;
    }

    private void OnGrowthTick()
    {
        ReconcileSettingsState();

        var units = _inputCounter.DrainUnits();
        if (_tokenUsageProvider is not null)
        {
            units += _tokenUsageProvider.ReadNewUnitsSinceLastCall();
        }
        if (units > 0)
        {
            _appState.ApplyActivityUnits(units, DateTimeOffset.UtcNow);
            _feedingService.EarnTreatsFromActivity(_appState.Feeding, units);
        }

        CheckHardcoreNeglect();

        // Piggybacks on this existing 5-second timer so a crash or Task Manager kill
        // never loses more than a few seconds of session state. JsonSaveStore.Save
        // already writes atomically (temp file + rename), so calling it repeatedly here
        // is safe and cheap enough at this cadence for a single-user desktop app.
        // Uses PersistState() (write-only), NOT SaveState() — SaveState() also disposes
        // the input hook/fullscreen monitor/tray icon, which must stay alive between ticks.
        PersistState();
    }

    /// Hardcore care mode's "убегает" mechanic (spec: "Уход за питомцем") only has a
    /// visible effect if something polls for it — this is that poll, riding the same
    /// 5-second timer as growth so it doesn't need a timer of its own.
    private void CheckHardcoreNeglect()
    {
        if (_appState.Settings.CareMode != CareMode.Hardcore) return;

        var active = _appState.Collection.GetActive();
        if (active is null) return;

        var careState = _appState.GetOrCreateCareState(active.Id);
        var now = DateTimeOffset.UtcNow;
        if (!_careModeService.ShouldRunAway(careState, now, _appState.Settings.CareMode)) return;

        _careModeService.TriggerRunAway(careState, now);
        _appState.Collection.ClearActive();
        RefreshOverlayForActiveIndividual();
        _trayIconService.ShowNotification("PokéCorner", "Питомец расстроен и не хочет возвращаться на стол ещё час.");
    }

    private void ReconcileSettingsState()
    {
        if (_appState.Settings.ActivityTrackingEnabled && _inputHook is null)
        {
            _inputHook = new GlobalInputHook(_inputCounter);
            _inputHook.Start();
        }
        else if (!_appState.Settings.ActivityTrackingEnabled && _inputHook is not null)
        {
            _inputHook.Dispose();
            _inputHook = null;
        }
        _trayIconService.SetActivityTrackingIndicator(_appState.Settings.ActivityTrackingEnabled);

        if (_appState.Settings.TokenTrackingEnabled && _tokenUsageProvider is null)
        {
            _tokenUsageProvider = new ClaudeCodeUsageProvider(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\.claude");
        }
        else if (!_appState.Settings.TokenTrackingEnabled && _tokenUsageProvider is not null)
        {
            _tokenUsageProvider = null;
        }

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

        var desiredCorner = ParseCorner(_appState.Settings.ScreenCorner);
        if (desiredCorner != _appliedCorner || _appState.Settings.MonitorIndex != _appliedMonitorIndex || _appState.Settings.PetSizePixels != _appliedPetSizePixels)
        {
            _overlayWindow.PositionAt(desiredCorner, _appState.Settings.MonitorIndex, _appState.Settings.PetSizePixels);
            _appliedCorner = desiredCorner;
            _appliedMonitorIndex = _appState.Settings.MonitorIndex;
            _appliedPetSizePixels = _appState.Settings.PetSizePixels;
        }
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
            _overlayWindow.Hide();
            return;
        }

        var displaySpeciesId = ResolveDisplaySpeciesId(active);

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
            _overlayWindow.SetSpriteFrames(new List<System.Windows.Media.ImageSource> { DecodeSprite(spriteBytes) });
        }

        if (_isFullscreenHiding)
        {
            _overlayWindow.Hide();
        }
        else
        {
            _overlayWindow.Show();
        }
    }

    // This lookup is scoped to the current 7-chain offline catalog on purpose — when the
    // full-catalog follow-up plan (docs/superpowers/plans/2026-08-19-pokecorner-full-experience-and-installer.md)
    // lands, this will need to search that catalog instead.
    private int ResolveDisplaySpeciesId(PokemonIndividual individual)
    {
        var chain = _spriteCache.LoadOfflineStarters().FirstOrDefault(c => c.ChainId == individual.EvolutionChainId);
        if (chain is null) return individual.SpeciesId; // defensive fallback, shouldn't happen given the current 7-chain catalog

        var stageIndex = (int)individual.CurrentStage;
        return stageIndex < chain.StageSpeciesIds.Count ? chain.StageSpeciesIds[stageIndex] : individual.SpeciesId;
    }

    private static System.Windows.Media.ImageSource DecodeSprite(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var bitmap = new System.Windows.Media.Imaging.BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private void OnCatchRollDue()
    {
        // PokemonCollection.Add() auto-activates via ActiveIndividualId ??= individual.Id
        // whenever nothing is currently active. That's correct for the very first
        // individual ever added (onboarding), but the only way ActiveIndividualId is null
        // with a non-empty collection here is that CheckHardcoreNeglect deliberately
        // called ClearActive() — and the spec is explicit that nobody should auto-appear
        // on the desk after a run-away ("никто не выходит автоматически"). Detect that
        // case up front and undo the auto-activation below.
        var hadNoActiveInNonEmptyCollection = _appState.Collection.ActiveIndividualId is null && _appState.Collection.Individuals.Count > 0;

        var ownedChainIds = _appState.Collection.Individuals.Select(i => i.EvolutionChainId).ToHashSet();
        var availableChains = _spriteCache.LoadOfflineStarters(); // Widened to the full Gen 1-5 catalog by Task 1 of docs/superpowers/plans/2026-08-19-pokecorner-full-experience-and-installer.md, once this plan ships and is verified.
        var ownedNatures = _appState.Collection.Individuals
            .Where(i => ownedChainIds.Contains(i.EvolutionChainId))
            .Select(i => i.Nature)
            .ToHashSet();

        var result = _catchRoller.RollCatch(availableChains, ownedChainIds, ownedNatures);
        _appState.Collection.Add(new PokemonIndividual
        {
            SpeciesId = result.SpeciesId,
            EvolutionChainId = result.ChainId,
            ChainLength = result.ChainLength,
            Nature = result.Nature,
            IsShiny = result.IsShiny,
            CaughtAtUtc = DateTimeOffset.UtcNow
        });

        if (hadNoActiveInNonEmptyCollection)
        {
            _appState.Collection.ClearActive();
        }

        RefreshOverlayForActiveIndividual();
        _trayIconService.ShowNotification("PokéCorner", "Поймал нового покемона! Загляни в коллекцию.");
    }

    private void TryFeedActiveIndividual()
    {
        var active = _appState.Collection.GetActive();
        if (active is null) return;

        var careState = _appState.GetOrCreateCareState(active.Id);
        _feedingService.TryFeed(_appState.Settings.FeedingMode, _appState.Feeding, careState, DateTimeOffset.UtcNow);
    }

    private void OpenCollectionWindow()
    {
        if (_collectionWindow is not null)
        {
            _collectionWindow.Activate();
            return;
        }

        var vm = new CollectionViewModel(_appState.Collection, _appState.CareStateByIndividualId, _careModeService, _appState.Settings.CareMode);
        _collectionWindow = new Window { Content = new CollectionView(vm, _appState, TryFeedActiveIndividual, RefreshOverlayForActiveIndividual), Title = "Коллекция", Width = 420, Height = 480 };
        _collectionWindow.Closed += (_, _) => _collectionWindow = null;
        _collectionWindow.Show();
    }

    private void OpenSettingsWindow()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        var vm = new SettingsViewModel(_appState.Settings);
        _settingsWindow = new Window { Content = new SettingsView(vm), Title = "Настройки", Width = 360, Height = 480 };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private static ScreenCorner ParseCorner(string corner) => corner switch
    {
        "TopLeft" => ScreenCorner.TopLeft,
        "TopRight" => ScreenCorner.TopRight,
        "BottomLeft" => ScreenCorner.BottomLeft,
        _ => ScreenCorner.BottomRight
    };

    private void SaveState()
    {
        _inputHook?.Dispose();
        _fullscreenMonitor?.Dispose();
        _trayIconService?.Dispose();

        PersistState();
    }

    // Just the data-write, with none of SaveState()'s resource disposal — used by the
    // periodic autosave in OnGrowthTick, which must NOT tear down the global input hook,
    // fullscreen monitor, or tray icon every 5 seconds. SaveState() (full shutdown path,
    // used on Exit and from the crash handler) still disposes those and then delegates
    // here for the actual write.
    private void PersistState()
    {
        var data = new SaveData
        {
            Individuals = _appState.Collection.Individuals.ToList(),
            ActiveIndividualId = _appState.Collection.ActiveIndividualId,
            Settings = _appState.Settings,
            FeedingTreats = _appState.Feeding.Treats,
            CareStates = new Dictionary<Guid, CareState>(_appState.CareStateByIndividualId)
        };
        _saveStore.Save(data);
    }
}
