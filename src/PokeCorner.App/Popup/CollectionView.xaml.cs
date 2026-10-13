using PokeCorner.App.Imaging;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PokeCorner.App.Overlay;
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
    private readonly Func<IReadOnlyList<byte[]>?> _getActiveSpriteFrames;

    /// Evolution is player-triggered, so this view needs both "can they?" (button visibility)
    /// and "do it".
    private readonly Func<bool> _onEvolveRequested;
    private readonly Func<bool> _isReadyToEvolve;
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
    private PokedexSortOrder _sortOrder = PokedexSortOrder.Default;

    /// Zero-based page of the filtered grid. Deliberately view STATE, not derived: Refresh()
    /// runs once a second, so recomputing it from scratch every tick would drop the player back
    /// on page 1 while they were still reading page 3. Only a filter or search change resets it
    /// (and RefreshPokedex clamps it when a release shrinks the list past the current page).
    private int _pageIndex;

    /// 4 cards fit per row (the card is 112px wide with 5px margins each side, inside a 580px
    /// window), so 4 rows fill the visible grid area without turning the tab into an endless
    /// scroll.
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

    public CollectionView(CollectionViewModel viewModel, AppState appState, SpriteCache spriteCache, Action onPetRequested, Action onFeedRequested, Action onStateChanged, Func<IReadOnlyList<byte[]>?> getActiveSpriteFrames, Func<bool> onEvolveRequested, Func<bool> isReadyToEvolve, Func<int, Task<IReadOnlyList<byte[]>?>>? getAnimatedFrames = null, Func<int, Task<byte[]?>>? fetchSprite = null)
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
        _onEvolveRequested();
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

    private void PokedexSortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isReady) return;

        _sortOrder = PokedexSortCombo.SelectedIndex switch
        {
            1 => PokedexSortOrder.Alphabetical,
            2 => PokedexSortOrder.CatchDate,
            _ => PokedexSortOrder.Default
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

    /// One card per individual (two owned Pikachus are two cards), in GetEntries' order:
    /// active, then favourites, then dex number — unless a sort option replaces it.
    private void RefreshPokedex(IReadOnlyList<CollectionEntry> entries)
    {
        // Distinct SPECIES even though the cards are per-individual: this line reports Pokédex
        // completion, which a second Pikachu does not advance; the owned total sits alongside it.
        var speciesCount = entries.Select(e => e.DisplaySpeciesId).Distinct().Count();
        PokedexCountText.Text = speciesCount == 1
            ? $"Открыт 1 вид · всего покемонов: {entries.Count}"
            : $"Открыто видов: {speciesCount} · всего покемонов: {entries.Count}";

        // All three filters narrow the list GetEntries already ordered (AND), then ApplySort
        // keeps that banding or replaces it with an alphabetical/catch-date order.
        var visible = CollectionViewModel.ApplySort(entries
            .Where(entry => _rarityFilter is null || entry.Rarity == _rarityFilter.Value)
            .Where(entry => !_hideFullyEvolved || entry.HasEvolutionsRemaining)
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
            $"{e.IndividualId}:{e.DisplaySpeciesId}:{e.Nickname}:{e.IsActive}:{e.IsFavorite}:{e.EvolutionState}:{Math.Round(e.EvolutionFraction ?? -1, 3)}:{TryGetThumbnail(e.DisplaySpeciesId) is not null}:{_animatedFrames.ContainsKey(e.DisplaySpeciesId)}"));
        if (signature == _pokedexSignature) return;
        _pokedexSignature = signature;

        PokedexGrid.ItemsSource = page.Select(BuildCard).ToList();
    }

    private PokedexCard BuildCard(CollectionEntry entry) => new()
    {
        IndividualId = entry.IndividualId,
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

    // ======================= Pokédex detail overlay =======================
    // Renders the PokedexCard it is handed and routes every mutation through the same
    // SelectIndividual/ReleaseIndividual/ToggleFavorite methods the rest of this view uses.

    /// The card currently on show, or null when the overlay is hidden.
    private PokedexCard? _detailCard;

    /// Local mirror of the pin so the star can flip immediately on click. The grid rebuilds
    /// from the model, but the overlay holds an immutable snapshot card — re-reading it would
    /// show the stale value.
    private bool _detailIsFavorite;

    private SpriteAnimator? _detailAnimator;

    /// Guards the nickname TextBox's TextChanged while ShowDetail populates it — see there.
    private bool _isLoadingDetailNickname;

    private void ShowDetail(PokedexCard card)
    {
        _detailCard = card;
        _detailIsFavorite = card.IsFavorite;

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

        DetailShinyBadge.Visibility = card.ShinyVisibility;
        DetailActiveBadge.Visibility = card.ActiveBadgeVisibility;

        // An already-active pet can't be selected again.
        DetailSelectButton.IsEnabled = card.CanSelect;
        DetailSelectButton.ToolTip = card.CanSelect ? null : "Этот покемон уже на рабочем столе";

        ApplyDetailFavoriteVisuals();
        ShowDetailSprite(card);

        DetailOverlay.Visibility = Visibility.Visible;
    }

    private void HideDetail()
    {
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
            : "В избранное — закрепить в начале списка";
        DetailFavoriteBadge.Visibility = _detailIsFavorite ? Visibility.Visible : Visibility.Collapsed;
    }

    /// Stays open, unlike the other two: pinning is a small reversible tweak, and closing the
    /// card under the player after it would make a second thought cost another two clicks.
    private void DetailFavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_detailCard is null) return;
        ToggleFavorite(_detailCard.IndividualId);
        _detailIsFavorite = !_detailIsFavorite;
        ApplyDetailFavoriteVisuals();
    }

    /// A stage in the evolution-chain strip was clicked: toggle it as the cosmetic display
    /// override for the open individual. Only a REVEALED stage can be picked — never a future
    /// one, which is the spoiler boundary the model enforces. e.Handled keeps the click from
    /// bubbling to the backdrop and dismissing the overlay.
    private void DetailEvolutionStage_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (_detailCard is null) return;
        if (((FrameworkElement)sender).DataContext is not EvolutionStageCard stage || !stage.IsRevealed) return;

        var individual = _appState.Collection.Individuals.FirstOrDefault(i => i.Id == _detailCard.IndividualId);
        if (individual is null) return;

        // Clicking the already-displayed stage resets to the real current stage instead of
        // re-picking the same one — the click is a toggle, not a one-way pin.
        _appState.SetDisplayStage(individual, stage.IsDisplayed ? null : stage.StageIndex);

        // Nudges the desktop pet's sprite immediately when this is the active individual.
        if (individual.Id == _appState.Collection.ActiveIndividualId) _onStateChanged();

        // Card stays open (like the favorite star): rebuild just the strip in place from fresh
        // model data rather than closing/reopening the whole detail overlay.
        var entry = _viewModel.GetEntries(DateTimeOffset.UtcNow).FirstOrDefault(x => x.IndividualId == individual.Id);
        if (entry is not null) DetailEvolutionChainStrip.ItemsSource = BuildEvolutionStageCards(entry);

        Refresh();
    }

    /// Live rename. Does NOT call _onStateChanged — the overlay has nothing to re-fetch; the
    /// periodic autosave persists it. The grid behind picks the new name up on the next tick,
    /// because Nickname is part of its rebuild signature.
    private void DetailNicknameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingDetailNickname || _detailCard is null) return;

        _viewModel.SetNickname(_detailCard.IndividualId, DetailNicknameBox.Text);

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
    private void DetailSelectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_detailCard is null) return;
        var id = _detailCard.IndividualId;
        HideDetail();
        SelectIndividual(id);
    }

    private void DetailReleaseButton_Click(object sender, RoutedEventArgs e)
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
        HideDetail();
        ReleaseIndividual(id);
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
        _viewModel.Release(id);
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

}

internal static class CollectionEntryFormatting
{
    public static string DexNumberText(this CollectionEntry entry) => $"#{entry.DisplaySpeciesId:D3}";
}
