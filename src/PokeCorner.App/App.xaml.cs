using System.Diagnostics;
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
    private const int ShopTabIndex = 1;
    private const int CatchTabIndex = 2;
    private const int SettingsTabIndex = 3;
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
    private static readonly TimeSpan AutosaveInterval = TimeSpan.FromSeconds(5);
    private DateTimeOffset _lastAutosaveUtc = DateTimeOffset.MinValue;
    private Guid? _evolutionReadyNotifiedFor;
    private Guid? _neglectWarningNotifiedFor;

    /// Set once by ResetSaveDataAndRestart; makes PersistState a no-op for the rest of this
    /// process's life so nothing writes the save file back after it has been deleted.
    private bool _isWipingSaveData;

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
        CheckEvolutionReady();

        // Backstop for the ForegroundWindowChanged hook: plenty of things reorder the normal
        // z-order band without a foreground change (a window being restored, shown, or moved
        // by another app), and SetWindowPos on an already-top window is a no-op, so this is
        // cheap enough to just keep doing on the existing tick.
        ReassertOverlayTopmost();

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

        // Before anything reads the neglect clock: a PC left on overnight, or the app being
        // closed for hours, must not cost the player their pet. Only the active individual is
        // adjusted — the same scope everything else in this method works at.
        _careModeService.DiscountIdleGap(careState, now);

        _overlayWindow.SetMoodTint(_careModeService.ComputeMood(careState, now));

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

        _overlayWindow.PositionAt(desiredCorner, _appState.Settings.MonitorIndex, _appState.Settings.PetSizePixels);
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
            _overlayWindow.SetSpriteFrames(pngFrames.Select(DecodeSprite).ToList());
            _activeSpriteFrames = pngFrames;
            _trayIconService.SetSpriteIcon(pngFrames[0]);

            if (_isFullscreenHiding)
            {
                _overlayWindow.Hide();
                _progressPanelWindow?.Hide();
            }
            else
            {
                _overlayWindow.Show();
                _progressPanelWindow?.Show();
            }

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
            _overlayWindow.SetSpriteFrames(new List<System.Windows.Media.ImageSource> { DecodeSprite(spriteBytes) });
            _activeSpriteFrames = new[] { spriteBytes }; // one real frame: static, but not "unknown"
            _trayIconService.SetSpriteIcon(spriteBytes);
        }

        if (_isFullscreenHiding)
        {
            _overlayWindow.Hide();
            _progressPanelWindow?.Hide();
        }
        else
        {
            _overlayWindow.Show();
            _progressPanelWindow?.Show();
        }
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
    /// The memo holds the raw GIF, not the decoded frames: decoding is cheap next to the two
    /// HTTP round trips, and caching bytes keeps this method's contract (fresh list per call)
    /// free of aliasing concerns between the overlay and the popup.
    private async Task<IReadOnlyList<byte[]>?> GetAnimatedFramesAsync(int speciesId)
    {
        if (!_animatedSpriteCache.TryGetValue(speciesId, out var animatedBytes))
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

            if (animatedBytes is not null)
            {
                _animatedSpriteCache[speciesId] = animatedBytes;
            }
        }

        if (animatedBytes is null) return null;

        try
        {
            // A non-GIF or truncated response body makes the decode throw, and a
            // malformed-but-decodable GIF can yield zero frames — both mean "no animation".
            var frames = GifFrameDecoder.DecodeFramesAsPng(animatedBytes);
            return frames.Count > 0 ? frames : null;
        }
        catch
        {
            return null;
        }
    }

    // Reads from the same widened species catalog as every other catch-pool call site here
    // (OpenMenu/TryPerformCatch/HatchEgg). It used to search only the 7-chain offline
    // starter pool, so any individual caught from the network catalog fell through to the
    // defensive fallback below and the sprite never advanced past its base form.
    private int ResolveDisplaySpeciesId(PokemonIndividual individual)
    {
        // LoadForLookup, not LoadCachedOrFallback: an individual migrated off the old colliding
        // offline pool sits on a reserved legacy chain id that only the lookup view contains.
        var chain = _speciesCatalog.LoadForLookup().FirstOrDefault(c => c.ChainId == individual.EvolutionChainId);
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

    /// No tray toast here on purpose: PlayPetReaction is already a visible on-screen response
    /// to every click, and pet/feed get clicked often enough that a notification per click was
    /// pure noise. Evolution no longer happens inline either — see EvolveActiveIndividual.
    private void PetActiveIndividual()
    {
        var active = _appState.Collection.GetActive();
        if (active is null) return;

        var careState = _appState.GetOrCreateCareState(active.Id);
        _feedingService.Pet(active, careState, DateTimeOffset.UtcNow);
        _overlayWindow.PlayPetReaction();
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
                var collectionView = new CollectionView(collectionVm, _appState, _spriteCache, PetActiveIndividual, FeedActiveIndividual, RefreshOverlayForActiveIndividual, () => _activeSpriteFrames, HatchEgg, EvolveActiveIndividual, IsActiveIndividualReadyToEvolve, GetAnimatedFramesAsync);
                var settingsView = new SettingsView(new SettingsViewModel(_appState.Settings), ApplyPetSizeAndPosition, ResetSaveDataAndRestart);
                var shopView = new ShopView(_appState, _shopService, BuyEgg, IsRarityTierAvailable, RefreshOverlayForActiveIndividual);
                var catchView = new CatchView(_appState, TryPerformCatch, speciesId => _spriteCache.GetOrFetchAsync(speciesId, () => _pokeApiClient.FetchSpriteAsync(speciesId)), () => _catchReady, RefreshOverlayForActiveIndividual, speciesId => _speciesCatalog.LoadForLookup().ResolveSpeciesName(speciesId));
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

    /// Re-inserts the overlay (and the panel anchored to it) directly behind the taskbar, so
    /// it stays above ordinary app windows but below every shell surface. Neither window sets
    /// Topmost="True" any more — that put them in the flat topmost band, where they could and
    /// did render over the taskbar and tray flyouts. See OverlayWindow.KeepAboveAppsBelowShell
    /// for why the obvious non-topmost alternative does not work either.
    private void ReassertOverlayTopmost()
    {
        OverlayWindow.KeepAboveAppsBelowShell(_overlayWindow);
        if (_progressPanelWindow is not null)
        {
            OverlayWindow.KeepAboveAppsBelowShell(_progressPanelWindow);
        }
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
    private void ResetSaveDataAndRestart()
    {
        _isWipingSaveData = true;

        var savePath = AppPaths.GetSaveFilePath();
        if (File.Exists(savePath))
        {
            File.Delete(savePath);
        }

        Process.Start(Environment.ProcessPath!);

        // Shutdown(), matching the tray's "Выход" item, rather than Environment.Exit: it runs
        // the Exit handler, so the global input hook, the fullscreen monitor and the tray icon
        // are all disposed properly instead of being abandoned while the replacement process
        // is already starting up. Its SaveState() call reaches a write that _isWipingSaveData
        // now suppresses.
        Shutdown();
    }

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
        // The one case where not writing is correct: ResetSaveDataAndRestart has just deleted
        // state.json, and every remaining write path leads back here — the autosave tick, the
        // Exit handler's SaveState(), the crash handler. Without this the shutdown that
        // follows the delete would immediately write the in-memory collection straight back
        // out and the relaunched process would find a save file exactly where it left one.
        if (_isWipingSaveData) return;

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
