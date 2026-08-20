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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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
        if (_appState.Settings.ActivityTrackingEnabled)
        {
            _inputHook = new GlobalInputHook(_inputCounter);
            _inputHook.Start();
        }

        if (_appState.Settings.TokenTrackingEnabled)
        {
            _tokenUsageProvider = new ClaudeCodeUsageProvider(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\.claude");
        }

        _growthTickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _growthTickTimer.Tick += (_, _) => OnGrowthTick();
        _growthTickTimer.Start();

        _overlayWindow = new OverlayWindow();
        _overlayWindow.PositionAt(ParseCorner(_appState.Settings.ScreenCorner), _appState.Settings.MonitorIndex, _appState.Settings.PetSizePixels);
        _overlayWindow.PetClicked += TryFeedActiveIndividual;
        RefreshOverlayForActiveIndividual();

        _fullscreenMonitor = new ForegroundWindowMonitor(
            onFullscreenEntered: () => { _isFullscreenHiding = true; RefreshOverlayForActiveIndividual(); },
            onFullscreenExited: () => { _isFullscreenHiding = false; RefreshOverlayForActiveIndividual(); });
        if (_appState.Settings.AutoHideInFullscreen) _fullscreenMonitor.Start();

        _trayIconService = new TrayIconService();
        _trayIconService.SetActivityTrackingIndicator(_appState.Settings.ActivityTrackingEnabled);
        _trayIconService.OpenCollectionRequested += OpenCollectionWindow;
        _trayIconService.OpenSettingsRequested += OpenSettingsWindow;
        _trayIconService.ExitRequested += () => Shutdown();

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

    /// Single entry point for "what should the overlay show right now" — called after
    /// startup, after switching/releasing in the collection popup, and after every
    /// fullscreen enter/exit, so the desktop sprite never drifts out of sync with
    /// AppState.Collection.GetActive().
    private void RefreshOverlayForActiveIndividual()
    {
        var active = _appState.Collection.GetActive();
        if (active is null)
        {
            _overlayWindow.Hide();
            return;
        }

        // Blocking on the sprite cache/download here is only ever slow on the very
        // first fetch of a given species (subsequent calls hit the on-disk cache from
        // SpriteCache); acceptable for now given how infrequently the active individual
        // changes, but convert to a fire-and-forget async load with a placeholder frame
        // if this is ever felt as UI jank.
        var spriteBytes = _spriteCache
            .GetOrFetchAsync(active.SpeciesId, () => _pokeApiClient.FetchSpriteAsync(active.SpeciesId))
            .GetAwaiter().GetResult();

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
        var vm = new CollectionViewModel(_appState.Collection, _appState.CareStateByIndividualId, _careModeService, _appState.Settings.CareMode);
        new Window { Content = new CollectionView(vm, _appState, TryFeedActiveIndividual, RefreshOverlayForActiveIndividual), Title = "Коллекция", Width = 420, Height = 480 }.Show();
    }

    private void OpenSettingsWindow()
    {
        var vm = new SettingsViewModel(_appState.Settings);
        new Window { Content = new SettingsView(vm), Title = "Настройки", Width = 360, Height = 480 }.Show();
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
