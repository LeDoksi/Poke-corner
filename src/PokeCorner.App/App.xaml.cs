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
    private SpeciesCatalog _speciesCatalog = null!;
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
    /// Every decoded frame of the active individual's sprite, in order — the same list the
    /// overlay animates from. This replaces the previous single-frame `_activeSpriteBytes`
    /// field: the collection popup only ever received frame 0 through it, which is why its
    /// hero sprite sat perfectly still while the desktop pet animated. A single-element list
    /// means "this species has no animated sprite", which the hero renders statically; null
    /// means "nothing cached yet".
    private IReadOnlyList<byte[]>? _activeSpriteFrames;
    private ProgressPanelWindow? _progressPanelWindow;

    /// Raw animated-GIF bytes per display species id. Deliberately in-memory only: unlike the
    /// static PNG path there's no SpriteCache disk slot for GIFs (its filenames are always
    /// "{speciesId}.png", so an animated entry would collide with the static one), and
    /// widening SpriteCache's public API would break other tasks' tests.
    private readonly Dictionary<int, byte[]> _animatedSpriteCache = new();

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
        var catalogPath = Path.Combine(AppPaths.GetAppDataDirectory(), "catalog.json");
        _speciesCatalog = new SpeciesCatalog(_pokeApiClient, _spriteCache, catalogPath);

        // The refresh crawls ~650 species (1000+ sequential requests against the free public
        // PokeAPI), so firing it on every launch of an autostart-with-Windows app is both
        // wasteful and rude to that API. Species data changes rarely — a weekly refresh is
        // plenty. Fire-and-forget when it does run; today's rolls still use whatever
        // LoadCachedOrFallback returns right now.
        var catalogIsStale = !File.Exists(catalogPath) || File.GetLastWriteTimeUtc(catalogPath) < DateTime.UtcNow.AddDays(-7);
        if (catalogIsStale)
        {
            _ = _speciesCatalog.RefreshFromNetworkAsync();
        }
        _catchRoller = new CatchRoller();
        _eggIncubationService = new EggIncubationService();
        _careModeService = new CareModeService();
        _feedingService = new FeedingService();
        _shopService = new ShopService();

        var loaded = _saveStore.Load();
        _appState = BuildAppState(loaded);

        if (FirstRunFlow.NeedsOnboarding(loaded))
        {
            var allChains = _spriteCache.LoadOfflineStarters();
            var commonCandidates = allChains
                .Where(c => RarityClassifier.Classify(c.CaptureRate) == RarityTier.Common)
                .OrderBy(_ => Random.Shared.Next())
                .Take(3)
                .ToList();
            if (commonCandidates.Count == 0)
            {
                // Defensive fallback — shouldn't happen given the offline catalog's known
                // capture rates, but never leave the player with zero choices.
                commonCandidates = allChains.Take(3).ToList();
            }

            var selectionWindow = new StarterSelectionWindow(commonCandidates, speciesId => _spriteCache.GetOrFetchAsync(speciesId, () => _pokeApiClient.FetchSpriteAsync(speciesId)));
            var dialogChoseOne = selectionWindow.ShowDialog() == true;
            var chosenChain = dialogChoseOne ? selectionWindow.Chosen! : commonCandidates[0]; // window closed without a pick (e.g. Alt+F4) still needs a starter

            var starter = FirstRunFlow.HatchChosen(chosenChain, _catchRoller, DateTimeOffset.UtcNow);
            _appState.Collection.Add(starter);

            new OnboardingWindow().ShowDialog();
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
        _overlayWindow.SetMoodTint(_careModeService.ComputeMood(careState, now));

        if (!_careModeService.ShouldRunAway(careState, now, _appState.Settings.CareMode)) return;

        _careModeService.TriggerRunAway(careState, now);
        _overlayWindow.PlayRanAwayReaction();
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

        ApplyPetSizeAndPosition();

        if (_appState.Settings.ShowProgressPanel && _progressPanelWindow is null)
        {
            _progressPanelWindow = new ProgressPanelWindow();
            _progressPanelWindow.PositionBelow(_overlayWindow);
            // Prime the bars immediately. Without this the panel spends its first seconds
            // showing three empty tracks — OnGrowthTick only pushes values every 5 seconds,
            // so a freshly enabled panel looked broken right at the moment the user turned
            // it on to look at it.
            _progressPanelWindow.UpdateProgress(ProgressCalculator.Compute(_appState.Activity, _appState.Collection.GetActive(), _appState.Feeding));
            _progressPanelWindow.Show();
        }
        else if (!_appState.Settings.ShowProgressPanel && _progressPanelWindow is not null)
        {
            _progressPanelWindow.Close();
            _progressPanelWindow = null;
        }

        _progressPanelWindow?.PositionBelow(_overlayWindow);
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

        _overlayWindow.PositionAt(desiredCorner, _appState.Settings.MonitorIndex, _appState.Settings.PetSizePixels);
        _appliedCorner = desiredCorner;
        _appliedMonitorIndex = _appState.Settings.MonitorIndex;
        _appliedPetSizePixels = _appState.Settings.PetSizePixels;

        // The panel is anchored to the overlay's bottom edge, which just moved.
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
            // Drop the cached sprite too, otherwise the collection popup's hero keeps
            // rendering (and now animating) the pet that just ran away or was released.
            _activeSpriteFrames = null;
            _overlayWindow.Hide();
            return;
        }

        var displaySpeciesId = ResolveDisplaySpeciesId(active);

        // This method runs on app start, every fullscreen enter/exit, every Pet/Feed click,
        // every Select/Release, every purchase, catch and hatch. Without this memo the
        // animated path would re-issue two real HTTP requests (the PokeAPI JSON document
        // plus the GIF download) plus a full decode every single time, and would lose
        // animation entirely when offline even for an already-seen species. In-memory only
        // is the deliberate trade-off: one legitimate fetch per active individual per launch.
        byte[]? animatedBytes = null;
        if (!_animatedSpriteCache.TryGetValue(displaySpeciesId, out animatedBytes))
        {
            try
            {
                animatedBytes = await _pokeApiClient.FetchAnimatedSpriteAsync(displaySpeciesId);
            }
            catch
            {
                // Same defense-in-depth as the static-sprite catch below — network errors already
                // degrade to null inside PokeApiClient's normal paths; this guards unexpected ones.
            }

            if (animatedBytes is not null)
            {
                _animatedSpriteCache[displaySpeciesId] = animatedBytes;
            }
        }

        if (animatedBytes is not null)
        {
            if (generation != _overlayRefreshGeneration)
            {
                // A newer refresh started (and possibly already finished) while we were
                // awaiting the animated-sprite fetch above — applying our now-stale result
                // would clobber it. Mirrors the staleness check below for the static path.
                return;
            }

            // The decode is NOT covered by the fetch's try/catch above, and a non-GIF or
            // truncated response body makes it throw — straight past all handling into
            // DispatcherUnhandledException, which deliberately doesn't suppress crashes.
            IReadOnlyList<byte[]> pngFrames;
            try
            {
                pngFrames = GifFrameDecoder.DecodeFramesAsPng(animatedBytes);
            }
            catch
            {
                pngFrames = Array.Empty<byte[]>();
            }

            // Guards pngFrames[0] below: a malformed-but-decodable GIF can yield zero frames.
            if (pngFrames.Count > 0)
            {
                _overlayWindow.SetSpriteFrames(pngFrames.Select(DecodeSprite).ToList());
                _activeSpriteFrames = pngFrames;
                _trayIconService.SetSpriteIcon(pngFrames[0]);

                if (_isFullscreenHiding)
                {
                    _overlayWindow.Hide();
                }
                else
                {
                    _overlayWindow.Show();
                }

                return;
            }

            // Zero frames (decode failed or malformed GIF) — fall through to the static
            // path below, exactly as if animatedBytes had been null.
        }

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
            _activeSpriteFrames = new[] { spriteBytes }; // one real frame: static, but not "unknown"
            _trayIconService.SetSpriteIcon(spriteBytes);
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

    // Reads from the same widened species catalog as every other catch-pool call site here
    // (OpenMenu/TryPerformCatch/HatchEgg). It used to search only the 7-chain offline
    // starter pool, so any individual caught from the network catalog fell through to the
    // defensive fallback below and the sprite never advanced past its base form.
    private int ResolveDisplaySpeciesId(PokemonIndividual individual)
    {
        var chain = _speciesCatalog.LoadCachedOrFallback().FirstOrDefault(c => c.ChainId == individual.EvolutionChainId);
        if (chain is null) return individual.SpeciesId; // defensive fallback for an individual whose chain isn't in the catalog

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

        var availableChains = _speciesCatalog.LoadCachedOrFallback();
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

        var stageBefore = active.CurrentStage;
        var careState = _appState.GetOrCreateCareState(active.Id);
        _feedingService.Pet(active, careState, DateTimeOffset.UtcNow);
        _appState.RecomputeStage(active);
        _trayIconService.ShowNotification("PokéCorner", "Погладил!");
        _overlayWindow.PlayPetReaction();
        if (active.CurrentStage != stageBefore) _overlayWindow.PlayEvolutionFlash();
    }

    private void FeedActiveIndividual()
    {
        var active = _appState.Collection.GetActive();
        if (active is null) return;

        var stageBefore = active.CurrentStage;
        var careState = _appState.GetOrCreateCareState(active.Id);
        var fed = _feedingService.Feed(active, _appState.Feeding, careState, DateTimeOffset.UtcNow);
        if (fed)
        {
            _appState.RecomputeStage(active);
            _trayIconService.ShowNotification("PokéCorner", "Покормил!");
            _overlayWindow.PlayFeedReaction();
            if (active.CurrentStage != stageBefore) _overlayWindow.PlayEvolutionFlash();
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

        var availableChains = _speciesCatalog.LoadCachedOrFallback();

        // Defense in depth behind the shop's tier gating (see IsRarityTierAvailable): a save
        // file written before that gating existed — or hand-edited — can still hold a pending
        // egg of a tier the current catalog can't fill. RollCatchFromTier throws in that case,
        // and DispatcherUnhandledException deliberately doesn't suppress crashes, so every
        // launch would crash the moment that egg became hatchable. Bail out instead, keeping
        // the egg pending so it can be retried once the catalog widens.
        if (!availableChains.Any(c => RarityClassifier.Classify(c.CaptureRate) == egg.Tier))
        {
            _trayIconService.ShowNotification("PokéCorner", "Пока нет доступных покемонов этой редкости — попробуй позже, когда каталог обновится.");
            return false;
        }

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

    /// Whether the species catalog currently in use (same source as BuyEgg/HatchEgg) has at
    /// least one chain in this rarity tier. The offline fallback catalog has no Uncommon and
    /// no Legendary chains at all, so without this the shop would happily sell an egg that
    /// can never hatch — spending the coins for good and crashing on the hatch attempt.
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
                var collectionVm = new CollectionViewModel(_appState.Collection, _appState.CareStateByIndividualId, _careModeService, _appState.Settings.CareMode, _speciesCatalog.LoadCachedOrFallback(), _appState.PendingEggs);
                var collectionView = new CollectionView(collectionVm, _appState, _spriteCache, PetActiveIndividual, FeedActiveIndividual, RefreshOverlayForActiveIndividual, () => _activeSpriteFrames, HatchEgg);
                var settingsView = new SettingsView(new SettingsViewModel(_appState.Settings), ApplyPetSizeAndPosition);
                var shopView = new ShopView(_appState, _shopService, BuyEgg, IsRarityTierAvailable, RefreshOverlayForActiveIndividual);
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
