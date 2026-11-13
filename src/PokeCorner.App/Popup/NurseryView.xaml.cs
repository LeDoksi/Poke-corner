using PokeCorner.App.Imaging;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;
using Button = System.Windows.Controls.Button;
using Image = System.Windows.Controls.Image;
using Orientation = System.Windows.Controls.Orientation;
using UserControl = System.Windows.Controls.UserControl;
using Brush = System.Windows.Media.Brush;

namespace PokeCorner.App.Popup;

public partial class NurseryView : UserControl
{
    private readonly Func<SaveData> _snapshot;
    private readonly Func<IReadOnlyList<Guid>, int?, NurseryPreview> _preview;
    private readonly Func<NurseryRequest, Task<NurseryAction>> _start;
    private readonly Func<Guid, Guid, Task<NurseryAction>> _hatch;
    private readonly Func<DateTimeOffset, Task<bool>> _refreshReady;
    private readonly Action<string, int> _shopShortage;
    private readonly Action<Guid> _openPokedex;
    private readonly Func<Guid, Task> _activate;
    private readonly CatalogPackage _catalog;
    private readonly NurseryCatalog _recipes;
    private readonly LevelProgression _levels;
    private readonly Dictionary<int, CatalogSpecies> _species;
    private readonly Dictionary<(int, bool), ImageSource?> _spriteCache = [];
    private readonly Dictionary<string, ImageSource?> _itemArtCache = [];
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private Guid _first, _second, _draftFirst, _draftSecond;
    private int? _choice;
    private int _page, _pages = 1, _editing, _selectionGeneration, _lifetimeGeneration;
    private NurseryRequest? _pendingStart;
    private Guid _pendingHatchNest, _pendingHatchOperation, _dismissedResult;
    private string? _shortageBerry;
    private int _shortageRequired;
    private bool _busy, _refreshingReady, _pickerOpen, _suspended;
    private readonly Func<IReadOnlyList<(string Item, int Quantity, Guid Operation)>, Task>? _buy;
    private readonly ShopService _shopGoods;
    private IReadOnlyList<NurseryPairOption> _pairs = [];
    private bool? _onlyNewPairs; // null until the first list decides: "Дадут новых" when there is any such pair
    private int _pairPage, _pairPages = 1;
    private bool _cartOpen;
    private readonly Dictionary<string, int> _cart = [];
    private readonly Dictionary<(string Item, int Quantity), Guid> _cartOperations = [];

    public NurseryView(Func<SaveData> snapshot, Func<IReadOnlyList<Guid>, int?, NurseryPreview> preview,
        Func<NurseryRequest, Task<NurseryAction>> start, Func<Guid, Guid, Task<NurseryAction>> hatch,
        Func<DateTimeOffset, Task<bool>> refreshReady, CatalogPackage catalog, Action<string, int> shopShortage,
        Action<Guid> openPokedex, Func<Guid, Task> activate,
        Func<IReadOnlyList<(string Item, int Quantity, Guid Operation)>, Task>? buy = null)
    {
        _snapshot = snapshot; _preview = preview; _start = start; _hatch = hatch; _refreshReady = refreshReady;
        _catalog = catalog; _recipes = new NurseryCatalog(catalog); _levels = new LevelProgression(catalog.DataRoot);
        _species = catalog.Species.ToDictionary(s => s.Id);
        _shopShortage = shopShortage; _openPokedex = openPokedex; _activate = activate; _buy = buy;
        _shopGoods = new ShopService(catalog.DataRoot);
        InitializeComponent();
        RarityBox.Items.Add(new ComboBoxItem { Content = "Редкость: любая", Tag = "" });
        foreach (var rarity in catalog.Species.Select(s => s.Rarity).Distinct())
            RarityBox.Items.Add(new ComboBoxItem { Content = RarityName(rarity), Tag = rarity });
        RarityBox.SelectedIndex = 0;
        _timer.Tick += async (_, _) => await TickAsync();
        Loaded += (_, _) =>
        {
            _suspended = false; _timer.Start();
            SystemParameters.StaticPropertyChanged -= MotionPolicyChanged;
            SystemParameters.StaticPropertyChanged += MotionPolicyChanged; Refresh();
        };
        Unloaded += (_, _) =>
        {
            _suspended = true; _lifetimeGeneration++; _timer.Stop(); _pickerOpen = false; _cartOpen = false;
            SystemParameters.StaticPropertyChanged -= MotionPolicyChanged;
            BabyImage.BeginAnimation(OpacityProperty, null); BabyImage.Opacity = 1;
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Escape) return;
            if (_cartOpen) { CloseCart(); e.Handled = true; }
            else if (_pickerOpen) { ClosePicker(); e.Handled = true; }
        };
        // A grown or shrunk data column changes how much height the scene may take (see MeasureOverride).
        DataColumn.SizeChanged += (_, e) => { if (e.HeightChanged) InvalidateMeasure(); };
        SizeChanged += (_, _) => PlaceMargin();
        SceneBorder.SizeChanged += (_, _) => PlaceMargin();
        // Scene art is 2048x768: it covers the band and is clipped evenly around the middle, where the nest sits.
        SceneGrid.SizeChanged += (_, _) =>
        {
            var scale = Math.Max(SceneGrid.ActualWidth / 2048, SceneGrid.ActualHeight / 768);
            GreenhouseImage.Width = 2048 * scale;
            GreenhouseImage.Height = 768 * scale;
            SceneGrid.Clip = new System.Windows.Media.RectangleGeometry(new Rect(0, 0, SceneGrid.ActualWidth, SceneGrid.ActualHeight), 17, 17);
        };
        PickerScroll.SizeChanged += (_, _) => { if (_pickerOpen && !_suspended) RefreshPicker(); };
        Refresh();
    }
    private void MotionPolicyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_suspended || e.PropertyName != nameof(SystemParameters.ClientAreaAnimation)) return;
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => MotionPolicyChanged(sender, e)); return; }
        if (!SystemParameters.ClientAreaAnimation) { BabyImage.BeginAnimation(OpacityProperty, null); BabyImage.Opacity = 1; }
    }
    private string SpeciesName(int id) => _species.GetValueOrDefault(id)?.DisplayName ?? $"№{id:D4}";
    private string ParentName(SaveData data, Guid id)
    {
        var p = data.Individuals.FirstOrDefault(p => p.Id == id);
        return p is null ? "Пустое место" : $"{p.Nickname ?? SpeciesName(p.CurrentSpeciesId)} · №{p.CurrentSpeciesId:D4}" + (p.LooksShiny ? " · шайни" : "") + "\n" + IndividualDisplay.Discriminator(data.Individuals, p.Id, always: true);
    }
    private static string RarityName(string rarity) => rarity.ToLowerInvariant() switch
    {
        "common" => "Обычный",
        "uncommon" => "Необычный",
        "rare" => "Редкий",
        "epic" => "Эпический",
        "legendary" => "Легендарный",
        _ => rarity
    };
    private string? Unavailable(SaveData data, PokemonIndividual p)
    {
        if (data.ActiveIndividualId == p.Id) return "На рабочем столе";
        if (data.Expedition is { Status: "running" or "ready" } trip && trip.ParticipantIds.Contains(p.Id)) return "В походе";
        if (ExpeditionService.BusyIndividualIds(data).Contains(p.Id)) return "Уже занят";
        if (!_catalog.CanObtain(p.CurrentSpeciesId, p.IsShiny)) return "Изображение этой формы пока недоступно";
        if (_recipes.Partners(p.CurrentSpeciesId).Count == 0) return "Нет доступного потомства";
        return null;
    }
    private bool PairValid(SaveData data, Guid first, Guid second)
    {
        var a = data.Individuals.FirstOrDefault(p => p.Id == first); var b = data.Individuals.FirstOrDefault(p => p.Id == second);
        if (a is null || b is null || first == second || Unavailable(data, a) != null || Unavailable(data, b) != null) return false;
        return _recipes.Find(a, b)?.IsAvailable == true;
    }
    private ImageSource? Sprite(int id, bool shiny)
    {
        var key = (id, shiny); if (_spriteCache.TryGetValue(key, out var image)) return image;
        try { var path = _catalog.GetAssetPath(id, shiny) ?? (shiny ? _catalog.GetAssetPath(id, false) : null); if (path is not null && File.Exists(path)) image = SpriteImages.Decode(File.ReadAllBytes(path)); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException or ArgumentException) { image = null; }
        if (_spriteCache.Count >= 128) _spriteCache.Clear(); _spriteCache[key] = image; return image;
    }
    private void Show(string text) { if (_suspended) return; MessageText.Text = text; MessagePanel.Visibility = Visibility.Visible; _messageState = NestState(); }

    // A status line belongs to the nest state it was said in: "Гнездо запущено." must not linger once the baby is ready.
    private string? _messageState;
    private string NestState() => _snapshot().Nest is { } nest ? $"{nest.Id}:{nest.Status}" : "";
    private void Slot(StackPanel panel, SaveData data, Guid id)
    {
        panel.Children.Clear(); var p = data.Individuals.FirstOrDefault(p => p.Id == id);
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new Image { Source = p is null ? null : Sprite(p.CurrentSpeciesId, p.LooksShiny), Width = 40, Height = 40, Margin = new Thickness(0, 0, 6, 0) });
        var text = p is null ? "Пустое место" : $"{p.Nickname ?? SpeciesName(p.CurrentSpeciesId)} · ур. {_levels.LevelForXp(p.TotalXp)}" + (p.LooksShiny ? " · шайни" : "");
        row.Children.Add(new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center,
            ToolTip = p is null ? null : ParentName(data, id) + "\n" + CollectionView.TraitLabel(p.FirstTraitId) + " — " + CollectionView.TraitEffect(p.FirstTraitId),
            MaxWidth = Math.Max(65, (ActualWidth > 0 ? Math.Min(ActualWidth, 968) : 800) * .5 - 190) });
        panel.Children.Add(row);
    }
    private void OpenPicker(int editing)
    {
        if (_busy || _suspended || _snapshot().Nest?.Status is "running" or "ready") return;
        _draftFirst = _first; _draftSecond = _second; _editing = editing == 1 && _first != Guid.Empty ? 1 : 0;
        _pickerOpen = true; Refresh(); ParentSearchBox.Focus();
    }
    /// From a Pokédex hint: puts the best free pet of each species into the pair, ready to start.
    /// Returns why it couldn't, so the hint can say so instead of landing on an empty nursery.
    public string? PresetPair(int firstSpecies, int secondSpecies)
    {
        var data = _snapshot();
        if (_busy || data.Nest?.Status is "running" or "ready") return "Питомник сейчас занят.";
        PokemonIndividual? Best(int species, Guid except) => data.Individuals
            .Where(p => _recipes.FamilyRoot(p.CurrentSpeciesId) == _recipes.FamilyRoot(species) && p.Id != except && Unavailable(data, p) == null)
            .OrderByDescending(p => p.TotalXp).FirstOrDefault();
        var first = Best(firstSpecies, Guid.Empty);
        var second = first is null ? null : Best(secondSpecies, first.Id);
        if (first is null || second is null) return $"Нет свободного {SpeciesName(first is null ? firstSpecies : secondSpecies)}.";
        if (!PairValid(data, first.Id, second.Id)) return "Эта пара сейчас недоступна.";
        _first = first.Id; _second = second.Id;
        _choice = null; _pendingStart = null; _selectionGeneration++;
        _pickerOpen = false; _cartOpen = false; ShowSelectedPairPage = _revealSelectedPair = true; Refresh();
        return null;
    }
    private void ChooseFirst_Click(object sender, RoutedEventArgs e) => OpenPicker(0);
    private void ChooseSecond_Click(object sender, RoutedEventArgs e) => OpenPicker(1);
    private void DraftFirst_Click(object sender, RoutedEventArgs e) { _editing = 0; _page = 0; RefreshPicker(); }
    private void DraftSecond_Click(object sender, RoutedEventArgs e) { if (_draftFirst == Guid.Empty) return; _editing = 1; _page = 0; RefreshPicker(); }
    private System.Windows.Size _host;

    // Sizes that depend on the space this view gets are settled here, inside the layout pass, so the first
    // frame at a new window size is already right (SizeChanged handlers only fix it one pass later).
    protected override System.Windows.Size MeasureOverride(System.Windows.Size constraint)
    {
        if (!double.IsInfinity(constraint.Width) && !double.IsInfinity(constraint.Height))
        {
            if (Math.Abs(constraint.Width - _host.Width) > 1 || Math.Abs(constraint.Height - _host.Height) > 1)
            {
                _host = constraint;
                if (_pickerOpen && !_suspended) RefreshPicker();
            }
            ArrangeColumns(constraint.Width);
            if (ScenePanel.Visibility == Visibility.Visible) FitScene(constraint);
        }
        return base.MeasureOverride(constraint);
    }

    // The scene takes the height the title, data column and footer leave, from 150 up to the art's own
    // height at this width (no vertical crop when there is room), so no window size gets an empty band.
    private void FitScene(System.Windows.Size constraint)
    {
        if (Preparing)
        {
            // The pair book needs the room: the scene keeps a band with the parents by the nest.
            var band = Math.Clamp(constraint.Height * .2, 110, 260);
            if (Math.Abs(SceneGrid.Height - band) > 1) SceneGrid.Height = band;
            PlaceSceneParents(band);
            return;
        }
        var probe = new System.Windows.Size(constraint.Width, double.PositiveInfinity);
        HeaderPanel.Measure(probe); DataColumn.Measure(probe); FooterHost.Measure(probe);
        var free = constraint.Height - 32 - HeaderPanel.DesiredSize.Height - DataColumn.DesiredSize.Height - FooterHost.DesiredSize.Height - 12;
        var height = Math.Clamp(free, 150, Math.Max(150, Math.Min(constraint.Width * 768 / 2048, 640)));
        if (Math.Abs(SceneGrid.Height - height) > 1) SceneGrid.Height = height;
    }
    private void ParentReset_Click(object sender, RoutedEventArgs e) { ParentSearchBox.Text = ""; RarityBox.SelectedIndex = 0; }
    private void ClosePicker() { _pickerOpen = false; Refresh(); ChooseFirstButton.Focus(); }
    private void PickerCancel_Click(object sender, RoutedEventArgs e) => ClosePicker();
    private void PickerSave_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _suspended || !PairValid(_snapshot(), _draftFirst, _draftSecond)) return;

        // Recheck the canonical producer preview before applying this presentation draft.
        try
        {
            _preview([_draftFirst, _draftSecond], null);
        }
        catch (Exception ex)
        {
            Show(ex.Message);
            RefreshPicker();
            return;
        }
        _first = _draftFirst;
        _second = _draftSecond;
        _choice = null;
        _pendingStart = null;
        _selectionGeneration++;
        ClosePicker();

    }
    private void ParentFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (!_pickerOpen) return;
        _page = 0;
        RefreshPicker();
    }
    private void PreviousPage_Click(object sender, RoutedEventArgs e)
    {
        if (_page > 0)
        {
            _page--;
            RefreshPicker();
            PickerScroll.ScrollToTop();
        }
    }
    private void NextPage_Click(object sender, RoutedEventArgs e)
    {
        if (_page + 1 < _pages)
        {
            _page++;
            RefreshPicker();
            PickerScroll.ScrollToTop();
        }
    }
    private bool Search(PokemonIndividual p, string query)
    {
        if (query.Length == 0) return true;

        if (SpeciesName(p.CurrentSpeciesId).Contains(query, StringComparison.CurrentCultureIgnoreCase) || (p.Nickname?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false)) return true;

        var number = query.TrimStart('#', '№');

        return int.TryParse(number, out var id) && id > 0 && p.CurrentSpeciesId == id;

    }
    private void RefreshPicker()
    {
        if (!_pickerOpen || _suspended) return;
        var data = _snapshot();

        DraftFirstButton.Content = new TextBlock { Text = "Первый: " + ParentName(data, _draftFirst), TextWrapping = TextWrapping.Wrap };

        DraftSecondButton.Content = new TextBlock { Text = "Второй: " + ParentName(data, _draftSecond), TextWrapping = TextWrapping.Wrap };

        DraftSecondButton.IsEnabled = _draftFirst != Guid.Empty;

        DraftFirstButton.BorderThickness = new Thickness(_editing == 0 ? 2 : 1);
        DraftSecondButton.BorderThickness = new Thickness(_editing == 1 ? 2 : 1);

        var first = data.Individuals.FirstOrDefault(p => p.Id == _draftFirst);

        var partners = first is null ? Array.Empty<int>() : _recipes.Partners(first.CurrentSpeciesId).ToArray();

        var rarity = (RarityBox.SelectedItem as ComboBoxItem)?.Tag as string;

        // A first parent is offered only when the collection already holds a free partner for it:
        // picking a pet that can't be paired was a dead end.
        var freeBySpecies = data.Individuals.Where(p => Unavailable(data, p) == null)
            .GroupBy(p => _recipes.FamilyRoot(p.CurrentSpeciesId)).ToDictionary(g => g.Key, g => g.Count());
        bool HasPartner(PokemonIndividual p) => _recipes.Partners(p.CurrentSpeciesId).Any(family =>
            freeBySpecies.GetValueOrDefault(family) > (family == _recipes.FamilyRoot(p.CurrentSpeciesId) && Unavailable(data, p) == null ? 1 : 0));

        var rows = data.Individuals.Where(p => Search(p, ParentSearchBox.Text.Trim()) &&
            (string.IsNullOrEmpty(rarity) || _species.GetValueOrDefault(p.CurrentSpeciesId)?.Rarity == rarity) &&
            (_editing == 0 ? HasPartner(p) : p.Id != _draftFirst && partners.Contains(_recipes.FamilyRoot(p.CurrentSpeciesId)) && Unavailable(data, p) == null))
            .OrderBy(p => p.CurrentSpeciesId).ThenBy(p => p.CaughtAtUtc).ThenBy(p => p.Id).ToArray();

        // Georgy 23.10: duplicates are rare (1-2 per species), so the species step was an extra click;
        // the picker lists individuals straight away, narrowed by search, rarity and shiny.
        PickerIndividualsPanel.Children.Clear();
        PickerIndividualsPanel.Visibility = Visibility.Visible;
        var individuals = rows;
        var count = individuals.Length;
        // D-17 tiles, same family as the Pokédex grid; pages hold as many full rows as the window fits.
        // From the space the view was given (picker margins 2x24, room for a scrollbar), not from the scroll
        // viewer, whose size is still the previous window's while a resize is being laid out.
        var viewport = _host.Width > 0 ? _host.Width - 66 : PickerScroll.ViewportWidth > 0 ? PickerScroll.ViewportWidth - 2 : Math.Max(400, (ActualWidth > 0 ? ActualWidth : 800) - 50);
        var columns = Math.Max(2, (int)((viewport - 8) / 150));
        var width = Math.Floor((viewport - 8) / columns) - 8;
        var rowsFit = _host.Height > 0 ? Math.Clamp((int)((_host.Height - 262) / 138), 3, 8) : 3; // 262: title, pair, filters, footer
        var size = Math.Max(12, columns * rowsFit);

        _pages = Math.Max(1, (count + size - 1) / size);
        _page = Math.Clamp(_page, 0, _pages - 1);

        foreach (var p in individuals.Skip(_page * size).Take(size))
            {
                var reason = Unavailable(data, p);
                var row = new StackPanel();

                row.Children.Add(new Image { Source = Sprite(p.CurrentSpeciesId, p.LooksShiny), Width = 56, Height = 56, HorizontalAlignment = HorizontalAlignment.Center });
                row.Children.Add(new TextBlock { Text = p.Nickname ?? SpeciesName(p.CurrentSpeciesId), FontWeight = FontWeights.Bold, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 0) });
                row.Children.Add(new TextBlock { Text = $"Ур. {_levels.LevelForXp(p.TotalXp)}" + (p.LooksShiny ? " · шайни" : ""), FontSize = 12, Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush") });
                row.Children.Add(new TextBlock { Text = CollectionView.TraitLabel(p.FirstTraitId), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush"), Margin = new Thickness(0, 2, 0, 0) });

                if (reason is not null) row.Children.Add(new TextBlock { Text = reason, TextWrapping = TextWrapping.Wrap, FontSize = 12, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 2, 0, 0) });

                var button = new Button { Tag = p.Id, Content = row, Width = width, MinHeight = 130, Margin = new Thickness(4), Padding = new Thickness(8), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top, Style = (Style)FindResource("SecondaryButtonStyle"), IsEnabled = reason is null,
                    ToolTip = (reason ?? "Выбрать этот экземпляр") + "\n" + ParentName(data, p.Id) + "\n" + CollectionView.TraitLabel(p.FirstTraitId) + " — " + CollectionView.TraitEffect(p.FirstTraitId) };

                System.Windows.Automation.AutomationProperties.SetName(button, ParentName(data, p.Id));

                button.Click += (_, _) =>
                {
                    if (_busy || !_pickerOpen || Unavailable(_snapshot(), p) != null) return;

                    if (_editing == 0)
                    {
                        _draftFirst = p.Id;
                        if (!PairValid(_snapshot(), _draftFirst, _draftSecond)) _draftSecond = Guid.Empty;
                        _editing = 1;
                       
                        _page = 0;
                        ParentSearchBox.Text = "";
                    }
                    else if (PairValid(_snapshot(), _draftFirst, p.Id)) _draftSecond = p.Id;

                    RefreshPicker();

                };
                PickerIndividualsPanel.Children.Add(button);

            }
        PickerCountText.Text = $"{(_editing == 0 ? "Первый" : "Второй")} родитель · найдено {rows.Length}";

        PickerPageText.Text = $"{_page + 1} / {_pages}";
        PickerPreviousButton.IsEnabled = _page > 0;
        PickerNextButton.IsEnabled = _page + 1 < _pages;

        PickerSaveButton.IsEnabled = PairValid(data, _draftFirst, _draftSecond);

    }
    private bool Preparing { get { var nest = _snapshot().Nest; return !_pickerOpen && nest?.Status is not ("running" or "ready") && !(nest?.Status == "hatched" && _dismissedResult != nest.Id); } }

    public void Refresh()
    {
        if (_suspended) return;
        var data = _snapshot();
        var nest = data.Nest;
        if (_messageState is not null && _messageState != NestState()) { MessagePanel.Visibility = Visibility.Collapsed; _messageState = null; }
        var active = nest?.Status is "running" or "ready";

        var result = nest?.Status == "hatched" && _dismissedResult != nest.Id;

        if (active || result) { _pickerOpen = false; _cartOpen = false; }

        var ready = nest?.Status == "ready";
        var preparing = !active && !result;

        NurseryNote.Text = ready ? "Треснуло! Всё бросаю, бегу знакомиться." : active ? "Яйцо молчит. Я тоже. Кто кого пересидит."
            : result ? "Первый день на свете. И сразу в дневник." : "Двое разных в одном гнезде. Что может пойти не так?";
        NurseryMargin.Show(ready || result ? "nursery-hatch" : "nursery", ready || result ? HatchMargin : NestMargin);
        SceneTitleText.Text = ready ? "Малыш готов появиться!" : active ? "В гнезде кто-то растёт" : result ? "Добро пожаловать в семью!" : "Будущие родители";

        SubtitleText.Text = ready ? "Пора познакомиться." : active ? "Можно закрыть приложение — время продолжит идти." : result ? "Новая история начинается здесь." : "Выберите пару — у каждой сразу видно, кто может родиться.";

        ScreenTitle.Visibility = _pickerOpen ? Visibility.Collapsed : Visibility.Visible;

        PreviewPanel.Visibility = preparing ? Visibility.Visible : Visibility.Collapsed;
        PairsSection.Visibility = preparing ? Visibility.Visible : Visibility.Collapsed;
        ArrangeColumns(ActualWidth > 0 ? ActualWidth : _host.Width);

        NestParentPanel.Visibility = active && !ready ? Visibility.Visible : Visibility.Collapsed;

        LuckPanel.Visibility = ready || result ? Visibility.Collapsed : Visibility.Visible;

        ScenePanel.Visibility = _pickerOpen ? Visibility.Collapsed : Visibility.Visible;
        PickerPanel.Visibility = _pickerOpen ? Visibility.Visible : Visibility.Collapsed;
        CartPanel.Visibility = _cartOpen && preparing && !_pickerOpen ? Visibility.Visible : Visibility.Collapsed;

        PreparationPanel.Visibility = preparing ? Visibility.Visible : Visibility.Collapsed;
        PreparationFooter.Visibility = _pickerOpen || !preparing ? Visibility.Collapsed : Visibility.Visible;
        NestPanel.Visibility = active && !ready ? Visibility.Visible : Visibility.Collapsed;

        ResultPanel.Visibility = result ? Visibility.Visible : Visibility.Collapsed;
        ResultFooter.Visibility = ResultPanel.Visibility;

        GreenhouseImage.Source = (ImageSource)FindResource(active ? (nest!.Status == "ready" ? "UiNurseryReadyImage" : "UiNurseryWaitingImage") : "UiNurseryImage");

        BabyImage.Visibility = Visibility.Collapsed;
        BabyImage.Source = null;
        SceneFirstParent.Visibility = SceneSecondParent.Visibility = Visibility.Collapsed;
        HatchButton.Visibility = Visibility.Collapsed;

        // D-34: ten clear steps instead of a bar that crept by a quarter of a percent.
        var steps = ShinyRules.NurseryLuckStepsOf(data.NurseryLuckBonus);
        RenderLuckSegments(steps);
        var shinyNow = ShinyRules.Chance(ShinySource.Nursery, data.NurseryLuckBonus, false, true);
        // Audit item 30: the line stays without percentages; the exact chance is in the tooltip.
        LuckText.Text = steps >= ShinyRules.NurseryLuckSteps
            ? $"{steps} из {ShinyRules.NurseryLuckSteps} · полная удача"
            : $"{steps} из {ShinyRules.NurseryLuckSteps} · обычный малыш +1 деление";
        LuckPanel.ToolTip = $"Шанс шайни в гнезде сейчас {Percent(shinyNow)}, каждое деление +{Percent(ShinyRules.NurseryLuckStep)}, максимум {Percent(ShinyRules.NurseryChanceCap)}. "
            + "Шайни-малыш сбрасывает удачу. Если у малыша нет шайни-формы, удача не меняется.";

        if (active)
        {
            RefreshNest(data, nest!);
            return;
        }
        var archived = nest is not null && nest.Status == "hatched" && data.HatchedNurseryResults.TryGetValue(nest.Id, out _);

        OpenPokedexButton.IsEnabled = ActivateButton.IsEnabled = !_busy && archived;
        AgainButton.IsEnabled = !_busy && nest is not null && nest.ParticipantIds.Count == 2 && PairValid(data, nest.ParticipantIds[0], nest.ParticipantIds[1]);

        if (result)
        {
            ResultNewText.Visibility = Visibility.Collapsed;
            if (!data.HatchedNurseryResults.TryGetValue(nest!.Id, out var child)) ResultText.Text = "Сохранённый результат вылупления недоступен. Откройте историю сохранений.";

            else
            {
                BabyImage.Source = Sprite(child.SpeciesId, child.IsShiny);
                BabyImage.Visibility = Visibility.Visible;

                var luck = nest.LuckBeforeHatch is not { } before ? "Изменение удачи этого старого вылупления не сохранено."
                    : nest.ShinySupported && child.IsShiny ? "Удача питомника сброшена: вылупился шайни."
                    : data.NurseryLuckBonus > before ? $"Удача питомника +1 деление ({ShinyRules.NurseryLuckStepsOf(data.NurseryLuckBonus)} из {ShinyRules.NurseryLuckSteps})."
                    : !nest.ShinySupported ? "Удача не изменилась: у этого малыша нет шайни-формы."
                    : "Удача уже полная.";

                ResultText.Text = $"№{child.SpeciesId} · {SpeciesName(child.SpeciesId)}{(child.IsShiny ? " · шайни" : "")} · характер {NatureDisplay.Name(child.Nature)}. Особенность: {CollectionView.TraitLabel(child.FirstTraitId)} — {CollectionView.TraitEffect(child.FirstTraitId)}. Малыш сохранён в коллекции. {luck}";

                ResultTitle.Text = $"{SpeciesName(child.SpeciesId)} появился!";

                // New for the collection: nobody else in it is, or ever was, this species.
                if (!data.Individuals.Any(p => p.Id != child.Id && (p.SpeciesId == child.SpeciesId || p.CurrentSpeciesId == child.SpeciesId || p.ReachedSpeciesIds.Contains(child.SpeciesId))))
                    ResultNewText.Visibility = Visibility.Visible;
            }
            return;

        }
        if (!_busy && (_first != Guid.Empty || _second != Guid.Empty) && !PairValid(data, _first, _second))
        {
            _first = _second = Guid.Empty;
            _choice = null;
            _pendingStart = null;
            _selectionGeneration++;

        }
        Slot(FirstSlot, data, _first);
        Slot(SecondSlot, data, _second);

        ChooseFirstButton.Content = _first == Guid.Empty ? "Выбрать" : "Сменить";
        ChooseSecondButton.Content = _second == Guid.Empty ? "Выбрать" : "Сменить";

        ChooseFirstButton.IsEnabled = ChooseSecondButton.IsEnabled = !_busy;

        RenderPairs(data);
        ShowPairInScene(data);

        OutcomePanel.Children.Clear();
        ResourcePricesPanel.Children.Clear();
        StartButton.IsEnabled = false;
        ShopButton.Visibility = Visibility.Collapsed;
        _shortageBerry = null;
        CostText.Text = ShortageText.Text = TraitsText.Text = "";

        if (_pickerOpen) RefreshPicker();

        if (_first == Guid.Empty || _second == Guid.Empty)
        {
            PreviewText.Text = _pairs.Count == 0 ? "Выберите двух разных родителей." : "Выберите пару слева: у каждой видно, кто может родиться.";
            CostText.Text = "Стоимость и время появятся после выбора родителей.";
            _cartOpen = false; CartPanel.Visibility = Visibility.Collapsed;
            return;
        }
        try
        {
            var preview = _preview([_first, _second], _choice);

            PreviewText.Text = preview.SelectionUseful
                ? "Один из них появится после ожидания. Нажмите на малыша, чтобы выбрать его наверняка: +4 ягоды и талисман потомства."
                : "Этот малыш появится после ожидания.";

            if (preview.SelectionUseful)
                OutcomePanel.Children.Add(ChoiceButton("Случайный малыш · без талисмана", null));
            foreach (var outcome in preview.Outcomes)
            {
                var id = outcome.SpeciesId;
                var known = data.NormalSpeciesIds.Contains(id);
                var row = new DockPanel { LastChildFill = true };
                var picture = Face(id, known, 44);
                picture.Margin = new Thickness(0, 0, 10, 0);
                DockPanel.SetDock(picture, Dock.Left);
                row.Children.Add(picture);
                var chance = preview.OutcomeChances.GetValueOrDefault(id);
                var percent = new TextBlock { Text = $"{Math.Round(chance * 100, MidpointRounding.AwayFromZero):0}%", FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                DockPanel.SetDock(percent, Dock.Right);
                row.Children.Add(percent);
                if (!known)
                {
                    var fresh = new TextBlock { Text = "✦ новый", Foreground = (Brush)FindResource("NdApricotBrush"), FontWeight = FontWeights.Bold, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                    DockPanel.SetDock(fresh, Dock.Right);
                    row.Children.Add(fresh);
                }
                row.Children.Add(new TextBlock {
                    Text = OutcomeName(data, id) + "\n" + (outcome.ShinyAvailable ? "Возможна шайни-форма" : "Шайни-форма пока недоступна"),
                    TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center
                });
                if (preview.SelectionUseful)
                {
                    var choice = ChoiceButton(OutcomeName(data, id), id);
                    choice.Content = row;
                    OutcomePanel.Children.Add(choice);
                }
                else OutcomePanel.Children.Add(new Border { Child = row, Margin = new Thickness(0, 4, 0, 4) });
            }
            TraitsText.Text = "Возможные особенности: " + string.Join("; ", preview.TraitIds.Select(t => CollectionView.TraitLabel(t) + " — " + CollectionView.TraitEffect(t))) + ".";

            RenderPrices(preview.Berries, data);

            CostText.Text = $"Ожидание: {preview.Hours} ч, с учётом родителей до {preview.ReadyAtUtc.ToLocalTime():g}." + (_choice is null ? "" : " Талисман потомства ×1.");

            var missing = Missing(data, preview);
            if (missing.Count > 0)
            {
                var berries = missing.Where(x => x.Key != "offspring-charm").ToArray();
                ShortageText.Text = berries.Length > 0
                    ? "Не хватает: " + string.Join(", ", berries.Select(x => $"{ExpeditionView.BerryName(x.Key)} ×{x.Value}")) + (missing.ContainsKey("offspring-charm") ? " и талисмана потомства" : "") + ". Можно докупить или найти в походе."
                    : "Не хватает талисмана потомства.";
                var first = missing.First();
                _shortageBerry = first.Key;
                _shortageRequired = first.Key == "offspring-charm" ? 1 : preview.Berries[first.Key];
                ShopButton.Content = $"Докупить · {CartCost(missing)} ●";
                ShopButton.Visibility = Visibility.Visible;
                ShopButton.IsEnabled = !_busy;
                if (_cartOpen) RenderCart(data, missing);
            }
            else
            {
                StartButton.IsEnabled = !_busy;
                if (_cartOpen) { _cartOpen = false; CartPanel.Visibility = Visibility.Collapsed; }
            }
        }
        catch (Exception ex)
        {
            PreviewText.Text = ex.Message;
            _cartOpen = false; CartPanel.Visibility = Visibility.Collapsed;
        }
    }

    private static string Percent(double value) => (value * 100).ToString(value * 100 < 1 ? "0.##" : "0.#", System.Globalization.CultureInfo.CurrentCulture) + "%";

    // A known species shows its sprite; an unknown one only its silhouette (or "?" without art), never its name.
    private FrameworkElement Face(int speciesId, bool known, double size)
    {
        var sprite = Sprite(speciesId, false);
        return known
            ? new Image { Source = sprite, Width = size, Height = size }
            : sprite is null ? new TextBlock { Text = "?", Width = size, FontSize = size * .6, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            : new Border { Width = size, Height = size, Background = (Brush)FindResource("NdSilhouetteBrush"),
                OpacityMask = new ImageBrush(sprite) { Stretch = Stretch.Uniform } };
    }

    // ---- "Книга пар" ----

    /// Set by a Pokédex hint or "Ещё раз с этой парой": the next list render turns to the page holding the pair.
    private bool ShowSelectedPairPage;
    /// Set only when the pair came from outside the book (Pokédex, "Ещё раз"): it must be visible even under "Дадут новых".
    private bool _revealSelectedPair;

    private void ArrangeColumns(double width)
    {
        // Two columns while preparing (pairs | chosen pair); one column otherwise and on narrow windows.
        var two = Preparing && (double.IsNaN(width) || width <= 0 || width >= 760);
        DataRightColumn.Width = two ? new GridLength(2, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumnSpan(LeftStack, two ? 1 : 2);
        Grid.SetColumn(RightStack, two ? 1 : 0);
        Grid.SetRow(RightStack, two ? 0 : 1);
        Grid.SetColumnSpan(RightStack, two ? 1 : 2);
        RightStack.Margin = new Thickness(two ? 16 : 0, 0, 0, 0);
    }

    private (int A, int B) SelectedSpecies(SaveData data)
    {
        var a = data.Individuals.FirstOrDefault(p => p.Id == _first); var b = data.Individuals.FirstOrDefault(p => p.Id == _second);
        // Pairs are keyed by family roots (D-32).
        return (a is null ? 0 : _recipes.FamilyRoot(a.CurrentSpeciesId), b is null ? 0 : _recipes.FamilyRoot(b.CurrentSpeciesId));
    }

    private IReadOnlyList<NurseryPairOption> VisiblePairs()
    {
        var query = PairSearchBox.Text.Trim();
        var data = _snapshot();
        bool Hit(NurseryPairOption pair)
        {
            if (query.Length == 0) return true;
            var number = query.TrimStart('#', '№');
            bool Species(int id, bool known) => (known && SpeciesName(id).Contains(query, StringComparison.CurrentCultureIgnoreCase))
                || (int.TryParse(number, out var n) && n > 0 && n == id);
            return Species(pair.FirstSpeciesId, true) || Species(pair.SecondSpeciesId, true)
                || pair.Outcomes.Any(o => Species(o.SpeciesId, !o.IsNew))
                || new[] { pair.FirstId, pair.SecondId }.Any(id => data.Individuals.FirstOrDefault(p => p.Id == id) is { } pet
                    && (pet.Nickname?.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true || Species(pet.CurrentSpeciesId, true)));
        }
        return NurseryPairList.Ordered(_pairs.Where(p => (_onlyNewPairs != true || (p.NewCount > 0 && p.Busy is null)) && Hit(p))).ToArray();
    }

    private void RenderPairs(SaveData data)
    {
        _pairs = NurseryPairList.Build(data, _recipes, p => Unavailable(data, p));
        var fresh = _pairs.Count(p => p.NewCount > 0 && p.Busy is null);
        _onlyNewPairs ??= fresh > 0;
        var (a, b) = SelectedSpecies(data);
        // A pair chosen from the Pokédex may give nothing new; it must still be visible.
        if (_revealSelectedPair && a != 0 && b != 0 && _onlyNewPairs == true && !_pairs.Any(p => p.Matches(a, b) && p.NewCount > 0)) _onlyNewPairs = false;
        _revealSelectedPair = false;
        NewPairsButton.Content = $"✦ Дадут новых · {fresh}";
        AllPairsButton.Content = $"Все пары · {_pairs.Count}";
        NewPairsButton.Style = (Style)FindResource(_onlyNewPairs == true ? "NdChipSelectedStyle" : "SecondaryButtonStyle");
        AllPairsButton.Style = (Style)FindResource(_onlyNewPairs == true ? "SecondaryButtonStyle" : "NdChipSelectedStyle");

        var rows = VisiblePairs();
        // Whole rows that fit beside the scene and footer; the page never scrolls inside itself.
        var size = _host.Height > 0 ? Math.Clamp((int)((_host.Height - 280) / 56), 5, 12) : 6;
        _pairPages = Math.Max(1, (rows.Count + size - 1) / size);
        if (ShowSelectedPairPage)
        {
            var index = rows.ToList().FindIndex(p => p.Matches(a, b));
            if (index >= 0) _pairPage = index / size;
            ShowSelectedPairPage = false;
        }
        _pairPage = Math.Clamp(_pairPage, 0, _pairPages - 1);

        PairListPanel.Children.Clear();
        foreach (var pair in rows.Skip(_pairPage * size).Take(size)) PairListPanel.Children.Add(PairRow(data, pair, pair.Matches(a, b)));

        PairEmptyPanel.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowAllPairsButton.Visibility = rows.Count == 0 && _onlyNewPairs == true && _pairs.Count > 0 && PairSearchBox.Text.Trim().Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        PairEmptyText.Text = PairSearchBox.Text.Trim().Length > 0 ? "Ничего не нашлось. Попробуйте другое имя или номер."
            : _pairs.Count == 0 ? "Пока ни одна пара из коллекции не подходит друг другу. Подсказки, кого поймать для гнезда, есть в карточке вида в покедексе."
            : "Все малыши, которых могут дать ваши пары, уже есть в покедексе.";
        PairPager.Visibility = _pairPages > 1 ? Visibility.Visible : Visibility.Collapsed;
        PairPageText.Text = $"{_pairPage + 1} / {_pairPages}";
        PairPreviousButton.IsEnabled = _pairPage > 0;
        PairNextButton.IsEnabled = _pairPage + 1 < _pairPages;
        PairCountText.Text = $"пар: {rows.Count}";
    }

    private Button PairRow(SaveData data, NurseryPairOption pair, bool selected)
    {
        var muted = (Brush)FindResource("MutedTextBrush");
        var row = new DockPanel { LastChildFill = false };
        void Left(UIElement element) { DockPanel.SetDock(element, Dock.Left); row.Children.Add(element); }
        Image Parent(Guid id, int species)
        {
            var pet = data.Individuals.FirstOrDefault(p => p.Id == id);
            // The pet that will really sit in the nest, at its own stage (D-32), not the family's first form.
            return new Image { Source = Sprite(pet?.CurrentSpeciesId ?? species, pet?.LooksShiny ?? false), Width = 36, Height = 36, Margin = new Thickness(0, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center };
        }
        Left(Parent(pair.FirstId, pair.FirstSpeciesId));
        Left(Parent(pair.SecondId, pair.SecondSpeciesId));
        var names = new StackPanel { Width = 136, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 4, 0) };
        string PetName(Guid id, int family) => data.Individuals.FirstOrDefault(p => p.Id == id) is { } pet ? pet.Nickname ?? SpeciesName(pet.CurrentSpeciesId) : SpeciesName(family);
        names.Children.Add(new TextBlock { Text = $"{PetName(pair.FirstId, pair.FirstSpeciesId)} + {PetName(pair.SecondId, pair.SecondSpeciesId)}", ToolTip = $"Семьи {SpeciesName(pair.FirstSpeciesId)} и {SpeciesName(pair.SecondSpeciesId)}: подходит любая стадия", FontWeight = FontWeights.SemiBold, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis });
        names.Children.Add(new TextBlock { Text = pair.Busy is { } why ? $"{SpeciesName(pair.BusySpeciesId)}: {why.ToLower(System.Globalization.CultureInfo.CurrentCulture)}" : (pair.IsSameSpecies ? $"малыш семьи · {pair.Recipe.Hours} ч" : $"ожидание {pair.Recipe.Hours} ч"), FontSize = 12, Foreground = muted, TextTrimming = TextTrimming.CharacterEllipsis });
        Left(names);
        Left(new TextBlock { Text = "→", Foreground = muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        foreach (var outcome in pair.Outcomes)
        {
            // Percent sits under the face, so it never covers a silhouette.
            var cell = new Grid { Width = 46, Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = OutcomeName(data, outcome.SpeciesId) + $" · {outcome.Percent}%" };
            cell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); cell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var face = Face(outcome.SpeciesId, !outcome.IsNew, 32); face.HorizontalAlignment = HorizontalAlignment.Center;
            cell.Children.Add(face);
            var share = new TextBlock { Text = $"{outcome.Percent}%", FontSize = 10, Foreground = muted, HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetRow(share, 1); cell.Children.Add(share);
            if (outcome.IsNew) cell.Children.Add(new TextBlock { Text = "✦", FontSize = 11, Foreground = (Brush)FindResource("NdApricotBrush"), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });
            Left(cell);
        }
        var meta = new TextBlock { Text = pair.NewCount > 0 ? $"✦ {pair.NewCount} {(pair.NewCount == 1 ? "новый" : "новых")}" : "все известны", FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            Foreground = pair.NewCount > 0 ? (Brush)FindResource("NdApricotBrush") : muted, FontWeight = pair.NewCount > 0 ? FontWeights.Bold : FontWeights.Normal, Margin = new Thickness(6, 0, 0, 0) };
        DockPanel.SetDock(meta, Dock.Right);
        row.Children.Insert(0, meta);
        var button = new Button { Content = row, Tag = pair, Style = (Style)FindResource("SecondaryButtonStyle"), HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 2, 10, 2), Margin = new Thickness(0, 0, 0, 6), MinHeight = 50,
            IsEnabled = !_busy && pair.Busy is null, BorderThickness = new Thickness(selected ? 2 : 1),
            ToolTip = pair.Busy is null ? "Поставить эту пару в гнездо" : pair.Busy };
        if (selected) button.BorderBrush = (Brush)FindResource("NdApricotBrush");
        ToolTipService.SetShowOnDisabled(button, true);
        if (pair.Busy is not null) row.Opacity = .5;
        System.Windows.Automation.AutomationProperties.SetName(button, $"{SpeciesName(pair.FirstSpeciesId)} и {SpeciesName(pair.SecondSpeciesId)}");
        button.Click += (_, _) => SelectPair(pair);
        return button;
    }

    private void SelectPair(NurseryPairOption pair)
    {
        if (_busy || _suspended || pair.Busy is not null) return;
        var data = _snapshot();
        var (a, b) = SelectedSpecies(data);
        // Clicking the chosen row again keeps the pets picked by hand through "Сменить".
        if (pair.Matches(a, b) && PairValid(data, _first, _second)) return;
        if (!PairValid(data, pair.FirstId, pair.SecondId)) { Refresh(); return; }
        _first = pair.FirstId; _second = pair.SecondId;
        _choice = null; _pendingStart = null; _selectionGeneration++; _cartOpen = false;
        Refresh();
    }

    private void ShowPairInScene(SaveData data)
    {
        var a = data.Individuals.FirstOrDefault(p => p.Id == _first); var b = data.Individuals.FirstOrDefault(p => p.Id == _second);
        if (a is null || b is null) return;
        SceneTitleText.Text = $"{a.Nickname ?? SpeciesName(a.CurrentSpeciesId)} + {b.Nickname ?? SpeciesName(b.CurrentSpeciesId)}";
        var pair = _pairs.FirstOrDefault(p => p.Matches(_recipes.FamilyRoot(a.CurrentSpeciesId), _recipes.FamilyRoot(b.CurrentSpeciesId)));
        var fresh = pair?.NewCount ?? 0;
        SubtitleText.Text = (fresh == 0 ? "Все малыши этой пары уже есть в покедексе" : $"Может родиться {fresh} {NewSpeciesWord(fresh)}") +
            (pair?.Recipe.Hours is { } hours ? $" · ожидание {hours} ч" : "");
        SceneFirstParent.Source = Sprite(a.CurrentSpeciesId, a.LooksShiny);
        SceneSecondParent.Source = Sprite(b.CurrentSpeciesId, b.LooksShiny);
        SceneFirstParent.Visibility = SceneSecondParent.Visibility = Visibility.Visible;
        PlaceSceneParents(SceneGrid.Height);
    }

    private static string NewSpeciesWord(int n) => (n % 10, n % 100) switch
    {
        (1, not 11) => "новый вид",
        (>= 2 and <= 4, < 10 or >= 20) => "новых вида",
        _ => "новых видов"
    };

    private void PlaceSceneParents(double band)
    {
        // Parents stand on the table either side of the nest, which sits in the middle of the art.
        var size = Math.Clamp(band * .45, 56, 112);
        foreach (var image in new[] { SceneFirstParent, SceneSecondParent }) { image.Width = image.Height = size; }
        var gap = Math.Clamp(band * .9, 110, 230);
        SceneFirstParent.Margin = new Thickness(0, 0, gap * 2, band * .12);
        SceneSecondParent.Margin = new Thickness(gap * 2, 0, 0, band * .12);
    }

    private void NewPairs_Click(object sender, RoutedEventArgs e) { if (_busy) return; _onlyNewPairs = true; _pairPage = 0; ShowSelectedPairPage = true; Refresh(); }
    private void AllPairs_Click(object sender, RoutedEventArgs e) { if (_busy) return; _onlyNewPairs = false; _pairPage = 0; ShowSelectedPairPage = true; Refresh(); }
    private void PairSearch_Changed(object sender, TextChangedEventArgs e) { if (_suspended) return; _pairPage = 0; Refresh(); }
    private void PairPrevious_Click(object sender, RoutedEventArgs e) { if (_pairPage > 0) { _pairPage--; Refresh(); } }
    private void PairNext_Click(object sender, RoutedEventArgs e) { if (_pairPage + 1 < _pairPages) { _pairPage++; Refresh(); } }

    // ---- Докупка на месте (Д2) ----

    /// What the nest still needs: berries short of the price, and the offspring charm when a baby is chosen.
    private Dictionary<string, int> Missing(SaveData data, NurseryPreview preview)
    {
        var missing = preview.Berries.Where(x => data.Berries.GetValueOrDefault(x.Key) < x.Value)
            .ToDictionary(x => x.Key, x => x.Value - data.Berries.GetValueOrDefault(x.Key));
        if (_choice is not null && data.Items.GetValueOrDefault("offspring-charm") < 1) missing["offspring-charm"] = 1;
        return missing;
    }

    private int Price(string id) => _shopGoods.Goods.FirstOrDefault(g => g.Id == id)?.Price ?? 0;
    private long CartCost(IReadOnlyDictionary<string, int> items) => items.Sum(x => (long)Price(x.Key) * x.Value);

    private void ShopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _suspended || _shortageBerry is null) return;
        _cart.Clear();
        _cartOpen = true;
        Refresh();
        CartBuyButton.Focus();
    }

    private void RenderCart(SaveData data, IReadOnlyDictionary<string, int> missing)
    {
        // The counts start at exactly what is missing; +/− lets the player take a little extra.
        foreach (var (id, count) in missing) if (!_cart.ContainsKey(id)) _cart[id] = count;
        foreach (var id in _cart.Keys.Where(id => !missing.ContainsKey(id)).ToArray()) _cart.Remove(id);
        CartRowsPanel.Children.Clear();
        foreach (var (id, need) in missing)
        {
            var have = id == "offspring-charm" ? data.Items.GetValueOrDefault(id) : data.Berries.GetValueOrDefault(id);
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
            var icon = new Image { Source = ItemArt(id), Width = 30, Height = 30, Margin = new Thickness(0, 0, 8, 0) };
            DockPanel.SetDock(icon, Dock.Left); row.Children.Add(icon);
            var cost = new TextBlock { Text = $"{(long)Price(id) * _cart[id]} ●", Width = 52, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("MutedTextBrush") };
            DockPanel.SetDock(cost, Dock.Right);
            var plus = Step("+", id, +1); var minus = Step("−", id, -1); minus.IsEnabled = _cart[id] > need && !_busy;
            var count = new TextBlock { Text = _cart[id].ToString(System.Globalization.CultureInfo.CurrentCulture), FontWeight = FontWeights.Bold, Width = 30, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            foreach (var element in new UIElement[] { cost, plus, count, minus }) { DockPanel.SetDock(element, Dock.Right); row.Children.Add(element); }
            var name = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            name.Children.Add(new TextBlock { Text = id == "offspring-charm" ? "Талисман потомства" : ExpeditionView.BerryName(id), FontWeight = FontWeights.SemiBold });
            name.Children.Add(new TextBlock { Text = $"в запасе {have} · нужно {have + need}", FontSize = 12, Foreground = (Brush)FindResource("MutedTextBrush") });
            row.Children.Add(name);
            CartRowsPanel.Children.Add(row);
        }
        var total = CartCost(_cart);
        var short_ = total - data.ShopCoins;
        CartTotalText.Text = short_ > 0
            ? $"Итого {total} ● · у вас {data.ShopCoins}. Не хватает {short_} монет: ягоды можно найти в походе."
            : $"Итого {total} ● · у вас {data.ShopCoins}";
        CartBuyButton.Content = $"Купить · {total} ●";
        CartBuyButton.IsEnabled = !_busy && _buy is not null && total > 0 && short_ <= 0;
        CartCancelButton.IsEnabled = CartShopButton.IsEnabled = !_busy;
    }

    private Button Step(string text, string id, int delta)
    {
        var button = new Button { Content = text, MinWidth = 28, Padding = new Thickness(4, 0, 4, 0), Style = (Style)FindResource("SecondaryButtonStyle"), IsEnabled = !_busy,
            ToolTip = delta > 0 ? "Купить на одну больше" : "На одну меньше" };
        button.Click += (_, _) =>
        {
            if (_busy || !_cart.ContainsKey(id)) return;
            _cart[id] = Math.Clamp(_cart[id] + delta, 1, 999);
            Refresh();
        };
        return button;
    }

    private void CloseCart() { _cartOpen = false; Refresh(); ShopButton.Focus(); }
    private void CartCancel_Click(object sender, RoutedEventArgs e) => CloseCart();

    // The old way stays: the shop opens with the first missing good filled in and a way back here.
    private void CartShop_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _suspended || _shortageBerry is not { } good) return;
        _cartOpen = false; Refresh();
        _shopShortage(good, _shortageRequired);
    }

    private async void CartBuy_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _suspended || _buy is null || _cart.Count == 0) return;
        var items = _cart.Where(x => x.Value > 0).Select(x =>
        {
            if (!_cartOperations.TryGetValue((x.Key, x.Value), out var op)) _cartOperations[(x.Key, x.Value)] = op = Guid.NewGuid();
            return (x.Key, x.Value, op);
        }).ToArray();
        var life = _lifetimeGeneration;
        _busy = true;
        Refresh();
        try
        {
            await _buy(items);
            if (!Live(life)) return;
            foreach (var (item, quantity, _) in items) _cartOperations.Remove((item, quantity));
            _cart.Clear();
            _cartOpen = false;
            Show("Куплено: " + string.Join(", ", items.Select(x => $"{(x.Item1 == "offspring-charm" ? "талисман потомства" : ExpeditionView.BerryName(x.Item1))} ×{x.Item2}")) + ". Можно начинать.");
        }
        catch (Exception ex)
        {
            if (Live(life)) Show(ex.Message + " Покупка не прошла, можно повторить.");
        }
        finally
        {
            ReleaseBusy();
            if (!_suspended && StartButton.IsEnabled) StartButton.Focus();
        }
    }

    private ImageSource? ItemArt(string id)
    {
        if (_itemArtCache.TryGetValue(id, out var image)) return image;
        try
        {
            var path = Path.Combine(_catalog.DataRoot, "final", "assets", "shop", id + ".png");
            if (File.Exists(path)) image = SpriteImages.Decode(File.ReadAllBytes(path));
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException or ArgumentException)
        {
            image = null;
        }
        return _itemArtCache[id] = image;
    }

    private void RenderPrices(IReadOnlyDictionary<string, int> prices, SaveData data)
    {
        foreach (var (id, count) in prices)
        {
            var have = data.Berries.GetValueOrDefault(id);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 12, 4), ToolTip = $"В запасе {have}, нужно {count}" };
            row.Children.Add(new Image { Source = ItemArt(id), Width = 26, Height = 26, Margin = new Thickness(0, 0, 5, 0) });
            var text = new TextBlock { Text = $"{ExpeditionView.BerryName(id)} ×{count}", VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.Bold };
            if (have < count) text.Foreground = (Brush)FindResource("NdDangerTextBrush");
            row.Children.Add(text);
            ResourcePricesPanel.Children.Add(row);
        }
    }

    private string OutcomeName(SaveData data, int id) => data.NormalSpeciesIds.Contains(id) ? $"№{id} · {SpeciesName(id)}" : $"№{id} · неизвестный вид";

    private Button ChoiceButton(string title, int? choice)
    {
        var button = new Button { Content = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap }, HorizontalContentAlignment = HorizontalAlignment.Stretch, Tag = choice, IsEnabled = !_busy, Margin = new Thickness(0, 0, 6, 6), Style = (Style)FindResource("SecondaryButtonStyle"), FontWeight = _choice == choice ? FontWeights.Bold : FontWeights.Normal };

        button.Click += (_, _) =>
        {
            if (_busy || _suspended) return;
            _choice = choice;
            _pendingStart = null;
            _selectionGeneration++;
            Refresh();
        };
        return button;

    }
    private void RefreshNest(SaveData data, NurseryState nest)
    {
        var ready = nest.Status == "ready";
        NestTitle.Text = ready ? "Малыш готов появиться" : "Тихое ожидание";

        Slot(NestFirstSlot, data, nest.ParticipantIds.ElementAtOrDefault(0));
        Slot(NestSecondSlot, data, nest.ParticipantIds.ElementAtOrDefault(1));

        NestParents.Text = "Родители";

        // Georgy 01.11: while the egg is warm, show who may hatch (the actual child stays hidden).
        NestTerms.Visibility = Visibility.Visible;

        NestTerms.ToolTip = null;

        NestOutcomePanel.Children.Clear();
        IReadOnlyList<int> shown = nest.SelectedSpeciesId is { } picked ? new[] { picked } : nest.PossibleOutcomeSpeciesIds;
        var parents = nest.ParticipantIds.Select(id => data.Individuals.FirstOrDefault(p => p.Id == id)).ToArray();
        var recipe = parents.Length == 2 && parents.All(p => p is not null) ? _recipes.Find(parents[0]!.CurrentSpeciesId, parents[1]!.CurrentSpeciesId) : null;
        var weight = recipe?.AvailableOutcomes.Where(o => nest.PossibleOutcomeSpeciesIds.Contains(o.SpeciesId)).Sum(o => o.Weight) ?? 0;
        foreach (var id in shown)
        {
            var known = data.NormalSpeciesIds.Contains(id);
            var cell = new StackPanel { Margin = new Thickness(0, 0, 14, 0), ToolTip = OutcomeName(data, id) };
            cell.Children.Add(Face(id, known, 44));
            var share = nest.SelectedSpeciesId is not null ? 100 : weight > 0 ? (int)Math.Round(100.0 * (recipe!.AvailableOutcomes.FirstOrDefault(o => o.SpeciesId == id)?.Weight ?? 0) / weight, MidpointRounding.AwayFromZero) : -1;
            cell.Children.Add(new TextBlock { Text = (known ? "" : "✦ ") + (share >= 0 ? $"{share}%" : "?"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (Brush)FindResource(known ? "MutedTextBrush" : "NdApricotBrush") });
            NestOutcomePanel.Children.Add(cell);
        }
        NestOutcomePanel.Visibility = shown.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        var variants = nest.PossibleOutcomeSpeciesIds.Count == 0 ? "Варианты этого старого гнезда не сохранены." : "Возможные виды: " + string.Join(", ", nest.PossibleOutcomeSpeciesIds.Select(id => OutcomeName(data, id))) + ".";

        NestTerms.Text = "Сохранённая цена: " + string.Join(", ", nest.ResourcePrices.Select(x => $"{ExpeditionView.BerryName(x.Key)} ×{x.Value}")) + ". " + (nest.SelectedSpeciesId is { } selected ? "Выбранный вид: " + OutcomeName(data, selected) + "." : variants + " Случайный исход скрыт до вылупления.");

        NestTime.Text = ready ? "Можно познакомиться с малышом." : $"Готовность: {nest.ReadyAtUtc.ToLocalTime():g} · осталось {Math.Max(0, Math.Ceiling((nest.ReadyAtUtc - DateTimeOffset.UtcNow).TotalMinutes))} мин";

        var total = (nest.ReadyAtUtc - nest.StartedAtUtc).TotalSeconds;
        NestProgress.Value = ready || total <= 0 ? 1 : Math.Clamp((DateTimeOffset.UtcNow - nest.StartedAtUtc).TotalSeconds / total, 0, 1);

        HatchButton.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
        HatchButton.IsEnabled = !_busy;

    }
    private bool Live(int generation) => !_suspended && generation == _lifetimeGeneration;
    private void ReleaseBusy()
    {
        _busy = false;
        // Result messages/reveals belong to the captured lifetime; enabled controls belong to the current one.
        if (!_suspended) Refresh();
    }

    private async Task TickAsync()
    {
        if (_suspended || _busy || _refreshingReady) return;
        var nest = _snapshot().Nest;
        if (nest?.Status != "running" || DateTimeOffset.UtcNow < nest.ReadyAtUtc)
        {
            if (nest is not null) Refresh();
            return;
        }

        var life = _lifetimeGeneration;
        _refreshingReady = true;
        try
        {
            await _refreshReady(DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            if (Live(life)) Show(ex.Message);
        }
        finally
        {
            _refreshingReady = false;
            if (!_suspended) Refresh();
        }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _suspended || !PairValid(_snapshot(), _first, _second)) return;
        var generation = _selectionGeneration;
        var life = _lifetimeGeneration;
        _pendingStart ??= new NurseryRequest([_first, _second], _choice, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var request = _pendingStart;
        _busy = true;
        Refresh();
        try
        {
            var result = await _start(request);
            if (!Live(life) || generation != _selectionGeneration) return;
            if (_snapshot().Nest?.Id != request.OperationId || !result.Success ||
                !_snapshot().CompletedOperations.ContainsKey($"nursery:start:{request.OperationId}"))
                throw new InvalidOperationException("Запуск не подтверждён сохранением.");
            _pendingStart = null; // the scene caption already says the nest is running; no extra status line
        }
        catch (Exception ex)
        {
            if (Live(life) && generation == _selectionGeneration) Show(ex.Message);
        }
        finally
        {
            ReleaseBusy();
        }
    }

    private async void HatchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _suspended || _snapshot().Nest is not { Status: "ready" } nest) return;
        if (_pendingHatchNest != nest.Id)
        {
            _pendingHatchNest = nest.Id;
            _pendingHatchOperation = Guid.NewGuid();
        }
        var life = _lifetimeGeneration;
        _busy = true;
        Refresh();
        try
        {
            var result = await _hatch(nest.Id, _pendingHatchOperation);
            if (!Live(life)) return;
            var current = _snapshot().Nest;
            if (!result.Success || current?.Id != nest.Id || current.Status != "hatched" ||
                current.HatchOperationId != _pendingHatchOperation || result.Child is null ||
                !_snapshot().HatchedNurseryResults.TryGetValue(nest.Id, out var child) || child.Id != result.Child.Id)
                throw new InvalidOperationException("Вылупление не подтверждено сохранением.");
            _pendingHatchNest = _pendingHatchOperation = Guid.Empty;
            Refresh(); // straight to the result: the baby fades in on it, no separate "appeared" step
            if (IsLoaded && SystemParameters.ClientAreaAnimation)
                BabyImage.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(450))
                {
                    FillBehavior = FillBehavior.Stop
                });
        }
        catch (Exception ex)
        {
            if (Live(life)) Show(ex.Message);
        }
        finally
        {
            ReleaseBusy();
        }
    }
    // "Ещё раз с этой парой": hunting a particular baby usually means the same parents again.
    private void Again_Click(object sender, RoutedEventArgs e)
    {
        var data = _snapshot();
        if (_busy || _suspended || data.Nest is not { Status: "hatched" } nest || nest.ParticipantIds.Count != 2) return;
        if (!PairValid(data, nest.ParticipantIds[0], nest.ParticipantIds[1])) { Show("Кто-то из родителей сейчас занят."); return; }
        _dismissedResult = nest.Id; _first = nest.ParticipantIds[0]; _second = nest.ParticipantIds[1];
        _choice = null; _pendingStart = null; _selectionGeneration++; ShowSelectedPairPage = _revealSelectedPair = true; Refresh();
    }
    private void NewNest_Click(object sender, RoutedEventArgs e) { if (_busy || _suspended) return; _dismissedResult = _snapshot().Nest?.Id ?? Guid.Empty; _first = _second = Guid.Empty; _choice = null; _pendingStart = null; Refresh(); }
    private void OpenPokedexButton_Click(object sender, RoutedEventArgs e) { var data = _snapshot(); if (!_busy && !_suspended && data.Nest is { Status: "hatched" } nest && data.HatchedNurseryResults.TryGetValue(nest.Id, out var child)) _openPokedex(child.Id); }
    private async void ActivateButton_Click(object sender, RoutedEventArgs e)
    {
        var data = _snapshot();
        if (_busy || _suspended || data.Nest is not { Status: "hatched" } nest ||
            !data.HatchedNurseryResults.TryGetValue(nest.Id, out var child)) return;
        var life = _lifetimeGeneration;
        _busy = true;
        Refresh();
        try
        {
            await _activate(child.Id);
            if (!Live(life)) return;
            if (_snapshot().ActiveIndividualId != child.Id)
                throw new InvalidOperationException("Спутник не подтверждён сохранением.");
            Show($"{SpeciesName(child.SpeciesId)} теперь на рабочем столе.");
        }
        catch (Exception ex)
        {
            if (Live(life)) Show(ex.Message);
        }
        finally
        {
            ReleaseBusy();
        }
    }

    private static readonly DiaryPiece[] NestMargin =
    [
        new("nursery-scheme", "Тепло снизу, сухо сверху, нервы в стороне."), new("nursery-thermometer"),
        new("nursery-paws", "Родители делают вид, что не волнуются."), new("nursery-small-egg"), new("hatch-shell"),
    ];
    private static readonly DiaryPiece[] HatchMargin =
    [
        new("hatch-shell", "Скорлупу не выбрасывать. Это память."), new("nursery-small-egg"), new("nursery-paws"), new("nursery-scheme"),
    ];

    // The margin is whatever the left-aligned 968 px column leaves on the right, below the full-width scene.
    private void PlaceMargin()
    {
        DiaryMargin.KeepColumn(this, 968);
        var column = Math.Min(Math.Max(0, ActualWidth - 48), 968);
        NurseryMargin.Width = Math.Max(0, ActualWidth - 24 - column - 16);
        NurseryMargin.Margin = new Thickness(0, SceneBorder.ActualHeight + 16, 8, 0);
    }

    private void RenderLuckSegments(int filled)
    {
        LuckSegments.Children.Clear();
        for (var i = 0; i < ShinyRules.NurseryLuckSteps; i++)
            LuckSegments.Children.Add(new Border
            {
                Margin = new Thickness(i == 0 ? 0 : 3, 0, 0, 0), CornerRadius = new CornerRadius(4),
                Background = i < filled ? ThemeBrushes.Get("NdShinyBrush") : ThemeBrushes.Get("NdSurfaceBrush"),
                BorderBrush = ThemeBrushes.Get("NdBorderBrush"), BorderThickness = new Thickness(1)
            });
    }
}
