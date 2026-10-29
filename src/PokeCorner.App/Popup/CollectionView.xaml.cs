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
    private readonly Func<Guid, RehomeQuote>? _previewRehome;
    private readonly Func<Guid, Guid, Task<RehomeAction>>? _rehomeIndividual;
    private readonly Func<Guid, int, RetrainingPreview>? _previewRetraining;
    private readonly Func<Guid, int, Guid, Task<RetrainingAction>>? _retrainIndividual;
    private readonly Action<MenuSection>? _navigate;
    private readonly Action<string, int>? _buyBerries;
    private readonly Func<string, string, string, Task<bool>>? _confirm;
    private readonly Func<string, string, string, Task<bool>>? _confirmDestructive;
    private readonly Func<Guid, string, IndividualItemPreview>? _previewItem;
    private readonly Func<Guid, string, Guid, Task<PurchaseResult>>? _useItem;
    private readonly Func<Guid, (string Text, double Fraction)>? _xpProgress;
    private readonly Func<int, bool, string>? _whereFind;
    private readonly Func<int, bool, SpeciesPaths?>? _speciesPaths;
    private readonly Action<string>? _openRoute;
    private readonly Func<int, int, string?>? _openPair;
    private TaskCompletionSource<bool>? _localConfirmation;
    private bool _detailPending;
    // Initial construction supports standalone fixtures; after Unloaded only Loaded resumes presentation.
    private bool _presentationSuspended;
    private HashSet<int> _shinyOnlySpecies = [];
    private (Guid Pet, int? Branch, Guid Operation)? _pendingEvolution;
    private (Guid Pet, Guid Operation)? _pendingRehome;
    private readonly Dictionary<(Guid Pet, string Item), Guid> _pendingItems = new();
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
    // 16 in narrow hosts; in the Night Diary layout six full rows of the closed grid, so a FullHD
    // window shows ~70 pets per page instead of 16. Derived from the full width, not the open-page
    // width, so opening a pet never moves it to another page.
    private int _pageSize = 16;

    /// Animated-GIF frames for one species, or null when it has none. Optional so the
    /// theme/layout tests can construct this view without an App behind them; a null delegate
    /// simply leaves every card on its static frame.
    private readonly Func<int, bool, Task<IReadOnlyList<byte[]>?>>? _getAnimatedFrames;

    /// Decoded animated frames per species id, and the set of species a fetch has already
    /// been kicked off for. Both are needed: Refresh() runs once a second over every visible
    /// card, so without the "already started" set the grid would re-request the same GIFs
    /// every tick, and without the decoded memo it would re-decode them every rebuild.
    private readonly Dictionary<(int, bool), IReadOnlyList<ImageSource>> _animatedFrames = new();
    private readonly HashSet<(int, bool)> _animatedFetchStarted = new();
    private readonly Func<int, Task<byte[]?>>? _fetchSprite;
    private readonly HashSet<int> _thumbnailFetchStarted = new();
    private readonly Func<int, bool, string?>? _getSpeciesAssetPath;
    private readonly Func<Action<SaveData>, Task>? _commitState;
    private readonly Dictionary<(int, bool), ImageSource> _packageSprites = new();
    private readonly HashSet<(int, bool)> _missingPackageSprites = new();
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

    public CollectionView(CollectionViewModel viewModel, AppState appState, SpriteCache spriteCache, Action onPetRequested, Action onFeedRequested, Action onStateChanged, Func<IReadOnlyList<byte[]>?> getActiveSpriteFrames, Func<bool> onEvolveRequested, Func<bool> isReadyToEvolve, Func<int, bool, Task<IReadOnlyList<byte[]>?>>? getAnimatedFrames = null, Func<int, Task<byte[]?>>? fetchSprite = null,
        Func<int, bool, string?>? getSpeciesAssetPath = null, Func<Action<SaveData>, Task>? commitState = null,
        Func<Guid, EvolutionPreview>? previewEvolution = null,
        Func<Guid, int?, Guid, Task<EvolutionAction>>? evolveIndividual = null,
        Action<MenuSection>? navigate = null, Action<string, int>? buyBerries = null,
        Func<Guid, RehomeQuote>? previewRehome = null, Func<Guid, Guid, Task<RehomeAction>>? rehomeIndividual = null,
        Func<Guid, int, RetrainingPreview>? previewRetraining = null,
        Func<Guid, int, Guid, Task<RetrainingAction>>? retrainIndividual = null,
        Func<string, string, string, Task<bool>>? confirmAsync = null,
        Func<Guid, string, IndividualItemPreview>? previewItem = null,
        Func<Guid, string, Guid, Task<PurchaseResult>>? useItem = null,
        Func<Guid, (string Text, double Fraction)>? xpProgress = null,
        Func<int, bool, string>? whereFind = null,
        Func<string, string, string, Task<bool>>? confirmDestructiveAsync = null,
        Func<int, bool, SpeciesPaths?>? speciesPaths = null, Action<string>? openRoute = null, Func<int, int, string?>? openPair = null)
    {
        InitializeComponent();
        SizeChanged += (_, _) => LayoutMasterDetail();
        DetailOverlay.SizeChanged += (_, _) => StackDetailColumns();
        ((System.Windows.Controls.TabControl)((Grid)Content).Children[0]).SelectionChanged += (_, e) => { if (e.OriginalSource is System.Windows.Controls.TabControl) LayoutMasterDetail(); };
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
        _previewRehome = previewRehome;
        _rehomeIndividual = rehomeIndividual;
        _previewRetraining = previewRetraining;
        _retrainIndividual = retrainIndividual;
        _navigate = navigate;
        _buyBerries = buyBerries;
        _confirm = confirmAsync; _confirmDestructive = confirmDestructiveAsync; _previewItem = previewItem; _useItem = useItem;
        _xpProgress = xpProgress; _whereFind = whereFind;
        _speciesPaths = speciesPaths; _openRoute = openRoute; _openPair = openPair;
        LocalConfirmationHost.Completed += CompleteLocalConfirmation;

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
        _presentationSuspended = false;
        Refresh();
        if (_detailCard is not null && DetailOverlay.Visibility == Visibility.Visible) ShowDetailSprite(_detailCard);
        _liveRefreshTimer?.Stop();
        _liveRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _liveRefreshTimer.Tick += (_, _) => Refresh();
        _liveRefreshTimer.Start();
    }

    private void StopLiveRefresh()
    {
        _presentationSuspended = true;
        _detailGeneration++;
        CompleteLocalConfirmation(false);
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
        if (_presentationSuspended) return;
        var nowUtc = DateTimeOffset.UtcNow;
        var active = _appState.Collection.GetActive();
        var entries = _viewModel.GetEntries(nowUtc);

        RefreshHero(active, entries);
        RefreshProgress(active);
        RefreshPokedex(entries);
        if (SpeciesPane.Visibility == Visibility.Visible) RefreshSpecies();
        if (_detailCard is not null && !_appState.Collection.Individuals.Any(i => i.Id == _detailCard.IndividualId)) HideDetail();
        if (_detailCard is not null && DetailOverlay.Visibility == Visibility.Visible)
        {
            RefreshDetailEvolution();
            RefreshDetailRetraining();
            RefreshDetailRehome();
            RefreshDetailItems();
        }
    }

    private void IndividualsMode_Click(object sender, RoutedEventArgs e)
    {
        IndividualsPane.Visibility = Visibility.Visible;
        SpeciesPane.Visibility = Visibility.Collapsed;
        IndividualsModeButton.Style = (Style)FindResource("NdChoiceSelectedStyle");
        SpeciesModeButton.Style = (Style)FindResource("NdChoiceStyle");
        LayoutMasterDetail();
    }

    private const double PageMin = 560, PageMax = 640, TileMin = 136;

    // Below grid + page width (only in narrow test hosts; the window minimum is 1200) the page overlays the grid as before.
    private bool MasterDetail => ActualWidth >= 1000 &&
        ((System.Windows.Controls.TabControl)((Grid)Content).Children[0]).SelectedIndex == 1;

    // D-17: the grid owns the full width while nothing is open; an open pet page takes the right
    // edge and the grid narrows beside it (no tile is hidden under the page), keeping the picked
    // tile in view. Clicking another tile swaps the page; × or Esc closes it and the grid widens.
    private void LayoutMasterDetail()
    {
        var width = ActualWidth;
        if (!MasterDetail || width <= 0)
        {
            ArrangeFilters(false);
            PokedexScroll.Margin = new Thickness(0);
            DetailOverlay.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            DetailOverlay.Width = double.NaN;
            DetailOverlay.Margin = new Thickness(0);
            DetailOverlay.BorderThickness = new Thickness(0);
            SpeciesDialog.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            SpeciesDialog.Width = double.NaN;
            DetailBackButton.Content = "К покедексу";
            return;
        }
        var open = DetailOverlay.Visibility == Visibility.Visible || SpeciesDialog.Visibility == Visibility.Visible;
        var page = Math.Clamp(width * 0.5, PageMin, PageMax);
        ArrangeFilters(open && width - page < 780);
        var pageSize = Math.Max(16, (int)((width - 40) / TileMin) * 6);
        if (pageSize != _pageSize)
        {
            _pageIndex = _pageIndex * _pageSize / pageSize;
            _pageSize = pageSize;
            Refresh();
        }
        PokedexScroll.Margin = new Thickness(0, 0, open ? page : 0, 0);
        DetailOverlay.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
        DetailOverlay.Width = page;
        DetailOverlay.Margin = new Thickness(0);
        DetailOverlay.BorderThickness = new Thickness(1, 0, 0, 0);
        SpeciesDialog.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
        SpeciesDialog.Width = page;
        DetailBackButton.Content = "✕";
        HighlightSelectedRow();
        if (open) Dispatcher.BeginInvoke(() => Rows(PokedexGrid).FirstOrDefault(r => Equals(r.Tag, _detailCard?.IndividualId))?.BringIntoView(),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // Beside an open page the grid is ~500 px: search takes its own row, the three selects share the next.
    private void ArrangeFilters(bool narrow)
    {
        var cells = PokedexFilterGrid.Children.OfType<FrameworkElement>().ToArray();
        for (var i = 0; i < cells.Length; i++)
        {
            Grid.SetRow(cells[i], narrow && i > 0 ? 1 : 0);
            Grid.SetColumn(cells[i], narrow ? (i == 0 ? 0 : i - 1) : i);
            Grid.SetColumnSpan(cells[i], narrow && i == 0 ? 3 : 1);
            cells[i].Margin = new Thickness(0, 0, !narrow && i < cells.Length - 1 || narrow && i is 1 or 2 ? 8 : 0, narrow && i == 0 ? 8 : 0);
        }
        PokedexFilterGrid.ColumnDefinitions[0].MinWidth = narrow ? 0 : 200;
        PokedexFilterGrid.ColumnDefinitions[3].Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        PokedexFilterGrid.ColumnDefinitions[3].MinWidth = narrow ? 0 : 140;
        PokedexFilterGrid.ColumnDefinitions[1].MinWidth = PokedexFilterGrid.ColumnDefinitions[2].MinWidth = narrow ? 0 : 120;
    }

    // Tiles stretch so each row is filled edge to edge instead of leaving a ragged gap.
    private void PokedexGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var available = PokedexGrid.ActualWidth - 8;
        if (available <= 0) return;
        var columns = Math.Max(1, (int)(available / TileMin));
        Resources["PokedexCardWidth"] = Math.Floor(available / columns) - 8;
    }

    // On a narrow page (the 1200x660 minimum) the stats column would squeeze to ~250 px and break
    // every line; it moves under the portrait instead.
    private void StackDetailColumns()
    {
        if (DetailPersonalGrid.RowDefinitions.Count == 0)
        {
            DetailPersonalGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            DetailPersonalGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        var narrow = DetailOverlay.ActualWidth < 780;
        var right = DetailPersonalGrid.Children.OfType<FrameworkElement>().FirstOrDefault(c => Grid.GetColumn(c) == 1 || Grid.GetRow(c) == 1);
        var left = DetailPersonalGrid.Children.OfType<FrameworkElement>().FirstOrDefault(c => !ReferenceEquals(c, right));
        if (right is null || left is null) return;
        DetailPersonalGrid.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(right, narrow ? 0 : 1);
        Grid.SetRow(right, narrow ? 1 : 0);
        Grid.SetColumnSpan(left, narrow ? 2 : 1);
        left.Margin = narrow ? new Thickness(0, 0, 0, 16) : new Thickness(0, 0, 18, 0);
    }

    private void HighlightSelectedRow()
    {
        var selected = _detailCard?.IndividualId;
        foreach (var row in Rows(PokedexGrid))
            row.BorderBrush = Equals(row.Tag, selected) ? (System.Windows.Media.Brush)FindResource("AccentRedBrush") : (System.Windows.Media.Brush)FindResource("CardBorderBrush");
    }

    private static IEnumerable<Border> Rows(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Border { Tag: Guid } border && border.DataContext is PokedexCard) yield return border;
            else foreach (var nested in Rows(child)) yield return nested;
        }
    }

    // A short truthful line from the trainer about this pet, chosen by what is most notable now.
    private static string TrainerNote(PokedexCard card) =>
        card.IsShiny ? "Редкий окрас! Таких встречаешь раз в тысячу."
        : card.ActiveBadgeVisibility == Visibility.Visible ? "Сейчас со мной на столе — не отходит ни на шаг."
        : card.FavoriteBadgeVisibility == Visibility.Visible ? "Любимчик. Его не отдам никому."
        : card.IsBusy ? "Сейчас в походе или в питомнике — жду обратно."
        : card.Level >= 30 ? "Опытный. С ним в любой поход не страшно."
        : "Ещё узнаём друг друга.";

    private void SpeciesMode_Click(object sender, RoutedEventArgs e)
    {
        IndividualsPane.Visibility = Visibility.Collapsed;
        SpeciesPane.Visibility = Visibility.Visible;
        SpeciesModeButton.Style = (Style)FindResource("NdChoiceSelectedStyle");
        IndividualsModeButton.Style = (Style)FindResource("NdChoiceStyle");
        LayoutMasterDetail();
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
        var all = SpeciesEntries();
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
            false, sort);
        var pageCount = Math.Max(1, (int)Math.Ceiling(visible.Count / (double)SpeciesPageSize));
        _speciesPageIndex = Math.Clamp(_speciesPageIndex, 0, pageCount - 1);
        SpeciesCountText.Text = $"Открыто видов: {all.Count(e => e.IsDiscovered)} · показано: {visible.Count}";
        SpeciesPageText.Text = $"{_speciesPageIndex + 1} / {pageCount}";
        SpeciesPrevButton.IsEnabled = _speciesPageIndex > 0;
        SpeciesNextButton.IsEnabled = _speciesPageIndex < pageCount - 1;
        SpeciesPagination.Visibility = pageCount > 1 ? Visibility.Visible : Visibility.Collapsed;
        var page = visible.Skip(_speciesPageIndex * SpeciesPageSize).Take(SpeciesPageSize).ToList();
        var signature = $"{_speciesPageIndex}|{type}|{rarity}|{SpeciesSearchBox.Text}|{sort}|" +
            string.Join("|", page.Select(e => $"{e.SpeciesId}:{e.IsDiscovered}:{string.Join(',', e.OwnedIndividuals.Select(i => $"{i.IndividualId}:{i.Nickname}:{i.IsBusy}:{i.Level}"))}"));
        if (signature != _speciesSignature)
        {
            _speciesSignature = signature;
            SpeciesGrid.ItemsSource = page.Select(e => new SpeciesCard(e, GetPackageSprite(e.SpeciesId, _shinyOnlySpecies.Contains(e.SpeciesId)))).ToList();
        }
        if (_selectedSpeciesId is { } id) ShowSpeciesDetail(all.FirstOrDefault(e => e.SpeciesId == id));
    }

    // One Pokédex for both forms (Georgy 26.10): the species page shows normal and shiny side by
    // side, so the grid needs no form toggle. A species is open if either form was caught; a
    // shiny-only species shows its shiny sprite, since that is the one the player has seen.
    private IReadOnlyList<PokedexSpeciesEntry> SpeciesEntries()
    {
        var shiny = _viewModel.GetSpeciesEntries(true);
        var shinyOnly = new HashSet<int>();
        var merged = _viewModel.GetSpeciesEntries(false).Select((normal, i) =>
        {
            var other = shiny[i];
            var useShiny = !normal.IsDiscovered && other.IsDiscovered;
            if (useShiny) shinyOnly.Add(normal.SpeciesId);
            return (useShiny ? other : normal) with { OwnedIndividuals = [.. normal.OwnedIndividuals, .. other.OwnedIndividuals] };
        }).ToList();
        _shinyOnlySpecies = shinyOnly;
        return merged;
    }

    private ImageSource? GetPackageSprite(int speciesId, bool isShiny = false)
    {
        if (_packageSprites.TryGetValue((speciesId, isShiny), out var cached)) return cached;
        if (_missingPackageSprites.Contains((speciesId, isShiny))) return null;
        var path = _getSpeciesAssetPath?.Invoke(speciesId, isShiny);
        if (path is null) return null;
        try
        {
            // A failed URI decode can retain WPF's file cache handle. Scope the stream even on failure.
            using var stream = File.OpenRead(path);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            _packageSprites[(speciesId, isShiny)] = bitmap;
            return bitmap;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException or FormatException or System.Runtime.InteropServices.COMException)
        {
            _missingPackageSprites.Add((speciesId, isShiny));
            return null;
        }
    }

    private void SpeciesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SpeciesGrid.SelectedItem is not SpeciesCard card) return;
        _selectedSpeciesId = card.Entry.SpeciesId;
        ShowSpeciesDetail(card.Entry);
        SpeciesDialog.Visibility = Visibility.Visible;
        SpeciesPageScroll.ScrollToTop();
        LayoutMasterDetail();
    }

    private void ShowSpeciesDetail(PokedexSpeciesEntry? entry)
    {
        SpeciesDetail.Visibility = entry is null ? Visibility.Collapsed : Visibility.Visible;
        if (entry is null) return;
        SpeciesDetailName.Text = entry.IsDiscovered ? entry.DisplayName ?? entry.Number : "Неизвестный вид";
        SpeciesDetailDescription.Text = entry.IsDiscovered ? CollectionPresentation.Fact(entry.SpeciesId) ?? string.Empty : _speciesPaths is null ? entry.RouteHint : "";
        SpeciesDetailTypes.Text = string.Join(" · ", new[] { entry.Number }.Concat(entry.IsDiscovered ? entry.Types.Select(TypeDisplay.GetLabel) : []));
        RenderSpeciesForms(entry.SpeciesId);
        SpeciesDetailOwnedLabel.Visibility = entry.OwnedIndividuals.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SpeciesDetailOwnedLabel.Text = entry.OwnedIndividuals.Count == 1 ? "Мой экземпляр" : $"Мои экземпляры · {entry.OwnedIndividuals.Count}";
        SpeciesOwnedList.ItemsSource = entry.OwnedIndividuals.Select(i => new IndividualChoice(i.IndividualId,
            string.Join(" · ", new[] { i.Nickname ?? entry.DisplayName, $"ур. {i.Level}", i.IsBusy ? "занят" : i.IsActive ? "на столе" : null,
                "пойман " + FormatCaughtDate(i.CaughtAtUtc), i.IsShiny ? "шайни" : null }.Where(x => x is not null)),
            GetPackageSprite(entry.SpeciesId, i.IsShiny), i.Nickname ?? entry.DisplayName ?? entry.Number,
            $"Ур. {i.Level}" + (i.IsShiny ? " · шайни" : "") + (i.IsBusy ? " · занят" : i.IsActive ? " · на столе" : "")))
            .ToList();
        RenderSpeciesPaths(entry);
    }

    // The "where to find" hint as actions (Georgy 23.10): a route opens that expedition, a parent pair
    // presets the nursery. The shiny form uses the same paths, only rarer, so it is one line, not a
    // second copy of every button; it gets its own list only when its paths actually differ.
    private void RenderSpeciesPaths(PokedexSpeciesEntry entry)
    {
        SpeciesPathsPanel.Children.Clear(); SpeciesPathsMessage.Visibility = Visibility.Collapsed;
        SpeciesPathsHeader.Visibility = _speciesPaths is null ? Visibility.Collapsed : Visibility.Visible;
        if (_speciesPaths?.Invoke(entry.SpeciesId, false) is not { } normal) return;
        var owned = _appState.Collection.Individuals.Select(p => p.CurrentSpeciesId).ToHashSet();
        AddPaths(normal, owned);
        var shiny = _speciesPaths(entry.SpeciesId, true);
        var same = shiny is not null && shiny.RouteId == normal.RouteId && shiny.Note == normal.Note
            && shiny.Pairs.Select(p => (p.FirstParentId, p.SecondParentId)).SequenceEqual(normal.Pairs.Select(p => (p.FirstParentId, p.SecondParentId)));
        if (shiny is null) return;
        SpeciesPathsPanel.Children.Add(new TextBlock { Text = "Шайни-форма", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 2) });
        if (same) SpeciesPathsPanel.Children.Add(new TextBlock { Text = "Теми же путями, только реже. Шанс повышает талисман удачи из магазина.", TextWrapping = TextWrapping.Wrap, Style = (Style)FindResource("CaptionTextStyle") });
        else AddPaths(shiny, owned);
    }

    private void AddPaths(SpeciesPaths paths, HashSet<int> owned)
    {
        if (paths.Note is not null)
            SpeciesPathsPanel.Children.Add(new TextBlock { Text = paths.Note, TextWrapping = TextWrapping.Wrap, Style = (Style)FindResource("CaptionTextStyle"), Margin = new Thickness(0, 0, 0, 6) });
        System.Windows.Controls.Button Action(string text, string? detail, bool enabled, string tip)
        {
            var label = new StackPanel();
            label.Children.Add(new TextBlock { Text = text, FontWeight = FontWeights.SemiBold });
            if (detail is not null) label.Children.Add(new TextBlock { Text = detail, FontSize = 12, Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush") });
            return new System.Windows.Controls.Button { Content = label, Style = (Style)FindResource("SecondaryButtonStyle"), Padding = new Thickness(12, 6, 12, 6),
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 6), IsEnabled = enabled, ToolTip = tip };
        }
        if (paths.RouteId is { } routeId)
        {
            var route = Action($"Встретить: {paths.RouteName}", "Откроет экспедиции с этим маршрутом", _openRoute is not null, "Открыть экспедиции с этим маршрутом");
            route.Click += (_, _) => { CloseSpeciesDialog(); _openRoute?.Invoke(routeId); };
            SpeciesPathsPanel.Children.Add(route);
        }
        foreach (var pair in paths.Pairs)
        {
            var missing = new[] { pair.FirstParentId, pair.SecondParentId }.Where(id => !owned.Contains(id)).Distinct().ToArray();
            var detail = missing.Length == 0 ? "Откроет питомник с этой парой" : "Не хватает: " + string.Join(", ", missing.Select(id => $"#{id:D4}"));
            var button = Action($"Питомник: {pair.Label}", detail, _openPair is not null && missing.Length == 0, detail);
            var faces = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            faces.Children.Add(SpeciesPicture(pair.FirstParentId, false, 40));
            faces.Children.Add(new TextBlock { Text = "+", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0), Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush") });
            faces.Children.Add(SpeciesPicture(pair.SecondParentId, false, 40));
            var label = (UIElement)button.Content; button.Content = null; var row = new DockPanel(); DockPanel.SetDock(faces, Dock.Left); row.Children.Add(faces); row.Children.Add(label);
            button.Content = row;
            button.Click += (_, _) =>
            {
                var reason = _openPair?.Invoke(pair.FirstParentId, pair.SecondParentId);
                if (reason is null) { CloseSpeciesDialog(); return; }
                SpeciesPathsMessage.Text = reason; SpeciesPathsMessage.Visibility = Visibility.Visible;
            };
            SpeciesPathsPanel.Children.Add(button);
        }
    }

    private void SpeciesOwned_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: Guid id }) return;
        OpenIndividual(id);
    }

    public Guid? SelectedIndividualId => _detailCard?.IndividualId;

    public void OpenItemSelection()
    {
        HideDetail();
        CloseSpeciesDialog();
        ((System.Windows.Controls.TabControl)((Grid)Content).Children[0]).SelectedIndex = 1;
        IndividualsMode_Click(this, new RoutedEventArgs());
        Refresh();
    }

    public bool OpenIndividual(Guid id)
    {
        var entry = _viewModel.GetEntry(id, DateTimeOffset.UtcNow);
        if (entry is null) return false;
        CloseSpeciesDialog();
        ShowDetail(BuildCard(entry));
        return true;
    }

    private sealed record IndividualChoice(Guid IndividualId, string Label, ImageSource? Sprite = null, string Title = "", string Meta = "");

    // A species or parent picture by the Pokédex's reveal rule: the sprite once that form is known,
    // a flat silhouette where the grid already shows one, otherwise a question mark.
    private FrameworkElement SpeciesPicture(int speciesId, bool shiny, double size)
    {
        var entry = SpeciesLookup(shiny).GetValueOrDefault(speciesId);
        var sprite = GetPackageSprite(speciesId, shiny);
        if (entry is { IsDiscovered: true } && sprite is not null)
        {
            var image = new System.Windows.Controls.Image { Source = sprite, Width = size, Height = size };
            System.Windows.Media.RenderOptions.SetBitmapScalingMode(image, System.Windows.Media.BitmapScalingMode.NearestNeighbor);
            return image;
        }
        if (entry is { HasSilhouette: true } && GetPackageSprite(speciesId, false) is { } shape)
            return new Border { Width = size, Height = size, Background = (System.Windows.Media.Brush)FindResource("NdSilhouetteBrush"),
                OpacityMask = new System.Windows.Media.ImageBrush(shape) { Stretch = System.Windows.Media.Stretch.Uniform } };
        return new TextBlock { Text = "?", Width = size, Height = size, FontSize = size * 0.45, TextAlignment = TextAlignment.Center,
            Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush"), Padding = new Thickness(0, size * 0.2, 0, 0) };
    }

    private Dictionary<int, PokedexSpeciesEntry>? _normalLookup, _shinyLookup;
    private Dictionary<int, PokedexSpeciesEntry> SpeciesLookup(bool shiny) => shiny
        ? _shinyLookup ??= _viewModel.GetSpeciesEntries(true).ToDictionary(e => e.SpeciesId)
        : _normalLookup ??= _viewModel.GetSpeciesEntries(false).ToDictionary(e => e.SpeciesId);

    private void RenderSpeciesForms(int speciesId)
    {
        _normalLookup = _shinyLookup = null;
        SpeciesForms.Children.Clear();
        foreach (var shiny in new[] { false, true })
        {
            var known = SpeciesLookup(shiny).GetValueOrDefault(speciesId)?.IsDiscovered == true;
            var panel = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            var well = new Border { Style = (Style)FindResource("WellStyle"), Height = 132, Child = SpeciesPicture(speciesId, shiny, 104) };
            panel.Children.Add(well);
            panel.Children.Add(new TextBlock { Text = (shiny ? "Шайни-форма" : "Обычная форма") + (known ? "" : " · не открыта"),
                Style = (Style)FindResource("CaptionTextStyle"), HorizontalAlignment = System.Windows.HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });
            SpeciesForms.Children.Add(panel);
        }
    }
    private sealed record SpeciesCard(PokedexSpeciesEntry Entry, ImageSource? SpriteImage)
    {
        public string Number => Entry.Number;
        public string AccessibleName => Entry.AccessibleName;
        public string NameText => Entry.DisplayName ?? "Неизвестный вид";
        public string SubText => Entry.IsDiscovered
            ? $"{string.Join(" · ", Entry.Types.Select(TypeDisplay.GetLabel))} · {RarityName}"
            : Entry.HasSilhouette ? Entry.RouteHint.StartsWith("Эволюция") ? "Открывается эволюцией" : "Можно встретить" : "Ещё не встречался";
        public Visibility WhereVisibility => Visibility.Visible;
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
                $"{speciesPrefix}{activeEntry.DexNumberText()} · {RarityDisplay.GetName(activeEntry.Rarity)} · характер {NatureDisplay.Name(activeEntry.Nature)} · пойман {FormatCaughtDate(activeEntry.CaughtAtUtc)}";
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

    /// The selected rarity combines with the other owned filters and survives refresh ticks.
    private void IndividualRarity_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_isReady) return;
        _rarityFilter = (IndividualRarityCombo.SelectedItem as ComboBoxItem)?.Tag is string tier
            ? Enum.Parse<RarityTier>(tier) : null;
        _pageIndex = 0;
        Refresh();
    }

    private void ResetIndividualFilters_Click(object sender, RoutedEventArgs e)
    {
        PokedexSearchBox.Clear();
        IndividualTypeCombo.SelectedIndex = IndividualRarityCombo.SelectedIndex = PokedexSortCombo.SelectedIndex = 0;
        EvolutionFilterToggle.IsChecked = DuplicatesFilterToggle.IsChecked =
            FavoritesFilterToggle.IsChecked = ShinyFilterToggle.IsChecked = false;
        _pageIndex = 0;
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
            1 => PokedexSortOrder.Level,
            2 => PokedexSortOrder.CatchDate,
            3 => PokedexSortOrder.Care,
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
        if (IndividualTypeCombo.Items.Count == 1)
        {
            foreach (var typeName in new[] { "normal", "grass", "poison", "fire", "water", "electric", "flying", "bug", "ground", "rock", "ice", "psychic", "dark", "ghost", "dragon", "fairy", "steel", "fighting" })
                IndividualTypeCombo.Items.Add(new ComboBoxItem { Content = TypeDisplay.GetLabel(typeName), Tag = typeName });
        }
        var visible = CollectionViewModel.FilterIndividuals(entries, _searchQuery,
            (IndividualTypeCombo.SelectedItem as ComboBoxItem)?.Tag as string, _rarityFilter,
            _hideFullyEvolved, DuplicatesFilterToggle.IsChecked == true, FavoritesFilterToggle.IsChecked == true,
            ShinyFilterToggle.IsChecked == true, _sortOrder);

        // Clamped rather than reset: releasing a Pokémon (or narrowing a filter) can shrink the
        // list past the page being read, and dumping the player back to page 1 for that would
        // be as jarring as doing it on every refresh tick.
        var pageCount = Math.Max(1, (int)Math.Ceiling(visible.Count / (double)_pageSize));
        _pageIndex = Math.Clamp(_pageIndex, 0, pageCount - 1);
        var page = visible.Skip(_pageIndex * _pageSize).Take(_pageSize).ToList();

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
        foreach (var entry in page) EnsureAnimatedFrames(entry.DisplaySpeciesId, entry.IsShiny);

        // The sprite's presence is part of the signature — both the static thumbnail and the
        // animated frames — so a card that rendered its placeholder before the species was
        // cached rebuilds itself once the artwork lands, instead of staying on the placeholder
        // until some unrelated field changes. The nickname is in there for the same reason:
        // renaming from the detail overlay must repaint the card behind it.
        var signature = $"{_rarityFilter}|{_searchQuery}|{_hideFullyEvolved}|{_sortOrder}|{_pageIndex}|" + string.Join("|", page.Select(e =>
            $"{e.IndividualId}:{e.DisplaySpeciesId}:{e.Nickname}:{IndividualDisplay.Discriminator(_appState.Collection.Individuals, e.IndividualId)}:{e.IsActive}:{e.IsFavorite}:{e.IsBusy}:{e.Level}:{e.FirstTraitId}:{e.SecondTraitId}:{e.DescriptionRu}:{e.EvolutionState}:{Math.Round(e.EvolutionFraction ?? -1, 3)}:{TryGetThumbnail(e.DisplaySpeciesId, e.IsShiny) is not null}:{_animatedFrames.ContainsKey((e.DisplaySpeciesId, e.IsShiny))}"));
        if (signature == _pokedexSignature) return;
        _pokedexSignature = signature;

        PokedexGrid.ItemsSource = page.Select(BuildCard).ToList();
        Dispatcher.BeginInvoke(HighlightSelectedRow, System.Windows.Threading.DispatcherPriority.Loaded);
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
        IndividualText = IndividualDisplay.Discriminator(_appState.Collection.Individuals, entry.IndividualId),
        DexNumber = entry.DexNumberText(),
        RarityName = RarityDisplay.GetName(entry.Rarity),
        RarityBrush = RarityDisplay.GetBrush(entry.Rarity),
        EvolutionFraction = entry.EvolutionFraction,
        EvolutionState = entry.EvolutionState,
        CaughtAtText = FormatCaughtDate(entry.CaughtAtUtc),
        NatureText = NatureDisplay.Name(entry.Nature),
        CanSelect = !entry.IsActive && !entry.IsBusy,
        IsShiny = entry.IsShiny,
        IsActive = entry.IsActive,
        IsFavorite = entry.IsFavorite,
        TypeBadges = TypeDisplay.ToBadges(entry.Types),
        EvolutionStages = BuildEvolutionStageCards(entry),
        SpriteImage = TryGetThumbnail(entry.DisplaySpeciesId, entry.IsShiny),
        SpriteFrames = _animatedFrames.GetValueOrDefault((entry.DisplaySpeciesId, entry.IsShiny))
    };

    /// Turns the view-model's spoiler-masked chain into renderable cards. The sprite lookup is
    /// guarded by IsRevealed so an unreached stage's artwork is never even fetched from the
    /// cache — the mask is enforced here as well as in the view model, not just in the template.
    private IReadOnlyList<EvolutionStageCard> BuildEvolutionStageCards(CollectionEntry entry) =>
        entry.EvolutionStages.Select(stage => new EvolutionStageCard
        {
            StageIndex = stage.StageIndex,
            Depth = stage.Depth,
            IsParallel = stage.IsParallel,
            IsRevealed = stage.IsRevealed,
            NameText = stage.IsRevealed ? stage.SpeciesName ?? "???" : stage.SilhouetteSpeciesId is { } number ? $"#{number:D4}" : "???",
            SilhouetteImage = !stage.IsRevealed && stage.SilhouetteSpeciesId is { } shape ? TryGetThumbnail(shape, entry.IsShiny) : null,
            SpriteImage = stage.IsRevealed && stage.SpeciesId is { } id ? TryGetThumbnail(id, entry.IsShiny) : null,
            TypeBadges = TypeDisplay.ToBadges(stage.Types),
            IsCurrent = stage.IsCurrent,
            IsDisplayed = stage.IsDisplayed,
            ShowArrow = !stage.IsParallel && stage.StageIndex < entry.EvolutionStages.Count - 1
        }).ToList();

    /// Fires the one-per-species animated-sprite fetch, at most once per species per session.
    /// Fire-and-forget on purpose: the cards already show their static frame, so there is
    /// nothing to wait for — when frames land, clearing the signature makes the next refresh
    /// tick rebuild the grid and pick them up.
    private void EnsureAnimatedFrames(int speciesId, bool isShiny)
    {
        if (_getAnimatedFrames is null) return;
        if (!_animatedFetchStarted.Add((speciesId, isShiny))) return;

        _ = LoadAsync();

        async Task LoadAsync()
        {
            IReadOnlyList<byte[]>? frames;
            try
            {
                frames = await _getAnimatedFrames(speciesId, isShiny);
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
            _animatedFrames[(speciesId, isShiny)] = await Task.Run(() => frames.Select(SpriteImages.Decode).ToList());
            _pokedexSignature = string.Empty; // force the next tick to rebuild with the frames
        }
    }

    /// Attaches an animator to one realized card Image. Mirrors the hero sprite's setup, just
    /// resolved per container: the frames come from the card the Image is bound to.
    private void PokedexSprite_Loaded(object sender, RoutedEventArgs e)
    {
        if (_presentationSuspended) return;
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
        if (e.Key != System.Windows.Input.Key.Escape) return;
        if (_localConfirmation is not null) { CompleteLocalConfirmation(false); e.Handled = true; return; }
        if (SpeciesDialog.Visibility == Visibility.Visible) { CloseSpeciesDialog(); e.Handled = true; return; }
        if (DetailOverlay.Visibility != Visibility.Visible) return;
        if (DetailNicknameEditor.Visibility == Visibility.Visible) { DetailNicknameCancel_Click(this, e); e.Handled = true; return; }
        HideDetail();
        e.Handled = true;
    }

    // ======================= Pokédex detail overlay =======================
    // Renders the PokedexCard it is handed and routes every mutation through the same
    // SelectIndividual/ToggleFavorite methods the rest of this view uses.

    /// The card currently on show, or null when the overlay is hidden.
    private PokedexCard? _detailCard;
    private readonly HashSet<Guid> _rehomingIndividuals = [];
    private int _detailGeneration;
    private string _evolutionChoicesSignature = string.Empty;
    private EvolutionPreview? _detailEvolutionPreview;
    private readonly HashSet<Guid> _evolvingIndividuals = [];
    private RetrainingPreview? _detailRetrainingPreview;
    private readonly HashSet<Guid> _retrainingIndividuals = [];
    private (Guid IndividualId, int Slot, Guid OperationId)? _pendingRetraining;

    private bool IsCurrentDetail(Guid id, int generation) =>
        _detailGeneration == generation && DetailOverlay.Visibility == Visibility.Visible &&
        _detailCard?.IndividualId == id;

    /// Local mirror of the pin so the star can flip immediately on click. The grid rebuilds
    /// from the model, but the overlay holds an immutable snapshot card — re-reading it would
    /// show the stale value.
    private bool _detailIsFavorite;

    private SpriteAnimator? _detailAnimator;


    private void ShowDetail(PokedexCard card)
    {
        _detailGeneration++;
        _detailCard = card;
        SetEvolutionSurface(false);
        DetailErrorText.Text = string.Empty;
        _detailIsFavorite = card.IsFavorite;
        DetailFavoriteButton.IsEnabled = true;
        RefreshDetailRehome();

        DetailDexNumberText.Text = card.DexNumber;
        DetailSpeciesNameText.Text = card.DisplayName;
        DetailSpeciesSubtitleText.Text = card.SpeciesSubtitle;
        DetailSpeciesSubtitleText.Visibility = card.SpeciesSubtitleVisibility;

        DetailNicknameBox.Text = card.Nickname ?? string.Empty;
        DetailNicknameEditor.Visibility = Visibility.Collapsed;
        DetailNicknameEditor.IsEnabled = true;
        DetailNameButton.Visibility = Visibility.Visible;
        DetailRetrainingEditor.Visibility = Visibility.Collapsed;
        DetailRetrainingToggle.Content = "Переобучение";
        System.Windows.Automation.AutomationProperties.SetName(DetailNameButton, "Изменить кличку: " + card.DisplayName);

        DetailNatureText.Text = "Характер: " + card.NatureText;
        DetailCaughtAtText.Text = "Пойман: " + card.CaughtAtText;
        DetailLevelText.Text = card.LevelText;
        DetailBusyText.Text = card.IsBusy ? card.AvailabilityText : string.Empty;
        DetailTraitsText.Text = "Особенности: " + string.Join(" · ",
            new[] { card.FirstTraitId, card.SecondTraitId }.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => TraitLabel(id) + " — " + TraitEffect(id)));
        DetailTraitsText.Visibility = card.FirstTraitId is null && card.SecondTraitId is null ? Visibility.Collapsed : Visibility.Visible;
        DetailDescriptionText.Text = card.DescriptionRu ?? string.Empty;
        DetailFactSection.Visibility = card.DescriptionRu is null ? Visibility.Collapsed : Visibility.Visible;
        DetailEvolutionBar.Value = card.EvolutionBarValue;
        DetailEvolutionBar.Opacity = card.EvolutionBarOpacity;
        DetailEvolutionBar.ToolTip = card.EvolutionToolTip;
        DetailEvolutionText.Text = card.EvolutionState == EvolutionProgressState.InProgress ? "Накопленный уход · точные условия ниже" : card.EvolutionLabel;
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
        RenderEvolutionGraph(card);
        _evolutionChoicesSignature = string.Empty;
        RefreshDetailEvolution();
        RefreshDetailRetraining();
        RefreshDetailItems();

        DetailShinyBadge.Visibility = card.ShinyVisibility;
        DetailActiveBadge.Visibility = card.ActiveBadgeVisibility;

        // An already-active pet can't be selected again.
        DetailSelectButton.IsEnabled = card.CanSelect;
        DetailSelectButton.Visibility = card.ActiveBadgeVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        DetailSelectButton.ToolTip = card.CanSelect ? null : "Этот покемон уже на рабочем столе";

        ApplyDetailFavoriteVisuals();
        ShowDetailSprite(card);

        DetailTrainerNote.Text = TrainerNote(card);
        DetailOverlay.Visibility = Visibility.Visible;
        DetailScroll.ScrollToTop();
        LayoutMasterDetail();
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
        DetailEvolutionBar.Value = Math.Clamp(preview.CareOwned / (double)Math.Max(1, preview.CareRequired), 0, 1);
        var enoughCare = preview.CareOwned >= preview.CareRequired;
        DetailEvolutionText.Text = EvolutionCareStateText.Text = enoughCare ? "Достаточно" : "Продолжай заботиться о питомце";
        EvolutionCareBar.Value = DetailEvolutionBar.Value;
        DetailEvolutionCostText.Text = $"× {preview.BerryCost} · в запасе {preview.BerriesOwned}";
        var berryPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "final", "assets", "shop", preview.BerryId + ".png");
        EvolutionBerryImage.Source = System.IO.File.Exists(berryPath) ? PokeCorner.App.Imaging.SpriteImages.Decode(System.IO.File.ReadAllBytes(berryPath)) : null;
        DetailEvolutionBlockedText.Text = preview.Branches.Any(x => !x.Available)
            ? "Недоступные ветви отмечены в списке; вариант без ресурса не подменяется."
            : string.Empty;
        UpdateEvolutionActionState();
        var choicePossible = preview.Branches.Count(x => x.Available) > 1;
        DetailEvolutionStoneToggle.Visibility = DetailEvolutionBranchPicker.Visibility = choicePossible ? Visibility.Visible : Visibility.Collapsed;
        DetailEvolutionStoneToggle.IsEnabled = choicePossible && preview.StonesOwned > 0 && !_detailPending;
        if (!choicePossible || preview.StonesOwned == 0) DetailEvolutionStoneToggle.IsChecked = false;
        DetailStoneStockText.Text = $"В запасе: {preview.StonesOwned}";
        DetailEvolutionBranchPicker.IsEnabled = DetailEvolutionStoneToggle.IsChecked == true && !_detailPending;
        var signature = string.Join("|", preview.Branches.Select(x => $"{x.SpeciesId}:{x.Available}"))
            + ":" + string.Join("|", (_detailCard.IsShiny ? _appState.ShinySpeciesIds : _appState.NormalSpeciesIds).Order());
        if (signature == _evolutionChoicesSignature) return;
        var selected = (DetailEvolutionBranchPicker.SelectedItem as ComboBoxItem)?.Tag as int?;
        DetailEvolutionBranchPicker.Items.Clear();
        DetailEvolutionBranchPicker.Items.Add(new ComboBoxItem { Content = "Случайная доступная ветвь · без камня" });
        foreach (var branch in preview.Branches)
        {
            var known = (_detailCard.IsShiny ? _appState.ShinySpeciesIds : _appState.NormalSpeciesIds).Contains(branch.SpeciesId);
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
        RenderEvolutionTargets(preview);
        UpdateEvolutionActionState();
    }

    private void DetailEvolutionBranchPicker_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateEvolutionActionState();

    private void UpdateEvolutionActionState()
    {
        if (_detailEvolutionPreview is null) return;
        var branch = SelectedEvolutionBranch;
        var needsStone = branch is not null && _detailEvolutionPreview.Branches.Count(x => x.Available) > 1;
        var reason = _detailEvolutionPreview.Reason
            ?? (needsStone && _detailEvolutionPreview.StonesOwned == 0 ? "Для выбора ветви нужен камень эволюции." : null);
        DetailEvolutionReasonText.Text = reason?.StartsWith("Нужно ещё ", StringComparison.Ordinal) == true && reason.EndsWith(" ухода.", StringComparison.Ordinal)
            ? "Продолжай гладить и кормить питомца." : reason ?? string.Empty;
        EvolutionRequiredStoneImage.Visibility = EvolutionRequiredStoneText.Visibility = needsStone ? Visibility.Visible : Visibility.Collapsed;
        EvolutionRequiredStoneText.Text = "× 1";
        RenderEvolutionTargets(_detailEvolutionPreview);
        DetailEvolveButton.IsEnabled = reason is null && !_detailPending && (_detailCard is null || !_evolvingIndividuals.Contains(_detailCard.IndividualId));
    }

    private async void DetailEvolveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_detailPending || _detailCard is null || _previewEvolution is null || _evolveIndividual is null) return;
        var id = _detailCard.IndividualId; var generation = _detailGeneration; var branch = SelectedEvolutionBranch;
        var preview = _previewEvolution(id);
        if (preview.Reason is not null) { DetailErrorText.Text = preview.Reason; return; }
        if (branch is not null && !preview.Branches.Any(x => x.SpeciesId == branch && x.Available)) return;
        _detailPending = true; RefreshDetailItems(); UpdateEvolutionActionState();
        try
        {
            var choice = branch is null ? "случайную доступную ветвь" : $"ветвь #{branch:D4}";
            if (!await ConfirmAsync("Подтвердить эволюцию?", $"Потратить {preview.BerryCost} ягод {preview.BerryId} на {choice}?"
                + (branch is not null && preview.Branches.Count(x => x.Available) > 1 ? " Будет потрачен камень выбора." : ""), "Эволюционировать")) return;
            if (!IsCurrentDetail(id, generation)) return;
            if (_pendingEvolution is not { } pending || pending.Pet != id || pending.Branch != branch)
                _pendingEvolution = (id, branch, Guid.NewGuid());
            _evolvingIndividuals.Add(id);
            var result = await _evolveIndividual(id, branch, _pendingEvolution.Value.Operation);
            if (result.Success) _pendingEvolution = null;
            _onStateChanged(); Refresh();
            if (IsCurrentDetail(id, generation)) { OpenIndividual(id); DetailErrorText.Text = result.Message; }
        }
        catch (Exception error) { if (IsCurrentDetail(id, generation)) DetailErrorText.Text = error.Message; }
        finally { _detailPending = false; _evolvingIndividuals.Remove(id); Refresh(); }
    }

    private void DetailEvolutionShopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_detailEvolutionPreview is { } preview && preview.BerriesOwned < preview.BerryCost)
            _buyBerries?.Invoke(preview.BerryId, preview.BerryCost);
        _navigate?.Invoke(MenuSection.Shop);
    }
    private void DetailEvolutionRouteButton_Click(object sender, RoutedEventArgs e) => _navigate?.Invoke(MenuSection.Expeditions);

    private void RefreshDetailRetraining()
    {
        if (_detailCard is null || _previewRetraining is null || _retrainIndividual is null ||
            !_appState.Collection.Individuals.Any(x => x.Id == _detailCard.IndividualId))
        {
            DetailRetrainingSection.Visibility = Visibility.Collapsed;
            return;
        }
        DetailRetrainingSection.Visibility = Visibility.Visible;
        var selected = (DetailRetrainingSlotPicker.SelectedItem as ComboBoxItem)?.Tag as int? ?? 1;
        DetailRetrainingSlotPicker.SelectionChanged -= DetailRetrainingSlotPicker_SelectionChanged;
        DetailRetrainingSlotPicker.Items.Clear();
        DetailRetrainingSlotPicker.Items.Add(new ComboBoxItem { Content = $"Первый слот · {TraitLabel(_detailCard.FirstTraitId ?? "")}", Tag = 1 });
        if (!string.IsNullOrWhiteSpace(_detailCard.SecondTraitId))
            DetailRetrainingSlotPicker.Items.Add(new ComboBoxItem { Content = $"Второй слот · {TraitLabel(_detailCard.SecondTraitId)}", Tag = 2 });
        DetailRetrainingSlotPicker.SelectedItem = DetailRetrainingSlotPicker.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(x => x.Tag is int slot && slot == selected) ?? DetailRetrainingSlotPicker.Items[0];
        DetailRetrainingSlotPicker.SelectionChanged += DetailRetrainingSlotPicker_SelectionChanged;
        selected = (int)((ComboBoxItem)DetailRetrainingSlotPicker.SelectedItem).Tag;
        var preview = _previewRetraining(_detailCard.IndividualId, selected);
        _detailRetrainingPreview = preview;
        DetailRetrainingCostText.Text = "Стоимость: " + string.Join(" · ",
            preview.Costs.Select(x => $"{ExpeditionView.BerryName(x.BerryId)} ×{x.Required} (в запасе {x.Owned})"));
        DetailRetrainingOutcomesText.Text = "Возможные новые особенности: " +
            string.Join(" · ", preview.Outcomes.Select(TraitLabel));
        DetailRetrainingReasonText.Text = preview.Reason ?? string.Empty;
        DetailRetrainButton.IsEnabled = preview.Reason is null && !_retrainingIndividuals.Contains(_detailCard.IndividualId);
        DetailRetrainingShopButton.Visibility = preview.Costs.Any(x => x.Owned < x.Required)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DetailRetrainingSlotPicker_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        RefreshDetailRetraining();

    private async void DetailRetrainButton_Click(object sender, RoutedEventArgs e)
    {
        if (_detailPending || _detailCard is null || _previewRetraining is null || _retrainIndividual is null) return;
        var id = _detailCard.IndividualId;
        var generation = _detailGeneration;
        var slot = (DetailRetrainingSlotPicker.SelectedItem as ComboBoxItem)?.Tag as int? ?? 1;
        var preview = _previewRetraining(id, slot);
        if (preview.Reason is not null) { DetailRetrainingReasonText.Text = preview.Reason; return; }
        var cost = string.Join(", ", preview.Costs.Select(x => $"{ExpeditionView.BerryName(x.BerryId)} ×{x.Required}"));
        _detailPending = true;
        DetailRetrainButton.IsEnabled = false;
        try
        {
        if (!await ConfirmAsync("Подтвердить переобучение?",
            $"Потратить {cost} и случайно заменить {TraitLabel(preview.CurrentTraitId!)}? Возможные результаты: " +
            string.Join(", ", preview.Outcomes.Select(TraitLabel)), "Переобучить")) return;
        if (!IsCurrentDetail(id, generation)) return;
        if (_pendingRetraining is not { } pending || pending.IndividualId != id || pending.Slot != slot)
            _pendingRetraining = (id, slot, Guid.NewGuid());
        var operationId = _pendingRetraining.Value.OperationId;
        _retrainingIndividuals.Add(id);
        DetailRetrainButton.IsEnabled = false;
            var result = await _retrainIndividual(id, slot, operationId);
            _pendingRetraining = null;
            Refresh();
            if (IsCurrentDetail(id, generation))
            {
                OpenIndividual(id);
                DetailErrorText.Text = result.Message;
            }
        }
        catch (Exception error)
        {
            if (IsCurrentDetail(id, generation)) DetailRetrainingReasonText.Text = error.Message;
        }
        finally
        {
            _detailPending = false;
            _retrainingIndividuals.Remove(id);
            if (_detailCard is not null && DetailOverlay.Visibility == Visibility.Visible) RefreshDetailRetraining();
        }
    }

    private void DetailRetrainingShopButton_Click(object sender, RoutedEventArgs e)
    {
        var shortage = _detailRetrainingPreview?.Costs.FirstOrDefault(x => x.Owned < x.Required);
        if (shortage is null) return;
        _buyBerries?.Invoke(shortage.BerryId, shortage.Required);
        _navigate?.Invoke(MenuSection.Shop);
    }

    private void HideDetail()
    {
        CompleteLocalConfirmation(false);
        _detailGeneration++;
        DetailOverlay.Visibility = Visibility.Collapsed;
        _detailCard = null;
        LayoutMasterDetail();

        // One DispatcherTimer per animator, so it must not outlive the card it was driving.
        _detailAnimator?.Stop();
        _detailAnimator = null;
    }

    /// Click-outside-to-close. The card marks its own mouse-down Handled (see
    /// DetailCard_MouseLeftButtonDown), so this only runs for a click on the dimmed backdrop.
    /// Handled here too, so a dismiss never also counts as a press on anything else.
    private void DetailOverlay_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (MasterDetail) return;
        HideDetail();
        e.Handled = true;
    }

    /// Stops a click inside the card from bubbling to the backdrop and closing the thing the
    /// player is trying to interact with.
    private void DetailCard_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        e.Handled = true;

    private void ShowDetailSprite(PokedexCard card)
    {
        if (_presentationSuspended) return;
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
        System.Windows.Automation.AutomationProperties.SetName(DetailFavoriteButton, (string)DetailFavoriteButton.ToolTip);
        DetailFavoriteBadge.Visibility = _detailIsFavorite ? Visibility.Visible : Visibility.Collapsed;
    }

    /// Stays open, unlike the other two: pinning is a small reversible tweak, and closing the
    /// card under the player after it would make a second thought cost another two clicks.
    private async void DetailFavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_detailPending || _detailCard is null) return;
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
            if (entry is not null) RenderEvolutionGraph(BuildCard(entry));
            else HideDetail();
        }

        Refresh();
    }

    private void DetailName_Click(object sender, RoutedEventArgs e)
    {
        if (_detailPending || _detailCard is null) return;
        DetailNicknameBox.Text = LiveNickname(_detailCard.IndividualId) ?? string.Empty;
        DetailSpeciesSubtitleText.Text = _detailCard.SpeciesName;
        DetailSpeciesSubtitleText.Visibility = Visibility.Visible;
        DetailNameButton.Visibility = Visibility.Collapsed;
        DetailNicknameEditor.Visibility = Visibility.Visible;
        DetailErrorText.Text = string.Empty;
        DetailNicknameBox.Focus();
        DetailNicknameBox.SelectAll();
    }

    private void DetailNicknameCancel_Click(object sender, RoutedEventArgs e)
    {
        if (_detailPending) return;
        DetailNicknameEditor.Visibility = Visibility.Collapsed;
        DetailNameButton.Visibility = Visibility.Visible;
        DetailSpeciesSubtitleText.Text = _detailCard?.SpeciesName ?? string.Empty;
        DetailSpeciesSubtitleText.Visibility = _detailCard is not null && LiveNickname(_detailCard.IndividualId) is not null ? Visibility.Visible : Visibility.Collapsed;
        DetailErrorText.Text = string.Empty;
        DetailNameButton.Focus();
    }

    private async void DetailNickname_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter) return;
        e.Handled = true;
        await SaveNicknameAsync();
    }

    private async void DetailNicknameSave_Click(object sender, RoutedEventArgs e) => await SaveNicknameAsync();

    private async Task SaveNicknameAsync()
    {
        if (_detailPending || _detailCard is null || DetailNicknameEditor.Visibility != Visibility.Visible) return;
        var text = DetailNicknameBox.Text;
        if (text.Length > 20 || text.Any(char.IsControl))
        {
            DetailErrorText.Text = "Кличка должна быть не длиннее 20 символов и без переносов строк.";
            return;
        }
        var id = _detailCard.IndividualId;
        var generation = _detailGeneration;
        _detailPending = true;
        DetailNicknameEditor.IsEnabled = false;
        try
        {
            if (_commitState is null) _viewModel.SetNickname(id, text);
            else if (!await CommitMutationAsync(next =>
            {
                var individual = next.Individuals.FirstOrDefault(i => i.Id == id)
                    ?? throw new InvalidOperationException("Этот покемон больше не найден.");
                individual.Nickname = text;
            }, id, generation)) return;
            if (!IsCurrentDetail(id, generation)) return;
            var nickname = LiveNickname(id);
            DetailSpeciesNameText.Text = nickname ?? _detailCard.SpeciesName;
            DetailSpeciesSubtitleText.Text = nickname is null ? string.Empty : _detailCard.SpeciesName;
            DetailSpeciesSubtitleText.Visibility = nickname is null ? Visibility.Collapsed : Visibility.Visible;
            System.Windows.Automation.AutomationProperties.SetName(DetailNameButton, "Изменить кличку: " + DetailSpeciesNameText.Text);
            DetailNicknameEditor.Visibility = Visibility.Collapsed;
            DetailNameButton.Visibility = Visibility.Visible;
            DetailErrorText.Text = string.Empty;
            DetailNameButton.Focus();
            Refresh();
        }
        finally
        {
            _detailPending = false;
            if (IsCurrentDetail(id, generation))
            {
                DetailNicknameEditor.IsEnabled = true;
                if (DetailNicknameEditor.Visibility == Visibility.Visible) DetailNicknameBox.Focus();
            }
        }
    }

    private void DetailRetrainingToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_detailPending) return;
        var show = DetailRetrainingEditor.Visibility != Visibility.Visible;
        DetailRetrainingEditor.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        DetailRetrainingToggle.Content = show ? "Скрыть переобучение" : "Переобучение";
        if (show) DetailRetrainingSlotPicker.Focus();
    }

    /// This individual's nickname straight from the model, or null. Read live rather than off
    /// the overlay's snapshot card, which predates any rename made while the card is open.
    private string? LiveNickname(Guid individualId) =>
        _appState.Collection.Individuals.FirstOrDefault(i => i.Id == individualId)?.Nickname;

    /// Both of these close: they change which cards exist (release) or which one is marked
    /// active (select), so the snapshot the overlay is rendering is stale the moment they run.
    private async void DetailSelectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_detailPending || _detailCard is null || _appState.BusyIndividualIds.Contains(_detailCard.IndividualId)) return;
        var id = _detailCard.IndividualId;
        var generation = _detailGeneration;
        if (_commitState is null) SelectIndividual(id);
        else
        {
            DetailSelectButton.IsEnabled = false;
            var committed = await CommitMutationAsync(next =>
            {
                if (!next.Individuals.Any(i => i.Id == id)) throw new InvalidOperationException("Этот покемон больше не найден.");
                if (ExpeditionService.BusyIndividualIds(next).Contains(id)) throw new InvalidOperationException("Питомец занят в походе или питомнике.");
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

    private void RefreshDetailRehome()
    {
        if (_detailCard is null || _previewRehome is null || _rehomeIndividual is null)
        {
            DetailRehomeButton.Visibility = Visibility.Visible;
            DetailRehomeButton.IsEnabled = false;
            DetailRehomeReasonText.Visibility = Visibility.Collapsed;
            return;
        }
        var quote = _previewRehome(_detailCard.IndividualId);
        DetailRehomeButton.Visibility = Visibility.Visible;
        DetailRehomeReasonText.Visibility = Visibility.Visible;
        // The core's wording speaks about a replacement the player never chose here; say what to do instead.
        DetailRehomeReasonText.Text = _detailCard.ActiveBadgeVisibility == Visibility.Visible && quote.Reason is not null
            ? "Питомца с рабочего стола отдать нельзя. Сначала поставьте на стол другого."
            : quote.Reason ?? $"Передача: +{quote.Coins} монет. Необратимо.";
        DetailRehomeButton.IsEnabled = quote.Reason is null && !_detailPending && !_rehomingIndividuals.Contains(_detailCard.IndividualId);
    }

    private async void DetailRehomeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_detailPending || _detailCard is null || _previewRehome is null || _rehomeIndividual is null) return;
        var id = _detailCard.IndividualId; var generation = _detailGeneration;
        var quote = _previewRehome(id);
        if (quote.Reason is not null) { DetailErrorText.Text = quote.Reason; return; }
        var nickname = LiveNickname(id);
        var name = nickname is null ? _detailCard.SpeciesName : $"{nickname} ({_detailCard.SpeciesName})";
        _detailPending = true; DetailRehomeButton.IsEnabled = false;
        try
        {
            if (!await ConfirmAsync("Найти новый дом?", $"Передать {name} за {quote.Coins} монет? Покемон покинет коллекцию навсегда. Открытая форма останется в покедексе. Это нельзя отменить.", "Передать", destructive: true)) return;
            if (!IsCurrentDetail(id, generation)) return;
            var current = _previewRehome(id);
            if (current.Reason is not null || current.Coins != quote.Coins)
            { DetailErrorText.Text = current.Reason ?? "Условия передачи изменились. Проверьте сумму ещё раз."; return; }
            if (_pendingRehome is not { } pending || pending.Pet != id) _pendingRehome = (id, Guid.NewGuid());
            _rehomingIndividuals.Add(id);
            var result = await _rehomeIndividual(id, _pendingRehome.Value.Operation);
            if (result.Success)
            {
                _pendingRehome = null;
                // Publication removed this GUID. Invalidate its card before any XP/item refresh or callback.
                if (_detailCard?.IndividualId == id) HideDetail();
            }
            _onStateChanged(); Refresh();
            if (!result.Success && IsCurrentDetail(id, generation)) DetailErrorText.Text = result.Message;
        }
        catch (Exception error) { if (IsCurrentDetail(id, generation)) DetailErrorText.Text = error.Message; }
        finally { _detailPending = false; _rehomingIndividuals.Remove(id); Refresh(); }
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

    /// Cache-only sprite lookup, safe on the UI thread. A species with no usable cached PNG
    /// returns null (the card shows its pokéball placeholder) and is fetched in the background.
    private ImageSource? TryGetThumbnail(int speciesId, bool isShiny = false)
    {
        if (_getSpeciesAssetPath is not null) return GetPackageSprite(speciesId, isShiny);
        if (isShiny) return null; // Legacy PNG cache contains only ordinary artwork.
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

    private void IndividualType_Changed(object sender, SelectionChangedEventArgs e) => IndividualExtraFilter_Changed(sender, e);
    private void DetailBack_Click(object sender, RoutedEventArgs e)
    {
        if (EvolutionSurface.Visibility == Visibility.Visible) SetEvolutionSurface(false);
        else HideDetail();
    }
    private void DetailConditions_Click(object sender, RoutedEventArgs e) => SetEvolutionSurface(true);
    private void DetailConditionsBack_Click(object sender, RoutedEventArgs e) => SetEvolutionSurface(false);
    private void SetEvolutionSurface(bool show)
    {
        if (show && DetailEvolutionActionSection.Visibility != Visibility.Visible) return;
        EvolutionFooter.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        EvolutionSurface.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        DetailPersonalGrid.Visibility = DetailIdentityHeader.Visibility = DetailRehomeSection.Visibility =
            show ? Visibility.Collapsed : Visibility.Visible;
        DetailScroll.ScrollToTop();
    }
    private void SpeciesBack_Click(object sender, RoutedEventArgs e) => CloseSpeciesDialog();
    private void CloseSpeciesDialog()
    {
        SpeciesDialog.Visibility = Visibility.Collapsed;
        _selectedSpeciesId = null;
        SpeciesGrid.SelectedIndex = -1;
        LayoutMasterDetail();
    }
    private void SpeciesWhere_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: int id }) return;
        var entry = SpeciesEntries().FirstOrDefault(x => x.SpeciesId == id);
        _selectedSpeciesId = id; ShowSpeciesDetail(entry); SpeciesDialog.Visibility = Visibility.Visible;
        if (entry is not null && _speciesPaths is null) SpeciesDetailDescription.Text = _whereFind?.Invoke(id, _shinyOnlySpecies.Contains(id)) ?? entry.RouteHint;
    }
    private Task<bool> ConfirmAsync(string title, string body, string caption, bool destructive = false)
    {
        if (destructive && _confirmDestructive is not null) return _confirmDestructive(title, body, caption);
        if (_confirm is not null) return _confirm(title, body, caption);
        if (System.Windows.Window.GetWindow(this) is AppMenuWindow shell) return destructive ? shell.ConfirmDestructiveAsync(title, body, caption) : shell.ConfirmAsync(title, body, caption);
        if (_localConfirmation is not null) return Task.FromResult(false);
        _localConfirmation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = _localConfirmation.Task; LocalConfirmationHost.Present(title, body, caption, destructive); return task;
    }
    private void CompleteLocalConfirmation(bool result)
    {
        var pending = _localConfirmation; _localConfirmation = null;
        LocalConfirmationHost.Visibility = Visibility.Collapsed; pending?.TrySetResult(result);
    }
    private int? SelectedEvolutionBranch => DetailEvolutionStoneToggle.IsChecked == true
        ? (DetailEvolutionBranchPicker.SelectedItem as ComboBoxItem)?.Tag as int? : null;
    private void EvolutionStone_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isReady) return;
        if (DetailEvolutionStoneToggle.IsChecked != true) DetailEvolutionBranchPicker.SelectedIndex = 0;
        else if ((DetailEvolutionBranchPicker.SelectedItem as ComboBoxItem)?.Tag is not int)
            DetailEvolutionBranchPicker.SelectedItem = DetailEvolutionBranchPicker.Items.Cast<ComboBoxItem>().FirstOrDefault(x => x.IsEnabled && x.Tag is int);
        DetailEvolutionBranchPicker.IsEnabled = DetailEvolutionStoneToggle.IsChecked == true && !_detailPending;
        UpdateEvolutionActionState();
    }
    private void RefreshDetailItems()
    {
        if (_detailCard is null) return;
        if (!_appState.Collection.Individuals.Any(i => i.Id == _detailCard.IndividualId)) { HideDetail(); return; }
        var xp = _xpProgress?.Invoke(_detailCard.IndividualId);
        DetailXpText.Text = xp?.Text ?? $"Опыт · уровень {_detailCard.Level}"; DetailXpBar.Value = xp?.Fraction ?? 0;
        foreach (var item in new[] { "care-booster", "xp-potion" })
        {
            var preview = _previewItem?.Invoke(_detailCard.IndividualId, item);
            var text = item == "care-booster" ? DetailCareBoosterText : DetailXpPotionText;
            var button = item == "care-booster" ? DetailCareBoosterButton : DetailXpPotionButton;
            text.Text = preview is null ? (item == "care-booster" ? "Бустер ухода" : "Зелье опыта") + " · нет в запасе"
                : $"{preview.Name} · в запасе: {preview.Stock}\n{preview.Effect}" + (preview.Reason is null ? "" : $"\n{preview.Reason}");
            button.IsEnabled = !_detailPending && preview is { Stock: > 0, Reason: null } && _useItem is not null;
        }
    }
    private async void DetailItem_Click(object sender, RoutedEventArgs e)
    {
        if (_detailPending || _detailCard is null || _previewItem is null || _useItem is null || sender is not System.Windows.Controls.Button { Tag: string item }) return;
        var id = _detailCard.IndividualId; var generation = _detailGeneration; var preview = _previewItem(id, item);
        if (preview.Reason is not null || preview.Stock < 1) return;
        _detailPending = true; RefreshDetailItems();
        try
        {
            if (!await ConfirmAsync(preview.Name, $"Применить к {_detailCard.DisplayName}? {preview.Effect} Будет потрачен 1 предмет.", "Применить")) return;
            if (!IsCurrentDetail(id, generation)) return;
            var key = (id, item);
            if (!_pendingItems.TryGetValue(key, out var operation)) _pendingItems[key] = operation = Guid.NewGuid();
            var result = await _useItem(id, item, operation);
            if (result.Success) _pendingItems.Remove(key);
            _onStateChanged(); Refresh();
            if (IsCurrentDetail(id, generation)) { OpenIndividual(id); DetailErrorText.Text = result.Message; }
        }
        catch (Exception error) { if (IsCurrentDetail(id, generation)) DetailErrorText.Text = error.Message; }
        finally { _detailPending = false; Refresh(); }
    }
    private void RenderEvolutionGraph(PokedexCard card)
    {
        var root = card.EvolutionStages.FirstOrDefault();
        var others = card.EvolutionStages.Skip(1).ToArray();
        var graph = new Grid();
        graph.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
        graph.ColumnDefinitions.Add(new ColumnDefinition());
        if (root is not null) graph.Children.Add(EvolutionTile(root, 46));
        var options = new System.Windows.Controls.Primitives.UniformGrid { Columns = others.Length > 3 ? 4 : Math.Max(1, others.Length) };
        Grid.SetColumn(options, 1);
        foreach (var stage in others) options.Children.Add(EvolutionTile(stage, 38));
        graph.Children.Add(options);
        DetailEvolutionChainStrip.ItemsSource = new[] { graph };
    }

    private Border EvolutionTile(EvolutionStageCard stage, double imageSize)
    {
        var contents = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        if (stage.SpriteImage is not null) contents.Children.Add(new System.Windows.Controls.Image { Source = stage.SpriteImage, Width = imageSize, Height = imageSize });
        else if (stage.SilhouetteImage is { } shape) contents.Children.Add(new Border
        {
            Width = imageSize, Height = imageSize, Background = ThemeBrushes.Get("NdSilhouetteBrush"),
            OpacityMask = new ImageBrush(shape) { Stretch = Stretch.Uniform }
        });
        else contents.Children.Add(new TextBlock { Text = "?", Height = imageSize, FontSize = 26, TextAlignment = TextAlignment.Center });
        contents.Children.Add(new TextBlock { Text = stage.NameText, FontSize = 10.5, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
        var tile = new Border { Child = contents, Padding = new Thickness(3), Margin = new Thickness(2), BorderThickness = new Thickness(1),
            BorderBrush = stage.BorderBrush, CornerRadius = new CornerRadius(6), DataContext = stage };
        if (stage.IsRevealed) tile.MouseLeftButtonDown += DetailEvolutionStage_Click;
        return tile;
    }

    private void RenderEvolutionTargets(EvolutionPreview preview)
    {
        if (_detailCard is null) return;
        var graph = new Grid();
        graph.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(135) });
        graph.ColumnDefinitions.Add(new ColumnDefinition());
        var current = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        current.Children.Add(new System.Windows.Controls.Image { Source = _detailCard.SpriteImage, Width = 90, Height = 90 });
        current.Children.Add(new TextBlock { Text = _detailCard.SpeciesName, TextAlignment = TextAlignment.Center });
        graph.Children.Add(current);
        var options = new System.Windows.Controls.Primitives.UniformGrid { Columns = preview.Branches.Count > 3 ? 4 : Math.Max(1, preview.Branches.Count) };
        Grid.SetColumn(options, 1);
        foreach (var branch in preview.Branches)
        {
            var known = (_detailCard.IsShiny ? _appState.ShinySpeciesIds : _appState.NormalSpeciesIds).Contains(branch.SpeciesId);
            var sprite = TryGetThumbnail(branch.SpeciesId, _detailCard.IsShiny);
            var stage = new EvolutionStageCard { StageIndex = 1, IsRevealed = known, NameText = known ? branch.Name : $"#{branch.SpeciesId:D4}",
                SpriteImage = known ? sprite : null, SilhouetteImage = known ? null : sprite,
                TypeBadges = [], IsCurrent = false, IsDisplayed = false, ShowArrow = false };
            var tile = EvolutionTile(stage, 54);
            tile.Tag = branch.SpeciesId;
            tile.IsEnabled = branch.Available && !_detailPending;
            if (!branch.Available)
                ((StackPanel)tile.Child).Children.Add(new TextBlock { Text = "Недоступно", FontSize = 10.5, TextAlignment = TextAlignment.Center });
            tile.ToolTip = branch.Reason;
            tile.Background = SelectedEvolutionBranch == branch.SpeciesId ? ThemeBrushes.Get("NdRaisedBrush") : ThemeBrushes.Get("CardBackgroundBrush");
            tile.BorderBrush = SelectedEvolutionBranch == branch.SpeciesId ? ThemeBrushes.Get("UiForestBrush") : ThemeBrushes.Get("CardBorderBrush");
            tile.MouseLeftButtonDown -= DetailEvolutionStage_Click;
            tile.MouseLeftButtonDown += (_, e) => {
                if (DetailEvolutionStoneToggle.IsEnabled)
                {
                    DetailEvolutionStoneToggle.IsChecked = true;
                    DetailEvolutionBranchPicker.SelectedItem = DetailEvolutionBranchPicker.Items.Cast<ComboBoxItem>().First(x => Equals(x.Tag, branch.SpeciesId));
                }
                e.Handled = true;
            };
            options.Children.Add(tile);
        }
        graph.Children.Add(options);
        DetailEvolutionTargets.ItemsSource = new[] { graph };
    }
    internal static string TraitEffect(string? id) => id switch
    {
        "gatherer" => "Больше ягод", "treasure-hunter" => "Больше монет", "tracker" => "Больше встреч",
        "finder" => "Больше находок", "mentor" => "Больше опыта команде", "collector" => "Больше новых видов",
        "relic-finder" => "Больше особых предметов", "terrain-expert" => "Быстрее проходит маршрут",
        "fast-grower" => "Быстрее готовится к эволюции", "rarity-expert" => "Помогает встретить редкий вид",
        "caring-parent" => "Ускоряет ожидание в питомнике", _ => ""
    };

    internal static string TraitLabel(string? id) => id switch
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
    public static string DexNumberText(this CollectionEntry entry) => $"#{entry.DisplaySpeciesId:D4}";
}
