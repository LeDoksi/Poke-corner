using PokeCorner.App.Imaging;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using PokeCorner.App.Overlay;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.PokeApi;
using PokeCorner.Core.Persistence;

namespace PokeCorner.App.Popup;

public partial class CollectionView : System.Windows.Controls.UserControl
{
    private readonly CollectionViewModel _viewModel;
    private readonly AppState _appState;
    private readonly SpriteCache _spriteCache;
    private readonly Action _onPetRequested;
    private readonly Action _onFeedRequested;
    private readonly Action _onStateChanged;
    private readonly Func<IReadOnlyList<byte[]>?> _getActiveSpriteFrames;

    /// Evolution is player-triggered, so this view needs both "can they?" (button visibility)
    /// and "do it".
    private readonly Func<bool> _onEvolveRequested;
    private readonly Func<bool> _isReadyToEvolve;
    private readonly Func<Guid, EvolutionPreview>? _previewEvolution;
    private readonly Func<Guid, int?, Guid, Task<EvolutionAction>>? _evolveIndividual;
    private readonly Action<MenuSection>? _navigate;
    private readonly Action<string, int>? _buyBerries;
    private DispatcherTimer? _liveRefreshTimer;

    /// Same idle cadence the desktop overlay animates at, so the hero sprite and the pet on
    /// the desktop visibly move in step rather than at two different speeds.
    private static readonly TimeSpan HeroFrameDuration = TimeSpan.FromMilliseconds(150);

    private SpriteAnimator? _heroAnimator;

    /// Identity of the frame set driving the hero image. Refresh() runs once a second; without
    /// this guard the animator would restart every tick and the sprite would sit on frame 0.
    /// Content-derived (individual + frame count + first frame size) so it stays stable even
    /// when the same frames arrive as a different list instance.
    private string _heroSpriteSignature = string.Empty;

    /// null = "Все". Which rarity the Pokédex grid is filtered to.
    private RarityTier? _rarityFilter;

    /// The search box's current text. ANDs with the rarity filter above.
    private string _searchQuery = string.Empty;

    /// "Не завершившие эволюцию" pill — ANDs with rarity and search, same as they AND with
    /// each other.
    private bool _hideFullyEvolved;

    /// Current Pokédex sort choice — see CollectionViewModel.ApplySort.
    private PokedexSortOrder _sortOrder = PokedexSortOrder.Number;

    /// Zero-based page of the filtered grid. Deliberately view STATE, not derived: Refresh()
    /// runs once a second, so recomputing it from scratch every tick would drop the player back
    /// on page 1 while they were still reading page 3. Only a filter or search change resets it
    /// (and RefreshPokedex clamps it when a release shrinks the list past the current page).
    private int _pageIndex;

    /// Keep the owned-individual list bounded while leaving room for the inline details.
    private const int PageSize = 16;

    /// Animated-GIF frames for one species, or null when it has none. Optional so the
    /// theme/layout tests can construct this view without an App behind them; a null delegate
    /// simply leaves every card on its static frame.
    private readonly Func<int, Task<IReadOnlyList<byte[]>?>>? _getAnimatedFrames;

    /// Decoded animated frames per species id, and the set of species a fetch has already
    /// been kicked off for. Both are needed: Refresh() runs once a second over every visible
    /// card, so without the "already started" set the grid would re-request the same GIFs
    /// every tick, and without the decoded memo it would re-decode them every rebuild.
    private readonly Dictionary<int, IReadOnlyList<ImageSource>> _animatedFrames = new();
    private readonly HashSet<int> _animatedFetchStarted = new();
    private readonly Func<int, Task<byte[]?>>? _fetchSprite;
    private readonly HashSet<int> _thumbnailFetchStarted = new();
    private readonly Func<int, string?>? _getSpeciesAssetPath;
    private readonly Func<Action<SaveData>, Task>? _commitState;
    private readonly Dictionary<int, ImageSource> _packageSprites = new();
    private readonly HashSet<int> _missingPackageSprites = new();
    private int _speciesPageIndex;
    private const int SpeciesPageSize = 12;
    private int? _selectedSpeciesId;
    private string _speciesSignature = string.Empty;
    private string _speciesTypeSignature = string.Empty;
    private bool _updatingSpeciesTypes;

    /// One SpriteAnimator per realized card Image. A DataTemplate gives no named element to
    /// hand the animator at construction time the way the hero Image does, so each Image
    /// registers itself on Loaded and is torn down on Unloaded (WrapPanel realizes eagerly,
    /// but ItemsSource is still replaced wholesale on every rebuild).
    private readonly Dictionary<System.Windows.Controls.Image, SpriteAnimator> _cardAnimators = new();

    /// The filter RadioButton declared IsChecked="True" in XAML raises Checked during
    /// InitializeComponent — before the readonly fields below are assigned. This flag keeps
    /// that startup event from calling Refresh() against a half-built view.
    private bool _isReady;

    // Decoded thumbnails are reused across refreshes: Refresh() runs once a second, and
    // re-decoding a PNG per visible card per tick would be pure waste. Only successful
    // decodes are memoized, so a species whose sprite is fetched later in the session
    // still picks it up on the next rebuild instead of being stuck on the placeholder.
    private readonly Dictionary<int, ImageSource> _spriteThumbnails = new();

    // The list rebuilds below replace ItemsSource wholesale, which tears down and recreates
    // every item container (each carrying a drop shadow). Doing that 60 times a minute for
    // data that rarely changes made the cards visibly flicker, so each list only rebuilds
    // when a cheap signature of its content actually changes.
    private string _pokedexSignature = string.Empty;

    public CollectionView(CollectionViewModel viewModel, AppState appState, SpriteCache spriteCache, Action onPetRequested, Action onFeedRequested, Action onStateChanged, Func<IReadOnlyList<byte[]>?> getActiveSpriteFrames, Func<bool> onEvolveRequested, Func<bool> isReadyToEvolve, Func<int, Task<IReadOnlyList<byte[]>?>>? getAnimatedFrames = null, Func<int, Task<byte[]?>>? fetchSprite = null,
        Func<int, string?>? getSpeciesAssetPath = null, Func<Action<SaveData>, Task>? commitState = null,
        Func<Guid, EvolutionPreview>? previewEvolution = null,
        Func<Guid, int?, Guid, Task<EvolutionAction>>? evolveIndividual = null,
        Action<MenuSection>? navigate = null, Action<string, int>? buyBerries = null)
    {
        InitializeComponent();
        _onEvolveRequested = onEvolveRequested;
        _isReadyToEvolve = isReadyToEvolve;
        _viewModel = viewModel;
        _appState = appState;
        _spriteCache = spriteCache;
        _onPetRequested = onPetRequested;
        _onFeedRequested = onFeedRequested;
        _onStateChanged = onStateChanged;
        _getActiveSpriteFrames = getActiveSpriteFrames;
        _getAnimatedFrames = getAnimatedFrames;
        _fetchSprite = fetchSprite;
        _getSpeciesAssetPath = getSpeciesAssetPath;
        _commitState = commitState;
        _previewEvolution = previewEvolution;
        _evolveIndividual = evolveIndividual;
        _navigate = navigate;
        _buyBerries = buyBerries;

        // Filter labels come from the same helper the rarity tags on the cards use, so a
        // rename there can never leave the filter pills saying something different.
        RarityFilterCommon.Content = RarityDisplay.GetName(RarityTier.Common);
        RarityFilterUncommon.Content = RarityDisplay.GetName(RarityTier.Uncommon);
        RarityFilterRare.Content = RarityDisplay.GetName(RarityTier.Rare);
        RarityFilterLegendary.Content = RarityDisplay.GetName(RarityTier.Legendary);

        Loaded += (_, _) => StartLiveRefresh();
        Unloaded += (_, _) => StopLiveRefresh();
        _isReady = true;
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

        // The hero animator runs on its own DispatcherTimer, which would otherwise keep
        // ticking (and holding every decoded frame alive) after the popup is gone. Clearing
        // the signature makes the next Load rebuild it instead of assuming it's still running.
        _heroAnimator?.Stop();
        _heroAnimator = null;
        _heroSpriteSignature = string.Empty;

        // Same reasoning for the per-card animators — one DispatcherTimer each, and there is
        // one per visible Pokédex card.
        foreach (var animator in _cardAnimators.Values) animator.Stop();
        _cardAnimators.Clear();

        // And the detail overlay's.
        _detailAnimator?.Stop();
        _detailAnimator = null;
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
        RefreshPokedex(entries);
        if (SpeciesPane.Visibility == Visibility.Visible) RefreshSpecies();
        if (_detailCard is not null && DetailOverlay.Visibility == Visibility.Visible) RefreshDetailEvolution();
    }

    private void IndividualsMode_Click(object sender, RoutedEventArgs e)
    {
        IndividualsPane.Visibility = Visibility.Visible;
        SpeciesPane.Visibility = Visibility.Collapsed;
        IndividualsModeButton.Style = null;
        SpeciesModeButton.Style = (Style)FindResource("SecondaryButtonStyle");
    }

    private void SpeciesMode_Click(object sender, RoutedEventArgs e)
    {
        IndividualsPane.Visibility = Visibility.Collapsed;
        SpeciesPane.Visibility = Visibility.Visible;
        SpeciesModeButton.Style = null;
        IndividualsModeButton.Style = (Style)FindResource("SecondaryButtonStyle");
        RefreshSpecies();
    }

    private void SpeciesFilters_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isReady || _updatingSpeciesTypes) return;
        _speciesPageIndex = 0;
        RefreshSpecies();
    }

    private void SpeciesFilters_Changed(object sender, SelectionChangedEventArgs e) => SpeciesFilters_Changed(sender, (RoutedEventArgs)e);
    private void SpeciesFilters_Changed(object sender, TextChangedEventArgs e) => SpeciesFilters_Changed(sender, (RoutedEventArgs)e);

    private void SpeciesPrev_Click(object sender, RoutedEventArgs e)
    {
        _speciesPageIndex = Math.Max(0, _speciesPageIndex - 1);
        RefreshSpecies();
    }

    private void SpeciesNext_Click(object sender, RoutedEventArgs e)
    {
        _speciesPageIndex++;
        RefreshSpecies();
    }

    private void RefreshSpecies()
    {
        if (!_isReady) return;
        var all = _viewModel.GetSpeciesEntries();
        var types = all.Where(e => e.IsDiscovered).SelectMany(e => e.Types).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(typeName => typeName, StringComparer.OrdinalIgnoreCase).ToArray();
        var typeSignature = string.Join("|", types);
        if (typeSignature != _speciesTypeSignature)
        {
            _updatingSpeciesTypes = true;
            var selectedType = (SpeciesTypeCombo.SelectedItem as ComboBoxItem)?.Tag as string;
            SpeciesTypeCombo.Items.Clear();
            SpeciesTypeCombo.Items.Add(new ComboBoxItem { Content = "Тип: любой" });
            foreach (var name in types) SpeciesTypeCombo.Items.Add(new ComboBoxItem { Content = $"Тип: {TypeDisplay.GetLabel(name)}", Tag = name });
            SpeciesTypeCombo.SelectedIndex = Math.Max(0, Array.IndexOf(types, selectedType) + 1);
            _speciesTypeSignature = typeSignature;
            _updatingSpeciesTypes = false;
        }
        var type = (SpeciesTypeCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        var rarity = (SpeciesRarityCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        var sort = SpeciesSortCombo.SelectedIndex switch { 1 => SpeciesSortOrder.CatchDate, 2 => SpeciesSortOrder.Type, _ => SpeciesSortOrder.Number };
        var visible = CollectionViewModel.FilterSpecies(all, SpeciesSearchBox.Text, type, rarity,
            SpeciesCanEvolve.IsChecked == true, sort);
        var pageCount = Math.Max(1, (int)Math.Ceiling(visible.Count / (double)SpeciesPageSize));
        _speciesPageIndex = Math.Clamp(_speciesPageIndex, 0, pageCount - 1);
        SpeciesCountText.Text = $"Открыто видов: {all.Count(e => e.IsDiscovered)} · показано: {visible.Count}";
        SpeciesPageText.Text = $"{_speciesPageIndex + 1} / {pageCount}";
        SpeciesPrevButton.IsEnabled = _speciesPageIndex > 0;
        SpeciesNextButton.IsEnabled = _speciesPageIndex < pageCount - 1;
        SpeciesPagination.Visibility = pageCount > 1 ? Visibility.Visible : Visibility.Collapsed;
        var page = visible.Skip(_speciesPageIndex * SpeciesPageSize).Take(SpeciesPageSize).ToList();
        var signature = $"{_speciesPageIndex}|{type}|{rarity}|{SpeciesSearchBox.Text}|{sort}|{SpeciesCanEvolve.IsChecked}|" +
            string.Join("|", page.Select(e => $"{e.SpeciesId}:{e.IsDiscovered}:{string.Join(',', e.OwnedIndividuals.Select(i => $"{i.IndividualId}:{i.Nickname}:{i.IsBusy}:{i.Level}"))}"));
        if (signature != _speciesSignature)
        {
            _speciesSignature = signature;
            SpeciesGrid.ItemsSource = page.Select(e => new SpeciesCard(e, GetPackageSprite(e.SpeciesId))).ToList();
        }
        if (_selectedSpeciesId is { } id) ShowSpeciesDetail(all.FirstOrDefault(e => e.SpeciesId == id));
    }

    private ImageSource? GetPackageSprite(int speciesId)
    {
        if (_packageSprites.TryGetValue(speciesId, out var cached)) return cached;
        if (_missingPackageSprites.Contains(speciesId)) return null;
        var path = _getSpeciesAssetPath?.Invoke(speciesId);
        if (path is null) return null;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            _packageSprites[speciesId] = bitmap;
            return bitmap;
        }
        catch (Exception error) when (error is IOException or NotSupportedException or System.Runtime.InteropServices.COMException)
        {
            _missingPackageSprites.Add(speciesId);
            return null;
        }
    }

    private void SpeciesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SpeciesGrid.SelectedItem is not SpeciesCard card) return;
        _selectedSpeciesId = card.Entry.SpeciesId;
        ShowSpeciesDetail(card.Entry);
    }

    private void ShowSpeciesDetail(PokedexSpeciesEntry? entry)
    {
        SpeciesDetail.Visibility = entry is null ? Visibility.Collapsed : Visibility.Visible;
        if (entry is null) return;
        SpeciesDetailName.Text = entry.IsDiscovered ? $"{entry.Number} · {entry.DisplayName}" : entry.Number;
        SpeciesDetailDescription.Text = entry.IsDiscovered ? entry.DescriptionRu : $"Подсказка: {entry.RouteHint}";
        SpeciesDetailTypes.Text = entry.IsDiscovered ? string.Join(" · ", entry.Types.Select(TypeDisplay.GetLabel)) : string.Empty;
        SpeciesDetailOwnedLabel.Visibility = entry.OwnedIndividuals.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SpeciesDetailOwnedLabel.Text = entry.OwnedIndividuals.Count == 1 ? "Мой экземпляр" : "Мои экземпляры";
        SpeciesOwnedList.ItemsSource = entry.OwnedIndividuals.Select(i => new IndividualChoice(i.IndividualId,
            $"{(i.Nickname ?? entry.DisplayName)} · ур. {i.Level} · {(i.IsBusy ? "Занят" : i.IsActive ? "На столе" : "Свободен")} · {FormatCaughtDate(i.CaughtAtUtc)}{(i.IsShiny ? " · Shiny" : "")}"))
            .ToList();
    }

    private void SpeciesOwned_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: Guid id }) return;
        OpenIndividual(id);
    }

    public Guid? SelectedIndividualId => _detailCard?.IndividualId;

    public bool OpenIndividual(Guid id)
    {
        var entry = _viewModel.GetEntry(id, DateTimeOffset.UtcNow);
        if (entry is null) return false;
        ShowDetail(BuildCard(entry));
        return true;
    }

    private sealed record IndividualChoice(Guid IndividualId, string Label);
    private sealed record SpeciesCard(PokedexSpeciesEntry Entry, ImageSource? SpriteImage)
    {
        public string Number => Entry.Number;
        public string AccessibleName => Entry.AccessibleName;
        public string NameText => Entry.DisplayName ?? "Неизвестный вид";
        public string SubText => Entry.IsDiscovered
            ? $"{string.Join(" · ", Entry.Types.Select(TypeDisplay.GetLabel))} · {RarityName} · {Entry.OwnedIndividuals.Count} моих"
            : Entry.HasSilhouette ? Entry.RouteHint : "Этот вид ещё никто не встречал";
        private string RarityName => Enum.TryParse<RarityTier>(Entry.Rarity, true, out var tier)
            ? RarityDisplay.GetName(tier) : Entry.Rarity ?? string.Empty;
        public Visibility RevealedVisibility => Entry.IsDiscovered && SpriteImage is not null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility SilhouetteVisibility => !Entry.IsDiscovered && Entry.HasSilhouette && SpriteImage is not null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility UnknownVisibility => SpriteImage is null || !Entry.IsDiscovered && !Entry.HasSilhouette ? Visibility.Visible : Visibility.Collapsed;
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
            ActiveTypeBadges.ItemsSource = null;
        }
        else
        {
            // Nickname as the headline, species name folded into the meta line beneath it, so
            // BOTH stay visible — the hero card never shows a nickname alone.
            var hasNickname = !string.IsNullOrWhiteSpace(activeEntry.Nickname);
            ActivePetNameText.Text = hasNickname ? activeEntry.Nickname! : activeEntry.SpeciesName;
            var speciesPrefix = hasNickname ? $"{activeEntry.SpeciesName} · " : string.Empty;
            ActivePetMetaText.Text =
                $"{speciesPrefix}{activeEntry.DexNumberText()} · {RarityDisplay.GetName(activeEntry.Rarity)} · характер {activeEntry.Nature} · пойман {FormatCaughtDate(activeEntry.CaughtAtUtc)}";
            CarePointsText.Text = active.AccumulatedCarePoints.ToString("N0", CultureInfo.CurrentCulture);
            ShinyBadge.Visibility = activeEntry.IsShiny ? Visibility.Visible : Visibility.Collapsed;
            ActiveTypeBadges.ItemsSource = TypeDisplay.ToBadges(activeEntry.Types);
        }

        TreatsText.Text = _appState.Feeding.Treats.ToString(CultureInfo.CurrentCulture);

        RefreshHeroSprite(active);
    }

    /// Drives the hero image from the SAME frame list the desktop overlay animates, with the
    /// overlay's FrameSequencer/SpriteAnimator pair.
    private void RefreshHeroSprite(PokemonIndividual? active)
    {
        var frames = _getActiveSpriteFrames();
        var hasFrames = frames is not null && frames.Count > 0;

        var signature = hasFrames ? $"{active?.Id}:{frames!.Count}:{frames[0].Length}" : string.Empty;
        if (signature == _heroSpriteSignature) return; // already showing (and animating) this sprite
        _heroSpriteSignature = signature;

        _heroAnimator?.Stop();
        _heroAnimator = null;

        ActiveSpritePlaceholder.Visibility = hasFrames ? Visibility.Collapsed : Visibility.Visible;

        if (!hasFrames)
        {
            ActiveSpriteImage.Source = null;
            return;
        }

        var decoded = frames!.Select(SpriteImages.Decode).ToList();

        // Assigned up front so the image is never blank for the animator's first tick, and so
        // a single-frame (no animated sprite for this species) list needs no timer at all.
        ActiveSpriteImage.Source = decoded[0];
        if (decoded.Count == 1) return;

        _heroAnimator = new SpriteAnimator(ActiveSpriteImage, new FrameSequencer(decoded.Count, HeroFrameDuration), decoded);
        _heroAnimator.Start();
    }

    private void RefreshProgress(PokemonIndividual? active)
    {
        var progress = ProgressCalculator.Compute(_appState.Activity, active, _appState.Feeding);
        CatchProgressBar.Value = progress.CatchFraction;
        TreatProgressBar.Value = progress.TreatFraction;

        CatchPercentText.Text = FormatPercent(progress.CatchFraction);
        TreatPercentText.Text = FormatPercent(progress.TreatFraction);

        // Evolution is the one bar with three possible readings rather than a percentage:
        // a real 0-99%, "this species never evolves" (empty + dimmed), or "final form reached"
        // (legitimately full). All three come from the shared display helper so this card, the
        // detail overlay and the desktop gauge always word them identically.
        EvolutionProgressBar.Value = EvolutionProgressDisplay.BarValue(progress.EvolutionFraction, progress.EvolutionState);
        EvolutionProgressBar.Opacity = EvolutionProgressDisplay.BarOpacity(progress.EvolutionState);
        EvolutionProgressBar.ToolTip = EvolutionProgressDisplay.ToolTip(progress.EvolutionState);
        EvolutionPercentText.Text = EvolutionProgressDisplay.Label(progress.EvolutionFraction, progress.EvolutionState);
        EvolutionPercentText.Foreground = progress.EvolutionState == EvolutionProgressState.InProgress
            ? ThemeBrushes.Get("SecondaryTextBrush")
            : ThemeBrushes.Get("MutedTextBrush");

        var remainingUnits = FeedingService.ActivityUnitsPerTreatEarned - _appState.Feeding.RemainderUnits;
        TreatHintText.Text = $"Ещё {remainingUnits} единиц активности до следующего лакомства.";

        // Appears only once the bar is actually full — a permanently-visible disabled button
        // would read as broken on a card the player looks at constantly.
        EvolveButton.Visibility = _isReadyToEvolve() ? Visibility.Visible : Visibility.Collapsed;
    }

    /// Deliberately does NOT call _onStateChanged: unlike Pet/Feed/Select, the evolve callback
    /// already refreshes the overlay itself (it has to, to swap in the new stage's sprite), so
    /// calling it again here would fire a second redundant sprite fetch per click.
    private void EvolveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_previewEvolution is not null && _appState.Collection.ActiveIndividualId is { } id)
            OpenIndividual(id);
        else _onEvolveRequested();
        Refresh();
    }

    /// One of the rarity filter pills was selected. RadioButton's GroupName already
    /// deselected the previous one, so this only has to record the new tier and force the
    /// grid to rebuild — going through Refresh() rather than touching ItemsSource directly
    /// means the filter also survives the once-a-second refresh tick.
    private void RarityFilter_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isReady) return; // fires once during InitializeComponent for the default pill

        _rarityFilter =
            ReferenceEquals(sender, RarityFilterCommon) ? RarityTier.Common :
            ReferenceEquals(sender, RarityFilterUncommon) ? RarityTier.Uncommon :
            ReferenceEquals(sender, RarityFilterRare) ? RarityTier.Rare :
            ReferenceEquals(sender, RarityFilterLegendary) ? RarityTier.Legendary :
            null;

        _pageIndex = 0; // a narrower list makes the old page number meaningless
        Refresh();
    }

    /// Search is applied on top of the rarity pills (AND, not OR) and matches the species name
    /// OR the player's nickname — see CollectionViewModel.MatchesSearch. Like the pills, it
    /// goes through Refresh() so it survives the once-a-second tick.
    private void PokedexSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isReady) return;

        _searchQuery = PokedexSearchBox.Text;
        _pageIndex = 0;
        Refresh();
    }

    /// The "Не завершившие эволюцию" pill. Independent CheckBox rather than a RadioButton group
    /// (see FilterPillCheckBoxStyle), so it fires on both Checked and Unchecked.
    private void EvolutionFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isReady) return;

        _hideFullyEvolved = EvolutionFilterToggle.IsChecked == true;
        _pageIndex = 0;
        Refresh();
    }

    private void IndividualExtraFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isReady) return;
        _pageIndex = 0;
        Refresh();
    }

    private void PokedexSortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isReady) return;

        _sortOrder = PokedexSortCombo.SelectedIndex switch
        {
            1 => PokedexSortOrder.Type,
            2 => PokedexSortOrder.CatchDate,
            _ => PokedexSortOrder.Number
        };
        _pageIndex = 0;
        Refresh();
    }

    private void PokedexPrevPage_Click(object sender, RoutedEventArgs e)
    {
        if (_pageIndex == 0) return;
        _pageIndex--;
        Refresh();
    }

    /// The upper bound is enforced in RefreshPokedex (which clamps against the live page count)
    /// rather than duplicated here, so there is one place that knows how long the list is.
    private void PokedexNextPage_Click(object sender, RoutedEventArgs e)
    {
        _pageIndex++;
        Refresh();
    }

    /// One card per individual (two owned Pikachus are two cards), in the selected order.
    private void RefreshPokedex(IReadOnlyList<CollectionEntry> entries)
    {
        // Distinct SPECIES even though the cards are per-individual: this line reports Pokédex
        // completion, which a second Pikachu does not advance; the owned total sits alongside it.
        var speciesCount = entries.Select(e => e.DisplaySpeciesId).Distinct().Count();
        PokedexCountText.Text = speciesCount == 1
            ? $"Открыт 1 вид · всего покемонов: {entries.Count}"
            : $"Открыто видов: {speciesCount} · всего покемонов: {entries.Count}";

        // Filters narrow the owned list together before number, type, or catch-date sorting.
        var visible = CollectionViewModel.ApplySort(entries
            .Where(entry => _rarityFilter is null || entry.Rarity == _rarityFilter.Value)
            .Where(entry => !_hideFullyEvolved || entry.HasEvolutionsRemaining)
             .Where(entry => FavoritesFilterToggle.IsChecked != true || entry.IsFavorite)
             .Where(entry => ShinyFilterToggle.IsChecked != true || entry.IsShiny)
            .Where(entry => CollectionViewModel.MatchesSearch(entry, _searchQuery))
            .ToList(), _sortOrder);

        // Clamped rather than reset: releasing a Pokémon (or narrowing a filter) can shrink the
        // list past the page being read, and dumping the player back to page 1 for that would
        // be as jarring as doing it on every refresh tick.
        var pageCount = Math.Max(1, (int)Math.Ceiling(visible.Count / (double)PageSize));
        _pageIndex = Math.Clamp(_pageIndex, 0, pageCount - 1);
        var page = visible.Skip(_pageIndex * PageSize).Take(PageSize).ToList();

        PokedexPagination.Visibility = pageCount > 1 ? Visibility.Visible : Visibility.Collapsed;
        PokedexPageText.Text = $"Страница {_pageIndex + 1} из {pageCount}";
        PokedexPrevPageButton.IsEnabled = _pageIndex > 0;
        PokedexNextPageButton.IsEnabled = _pageIndex < pageCount - 1;

        PokedexEmptyText.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PokedexEmptyText.Text = entries.Count == 0
            ? "Пока пусто — поймай первого покемона на вкладке «Поймать»."
            : !string.IsNullOrWhiteSpace(_searchQuery)
                ? "Ничего не найдено — проверь имя вида или прозвище."
                : "В этой редкости пока нет открытых видов — попробуй другой фильтр.";

        // Kick off the animated-sprite fetches for whatever is on screen. Cheap after the
        // first pass: EnsureAnimatedFrames is a no-op for any species already requested.
        foreach (var entry in page) EnsureAnimatedFrames(entry.DisplaySpeciesId);

        // The sprite's presence is part of the signature — both the static thumbnail and the
        // animated frames — so a card that rendered its placeholder before the species was
        // cached rebuilds itself once the artwork lands, instead of staying on the placeholder
        // until some unrelated field changes. The nickname is in there for the same reason:
        // renaming from the detail overlay must repaint the card behind it.
        var signature = $"{_rarityFilter}|{_searchQuery}|{_hideFullyEvolved}|{_sortOrder}|{_pageIndex}|" + string.Join("|", page.Select(e =>
            $"{e.IndividualId}:{e.DisplaySpeciesId}:{e.Nickname}:{e.IsActive}:{e.IsFavorite}:{e.IsBusy}:{e.Level}:{e.FirstTraitId}:{e.SecondTraitId}:{e.DescriptionRu}:{e.EvolutionState}:{Math.Round(e.EvolutionFraction ?? -1, 3)}:{TryGetThumbnail(e.DisplaySpeciesId) is not null}:{_animatedFrames.ContainsKey(e.DisplaySpeciesId)}"));
        if (signature == _pokedexSignature) return;
        _pokedexSignature = signature;

        PokedexGrid.ItemsSource = page.Select(BuildCard).ToList();
    }

    private PokedexCard BuildCard(CollectionEntry entry) => new()
    {
        IndividualId = entry.IndividualId,
        Level = entry.Level,
        IsBusy = entry.IsBusy,
        FirstTraitId = entry.FirstTraitId,
        SecondTraitId = entry.SecondTraitId,
        DescriptionRu = entry.DescriptionRu,
        SpeciesId = entry.DisplaySpeciesId,
        SpeciesName = entry.SpeciesName,
        Nickname = entry.Nickname,
        DexNumber = entry.DexNumberText(),
        RarityName = RarityDisplay.GetName(entry.Rarity),
        RarityBrush = RarityDisplay.GetBrush(entry.Rarity),
        EvolutionFraction = entry.EvolutionFraction,
        EvolutionState = entry.EvolutionState,
        CaughtAtText = FormatCaughtDate(entry.CaughtAtUtc),
        NatureText = entry.Nature.ToString(),
        CanSelect = !entry.IsActive,
        IsShiny = entry.IsShiny,
        IsActive = entry.IsActive,
        IsFavorite = entry.IsFavorite,
        TypeBadges = TypeDisplay.ToBadges(entry.Types),
        EvolutionStages = BuildEvolutionStageCards(entry),
        SpriteImage = TryGetThumbnail(entry.DisplaySpeciesId),
        SpriteFrames = _animatedFrames.GetValueOrDefault(entry.DisplaySpeciesId)
    };

    /// Turns the view-model's spoiler-masked chain into renderable cards. The sprite lookup is
    /// guarded by IsRevealed so an unreached stage's artwork is never even fetched from the
    /// cache — the mask is enforced here as well as in the view model, not just in the template.
    private IReadOnlyList<EvolutionStageCard> BuildEvolutionStageCards(CollectionEntry entry) =>
        entry.EvolutionStages.Select(stage => new EvolutionStageCard
        {
            StageIndex = stage.StageIndex,
            IsRevealed = stage.IsRevealed,
            NameText = stage.IsRevealed ? stage.SpeciesName ?? "???" : "???",
            SpriteImage = stage.IsRevealed && stage.SpeciesId is { } id ? TryGetThumbnail(id) : null,
            TypeBadges = TypeDisplay.ToBadges(stage.Types),
            IsCurrent = stage.IsCurrent,
            IsDisplayed = stage.IsDisplayed,
            ShowArrow = stage.StageIndex < entry.EvolutionStages.Count - 1
        }).ToList();

    /// Fires the one-per-species animated-sprite fetch, at most once per species per session.
    /// Fire-and-forget on purpose: the cards already show their static frame, so there is
    /// nothing to wait for — when frames land, clearing the signature makes the next refresh
    /// tick rebuild the grid and pick them up.
    private void EnsureAnimatedFrames(int speciesId)
    {
        if (_getAnimatedFrames is null) return;
        if (!_animatedFetchStarted.Add(speciesId)) return;

        _ = LoadAsync();

        async Task LoadAsync()
        {
            IReadOnlyList<byte[]>? frames;
            try
            {
                frames = await _getAnimatedFrames(speciesId);
            }
            catch
            {
                // The App-side delegate already swallows fetch/decode failures; this is the
                // same defense in depth the overlay path keeps, so one bad species can never
                // reach DispatcherUnhandledException from a fire-and-forget continuation.
                return;
            }

            // Fewer than two frames means nothing to cycle — leave the static image alone
            // rather than spinning up a timer that repaints the same bitmap.
            if (frames is null || frames.Count < 2) return;

            // Off the UI thread: SpriteImages.Decode freezes each bitmap, which is what makes it legal
            // to hand them back here.
            _animatedFrames[speciesId] = await Task.Run(() => frames.Select(SpriteImages.Decode).ToList());
            _pokedexSignature = string.Empty; // force the next tick to rebuild with the frames
        }
    }

    /// Attaches an animator to one realized card Image. Mirrors the hero sprite's setup, just
    /// resolved per container: the frames come from the card the Image is bound to.
    private void PokedexSprite_Loaded(object sender, RoutedEventArgs e)
    {
        var image = (System.Windows.Controls.Image)sender;
        StopCardAnimator(image);

        if (image.DataContext is not PokedexCard card) return;
        var frames = card.SpriteFrames;
        if (frames is null || frames.Count < 2) return;

        image.Source = frames[0];
        var animator = new SpriteAnimator(image, new FrameSequencer(frames.Count, HeroFrameDuration), frames);
        animator.Start();
        _cardAnimators[image] = animator;
    }

    private void PokedexSprite_Unloaded(object sender, RoutedEventArgs e) =>
        StopCardAnimator((System.Windows.Controls.Image)sender);

    private void StopCardAnimator(System.Windows.Controls.Image image)
    {
        if (!_cardAnimators.Remove(image, out var animator)) return;
        animator.Stop();
    }

    /// A Pokédex card was clicked: show the detail/actions overlay for that individual.
    ///
    /// Bound to MouseLeftButtonDOWN, paired with the backdrop's dismiss below: on Up, one
    /// physical click on the backdrop dismissed the overlay on its down-stroke and then opened
    /// whatever card lay under the pointer on its up-stroke.
    private void PokedexCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not PokedexCard card) return;
        ShowDetail(card);
    }

    private void PokedexCard_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is not (System.Windows.Input.Key.Enter or System.Windows.Input.Key.Space) ||
            ((FrameworkElement)sender).DataContext is not PokedexCard card) return;
        ShowDetail(card);
        e.Handled = true;
    }

    private void PokedexCard_GotKeyboardFocus(object sender, RoutedEventArgs e) =>
        ((Border)sender).BorderBrush = ThemeBrushes.Get("AccentGoldBrush");

    private void PokedexCard_LostKeyboardFocus(object sender, RoutedEventArgs e) =>
        ((Border)sender).ClearValue(Border.BorderBrushProperty);

    private void CollectionView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape || DetailOverlay.Visibility != Visibility.Visible) return;
        HideDetail();
        e.Handled = true;
    }

    // ======================= Pokédex detail overlay =======================
    // Renders the PokedexCard it is handed and routes every mutation through the same
    // SelectIndividual/ReleaseIndividual/ToggleFavorite methods the rest of this view uses.

    /// The card currently on show, or null when the overlay is hidden.
    private PokedexCard? _detailCard;
    private int _detailGeneration;
    private string _evolutionChoicesSignature = string.Empty;
    private EvolutionPreview? _detailEvolutionPreview;
    private readonly HashSet<Guid> _evolvingIndividuals = [];

    private bool IsCurrentDetail(Guid id, int generation) =>
        _detailGeneration == generation && DetailOverlay.Visibility == Visibility.Visible &&
        _detailCard?.IndividualId == id;

    /// Local mirror of the pin so the star can flip immediately on click. The grid rebuilds
    /// from the model, but the overlay holds an immutable snapshot card — re-reading it would
    /// show the stale value.
    private bool _detailIsFavorite;

    private SpriteAnimator? _detailAnimator;

    /// Guards the nickname TextBox's TextChanged while ShowDetail populates it — see there.
    private bool _isLoadingDetailNickname;

    private void ShowDetail(PokedexCard card)
    {
        _detailGeneration++;
        _detailCard = card;
        DetailErrorText.Text = string.Empty;
        _detailIsFavorite = card.IsFavorite;
        DetailFavoriteButton.IsEnabled = true;
        DetailReleaseButton.IsEnabled = !_appState.BusyIndividualIds.Contains(card.IndividualId);

        DetailDexNumberText.Text = card.DexNumber;
        DetailSpeciesNameText.Text = card.DisplayName;
        DetailSpeciesSubtitleText.Text = card.SpeciesSubtitle;
        DetailSpeciesSubtitleText.Visibility = card.SpeciesSubtitleVisibility;

        // Suppressed so filling the box from the model doesn't read as the player typing —
        // that would write the nickname straight back and, worse, do it on every card open.
        _isLoadingDetailNickname = true;
        DetailNicknameBox.Text = card.Nickname ?? string.Empty;
        _isLoadingDetailNickname = false;

        DetailNatureText.Text = card.NatureText;
        DetailCaughtAtText.Text = card.CaughtAtText;
        DetailLevelText.Text = card.LevelText;
        DetailBusyText.Text = card.AvailabilityText;
        DetailTraitsText.Text = "Особенности: " + string.Join(" · ",
            new[] { card.FirstTraitId, card.SecondTraitId }.Where(id => !string.IsNullOrWhiteSpace(id)).Select(TraitLabel));
        DetailTraitsText.Visibility = card.FirstTraitId is null && card.SecondTraitId is null ? Visibility.Collapsed : Visibility.Visible;
        DetailDescriptionText.Text = card.DescriptionRu ?? "Описание вида пока недоступно.";
        DetailEvolutionBar.Value = card.EvolutionBarValue;
        DetailEvolutionBar.Opacity = card.EvolutionBarOpacity;
        DetailEvolutionBar.ToolTip = card.EvolutionToolTip;
        DetailEvolutionText.Text = card.EvolutionLabel;
        DetailEvolutionText.Foreground = card.EvolutionState == EvolutionProgressState.InProgress
            ? ThemeBrushes.Get("SecondaryTextBrush")
            : ThemeBrushes.Get("MutedTextBrush");

        DetailRarityPill.BorderBrush = card.RarityBrush;
        DetailRarityDot.Fill = card.RarityBrush;
        DetailRarityText.Text = card.RarityName;
        DetailRarityText.Foreground = card.RarityBrush;

        DetailTypeBadges.ItemsSource = card.TypeBadges;
        DetailTypeBadges.Visibility = card.TypeBadgesVisibility;

        // The evolution-chain strip. Collapsed outright for a single-stage species — a lone
        // node with no arrows would read as a broken chain rather than as "no chain".
        DetailEvolutionChainSection.Visibility = card.EvolutionStages.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        DetailEvolutionChainStrip.ItemsSource = card.EvolutionStages;
        _evolutionChoicesSignature = string.Empty;
        RefreshDetailEvolution();

        DetailShinyBadge.Visibility = card.ShinyVisibility;
        DetailActiveBadge.Visibility = card.ActiveBadgeVisibility;

        // An already-active pet can't be selected again.
        DetailSelectButton.IsEnabled = card.CanSelect;
        DetailSelectButton.ToolTip = card.CanSelect ? null : "Этот покемон уже на рабочем столе";

        ApplyDetailFavoriteVisuals();
        ShowDetailSprite(card);

        DetailOverlay.Visibility = Visibility.Visible;
    }

    private void RefreshDetailEvolution()
    {
        if (_detailCard is null || _previewEvolution is null || _evolveIndividual is null) return;
        if (!_appState.Collection.Individuals.Any(x => x.Id == _detailCard.IndividualId))
        {
            DetailEvolutionActionSection.Visibility = Visibility.Collapsed;
            return;
        }
        var preview = _previewEvolution(_detailCard.IndividualId);
        _detailEvolutionPreview = preview;
        DetailEvolutionActionSection.Visibility = preview.Branches.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (preview.Branches.Count == 0) return;
        DetailEvolutionCostText.Text = $"Уход: {preview.CareOwned:N0} / {preview.CareRequired:N0} · Ягоды {preview.BerryId}: {preview.BerriesOwned} / {preview.BerryCost} · Камни: {preview.StonesOwned}";
        DetailEvolutionBlockedText.Text = preview.Branches.Any(x => !x.Available)
            ? "Недоступные ветви отмечены в списке; вариант без ресурса не подменяется."
            : string.Empty;
        UpdateEvolutionActionState();
        var signature = string.Join("|", preview.Branches.Select(x => $"{x.SpeciesId}:{x.Available}"))
            + ":" + string.Join("|", _appState.DiscoveredSpeciesIds.Order());
        if (signature == _evolutionChoicesSignature) return;
        var selected = (DetailEvolutionBranchPicker.SelectedItem as ComboBoxItem)?.Tag as int?;
        DetailEvolutionBranchPicker.Items.Clear();
        DetailEvolutionBranchPicker.Items.Add(new ComboBoxItem { Content = "Случайная доступная ветвь · без камня" });
        foreach (var branch in preview.Branches)
        {
            var known = _appState.DiscoveredSpeciesIds.Contains(branch.SpeciesId);
            DetailEvolutionBranchPicker.Items.Add(new ComboBoxItem
            {
                Tag = branch.SpeciesId,
                Content = $"#{branch.SpeciesId} · {(known ? branch.Name : "?")}{(branch.Available ? string.Empty : " · недоступно")}",
                IsEnabled = branch.Available,
                ToolTip = branch.Reason
            });
        }
        DetailEvolutionBranchPicker.SelectedItem = DetailEvolutionBranchPicker.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(x => x.IsEnabled && x.Tag is int id && id == selected)
            ?? DetailEvolutionBranchPicker.Items[0];
        _evolutionChoicesSignature = signature;
        UpdateEvolutionActionState();
    }

    private void DetailEvolutionBranchPicker_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateEvolutionActionState();

    private void UpdateEvolutionActionState()
    {
        if (_detailEvolutionPreview is null) return;
        var branch = (DetailEvolutionBranchPicker.SelectedItem as ComboBoxItem)?.Tag as int?;
        var needsStone = branch is not null && _detailEvolutionPreview.Branches.Count(x => x.Available) > 1;
        var reason = _detailEvolutionPreview.Reason
            ?? (needsStone && _detailEvolutionPreview.StonesOwned == 0 ? "Для выбора ветви нужен камень эволюции." : null);
        DetailEvolutionReasonText.Text = reason ?? string.Empty;
        DetailEvolveButton.IsEnabled = reason is null && (_detailCard is null || !_evolvingIndividuals.Contains(_detailCard.IndividualId));
    }

    private async void DetailEvolveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_detailCard is null || _previewEvolution is null || _evolveIndividual is null) return;
        var id = _detailCard.IndividualId;
        var generation = _detailGeneration;
        var branch = (DetailEvolutionBranchPicker.SelectedItem as ComboBoxItem)?.Tag as int?;
        var preview = _previewEvolution(id);
        if (preview.Reason is not null) { DetailErrorText.Text = preview.Reason; return; }
        if (branch is not null && !preview.Branches.Any(x => x.SpeciesId == branch && x.Available))
        { DetailErrorText.Text = "Выбранная ветвь недоступна."; return; }
        var choice = branch is null ? "случайную доступную ветвь" : $"ветвь #{branch}";
        if (!ConfirmDialog.Ask("Подтвердить эволюцию?",
            $"Потратить {preview.BerryCost} ягод {preview.BerryId} на {choice}?" +
            (branch is not null && preview.Branches.Count(x => x.Available) > 1 ? " Будет потрачен камень выбора." : string.Empty),
            "Эволюционировать", System.Windows.Window.GetWindow(this))) return;
        if (!IsCurrentDetail(id, generation)) return;
        _evolvingIndividuals.Add(id);
        DetailEvolveButton.IsEnabled = false;
        try
        {
            await _evolveIndividual(id, branch, Guid.NewGuid());
            Refresh();
            if (IsCurrentDetail(id, generation)) OpenIndividual(id);
        }
        catch (Exception error)
        {
            if (IsCurrentDetail(id, generation)) DetailErrorText.Text = error.Message;
        }
        finally
        {
            _evolvingIndividuals.Remove(id);
            if (_detailCard is not null && DetailOverlay.Visibility == Visibility.Visible) RefreshDetailEvolution();
        }
    }

    private void DetailEvolutionShopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_detailEvolutionPreview is { } preview && preview.BerriesOwned < preview.BerryCost)
            _buyBerries?.Invoke(preview.BerryId, preview.BerryCost);
        _navigate?.Invoke(MenuSection.Shop);
    }
    private void DetailEvolutionRouteButton_Click(object sender, RoutedEventArgs e) => _navigate?.Invoke(MenuSection.Expeditions);

    private void HideDetail()
    {
        _detailGeneration++;
        DetailOverlay.Visibility = Visibility.Collapsed;
        _detailCard = null;

        // One DispatcherTimer per animator, so it must not outlive the card it was driving.
        _detailAnimator?.Stop();
        _detailAnimator = null;
    }

    /// Click-outside-to-close. The card marks its own mouse-down Handled (see
    /// DetailCard_MouseLeftButtonDown), so this only runs for a click on the dimmed backdrop.
    /// Handled here too, so a dismiss never also counts as a press on anything else.
    private void DetailOverlay_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        HideDetail();
        e.Handled = true;
    }

    /// Stops a click inside the card from bubbling to the backdrop and closing the thing the
    /// player is trying to interact with.
    private void DetailCard_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        e.Handled = true;

    private void ShowDetailSprite(PokedexCard card)
    {
        _detailAnimator?.Stop();
        _detailAnimator = null;

        var frames = card.SpriteFrames;
        var hasStatic = card.SpriteImage is not null;

        DetailSpritePlaceholder.Visibility = hasStatic || frames is { Count: > 0 }
            ? Visibility.Collapsed
            : Visibility.Visible;

        // Assigned up front so the image is never blank for the animator's first tick, and so
        // a species with no animated sprite needs no timer at all.
        DetailSpriteImage.Source = frames is { Count: > 0 } ? frames[0] : card.SpriteImage;

        if (frames is null || frames.Count < 2) return;

        _detailAnimator = new SpriteAnimator(DetailSpriteImage, new FrameSequencer(frames.Count, HeroFrameDuration), frames);
        _detailAnimator.Start();
    }

    private void ApplyDetailFavoriteVisuals()
    {
        DetailFavoriteStar.Fill = _detailIsFavorite
            ? ThemeBrushes.Get("AccentGoldBrush")
            : System.Windows.Media.Brushes.Transparent;
        DetailFavoriteStar.Stroke = _detailIsFavorite
            ? ThemeBrushes.Get("AccentGoldBrush")
            : ThemeBrushes.Get("MutedTextBrush");
        DetailFavoriteButton.ToolTip = _detailIsFavorite
            ? "Убрать из избранного"
            : "Добавить в избранное";
        DetailFavoriteBadge.Visibility = _detailIsFavorite ? Visibility.Visible : Visibility.Collapsed;
    }

    /// Stays open, unlike the other two: pinning is a small reversible tweak, and closing the
    /// card under the player after it would make a second thought cost another two clicks.
    private async void DetailFavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_detailCard is null) return;
        var id = _detailCard.IndividualId;
        var generation = _detailGeneration;
        if (_commitState is null) ToggleFavorite(id);
        else
        {
            DetailFavoriteButton.IsEnabled = false;
            var committed = await CommitMutationAsync(next =>
            {
                var individual = next.Individuals.FirstOrDefault(i => i.Id == id)
                    ?? throw new InvalidOperationException("Этот покемон больше не найден.");
                individual.IsFavorite = !individual.IsFavorite;
            }, id, generation);
            if (!IsCurrentDetail(id, generation)) return;
            DetailFavoriteButton.IsEnabled = true;
            if (!committed) return;
            Refresh();
        }
        if (!IsCurrentDetail(id, generation)) return;
        var current = _appState.Collection.Individuals.FirstOrDefault(i => i.Id == id);
        if (current is null) { HideDetail(); return; }
        _detailIsFavorite = current.IsFavorite;
        ApplyDetailFavoriteVisuals();
    }

    /// A stage in the evolution-chain strip was clicked: toggle it as the cosmetic display
    /// override for the open individual. Only a REVEALED stage can be picked — never a future
    /// one, which is the spoiler boundary the model enforces. e.Handled keeps the click from
    /// bubbling to the backdrop and dismissing the overlay.
    private async void DetailEvolutionStage_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_detailCard is null) return;
        if (((FrameworkElement)sender).DataContext is not EvolutionStageCard stage || !stage.IsRevealed) return;

        var individual = _appState.Collection.Individuals.FirstOrDefault(i => i.Id == _detailCard.IndividualId);
        if (individual is null) return;
        var generation = _detailGeneration;

        // Clicking the already-displayed stage resets to the real current stage instead of
        // re-picking the same one — the click is a toggle, not a one-way pin.
        if (_commitState is null) _appState.SetDisplayStage(individual, stage.IsDisplayed ? null : stage.StageIndex);
        else if (!await CommitMutationAsync(next =>
        {
            var current = next.Individuals.FirstOrDefault(i => i.Id == individual.Id)
                ?? throw new InvalidOperationException("Этот покемон больше не найден.");
            if (stage.StageIndex > (int)current.CurrentStage ||
                current.CurrentSpeciesId > 0 && stage.StageIndex >= current.ReachedSpeciesIds.Count)
                throw new InvalidOperationException("Эта форма ещё не открыта.");
            current.DisplayStageOverride = stage.IsDisplayed ? null : stage.StageIndex;
        }, individual.Id, generation)) return;

        // Nudges the desktop pet's sprite immediately when this is the active individual.
        if (individual.Id == _appState.Collection.ActiveIndividualId) _onStateChanged();

        // Card stays open (like the favorite star): rebuild just the strip in place from fresh
        // model data rather than closing/reopening the whole detail overlay.
        if (IsCurrentDetail(individual.Id, generation))
        {
            var entry = _viewModel.GetEntry(individual.Id, DateTimeOffset.UtcNow);
            if (entry is not null) DetailEvolutionChainStrip.ItemsSource = BuildEvolutionStageCards(entry);
            else HideDetail();
        }

        Refresh();
    }

    /// Live rename. Does NOT call _onStateChanged — the overlay has nothing to re-fetch; the
    /// periodic autosave persists it. The grid behind picks the new name up on the next tick,
    /// because Nickname is part of its rebuild signature.
    private async void DetailNicknameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingDetailNickname || _detailCard is null) return;
        var id = _detailCard.IndividualId;
        var generation = _detailGeneration;
        var nicknameText = DetailNicknameBox.Text;
        if (_commitState is null) _viewModel.SetNickname(id, nicknameText);
        else if (!await CommitMutationAsync(next =>
        {
            var individual = next.Individuals.FirstOrDefault(i => i.Id == id)
                ?? throw new InvalidOperationException("Этот покемон больше не найден.");
            individual.Nickname = nicknameText;
        }, id, generation))
        {
            if (IsCurrentDetail(id, generation))
            {
                _isLoadingDetailNickname = true;
                DetailNicknameBox.Text = LiveNickname(id) ?? string.Empty;
                _isLoadingDetailNickname = false;
            }
            return;
        }
        if (!IsCurrentDetail(id, generation)) return;

        // Re-read from the model rather than from the box: the setter trims and clamps, so a
        // whitespace-only entry must show as "back to the species name", not as a blank title.
        var nickname = LiveNickname(_detailCard.IndividualId);
        var hasNickname = nickname is not null;

        DetailSpeciesNameText.Text = hasNickname ? nickname! : _detailCard.SpeciesName;
        DetailSpeciesSubtitleText.Text = hasNickname ? _detailCard.SpeciesName : string.Empty;
        DetailSpeciesSubtitleText.Visibility = hasNickname ? Visibility.Visible : Visibility.Collapsed;
    }

    /// This individual's nickname straight from the model, or null. Read live rather than off
    /// the overlay's snapshot card, which predates any rename made while the card is open.
    private string? LiveNickname(Guid individualId) =>
        _appState.Collection.Individuals.FirstOrDefault(i => i.Id == individualId)?.Nickname;

    /// Both of these close: they change which cards exist (release) or which one is marked
    /// active (select), so the snapshot the overlay is rendering is stale the moment they run.
    private async void DetailSelectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_detailCard is null) return;
        var id = _detailCard.IndividualId;
        var generation = _detailGeneration;
        if (_commitState is null) SelectIndividual(id);
        else
        {
            DetailSelectButton.IsEnabled = false;
            var committed = await CommitMutationAsync(next =>
            {
                if (!next.Individuals.Any(i => i.Id == id)) throw new InvalidOperationException("Этот покемон больше не найден.");
                next.ActiveIndividualId = id;
                if (!next.CareStates.TryGetValue(id, out var care)) next.CareStates[id] = care = new CareState();
                care.LastInteractionUtc = DateTimeOffset.UtcNow;
            }, id, generation);
            if (!committed) { if (IsCurrentDetail(id, generation)) DetailSelectButton.IsEnabled = true; return; }
            _onStateChanged();
            Refresh();
        }
        if (IsCurrentDetail(id, generation)) HideDetail();
    }

    private async void DetailReleaseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_detailCard is null) return;

        // Releasing is permanent and the button sits one click from a card the player opened
        // just to look at, so it gets the same themed confirmation the save-reset flow uses.
        // Both names in the prompt when the pet has a nickname: "Sparky" alone would leave the
        // player unsure WHICH Pikachu is about to be released permanently.
        var nickname = LiveNickname(_detailCard.IndividualId);
        var name = nickname is null ? _detailCard.SpeciesName : $"{nickname} ({_detailCard.SpeciesName})";

        var confirmed = ConfirmDialog.Ask(
            "Отпустить покемона?",
            $"{name} будет навсегда удалён из коллекции. Это нельзя отменить.",
            "Отпустить", System.Windows.Window.GetWindow(this));
        if (!confirmed) return;

        var id = _detailCard.IndividualId;
        var generation = _detailGeneration;
        if (_commitState is null) ReleaseIndividual(id);
        else
        {
            DetailReleaseButton.IsEnabled = false;
            var committed = await CommitMutationAsync(next =>
            {
                if (ExpeditionService.BusyIndividualIds(next).Contains(id))
                    throw new InvalidOperationException("Покемон занят в походе или питомнике.");
                var individual = next.Individuals.FirstOrDefault(i => i.Id == id)
                    ?? throw new InvalidOperationException("Этот покемон больше не найден.");
                next.DiscoveredSpeciesIds.UnionWith(individual.ReachedSpeciesIds);
                next.DiscoveredSpeciesIds.Add(individual.SpeciesId);
                next.Individuals.Remove(individual);
                next.CareStates.Remove(id);
                if (next.ActiveIndividualId == id) next.ActiveIndividualId = next.Individuals.FirstOrDefault()?.Id;
            }, id, generation);
            if (!committed) { if (IsCurrentDetail(id, generation)) DetailReleaseButton.IsEnabled = true; return; }
            _onStateChanged();
            Refresh();
        }
        if (IsCurrentDetail(id, generation)) HideDetail();
    }

    private async Task<bool> CommitMutationAsync(Action<SaveData> update, Guid id, int generation)
    {
        try
        {
            await _commitState!(update);
            if (IsCurrentDetail(id, generation)) DetailErrorText.Text = string.Empty;
            return true;
        }
        catch (Exception error)
        {
            if (IsCurrentDetail(id, generation)) DetailErrorText.Text = error.Message;
            return false;
        }
    }

    private void SelectIndividual(Guid id)
    {
        if (_viewModel.TrySetActive(id, DateTimeOffset.UtcNow))
        {
            _onStateChanged();
        }
        Refresh();
    }

    /// Pins/unpins one individual so it sorts to the top of the grid. Does NOT call
    /// _onStateChanged: that re-runs the overlay's sprite fetch for a change that only affects
    /// list ordering. The periodic autosave persists the flag.
    private void ToggleFavorite(Guid id)
    {
        _viewModel.ToggleFavorite(id);
        Refresh();
    }

    private void ReleaseIndividual(Guid id)
    {
        if (!_viewModel.Release(id)) return;
        _onStateChanged();
        Refresh();
    }

    /// Cache-only sprite lookup, safe on the UI thread. A species with no usable cached PNG
    /// returns null (the card shows its pokéball placeholder) and is fetched in the background.
    private ImageSource? TryGetThumbnail(int speciesId)
    {
        if (_spriteThumbnails.TryGetValue(speciesId, out var cached)) return cached;

        var bytes = _spriteCache.TryGetCachedBytes(speciesId);
        if (bytes is null || bytes.Length == 0)
        {
            EnsureThumbnailFetched(speciesId);
            return null;
        }

        var decoded = SpriteImages.Decode(bytes);
        _spriteThumbnails[speciesId] = decoded;
        return decoded;
    }

    /// A species with no usable cached PNG — never fetched, or a truncated file SpriteCache just
    /// dropped — is fetched once in the background; the next refresh tick then finds it on disk.
    private void EnsureThumbnailFetched(int speciesId)
    {
        if (_fetchSprite is null || !_thumbnailFetchStarted.Add(speciesId)) return;

        _ = FetchAsync();

        async Task FetchAsync()
        {
            try
            {
                if (await _fetchSprite(speciesId) is not null) _pokedexSignature = string.Empty;
            }
            catch
            {
                // Same as the animated fetch: a fire-and-forget continuation must never crash the app.
            }
        }
    }

    private static string FormatPercent(double fraction) =>
        $"{Math.Clamp(fraction, 0.0, 1.0) * 100:0}%";

    private static string FormatCaughtDate(DateTimeOffset caughtAtUtc) =>
        caughtAtUtc.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.CurrentCulture);

    private static string TraitLabel(string? id) => id switch
    {
        "gatherer" => "Собиратель",
        "treasure-hunter" => "Искатель сокровищ",
        "tracker" => "Следопыт",
        "finder" => "Находчивый",
        "mentor" => "Наставник",
        "collector" => "Коллекционер",
        "relic-finder" => "Искатель реликвий",
        "terrain-expert" => "Знаток местности",
        "fast-grower" => "Быстрорастущий",
        "rarity-expert" => "Знаток редкостей",
        "caring-parent" => "Заботливый родитель",
        _ => id ?? string.Empty
    };

}

internal static class CollectionEntryFormatting
{
    public static string DexNumberText(this CollectionEntry entry) => $"#{entry.DisplaySpeciesId:D3}";
}
