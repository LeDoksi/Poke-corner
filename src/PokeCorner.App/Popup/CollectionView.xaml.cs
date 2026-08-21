using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;

namespace PokeCorner.App.Popup;

public partial class CollectionView : System.Windows.Controls.UserControl
{
    private readonly CollectionViewModel _viewModel;
    private readonly AppState _appState;
    private readonly SpriteCache _spriteCache;
    private readonly Action _onPetRequested;
    private readonly Action _onFeedRequested;
    private readonly Action _onStateChanged;
    private readonly Func<byte[]?> _getActiveSpriteBytes;
    private readonly Func<Guid, bool> _onHatchRequested;
    private DispatcherTimer? _liveRefreshTimer;

    // Decoded thumbnails are reused across refreshes: Refresh() runs once a second, and
    // re-decoding a PNG per visible card per tick would be pure waste. Only successful
    // decodes are memoized, so a species whose sprite is fetched later in the session
    // still picks it up on the next rebuild instead of being stuck on the placeholder.
    private readonly Dictionary<int, ImageSource> _spriteThumbnails = new();

    // The list rebuilds below replace ItemsSource wholesale, which tears down and recreates
    // every item container (each carrying a drop shadow). Doing that 60 times a minute for
    // data that rarely changes made the cards visibly flicker, so each list only rebuilds
    // when a cheap signature of its content actually changes.
    private string _rosterSignature = string.Empty;
    private string _eggsSignature = string.Empty;
    private string _pokedexSignature = string.Empty;

    public CollectionView(CollectionViewModel viewModel, AppState appState, SpriteCache spriteCache, Action onPetRequested, Action onFeedRequested, Action onStateChanged, Func<byte[]?> getActiveSpriteBytes, Func<Guid, bool> onHatchRequested)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _appState = appState;
        _spriteCache = spriteCache;
        _onPetRequested = onPetRequested;
        _onFeedRequested = onFeedRequested;
        _onStateChanged = onStateChanged;
        _getActiveSpriteBytes = getActiveSpriteBytes;
        _onHatchRequested = onHatchRequested;
        Loaded += (_, _) => StartLiveRefresh();
        Unloaded += (_, _) => StopLiveRefresh();
        Refresh();
    }

    private void StartLiveRefresh()
    {
        _liveRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _liveRefreshTimer.Tick += (_, _) => Refresh();
        _liveRefreshTimer.Start();
    }

    private void StopLiveRefresh()
    {
        _liveRefreshTimer?.Stop();
        _liveRefreshTimer = null;
    }

    private void PetButton_Click(object sender, RoutedEventArgs e)
    {
        _onPetRequested();
        _onStateChanged();
        Refresh();
    }

    private void FeedButton_Click(object sender, RoutedEventArgs e)
    {
        _onFeedRequested();
        _onStateChanged();
        Refresh();
    }

    public void Refresh()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var active = _appState.Collection.GetActive();
        var entries = _viewModel.GetEntries(nowUtc);

        RefreshHero(active, entries);
        RefreshProgress(active);
        RefreshEggs(nowUtc);
        RefreshRoster(entries);
        RefreshPokedex();
    }

    private void RefreshHero(PokemonIndividual? active, IReadOnlyList<CollectionEntry> entries)
    {
        var activeEntry = entries.FirstOrDefault(e => e.IsActive);

        if (active is null || activeEntry is null)
        {
            ActivePetNameText.Text = "Нет активного питомца";
            ActivePetMetaText.Text = "Выбери покемона в списке ниже — он появится на рабочем столе.";
            CarePointsText.Text = "—";
            ShinyBadge.Visibility = Visibility.Collapsed;
        }
        else
        {
            ActivePetNameText.Text = activeEntry.SpeciesName;
            ActivePetMetaText.Text =
                $"{activeEntry.DexNumberText()} · {RarityDisplay.GetName(activeEntry.Rarity)} · характер {activeEntry.Nature} · поймана {FormatCaughtDate(activeEntry.CaughtAtUtc)}";
            CarePointsText.Text = active.AccumulatedCarePoints.ToString("N0", CultureInfo.CurrentCulture);
            ShinyBadge.Visibility = activeEntry.IsShiny ? Visibility.Visible : Visibility.Collapsed;
        }

        TreatsText.Text = _appState.Feeding.Treats.ToString(CultureInfo.CurrentCulture);

        var spriteBytes = _getActiveSpriteBytes();
        ActiveSpriteImage.Source = spriteBytes is null ? null : DecodePng(spriteBytes);
        ActiveSpritePlaceholder.Visibility = spriteBytes is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshProgress(PokemonIndividual? active)
    {
        var progress = ProgressCalculator.Compute(_appState.Activity, active, _appState.Feeding);
        CatchProgressBar.Value = progress.CatchFraction;
        EvolutionProgressBar.Value = progress.EvolutionFraction;
        TreatProgressBar.Value = progress.TreatFraction;

        CatchPercentText.Text = FormatPercent(progress.CatchFraction);
        EvolutionPercentText.Text = FormatPercent(progress.EvolutionFraction);
        TreatPercentText.Text = FormatPercent(progress.TreatFraction);

        var remainingUnits = FeedingService.ActivityUnitsPerTreatEarned - _appState.Feeding.RemainderUnits;
        TreatHintText.Text = $"Ещё {remainingUnits} единиц активности до следующего лакомства.";
    }

    private void RefreshEggs(DateTimeOffset nowUtc)
    {
        var eggs = _viewModel.GetPendingEggEntries(nowUtc);
        EggsEmptyText.Visibility = eggs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var signature = string.Join("|", eggs.Select(e => $"{e.EggId}:{e.IsReady}:{Math.Round(e.HatchFraction, 3)}"));
        if (signature == _eggsSignature) return;
        _eggsSignature = signature;

        PendingEggsList.ItemsSource = eggs.Select(egg => new PendingEggCard
        {
            EggId = egg.EggId,
            TierName = RarityDisplay.GetName(egg.Tier),
            TierBrush = RarityDisplay.GetBrush(egg.Tier),
            HatchFraction = egg.HatchFraction,
            IsReady = egg.IsReady,
            StatusText = egg.IsReady
                ? "Готово — можно вылуплять!"
                : $"Осталось ≈ {RemainingMinutes(egg.HatchFraction)} мин"
        }).ToList();
    }

    private void RefreshRoster(IReadOnlyList<CollectionEntry> entries)
    {
        RosterCountText.Text = $"Всего: {entries.Count}";

        // The sprite's presence is part of the signature so a card that rendered its
        // placeholder before the species was cached rebuilds itself once the sprite lands,
        // instead of staying on the placeholder until some unrelated field changes.
        var signature = string.Join("|", entries.Select(e =>
            $"{e.IndividualId}:{e.DisplaySpeciesId}:{e.IsActive}:{e.IsAvailableToActivate}:{Math.Round(e.EvolutionFraction, 3)}:{TryGetThumbnail(e.DisplaySpeciesId) is not null}"));
        if (signature == _rosterSignature) return;
        _rosterSignature = signature;

        EntriesList.ItemsSource = entries.Select(entry => new CollectionCard
        {
            IndividualId = entry.IndividualId,
            SpeciesName = entry.SpeciesName,
            DexNumber = entry.DexNumberText(),
            MetaLine = $"Характер: {entry.Nature} · поймана {FormatCaughtDate(entry.CaughtAtUtc)}",
            RarityName = RarityDisplay.GetName(entry.Rarity),
            RarityBrush = RarityDisplay.GetBrush(entry.Rarity),
            EvolutionFraction = entry.EvolutionFraction,
            CanSelect = !entry.IsActive && entry.IsAvailableToActivate,
            AvailabilityToolTip = entry.AvailableAgainAtUtc is { } until
                ? $"Питомец обиделся и вернётся {until.ToLocalTime():dd.MM.yyyy HH:mm}"
                : "Питомец временно недоступен",
            SpriteImage = TryGetThumbnail(entry.DisplaySpeciesId),
            ShinyVisibility = entry.IsShiny ? Visibility.Visible : Visibility.Collapsed,
            ActiveBadgeVisibility = entry.IsActive ? Visibility.Visible : Visibility.Collapsed,
            UnavailableBadgeVisibility = entry.IsAvailableToActivate ? Visibility.Collapsed : Visibility.Visible
        }).ToList();
    }

    private void RefreshPokedex()
    {
        var pokedex = _viewModel.GetPokedexEntries();
        PokedexCountText.Text = pokedex.Count == 1
            ? "Открыт 1 вид"
            : $"Открыто видов: {pokedex.Count}";
        PokedexEmptyText.Visibility = pokedex.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var signature = string.Join("|", pokedex.Select(p => $"{p.SpeciesId}:{TryGetThumbnail(p.SpeciesId) is not null}"));
        if (signature == _pokedexSignature) return;
        _pokedexSignature = signature;

        PokedexGrid.ItemsSource = pokedex
            .OrderBy(entry => entry.SpeciesId)
            .Select(entry => new PokedexCard
            {
                SpeciesId = entry.SpeciesId,
                SpeciesName = entry.SpeciesName,
                DexNumber = $"#{entry.SpeciesId:D3}",
                RarityName = RarityDisplay.GetName(entry.Rarity),
                RarityBrush = RarityDisplay.GetBrush(entry.Rarity),
                SpriteImage = TryGetThumbnail(entry.SpeciesId)
            })
            .ToList();
    }

    /// Cache-only sprite lookup: never touches the network, so it's safe on the UI thread.
    /// A species that hasn't been fetched yet simply returns null and the card falls back to
    /// its pokéball placeholder.
    private ImageSource? TryGetThumbnail(int speciesId)
    {
        if (_spriteThumbnails.TryGetValue(speciesId, out var cached)) return cached;

        var bytes = _spriteCache.TryGetCachedBytes(speciesId);
        if (bytes is null || bytes.Length == 0) return null;

        var decoded = DecodePng(bytes);
        _spriteThumbnails[speciesId] = decoded;
        return decoded;
    }

    private static string FormatPercent(double fraction) =>
        $"{Math.Clamp(fraction, 0.0, 1.0) * 100:0}%";

    private static string FormatCaughtDate(DateTimeOffset caughtAtUtc) =>
        caughtAtUtc.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.CurrentCulture);

    private static int RemainingMinutes(double hatchFraction)
    {
        var remaining = EggIncubationService.HatchDuration * (1.0 - Math.Clamp(hatchFraction, 0.0, 1.0));
        return Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
    }

    private static ImageSource DecodePng(byte[] bytes)
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

    // Fully qualified as System.Windows.Controls.Button, not bare Button — Task 5 turned
    // on UseWindowsForms for TrayIconService's NotifyIcon, which makes Button ambiguous
    // with System.Windows.Forms.Button project-wide from that task onward (the same
    // Application-ambiguity issue Task 5 hit, just a different colliding type name).
    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        var id = (Guid)((System.Windows.Controls.Button)sender).Tag;
        if (_viewModel.TrySetActive(id, DateTimeOffset.UtcNow))
        {
            _onStateChanged();
        }
        Refresh();
    }

    private void ReleaseButton_Click(object sender, RoutedEventArgs e)
    {
        var id = (Guid)((System.Windows.Controls.Button)sender).Tag;
        _viewModel.Release(id);
        _onStateChanged();
        Refresh();
    }

    private void HatchButton_Click(object sender, RoutedEventArgs e)
    {
        var eggId = (Guid)((System.Windows.Controls.Button)sender).Tag;
        if (_onHatchRequested(eggId))
        {
            _onStateChanged();
        }
        Refresh();
    }
}

internal static class CollectionEntryFormatting
{
    public static string DexNumberText(this CollectionEntry entry) => $"#{entry.DisplaySpeciesId:D3}";
}
