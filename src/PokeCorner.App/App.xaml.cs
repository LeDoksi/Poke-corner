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
    private EggIncubationService _eggIncubationService = null!;
    private CareModeService _careModeService = null!;
    private FeedingService _feedingService = null!;
    private bool _isFullscreenHiding;
    private bool _isFullscreenMonitorRunning;
    private int _overlayRefreshGeneration;
    private AppMenuWindow? _menuWindow;
    private ShopService _shopService = null!;
    private const int CollectionTabIndex = 0;
    private const int SettingsTabIndex = 1;
    private const int ShopTabIndex = 2;
    private const int CatchTabIndex = 3;
    private ScreenCorner _appliedCorner;
    private int _appliedMonitorIndex;
    private int _appliedPetSizePixels;
    private bool _catchReady;
    private byte[]? _activeSpriteBytes;
    private ProgressPanelWindow? _progressPanelWindow;

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
        _eggIncubationService = new EggIncubationService();
        _careModeService = new CareModeService();
        _feedingService = new FeedingService();
        _shopService = new ShopService();

        var loaded = _saveStore.Load();
        _appState = BuildAppState(loaded);

        if (FirstRunFlow.NeedsOnboarding(loaded))
        {
            var starter = FirstRunFlow.HatchStarter(_catchRoller, _spriteCache.LoadOfflineStarters(), DateTimeOffset.UtcNow);
            _appState.Collection.Add(starter);
        }

        _appState.CatchRollDue += OnCatchRollDue; // now just flips "ready" state — see OnCatchRollDue

        _inputCounter = new InputEventCounter();

        _growthTickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _growthTickTimer.Tick += (_, _) => OnGrowthTick();
        _growthTickTimer.Start();

        _overlayWindow = new OverlayWindow();
        _overlayWindow.PetClicked += PetActiveIndividual;
        _overlayWindow.MenuRequested += () => OpenMenu(CollectionTabIndex);
        RefreshOverlayForActiveIndividual();

        _fullscreenMonitor = new ForegroundWindowMonitor(
            onFullscreenEntered: () => { _isFullscreenHiding = true; RefreshOverlayForActiveIndividual(); },
            onFullscreenExited: () => { _isFullscreenHiding = false; RefreshOverlayForActiveIndividual(); });

        _trayIconService = new TrayIconService();
        _trayIconService.OpenCollectionRequested += () => OpenMenu(CollectionTabIndex);
        _trayIconService.OpenSettingsRequested += () => OpenMenu(SettingsTabIndex);
        _trayIconService.OpenShopRequested += () => OpenMenu(ShopTabIndex);
        _trayIconService.CatchRequested += () => OpenMenu(CatchTabIndex);
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
            _shopService.EarnCoinsFromActivity(_appState.Shop, units);
        }

        _progressPanelWindow?.UpdateProgress(ProgressCalculator.Compute(_appState.Activity, _appState.Collection.GetActive(), _appState.Feeding));

        CheckHardcoreNeglect();

        // Any window on the system being activated (ours or someone else's — e.g. an
        // Alt-Tab back to another app) can bump a WS_EX_TOPMOST window out of the
        // topmost band on some Windows builds; re-asserting Topmost periodically is the
        // standard workaround, cheap enough to just do on this existing 5-second tick.
        ReassertOverlayTopmost();

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

        if (_appState.Settings.ShowProgressPanel && _progressPanelWindow is null)
        {
            _progressPanelWindow = new ProgressPanelWindow();
            _progressPanelWindow.PositionBelow(_overlayWindow);
            _progressPanelWindow.Show();
        }
        else if (!_appState.Settings.ShowProgressPanel && _progressPanelWindow is not null)
        {
            _progressPanelWindow.Close();
            _progressPanelWindow = null;
        }

        _progressPanelWindow?.PositionBelow(_overlayWindow);
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
            _activeSpriteBytes = spriteBytes;
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

    /// Activity has crossed the threshold for a catch — per explicit feedback, this
    /// should NOT catch automatically (that took the moment away from the player).
    /// Instead it just flips "ready" so the player triggers the actual catch themselves
    /// from the Catch tab — the tray's "Поймать!" item now just opens the menu there
    /// (see the CatchRequested subscription in OnStartup) and the tab's button calls
    /// TryPerformCatch.
    private void OnCatchRollDue()
    {
        if (_catchReady) return; // already waiting on the player — don't re-notify

        _catchReady = true;
        _trayIconService.SetCatchReady(true);
        _trayIconService.ShowNotification("PokéCorner", "Готов новый покемон! Открой меню трея и нажми «Поймать!».");
    }

    private CatchReveal? TryPerformCatch()
    {
        if (!_catchReady) return null;

        // PokemonCollection.Add() auto-activates via ActiveIndividualId ??= individual.Id
        // whenever nothing is currently active — correct for onboarding, but the only way
        // ActiveIndividualId is null with a non-empty collection here is a deliberate
        // Hardcore run-away, and nobody should auto-appear on the desk after that.
        var hadNoActiveInNonEmptyCollection = _appState.Collection.ActiveIndividualId is null && _appState.Collection.Individuals.Count > 0;

        var availableChains = _spriteCache.LoadOfflineStarters(); // Task 20 swaps this to _speciesCatalog.LoadCachedOrFallback()
        var ownedChainIds = _appState.Collection.Individuals.Select(i => i.EvolutionChainId).ToHashSet();
        var ownedNatures = _appState.Collection.Individuals
            .Where(i => ownedChainIds.Contains(i.EvolutionChainId))
            .Select(i => i.Nature)
            .ToHashSet();

        var shinyMultiplier = _appState.Shop.CatchLuckyCharmActive ? ShopService.CatchLuckyCharmShinyMultiplier : 1.0;
        var result = _catchRoller.RollCatch(availableChains, ownedChainIds, ownedNatures, shinyMultiplier);
        _appState.Shop.CatchLuckyCharmActive = false; // consumed regardless of outcome

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

        _catchReady = false;
        _trayIconService.SetCatchReady(false);
        RefreshOverlayForActiveIndividual();

        var chain = availableChains.FirstOrDefault(c => c.ChainId == result.ChainId);
        var rarity = chain is null ? RarityTier.Common : RarityClassifier.Classify(chain.CaptureRate);
        return new CatchReveal(result, rarity);
    }

    private void PetActiveIndividual()
    {
        var active = _appState.Collection.GetActive();
        if (active is null) return;

        var careState = _appState.GetOrCreateCareState(active.Id);
        _feedingService.Pet(active, careState, DateTimeOffset.UtcNow);
        _appState.RecomputeStage(active);
        _trayIconService.ShowNotification("PokéCorner", "Погладил!");
    }

    private void FeedActiveIndividual()
    {
        var active = _appState.Collection.GetActive();
        if (active is null) return;

        var careState = _appState.GetOrCreateCareState(active.Id);
        var fed = _feedingService.Feed(active, _appState.Feeding, careState, DateTimeOffset.UtcNow);
        if (fed)
        {
            _appState.RecomputeStage(active);
            _trayIconService.ShowNotification("PokéCorner", "Покормил!");
        }
        else
        {
            _trayIconService.ShowNotification("PokéCorner", "Не хватает лакомств — накопи ещё активности.");
        }
    }

    private bool HatchEgg(Guid eggId)
    {
        var egg = _appState.PendingEggs.FirstOrDefault(e => e.Id == eggId);
        if (egg is null || !_eggIncubationService.IsReady(egg, DateTimeOffset.UtcNow)) return false;

        var availableChains = _spriteCache.LoadOfflineStarters(); // Task 20 swaps this to _speciesCatalog.LoadCachedOrFallback()
        var ownedChainIds = _appState.Collection.Individuals.Select(i => i.EvolutionChainId).ToHashSet();
        var ownedNatures = _appState.Collection.Individuals
            .Where(i => ownedChainIds.Contains(i.EvolutionChainId))
            .Select(i => i.Nature)
            .ToHashSet();

        var result = _catchRoller.RollCatchFromTier(availableChains, egg.Tier, ownedChainIds, ownedNatures);
        _appState.Collection.Add(new PokemonIndividual
        {
            SpeciesId = result.SpeciesId,
            EvolutionChainId = result.ChainId,
            ChainLength = result.ChainLength,
            Nature = result.Nature,
            IsShiny = result.IsShiny,
            CaughtAtUtc = DateTimeOffset.UtcNow
        });
        _appState.PendingEggs.Remove(egg);

        _menuWindow?.ShowCatchReveal(new CatchReveal(result, egg.Tier));
        return true;
    }

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
                var collectionVm = new CollectionViewModel(_appState.Collection, _appState.CareStateByIndividualId, _careModeService, _appState.Settings.CareMode, _spriteCache.LoadOfflineStarters(), _appState.PendingEggs);
                var collectionView = new CollectionView(collectionVm, _appState, PetActiveIndividual, FeedActiveIndividual, RefreshOverlayForActiveIndividual, () => _activeSpriteBytes, HatchEgg);
                var settingsView = new SettingsView(new SettingsViewModel(_appState.Settings));
                var shopView = new ShopView(_appState, _shopService, BuyEgg, RefreshOverlayForActiveIndividual);
                var catchView = new CatchView(_appState, TryPerformCatch, speciesId => _spriteCache.GetOrFetchAsync(speciesId, () => _pokeApiClient.FetchSpriteAsync(speciesId)), () => _catchReady, RefreshOverlayForActiveIndividual);
                _menuWindow = new AppMenuWindow(collectionView, settingsView, shopView, catchView);
                _menuWindow.Closed += (_, _) => _menuWindow = null;
                _menuWindow.Show();
            }
            _menuWindow.SelectTab(tabIndex);
            _menuWindow.Activate();
            ReassertOverlayTopmost();
        }
        catch (Exception ex)
        {
            _trayIconService.ShowNotification("PokéCorner — ошибка", ex.Message);
        }
    }

    /// Forces Windows to re-insert the overlay at the top of the topmost band. Toggling
    /// Topmost off/on is the standard trick — just leaving it "true" the whole time
    /// doesn't stop another window's activation from bumping it down.
    private void ReassertOverlayTopmost()
    {
        _overlayWindow.Topmost = false;
        _overlayWindow.Topmost = true;
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
            ShopCoins = _appState.Shop.Coins,
            ShopRemainderUnits = _appState.Shop.RemainderUnits,
            ShopCatchLuckyCharmActive = _appState.Shop.CatchLuckyCharmActive,
            PendingEggs = _appState.PendingEggs,
            CareStates = new Dictionary<Guid, CareState>(_appState.CareStateByIndividualId)
        };
        _saveStore.Save(data);
    }
}
