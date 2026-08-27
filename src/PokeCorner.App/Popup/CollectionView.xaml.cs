using System.Globalization;
using System.IO;
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
    private readonly Func<Guid, bool> _onHatchRequested;

    /// Evolution is player-triggered now (it used to fire automatically the instant the bar
    /// filled), so this view needs both "can they?" for the button's visibility and "do it".
    private readonly Func<bool> _onEvolveRequested;
    private readonly Func<bool> _isReadyToEvolve;
    private DispatcherTimer? _liveRefreshTimer;

    /// Same idle cadence the desktop overlay animates at, so the hero sprite and the pet on
    /// the desktop visibly move in step rather than at two different speeds.
    private static readonly TimeSpan HeroFrameDuration = TimeSpan.FromMilliseconds(150);

    private SpriteAnimator? _heroAnimator;

    /// Identity of the frame set currently driving the hero image. Refresh() runs once a
    /// second; without this guard the animator would be torn down and restarted on every
    /// tick, which resets the cycle to frame 0 and reads as a stuck sprite rather than an
    /// animated one. Deliberately content-derived (individual + frame count + first frame
    /// size) rather than a reference check: App re-decodes the GIF into a brand new list on
    /// every overlay refresh, so reference identity would churn for no visible reason.
    private string _heroSpriteSignature = string.Empty;

    /// null = "Все". Which rarity the Pokédex grid is filtered to.
    private RarityTier? _rarityFilter;

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
    private string _eggsSignature = string.Empty;
    private string _pokedexSignature = string.Empty;

    public CollectionView(CollectionViewModel viewModel, AppState appState, SpriteCache spriteCache, Action onPetRequested, Action onFeedRequested, Action onStateChanged, Func<IReadOnlyList<byte[]>?> getActiveSpriteFrames, Func<Guid, bool> onHatchRequested, Func<bool> onEvolveRequested, Func<bool> isReadyToEvolve, Func<int, Task<IReadOnlyList<byte[]>?>>? getAnimatedFrames = null)
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
        _onHatchRequested = onHatchRequested;
        _getAnimatedFrames = getAnimatedFrames;

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

        // And the detail overlay's, which now lives in this view rather than in a window whose
        // own Closed handler used to stop it.
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
        RefreshEggs(nowUtc);
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
            ActivePetNameText.Text = activeEntry.SpeciesName;
            ActivePetMetaText.Text =
                $"{activeEntry.DexNumberText()} · {RarityDisplay.GetName(activeEntry.Rarity)} · характер {activeEntry.Nature} · пойман {FormatCaughtDate(activeEntry.CaughtAtUtc)}";
            CarePointsText.Text = active.AccumulatedCarePoints.ToString("N0", CultureInfo.CurrentCulture);
            ShinyBadge.Visibility = activeEntry.IsShiny ? Visibility.Visible : Visibility.Collapsed;
            ActiveTypeBadges.ItemsSource = TypeDisplay.ToBadges(activeEntry.Types);
        }

        TreatsText.Text = _appState.Feeding.Treats.ToString(CultureInfo.CurrentCulture);

        RefreshHeroSprite(active);
    }

    /// Drives the hero image from the SAME frame list the desktop overlay animates, so the pet
    /// in the menu moves instead of sitting on a single frozen frame. Reuses the overlay's
    /// FrameSequencer/SpriteAnimator pair — both already take their target Image as a
    /// constructor argument, so pointing them at this view's Image needed no changes there.
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

        var decoded = frames!.Select(DecodePng).ToList();

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
        EvolutionProgressBar.ToolTip = EvolutionProgressDisplay.ToolTip(progress.EvolutionFraction, progress.EvolutionState);
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

        Refresh();
    }

    /// The grid is fed by GetEntries — the SAME per-individual source the removed roster list
    /// used — rendered in the Pokédex card's style. That is the whole of item 4: one browsable
    /// list instead of a deduped species grid sitting above a parallel roster of the same
    /// Pokémon. Two owned Pikachus are therefore two cards, and the order is GetEntries' own
    /// (favourites pinned first, then catch order) rather than dex number.
    private void RefreshPokedex(IReadOnlyList<CollectionEntry> entries)
    {
        // Distinct SPECIES even though the cards are per-individual: this line reports Pokédex
        // completion, which a second Pikachu does not advance. The owned total is reported
        // alongside it because the cards no longer make it countable at a glance.
        var speciesCount = entries.Select(e => e.DisplaySpeciesId).Distinct().Count();
        PokedexCountText.Text = speciesCount == 1
            ? $"Открыт 1 вид · всего покемонов: {entries.Count}"
            : $"Открыто видов: {speciesCount} · всего покемонов: {entries.Count}";

        var visible = _rarityFilter is null
            ? entries
            : entries.Where(entry => entry.Rarity == _rarityFilter.Value).ToList();

        PokedexEmptyText.Visibility = visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PokedexEmptyText.Text = entries.Count == 0
            ? "Пока пусто — поймай первого покемона на вкладке «Поймать»."
            : "В этой редкости пока нет открытых видов — попробуй другой фильтр.";

        // Kick off the animated-sprite fetches for whatever is on screen. Cheap after the
        // first pass: EnsureAnimatedFrames is a no-op for any species already requested.
        foreach (var entry in visible) EnsureAnimatedFrames(entry.DisplaySpeciesId);

        // The sprite's presence is part of the signature — both the static thumbnail and the
        // animated frames — so a card that rendered its placeholder before the species was
        // cached rebuilds itself once the artwork lands, instead of staying on the placeholder
        // until some unrelated field changes.
        var signature = $"{_rarityFilter}|" + string.Join("|", visible.Select(e =>
            $"{e.IndividualId}:{e.DisplaySpeciesId}:{e.IsActive}:{e.IsAvailableToActivate}:{e.IsFavorite}:{e.EvolutionState}:{Math.Round(e.EvolutionFraction ?? -1, 3)}:{TryGetThumbnail(e.DisplaySpeciesId) is not null}:{_animatedFrames.ContainsKey(e.DisplaySpeciesId)}"));
        if (signature == _pokedexSignature) return;
        _pokedexSignature = signature;

        PokedexGrid.ItemsSource = visible.Select(BuildCard).ToList();
    }

    private PokedexCard BuildCard(CollectionEntry entry) => new()
    {
        IndividualId = entry.IndividualId,
        SpeciesId = entry.DisplaySpeciesId,
        SpeciesName = entry.SpeciesName,
        DexNumber = entry.DexNumberText(),
        RarityName = RarityDisplay.GetName(entry.Rarity),
        RarityBrush = RarityDisplay.GetBrush(entry.Rarity),
        EvolutionFraction = entry.EvolutionFraction,
        EvolutionState = entry.EvolutionState,
        CaughtAtText = FormatCaughtDate(entry.CaughtAtUtc),
        NatureText = entry.Nature.ToString(),
        CanSelect = !entry.IsActive && entry.IsAvailableToActivate,
        AvailabilityToolTip = entry.AvailableAgainAtUtc is { } until
            ? $"Питомец обиделся и вернётся {until.ToLocalTime():dd.MM.yyyy HH:mm}"
            : "Питомец временно недоступен",
        IsShiny = entry.IsShiny,
        IsActive = entry.IsActive,
        IsAvailableToActivate = entry.IsAvailableToActivate,
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
            IsRevealed = stage.IsRevealed,
            NameText = stage.IsRevealed ? stage.SpeciesName ?? "???" : "???",
            SpriteImage = stage.IsRevealed && stage.SpeciesId is { } id ? TryGetThumbnail(id) : null,
            TypeBadges = TypeDisplay.ToBadges(stage.Types),
            IsCurrent = stage.IsCurrent,
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

            _animatedFrames[speciesId] = frames.Select(DecodePng).ToList();
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

    /// A Pokédex card was clicked: show the detail/actions card for that individual. The three
    /// actions call straight back into the same CollectionViewModel methods the removed
    /// roster's inline buttons used, so behaviour (including the Hardcore availability gate on
    /// Выбрать) is unchanged — only where the buttons live has moved.
    ///
    /// This used to construct a PokemonDetailWindow and ShowDialog() it. That window is gone:
    /// a card in a grid spawning a second OS window read as foreign, so the same content is an
    /// overlay layer inside this view now (see CollectionView.xaml's detail overlay).
    private void PokedexCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not PokedexCard card) return;
        ShowDetail(card);
    }

    // ======================= Pokédex detail overlay =======================
    // Ported from PokemonDetailWindow's code-behind. Deliberately dumb: it renders the
    // PokedexCard it is handed and routes every mutation through the same
    // SelectIndividual/ReleaseIndividual/ToggleFavorite methods the rest of this view uses.

    /// The card currently on show, or null when the overlay is hidden.
    private PokedexCard? _detailCard;

    /// Local mirror of the pin so the star can flip immediately on click. The grid rebuilds
    /// from the model, but the overlay holds an immutable snapshot card — re-reading it would
    /// show the stale value.
    private bool _detailIsFavorite;

    private SpriteAnimator? _detailAnimator;

    private void ShowDetail(PokedexCard card)
    {
        _detailCard = card;
        _detailIsFavorite = card.IsFavorite;

        DetailDexNumberText.Text = card.DexNumber;
        DetailSpeciesNameText.Text = card.SpeciesName;
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
        DetailUnavailableBadge.Visibility = card.UnavailableBadgeVisibility;
        DetailUnavailableBadge.ToolTip = card.AvailabilityToolTip;

        // Exactly the roster's rule: an already-active pet, or one that has run away in
        // Hardcore, can't be selected.
        DetailSelectButton.IsEnabled = card.CanSelect;
        DetailSelectButton.ToolTip = card.CanSelect ? null
            : card.IsActive ? "Этот покемон уже на рабочем столе"
            : card.AvailabilityToolTip;

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

    /// Click-outside-to-close. The card itself marks its own mouse-down Handled (see
    /// DetailCard_MouseLeftButtonDown), so this only ever runs for a click on the dimmed
    /// backdrop around it.
    private void DetailOverlay_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        HideDetail();

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
        var confirmed = ConfirmDialog.Ask(
            "Отпустить покемона?",
            $"{_detailCard.SpeciesName} будет навсегда удалён из коллекции. Это нельзя отменить.",
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

    /// Pins/unpins one individual so it sorts to the top of the grid.
    /// Deliberately does NOT call _onStateChanged: that re-runs the overlay's sprite fetch,
    /// which is real network work for a change that only affects list ordering. The flag is
    /// written straight onto the individual and the app's periodic autosave persists it.
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

    // (SelectButton_Click / FavoriteButton_Click / ReleaseButton_Click lived here as handlers
    // for the roster rows' inline action buttons. The roster is gone; the same three
    // operations are now SelectIndividual/ToggleFavorite/ReleaseIndividual above, invoked by
    // PokemonDetailWindow.)

    // Fully qualified as System.Windows.Controls.Button, not bare Button — Task 5 turned
    // on UseWindowsForms for TrayIconService's NotifyIcon, which makes Button ambiguous
    // with System.Windows.Forms.Button project-wide from that task onward (the same
    // Application-ambiguity issue Task 5 hit, just a different colliding type name).
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
