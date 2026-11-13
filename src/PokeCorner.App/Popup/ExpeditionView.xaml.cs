using System.Globalization;
using System.IO;
using Image = System.Windows.Controls.Image;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using System.Windows.Controls.Primitives;
using PokeCorner.App.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using Brush = System.Windows.Media.Brush;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace PokeCorner.App.Popup;

public partial class ExpeditionView : UserControl
{
    private static readonly (string Id, string Name)[] Routes =
    [
        ("grove", "Лесная роща"), ("lake", "Озёрный берег"), ("coast", "Морское побережье"),
        ("mountains", "Горные тропы"), ("desert", "Пустынные дюны"), ("volcano", "Вулканическая долина"),
        ("snow", "Снежный перевал"), ("ruins", "Древние руины")
    ];
    private string? _displayedRoute;
    private readonly Func<SaveData> _snapshot;
    private readonly Func<ExpeditionRequest, ExpeditionPreview> _preview;
    private readonly Func<ExpeditionRequest, Task<ExpeditionAction>> _start;
    private readonly Func<Guid, DateTimeOffset, Task<ExpeditionAction>> _cancel;
    private readonly Func<Guid, Guid, Task<ExpeditionAction>> _claim;
    private readonly Func<Guid, Task<EncounterOpenResult>> _open;
    private readonly Action<Guid>? _enterEncounter;
    private readonly CatalogPackage? _catalog;
    private readonly LevelProgression? _levels;
    private readonly Func<bool>? _confirmCancel;
    private readonly Func<string, string, string, Task<bool>>? _confirmAsync;
    private readonly Func<string, string, string, Task<bool>>? _confirmDestructive;
    private readonly ExpeditionTeamSelector _selector;
    private readonly HashSet<Guid> _draft = [];
    private readonly Dictionary<(int, bool), ImageSource?> _spriteCache = [];
    private readonly Dictionary<string, ImageSource?> _rewardArtCache = [];
    private TaskCompletionSource<bool>? _localConfirmation;
    private ExpeditionPreview? _currentPreview;
    private bool _pickerOpen, _suspended, _continueAfterResult;
    private int _pickerPage, _generation;
    private string _pickerState = "";
    private readonly HashSet<Guid> _selected = [];
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private string _route = "grove";
    private ExpeditionRequest? _pendingStart;
    private Guid _pendingClaimId;
    private Guid _pendingClaimTripId;
    private bool _busy;
    private bool _initializing;
    private Guid _seededTripId;
    private bool _routeChosen;

    public ExpeditionView(Func<SaveData> snapshot, Func<ExpeditionRequest, ExpeditionPreview> preview,
        Func<ExpeditionRequest, Task<ExpeditionAction>> start, Func<Guid, DateTimeOffset, Task<ExpeditionAction>> cancel,
        Func<Guid, Guid, Task<ExpeditionAction>> claim, Func<Guid, Task<EncounterOpenResult>> open,
        CatalogPackage? catalog = null, Func<bool>? confirmCancel = null,
        Func<string, string, string, Task<bool>>? confirmAsync = null, Action<Guid>? enterEncounter = null,
        Func<string, string, string, Task<bool>>? confirmDestructiveAsync = null)
    {
        _snapshot = snapshot;
        _preview = preview;
        _start = start;
        _cancel = cancel;
        _claim = claim;
        _open = open;
        _enterEncounter = enterEncounter;
        _catalog = catalog;
        _levels = catalog is null ? null : new LevelProgression(catalog.DataRoot);
        _confirmCancel = confirmCancel;
        _confirmAsync = confirmAsync;
        _confirmDestructive = confirmDestructiveAsync;
        _selector = new ExpeditionTeamSelector(catalog);
        _initializing = true;
        InitializeComponent();
        foreach (var route in Routes)
        {
            // A postcard: the route's own picture under its short name. The picture arrives after the first frame.
            var face = new Grid { Height = 58 };
            face.Children.Add(new Image { Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            face.Children.Add(new System.Windows.Shapes.Rectangle { Fill = PostcardShade, IsHitTestVisible = false });
            face.Children.Add(new TextBlock
            {
                Text = route.Id switch { "grove" => "Роща", "lake" => "Озеро", "coast" => "Море", "mountains" => "Горы", "desert" => "Пустыня", "volcano" => "Вулкан", "snow" => "Снег", _ => "Руины" },
                FontSize = 12, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(2, 0, 2, 5), TextTrimming = TextTrimming.CharacterEllipsis
            });
            face.SizeChanged += (_, e) => face.Clip = new RectangleGeometry(new Rect(e.NewSize), 10, 10);
            var button = new Button
            {
                Content = face, Tag = route.Id, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(0), ToolTip = route.Name,
                Style = (Style)FindResource("NdTileButtonStyle"), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch
            };
            button.Click += (_, _) => { if (_busy) return; _route = route.Id; _routeChosen = true; _pendingStart = null; Refresh(); };
            RouteButtons.Children.Add(button);
        }
        SceneFrame.SizeChanged += (_, e) =>
        {
            SceneFrame.Clip = new RectangleGeometry(new Rect(e.NewSize), 13, 13);
            // The taped note needs a picture big enough to carry it; on a small scene the route name matters more.
            SceneNote.Visibility = e.NewSize.Width >= 430 && e.NewSize.Height >= 230 ? Visibility.Visible : Visibility.Collapsed;
        };
        StageGrid.SizeChanged += (_, _) => LayoutScene();
        RouteSelectionPanel.SizeChanged += (_, _) => LayoutScene();
        RouteSelectionPanel.IsVisibleChanged += (_, _) => LayoutScene();
        SceneMargin.Show("expedition", SceneMarginPieces);
        DifficultyBox.SelectedIndex = 1;
        DurationBox.SelectedIndex = 1;
        PickerTypeBox.Items.Add(new ComboBoxItem { Content = "Тип: любой", Tag = "" });
        foreach (var type in catalog?.Species.SelectMany(s => s.Types).Distinct().OrderBy(ExpeditionTeamSelector.TypeName).ToArray() ?? [])
            PickerTypeBox.Items.Add(new ComboBoxItem { Content = ExpeditionTeamSelector.TypeName(type), Tag = type });
        PickerTraitBox.Items.Add(new ComboBoxItem { Content = "Особенность: любая", Tag = "" });
        foreach (var trait in ExpeditionTeamSelector.TraitIds)
            PickerTraitBox.Items.Add(new ComboBoxItem { Content = CollectionView.TraitLabel(trait), Tag = trait });
        PickerTypeBox.SelectedIndex = PickerTraitBox.SelectedIndex = PickerSortBox.SelectedIndex = 0;
        LocalConfirmation.UseNightDiaryTheme();
        LocalConfirmation.Completed += CompleteLocalConfirmation;
        PickerScroll.SizeChanged += (_, _) => { if (_pickerOpen && !_suspended) RefreshPicker(); };
        PreviewKeyDown += (_, e) => {
            if (e.Key != System.Windows.Input.Key.Escape) return;
            if (_localConfirmation is not null) { CompleteLocalConfirmation(false); e.Handled = true; }
            else if (_pickerOpen) { ClosePicker(); Refresh(); e.Handled = true; }
        };
        _initializing = false;
        SizeChanged += (_, _) => {
            LayoutColumns();
            if (_pickerOpen) RefreshPicker();
        };
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) => { _suspended = false; _timer.Start(); Refresh(); LoadPostcards(); };
        Unloaded += (_, _) => {
            _suspended = true; _generation++; _timer.Stop();
            ClosePicker(); CompleteLocalConfirmation(false);
        };
        Refresh();
    }

    /// From a Pokédex hint: shows the route on the preparation screen. A trip already under way keeps its own route.
    public void SelectRoute(string routeId)
    {
        if (_busy || _snapshot().Expedition?.Status is "running" or "ready" || Routes.All(r => r.Id != routeId)) return;
        _route = routeId; _routeChosen = true; _pendingStart = null;
        if (_pickerOpen) ClosePicker();
        Refresh();
    }

    private string SpeciesName(int speciesId) =>
        _catalog?.Species.FirstOrDefault(s => s.Id == speciesId)?.DisplayName ?? $"№{speciesId}";

    private static string RouteName(string id) => Routes.FirstOrDefault(r => r.Id == id).Name ?? id;

    private ExpeditionRequest Request(DateTimeOffset now, IEnumerable<Guid>? team = null) => new(_route,
        (string)((ListBoxItem)DifficultyBox.SelectedItem).Tag,
        int.Parse((string)((ListBoxItem)DurationBox.SelectedItem).Tag, CultureInfo.InvariantCulture),
        (team ?? _selected).ToArray(), Guid.NewGuid(), now);

    private void ChoiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initializing && DifficultyBox.SelectedItem is not null && DurationBox.SelectedItem is not null)
        {
            if (_busy && _pendingStart is { } pending)
            {
                _initializing = true;
                DifficultyBox.SelectedItem = DifficultyBox.Items.OfType<ListBoxItem>().Single(item => Equals(item.Tag, pending.Difficulty));
                DurationBox.SelectedItem = DurationBox.Items.OfType<ListBoxItem>().Single(item => Equals(item.Tag, pending.DurationMinutes.ToString(CultureInfo.InvariantCulture)));
                _initializing = false;
                return;
            }
            _pendingStart = null;
            Refresh();
        }
    }

    private void Show(string message) => Show(message, null);

    // A status line belongs to the moment it was said in: "Поход начался." must not linger on the return screen.
    private string? _messageState;
    private string TripState() => _snapshot().Expedition is { } trip ? $"{trip.Id}:{trip.Status}" : "";

    // Errors lead with what happened to the player's stuff; the raw cause stays as a small second line.
    private void Show(string message, Exception? error)
    {
        if (_suspended) return;
        MessageText.Text = message;
        MessageDetailText.Text = error?.Message ?? "";
        MessageDetailText.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
        MessageGlyph.Visibility = error is null ? Visibility.Collapsed : Visibility.Visible;
        MessagePanel.Background = error is null ? (Brush)FindResource("NdSurfaceBrush") : (Brush)FindResource("NdDangerSurfaceBrush");
        MessagePanel.BorderBrush = error is null ? (Brush)FindResource("NdBorderBrush") : (Brush)FindResource("NdDangerLineBrush");
        MessagePanel.Visibility = Visibility.Visible;
        _messageState = error is null ? TripState() : null;
    }

    public void Refresh()
    {
        if (_initializing || _suspended) return;
        var data = _snapshot();
        var trip = data.Expedition;
        if (_messageState is not null && _messageState != TripState()) { MessagePanel.Visibility = Visibility.Collapsed; _messageState = null; }
        var active = trip?.Status is "running" or "ready";
        // Georgy 11.11: the team lived only in this view, so it survived a trip when the window stayed open
        // and vanished when the window had been closed meanwhile. The last finished trip is the default plan.
        if (!active && !_busy && trip is { Status: "claimed" } last && last.Id != _seededTripId)
        {
            _seededTripId = last.Id;
            _selected.Clear(); _selected.UnionWith(last.ParticipantIds.Take(3));
            if (!_routeChosen && Routes.Any(r => r.Id == last.LocationId)) _route = last.LocationId;
            _initializing = true;
            try
            {
                if (DifficultyBox.Items.OfType<ListBoxItem>().FirstOrDefault(i => Equals(i.Tag, last.Difficulty)) is { } difficulty)
                    DifficultyBox.SelectedItem = difficulty;
                if (DurationBox.Items.OfType<ListBoxItem>().FirstOrDefault(i =>
                        Equals(i.Tag, last.DurationMinutes.ToString(CultureInfo.InvariantCulture))) is { } duration)
                    DurationBox.SelectedItem = duration;
            }
            finally { _initializing = false; }
            _pendingStart = null;
        }
        if (active && _pickerOpen) ClosePicker();
        foreach (Button routeButton in RouteButtons.Children) routeButton.IsEnabled = !_busy && !active;
        DifficultyBox.IsEnabled = DurationBox.IsEnabled = !_busy && !active;
        // ponytail: result screen retired (loot is on the ready screen, encounter opens straight from the claim); its panel stays only because receipt tests still read ResultText.
        PreparationPanel.Visibility = PreparationDetailsPanel.Visibility = PreparationFooter.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        RouteSelectionPanel.Visibility = active || _pickerOpen ? Visibility.Collapsed : Visibility.Visible;
        ActionFooter.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        ContinueButton.Visibility = Visibility.Collapsed;
        if (!active) ClaimButton.Visibility = CancelButton.Visibility = Visibility.Collapsed;
        TripPanel.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        ResultPanel.Visibility = Visibility.Collapsed;
        EncounterPanel.Visibility = Visibility.Collapsed; // RefreshResult re-shows it for a claimed trip with an unopened encounter
        // In preparation the line beside the title says what this team will bring; on a trip it says how the trip is doing.
        SubtitleText.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        ContributionsText.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        if (!active) {
            TrainerNote.Text = RouteNote(_route);
            HeaderSketch.Source = (ImageSource)FindResource("NdMapImage");
        }
        var shownRoute = active ? trip!.LocationId : _route;
        var shownDifficulty = active ? trip!.Difficulty : Difficulty;
        RefreshLandscape(shownRoute);
        RouteHintText.Text = _selector.RouteHint(shownRoute, shownDifficulty);
        RenderRouteChips(shownRoute, shownDifficulty);
        if (active)
        {
            _currentPreview = null; // a plan's promise is not this trip's
            RefreshTrip(data, trip!);
            return;
        }

        foreach (Button button in RouteButtons.Children)
        {
            var selected = Equals(button.Tag, _route);
            button.BorderBrush = (Brush)FindResource(selected ? "NdApricotBrush" : "NdBorderBrush");
            button.BorderThickness = new Thickness(selected ? 2 : 1);
            button.Effect = selected ? (System.Windows.Media.Effects.Effect)FindResource("NdApricotGlow") : null;
            System.Windows.Automation.AutomationProperties.SetName(button, RouteName((string)button.Tag) + (selected ? " · выбран" : ""));
        }
        if (!_busy && _selected.RemoveWhere(id => data.Individuals.FirstOrDefault(p => p.Id == id) is not { } member ||
                _selector.Unavailable(member, data) is not null) > 0) _pendingStart = null;
        if (_pickerOpen) {
            var signature = string.Join("|", data.Individuals.Select(p => $"{p.Id}:{p.TotalXp}:{p.Nickname}")) + data.ActiveIndividualId +
                data.Nest?.Status + string.Join(",", data.Nest?.ParticipantIds ?? []);
            if (_pickerState != signature) { _pickerState = signature; RefreshPicker(); } // renders the plan itself
            else RenderPlan(data);
        }
        else RenderPlan(data);
        if (trip?.Status == "claimed") RefreshResult(data, trip);
    }

    // The team shown in the plan: the draft while the picker is open (it follows every click), the saved team otherwise.
    private HashSet<Guid> Team => _pickerOpen ? _draft : _selected;

    private void RenderPlan(SaveData data)
    {
        var team = Team;
        ChooseTeamButton.IsEnabled = AutoTeamButton.IsEnabled = !_busy && !_pickerOpen;
        ChooseTeamButton.Visibility = AutoTeamButton.Visibility = _pickerOpen ? Visibility.Hidden : Visibility.Visible;
        TeamHeading.Text = $"Команда {team.Count}/3";
        StartButton.Content = _busy ? "Отправляем команду…" : _pendingStart is not null ? "Повторить отправку" : "Отправить команду";
        ChooseTeamButton.Content = team.Count == 0 ? "Выбрать" : "Изменить";
        if (team.Count == 0)
        {
            PreviewText.Text = "Выберите хотя бы одного участника.";
            ContributionsText.Text = "Выберите команду из 1–3 питомцев. Полная тройка может принести дополнительные находки и встречу.";
            _currentPreview = null;
            RenderRouteOutlook();
            StartButton.IsEnabled = false;
        }
        else
        {
            try
            {
                var preview = _currentPreview = _preview(Request(DateTimeOffset.UtcNow, team));
                LootLabel.Text = "ГАРАНТИРОВАННАЯ ДОБЫЧА";
                RenderRewards(PreviewRewardCards, preview.PrimaryBerryId, preview.PrimaryBerries, preview.Coins, $"{preview.XpPerParticipant} XP каждому");
                PreviewText.Text = _pickerOpen ? "Нажмите «Готово», и команду можно отправлять." : "Вернутся через " + ((ListBoxItem)DurationBox.SelectedItem).Content;
                PreviewText.ToolTip = $"Ожидаемое возвращение: {preview.ReadyAtUtc.ToLocalTime():g}";
                ContributionsText.Text = team.Count < 3
                    ? "Добыча и опыт доступны. Для дополнительных находок и встречи нужны трое."
                    : "Полная команда: возможны находки и встреча.";
                StartButton.IsEnabled = !_busy && !_pickerOpen;
            }
            catch (Exception ex)
            {
                _currentPreview = null; PreviewRewardCards.Children.Clear();
                PreviewText.Text = ex.Message;
                ContributionsText.Text = "";
                StartButton.IsEnabled = false;
            }
        }
        // After the preview: a slot shows the XP its pet will bring home.
        RenderSlots(TeamSlots, team, data, _pickerOpen);
    }

    // Wide windows give the plan more room; a narrow one keeps the scene from collapsing.
    private void LayoutColumns()
    {
        var width = ActualWidth >= 1300 ? 372 : ActualWidth >= 980 ? 340 : ActualWidth >= 760 ? 300 : 248;
        if (Math.Abs(PlanColumn.Width.Value - width) > 0.5) PlanColumn.Width = new GridLength(width);
        LayoutScene();
    }

    private static readonly DiaryPiece[] PlanMarginPieces =
    [
        new("exp-map", "Маршрут начерчен. Обратно по нему же."), new("exp-lantern", "Без меня там справятся. Наверное."), new("exp-tracks"),
    ];

    private void ScenarioContent_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        PlanMargin.Margin = new Thickness(0, ScenarioContent.ActualHeight + 18, 0, 0);
        PlanMargin.Show("expedition-plan", PlanMarginPieces);
    }

    private static readonly DiaryPiece[] SceneMarginPieces =
    [
        new("exp-tent", "Палатка за четыре минуты. Личный рекорд."), new("exp-tracks", "Следы свежие. Чьи, выясним."),
        new("exp-binoculars", "Сначала бинокль. Потом ноги."), new("exp-lantern", "Фонарь беру всегда. Даже днём."),
    ];

    // D-54: the scene is as tall as its 3:1 picture allows with a mild crop (about four fifths of it stays in view).
    // When the window gives much more height than that, the rest of the page goes to the diary margin, not to the crop.
    private void LayoutScene()
    {
        var width = StageGrid.ActualWidth - PlanColumn.Width.Value - 14; var height = StageGrid.ActualHeight;
        if (width <= 0 || height <= 0) return;
        var postcards = RouteSelectionPanel.IsVisible ? RouteSelectionPanel.ActualHeight + 10 : 0;
        var free = Math.Max(96, height - postcards);
        var panorama = width / 2.4;
        var scene = free - panorama >= 120 ? panorama : free;
        if (double.IsNaN(Landscape.Height) || Math.Abs(Landscape.Height - scene) > .5) Landscape.Height = scene;
    }

    private static readonly Brush PostcardShade = Shade();
    private static Brush Shade()
    {
        var brush = new LinearGradientBrush(Color.FromArgb(0, 12, 15, 31), Color.FromArgb(215, 12, 15, 31), 90);
        brush.Freeze(); return brush;
    }

    // Type chips and the recommended level, on the picture itself.
    private string _chipState = "";
    private void RenderRouteChips(string route, string difficulty)
    {
        var state = route + "|" + difficulty;
        if (_chipState == state) return;
        _chipState = state;
        RouteChips.Children.Clear();
        void Chip(string text, Brush ink, string tip) => RouteChips.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 3, 10, 4), Margin = new Thickness(0, 0, 6, 4), BorderThickness = new Thickness(1),
            BorderBrush = ink, Background = new SolidColorBrush(Color.FromArgb(200, 16, 20, 38)), ToolTip = tip,
            Child = new TextBlock { Text = text, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = ink }
        });
        foreach (var type in _selector.RouteTypes(route))
            Chip(ExpeditionTeamSelector.TypeName(type), TypeDisplay.GetBrush(type), "Питомцы этого типа помогают команде на маршруте.");
        Chip($"рек. ур. {_selector.Recommended(difficulty)}", (Brush)FindResource("NdApricotBrush"), "Рекомендуемый уровень для выбранной сложности.");
    }

    // Postcards are decoded small and one at a time after the screen is up, so opening the tab never waits for eight pictures.
    private bool _postcardsLoaded;
    private void LoadPostcards()
    {
        if (_postcardsLoaded) return;
        _postcardsLoaded = true;
        foreach (Button button in RouteButtons.Children)
            Dispatcher.BeginInvoke(() =>
            {
                if (button.Content is Grid face && face.Children[0] is Image image && image.Source is null) image.Source = Postcard((string)button.Tag);
            }, DispatcherPriority.Background);
    }

    private ImageSource? Postcard(string routeId)
    {
        try
        {
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            if (routeId == "grove") bitmap.UriSource = new Uri("pack://application:,,,/PokeCorner;component/Assets/NightDiary/grove-environment.png");
            else
            {
                var path = _catalog is null ? null : Path.Combine(_catalog.DataRoot, "expeditions", routeId + ".png");
                if (path is null || !File.Exists(path)) return null;
                bitmap.UriSource = new Uri(path);
            }
            bitmap.DecodePixelWidth = 260;
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or NotSupportedException or ArgumentException or UriFormatException) { return null; }
    }

    // Trainer's field note for the chosen place: flavour drawn from each route's art brief, never a mechanic.
    // Each note names who lives there by type, from the route's own type weights (D-24).
    private static string RouteNote(string route) => route switch
    {
        "grove" => "В листве шуршит. Жуки делают вид, что это не они.",
        "lake" => "Вода гладкая. Слишком гладкая. Водные что-то задумали.",
        "coast" => "Соль на губах, песок в ботинках. Над волной кружат не чайки.",
        "mountains" => "Воздуха мало, вида много. Выше, говорят, драконы.",
        "desert" => "Песок везде. Даже в бутерброде. А под ним кто-то роет.",
        "volcano" => "Подошвы плавятся. Огненным тут курорт.",
        "snow" => "Нос замёрз, зато следы видно. Ледяные где-то рядом.",
        _ => "Темно, и кто-то дышит в затылок. Призраки шутят."
    };

    private string Difficulty => (DifficultyBox.SelectedItem as ListBoxItem)?.Tag as string ?? "normal";
    private string DisplayName(PokemonIndividual pet, SaveData data) => _selector.Name(pet) + (pet.Nickname is null ? "" : $" · {pet.Nickname}") + (pet.LooksShiny ? " · шайни" : "")
        + (IndividualDisplay.Discriminator(data.Individuals, pet.Id) is { Length: > 0 } label ? "\n" + label : "");
    public static string Coins(long n) => n + (n % 10 == 1 && n % 100 != 11 ? " монета" : n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 ? " монеты" : " монет");
    internal static string BerryName(string id) => id switch { "rawst" => "Равст", "oran" => "Оран", "pecha" => "Печа", "cheri" => "Чери", "chesto" => "Често", "aspear" => "Аспир", _ => "ягоды" };
    private static string ItemName(string id) => id switch { "evolution-stone" => "Камень эволюции", "offspring-charm" => "Талисман потомства", "lucky-charm" => "Талисман удачи", _ => "Предмет" };
    private ImageSource? Sprite(PokemonIndividual pet)
    {
        var key = (pet.CurrentSpeciesId, IsShiny: pet.LooksShiny);
        if (_spriteCache.TryGetValue(key, out var cached)) return cached;
        ImageSource? image = null;
        try { var path = _catalog?.GetAssetPath(key.CurrentSpeciesId, key.IsShiny) ?? (key.IsShiny ? _catalog?.GetAssetPath(key.CurrentSpeciesId, false) : null);
            if (path is not null && File.Exists(path)) image = SpriteImages.Decode(File.ReadAllBytes(path)); }
        catch (Exception error) when (error is IOException or InvalidOperationException or NotSupportedException or ArgumentException) { }
        if (_spriteCache.Count >= 128) _spriteCache.Clear();
        _spriteCache[key] = image; return image;
    }
    // One row per place in the team: who, level against the route's recommendation, fit, and the XP coming home.
    // Rebuilt only when something shown changes, so a hover or a click is never cut short by the one-second refresh.
    private void RenderSlots(UniformGrid panel, IEnumerable<Guid> ids, SaveData data, bool editable, IReadOnlyDictionary<Guid, long>? xpGain = null)
    {
        var members = ids.ToArray();
        var route = data.Expedition is { Status: "running" or "ready" } trip ? trip.LocationId : _route;
        var difficulty = data.Expedition is { Status: "running" or "ready" } current ? current.Difficulty : Difficulty;
        var recommended = _selector.Recommended(difficulty);
        long? Xp(Guid id)
        {
            if (xpGain is not null) return xpGain.TryGetValue(id, out var gained) ? gained : null;
            return _currentPreview is { } preview && preview.XpByParticipant.TryGetValue(id, out var promised) ? promised : null;
        }
        var state = string.Join("|", members.Select(id => data.Individuals.FirstOrDefault(p => p.Id == id) is { } m
            ? $"{id}:{m.CurrentSpeciesId}:{m.LooksShiny}:{m.Nickname}:{_selector.Level(m)}:{Xp(id)}" : id.ToString()))
            + $"#{editable}#{_busy}#{route}#{difficulty}#{xpGain is not null}";
        if (panel.Tag as string == state && panel.Children.Count == 3) return;
        panel.Tag = state;
        panel.Children.Clear();
        for (var i = 0; i < 3; i++) {
            var member = i < members.Length ? data.Individuals.FirstOrDefault(p => p.Id == members[i]) : null;
            if (member is null) {
                var invite = new Grid { MinHeight = 38 };
                invite.Children.Add(new System.Windows.Shapes.Rectangle { Stroke = (Brush)FindResource("NdBorderBrush"), StrokeThickness = 1.2, StrokeDashArray = [4, 3], RadiusX = 11, RadiusY = 11 });
                invite.Children.Add(new TextBlock { Text = ReferenceEquals(panel, TeamSlots) && !editable ? "+  Выбрать покемона" : $"Место {i + 1} свободно", Foreground = (Brush)FindResource(ReferenceEquals(panel, TeamSlots) ? "NdLavenderBrush" : "NdMutedBrush"), FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                if (ReferenceEquals(panel, TeamSlots) && !editable) {
                    var slot = new Button { Content = invite, Style = (Style)FindResource("NdLinkButtonStyle"), Padding = new Thickness(0), Margin = new Thickness(0, 0, 0, 5), MinHeight = 38, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, IsEnabled = !_busy };
                    System.Windows.Automation.AutomationProperties.SetName(slot, $"Выбрать покемона на место {i + 1}");
                    slot.Click += ChooseTeam_Click;
                    panel.Children.Add(slot);
                } else {
                    invite.Margin = new Thickness(0, 0, 0, 5);
                    System.Windows.Automation.AutomationProperties.SetName(invite, $"Пустое место {i + 1}");
                    panel.Children.Add(invite);
                }
                continue;
            }
            var level = _selector.Level(member);
            var fits = _selector.Matches(member, route);
            var row = new DockPanel { LastChildFill = true };
            var sprite = new Image { Source = Sprite(member), Width = 32, Height = 32, Margin = new Thickness(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center };
            RenderOptions.SetBitmapScalingMode(sprite, BitmapScalingMode.NearestNeighbor);
            DockPanel.SetDock(sprite, Dock.Left);
            row.Children.Add(sprite);
            if (editable) {
                var remove = new Button { Content = "×", Tag = member.Id, ToolTip = "Убрать из команды", Width = 26, Height = 26, MinHeight = 26, Padding = new Thickness(0), Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Style = (Style)FindResource("NdTileButtonStyle") };
                System.Windows.Automation.AutomationProperties.SetName(remove, "Убрать из команды: " + _selector.Name(member));
                remove.Click += (_, _) => { _draft.Remove((Guid)remove.Tag); RefreshPicker(); };
                DockPanel.SetDock(remove, Dock.Right);
                row.Children.Add(remove);
            }
            var side = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            if (fits) side.Children.Add(new TextBlock { Text = "✓ подходит", FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("NdSuccessBrush"), HorizontalAlignment = HorizontalAlignment.Right });
            if (Xp(member.Id) is { } points) side.Children.Add(new TextBlock { Text = $"+{points} XP", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource(xpGain is null ? "NdLavenderBrush" : "NdApricotBrush"), HorizontalAlignment = HorizontalAlignment.Right });
            DockPanel.SetDock(side, Dock.Right);
            if (side.Children.Count > 0) row.Children.Add(side);
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = _selector.Name(member) + (member.LooksShiny ? " ✦" : ""), FontWeight = FontWeights.Bold, FontSize = 13.5, TextTrimming = TextTrimming.CharacterEllipsis });
            text.Children.Add(new TextBlock { Text = $"ур. {level}" + (level < recommended ? " · ниже рекомендуемого" : member.Nickname is { Length: > 0 } nick ? " · " + nick : ""), FontSize = 12, Foreground = (Brush)FindResource("NdMutedBrush"), TextTrimming = TextTrimming.CharacterEllipsis });
            row.Children.Add(text);
            row.ToolTip = $"{DisplayName(member, data)}\nУровень {level}, рекомендуемый для похода — {recommended}\nМаршрут: {(fits ? "подходящий тип — помогает команде" : "без совпадения типа")}\nОсобенности: {_selector.Traits(member)}";
            System.Windows.Automation.AutomationProperties.SetName(row, DisplayName(member, data));
            panel.Children.Add(new Border { Child = row, Padding = new Thickness(8, 2, 10, 2), Margin = new Thickness(0, 0, 0, 5), MinHeight = 38, CornerRadius = new CornerRadius(11), BorderThickness = new Thickness(1), BorderBrush = (Brush)FindResource("NdBorderBrush"), Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1D, 0x36)) });
        }
    }
    private void ChooseTeam_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _suspended || _pickerOpen || _snapshot().Expedition?.Status is "running" or "ready") return;
        _draft.Clear(); _draft.UnionWith(_selected); _pickerOpen = true;
        PickerPanel.Visibility = Visibility.Visible; RouteSelectionPanel.Visibility = Visibility.Collapsed;
        ResetPickerFilters(); RefreshPicker(); PickerSearchBox.Focus();
        Theme.Motion.Enter(PickerPanel, 10, 180);
    }
    private void ClosePicker()
    {
        _pickerOpen = false; _draft.Clear(); PickerPanel.Visibility = Visibility.Collapsed; RouteSelectionPanel.Visibility = Visibility.Visible;
        if (!_suspended) ChooseTeamButton.Focus();
    }
    // The best free three for this route: type match first, then level against the recommendation, then level.
    private Guid[] BestTeam(SaveData data) => _selector.Query(data, _route, Difficulty, "", null, null, true, false, 0).Take(3).Select(p => p.Id).ToArray();
    private void PickerAuto_Click(object sender, RoutedEventArgs e)
    {
        if (!_pickerOpen || _busy || _suspended) return;
        _draft.Clear(); _draft.UnionWith(BestTeam(_snapshot()));
        RefreshPicker();
    }
    private void AutoTeam_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _suspended || _pickerOpen || _snapshot().Expedition?.Status is "running" or "ready") return;
        var best = BestTeam(_snapshot());
        if (best.Length == 0) { Show("Свободных питомцев сейчас нет: все заняты или на рабочем столе."); return; }
        _selected.Clear(); _selected.UnionWith(best); _pendingStart = null;
        Refresh();
    }
    private void PickerCancel_Click(object sender, RoutedEventArgs e) { ClosePicker(); Refresh(); }
    private void PickerDone_Click(object sender, RoutedEventArgs e)
    {
        if (!_pickerOpen || _draft.Count is < 1 or > 3 || _busy || _suspended) return;
        var data = _snapshot();
        if (data.Expedition?.Status is "running" or "ready") { ClosePicker(); Refresh(); return; }
        if (_draft.Any(id => data.Individuals.FirstOrDefault(p => p.Id == id) is not { } p || _selector.Unavailable(p, data) is not null)) { RefreshPicker(); return; }
        _selected.Clear(); _selected.UnionWith(_draft); _pendingStart = null; ClosePicker(); Refresh();
    }
    private void PickerFilterChanged(object sender, RoutedEventArgs e) { if (_initializing || !_pickerOpen) return; _pickerPage = 0; RefreshPicker(); }
    private void ResetPickerFilters()
    {
        _initializing = true; PickerSearchBox.Text = ""; PickerTypeBox.SelectedIndex = PickerTraitBox.SelectedIndex = PickerSortBox.SelectedIndex = 0;
        PickerAvailableBox.IsChecked = true; PickerRouteBox.IsChecked = false; _pickerPage = 0; _initializing = false;
    }
    private void PickerReset_Click(object sender, RoutedEventArgs e) { ResetPickerFilters(); RefreshPicker(); }
    private void PickerPrevious_Click(object sender, RoutedEventArgs e) { _pickerPage--; RefreshPicker(); PickerScroll.ScrollToTop(); }
    private void PickerNext_Click(object sender, RoutedEventArgs e) { _pickerPage++; RefreshPicker(); PickerScroll.ScrollToTop(); }
    private System.Windows.Size _host;

    // The picker page is sized inside the layout pass, so the first frame at a new window size is right.
    protected override System.Windows.Size MeasureOverride(System.Windows.Size constraint)
    {
        if (!double.IsInfinity(constraint.Width) && !double.IsInfinity(constraint.Height) &&
            (Math.Abs(constraint.Width - _host.Width) > 1 || Math.Abs(constraint.Height - _host.Height) > 1))
        {
            _host = constraint;
            if (_pickerOpen && !_suspended) RefreshPicker();
        }
        return base.MeasureOverride(constraint);
    }

    private void RefreshPicker()
    {
        if (!_pickerOpen || _suspended) return;
        var data = _snapshot();
        _draft.RemoveWhere(id => data.Individuals.FirstOrDefault(p => p.Id == id) is not { } p || _selector.Unavailable(p, data) is not null);
        var rows = _selector.Query(data, _route, Difficulty, PickerSearchBox.Text,
            (PickerTypeBox.SelectedItem as ComboBoxItem)?.Tag as string, (PickerTraitBox.SelectedItem as ComboBoxItem)?.Tag as string,
            PickerAvailableBox.IsChecked == true, PickerRouteBox.IsChecked == true, PickerSortBox.SelectedIndex);
        // Tiles of the same family as the Pokédex grid: sprite, name, level, plus the two facts a team pick needs
        // (route fit, traits). Sized from the space the view was given, not from the scroll viewer, whose size is
        // still the previous window's while a resize is being laid out.
        var plan = PlanColumn.Width.Value;
        var viewportWidth = _host.Width > 0 ? _host.Width - 48 - plan - 14 - 30 - 18 : PickerScroll.ViewportWidth > 0 ? PickerScroll.ViewportWidth - 2 : Math.Max(400, ActualWidth - 48);
        var columns = Math.Max(2, (int)((viewportWidth - 8) / 138));
        var width = Math.Floor((viewportWidth - 8) / columns) - 8;
        // As many full rows as the window height holds, so FullHD fills its grid instead of a void.
        var rowsFit = _host.Height > 0 ? Math.Clamp((int)((_host.Height - 330) / 150), 2, 8) : 3; // 330: title, filters, footer
        var pageSize = Math.Max(12, columns * rowsFit);
        var pages = Math.Max(1, (rows.Count + pageSize - 1) / pageSize); _pickerPage = Math.Clamp(_pickerPage, 0, pages - 1);
        var order = _draft.ToList();
        PickerMembers.Children.Clear();
        foreach (var member in rows.Skip(_pickerPage * pageSize).Take(pageSize)) {
            var reason = _selector.Unavailable(member, data); var chosen = _draft.Contains(member.Id);
            var fits = _selector.Matches(member, _route);
            var content = new StackPanel();
            var well = new Grid { Height = 60 };
            var sprite = new Image { Source = Sprite(member), Width = 56, Height = 56, HorizontalAlignment = HorizontalAlignment.Center };
            RenderOptions.SetBitmapScalingMode(sprite, BitmapScalingMode.NearestNeighbor);
            well.Children.Add(sprite);
            if (fits) well.Children.Add(new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(Color.FromRgb(0x24, 0x40, 0x3A)), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, ToolTip = "Подходит маршруту",
                Child = new TextBlock { Text = "✓", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("NdSuccessBrush"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
            if (chosen) well.Children.Add(new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = (Brush)FindResource("NdApricotFaceBrush"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock { Text = (order.IndexOf(member.Id) + 1).ToString(CultureInfo.InvariantCulture), FontSize = 12, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("NdOnApricotBrush"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
            content.Children.Add(well);
            content.Children.Add(new TextBlock { Text = DisplayName(member, data).Split('\n')[0], FontWeight = FontWeights.Bold, FontSize = 13, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 3, 0, 0) });
            content.Children.Add(new TextBlock { Text = $"ур. {_selector.Level(member)} · #{member.CurrentSpeciesId:D4}", FontSize = 12, TextAlignment = TextAlignment.Center, Foreground = (Brush)FindResource("NdMutedBrush") });
            content.Children.Add(new TextBlock { Text = string.Join(" / ", _selector.Types(member).Select(ExpeditionTeamSelector.TypeName)), FontSize = 11.5, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = (Brush)FindResource(fits ? "NdSuccessBrush" : "NdMutedBrush"), Margin = new Thickness(0, 1, 0, 0) });
            content.Children.Add(new TextBlock { Text = string.Join(" · ", new[] { member.FirstTraitId, _selector.Level(member) >= _selector.SecondTraitLevel ? member.SecondTraitId : null }.Where(t => !string.IsNullOrWhiteSpace(t)).Select(CollectionView.TraitLabel)), FontSize = 11.5, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = (Brush)FindResource("NdMutedBrush") });
            if (reason is not null) content.Children.Add(new TextBlock { Text = reason, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontSize = 11.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 1, 0, 0) });
            var button = new Button { Content = content, Tag = member.Id, Width = width, MinHeight = 134,
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top,
                Margin = new Thickness(4), Padding = new Thickness(8, 7, 8, 7), Style = (Style)FindResource("NdTileButtonStyle"),
                IsEnabled = reason is null && (chosen || _draft.Count < 3),
                ToolTip = (reason ?? (chosen ? "Убрать из команды" : _draft.Count >= 3 ? "В команде уже трое. Уберите участника, чтобы выбрать другого." : "Выбрать питомца")) + "\n" + DisplayName(member, data) + "\n" + _selector.Traits(member),
                BorderThickness = new Thickness(chosen ? 2 : 1), BorderBrush = (Brush)FindResource(chosen ? "NdApricotBrush" : "NdBorderBrush") };
            if (chosen) button.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x26, 0x40));
            System.Windows.Automation.AutomationProperties.SetName(button, DisplayName(member, data) + " · " + (reason ?? (chosen ? "выбран" : "выбрать")));
            button.Click += (_, _) => { if (!button.IsEnabled || !_pickerOpen) return; if (!_draft.Remove(member.Id) && _draft.Count < 3) _draft.Add(member.Id); RefreshPicker(); };
            PickerMembers.Children.Add(button);
        }
        RenderPlan(data);
        PickerRouteHintText.Text = $"{RouteName(_route)} · {((ListBoxItem)DifficultyBox.SelectedItem).Content} · {((ListBoxItem)DurationBox.SelectedItem).Content} · {_selector.RouteHint(_route, Difficulty)}";
        PickerCountText.Text = $"Выбрано {_draft.Count} / 3 · найдено {rows.Count}";
        PickerAutoButton.IsEnabled = !_busy;
        PickerPageText.Text = $"{_pickerPage + 1} / {pages}";
        PickerPreviousButton.IsEnabled = _pickerPage > 0; PickerNextButton.IsEnabled = _pickerPage + 1 < pages;
        PickerDoneButton.IsEnabled = _draft.Count is >= 1 and <= 3;
        PickerEmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Continue_Click(object sender, RoutedEventArgs e) { if (_busy) return; _continueAfterResult = true; Refresh(); }
    private ImageSource? ShopArt(string id)
    {
        if (_rewardArtCache.TryGetValue(id, out var cached)) return cached;
        ImageSource? image = null;
        var path = _catalog is null ? null : Path.Combine(_catalog.DataRoot, "final", "assets", "shop", id + ".png");
        try { if (path is not null && File.Exists(path)) image = SpriteImages.Decode(File.ReadAllBytes(path)); }
        catch (Exception error) when (error is IOException or InvalidOperationException or NotSupportedException or ArgumentException) { }
        return _rewardArtCache[id] = image;
    }

    // Extras were rolled when the team set out, so the ready screen can show the whole haul in one row.
    // Before a team is chosen the route still says what it gives (Georgy 04.11, item 1): the primary berry
    // with the floor any team gets, the berries extra finds can bring, and the flat XP. Coins depend on the team.
    private void RenderRouteOutlook()
    {
        PreviewRewardCards.Children.Clear();
        ExpeditionService.RouteOutlook outlook;
        try { if (_catalog is null) throw new InvalidOperationException(); outlook = ExpeditionService.Outlook(_catalog, _route); }
        catch (Exception ex) when (ex is InvalidOperationException or System.IO.IOException or System.Text.Json.JsonException or KeyNotFoundException)
        {
            LootLabel.Text = "ДОБЫЧА ПОЯВИТСЯ ПОСЛЕ ВЫБОРА КОМАНДЫ";
            return;
        }
        var minutes = DurationBox.SelectedItem is ListBoxItem { Tag: string tag } && int.TryParse(tag, out var m) ? m : 360;
        var berries = minutes * outlook.MinimumPrimaryPerHour / 60;
        // Only the easy trip guarantees the floor (D-29); on harder ones the amount depends on the team's level.
        var easy = Difficulty == "easy";
        LootLabel.Text = easy ? "ДАЖЕ С ОДНИМ ПИТОМЦЕМ" : "МАРШРУТ ДАЁТ";
        var cards = new List<(ImageSource? Image, string Text, string Tip)>
        {
            (ShopArt(outlook.PrimaryBerryId), easy ? $"{BerryName(outlook.PrimaryBerryId)} от {berries}" : $"{BerryName(outlook.PrimaryBerryId)} — основная",
                easy ? "Основная ягода маршрута. С сильной командой её больше." : "Основная ягода маршрута. Сколько — зависит от уровня команды: слабой команде сложный поход даёт меньше."),
            (null, $"{minutes * outlook.XpPerHour / 60} XP каждому", "Опыт не зависит от сложности и состава."),
            (ShopArt(outlook.ExtraBerryIds.FirstOrDefault() ?? outlook.PrimaryBerryId),
                string.Join(", ", outlook.ExtraBerryIds.Select(BerryName)) + " — находки", "Попутные ягоды и монеты находит только полная тройка."),
        };
        foreach (var (image, text, tip) in cards) PreviewRewardCards.Children.Add(RewardCard(image, text, tip));
    }

    // A small card of the plan: the picture on top, the amount under it.
    private Border RewardCard(ImageSource? image, string text, string? tip)
    {
        var column = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        if (image is not null) column.Children.Add(new Image { Source = image, Width = 28, Height = 28, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 3) });
        else column.Children.Add(new TextBlock { Text = "XP", FontFamily = (System.Windows.Media.FontFamily)FindResource("NdDisplayFont"), FontSize = 15, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("NdLavenderBrush"), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 6) });
        column.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontSize = 12, FontWeight = FontWeights.Bold, MaxHeight = 34, TextTrimming = TextTrimming.CharacterEllipsis });
        return new Border { Child = column, ToolTip = tip ?? text, Padding = new Thickness(5, 7, 5, 7), Margin = new Thickness(0, 0, 6, 6), MinHeight = 66, CornerRadius = new CornerRadius(11), BorderThickness = new Thickness(1), BorderBrush = (Brush)FindResource("NdBorderBrush"), Background = new SolidColorBrush(Color.FromRgb(0x14, 0x17, 0x2B)) };
    }

    private void RenderRewards(UniformGrid panel, string berry, int berries, int coins, string? xp, ExpeditionRewards? extras = null)
    {
        panel.Children.Clear();
        var berryCounts = new Dictionary<string, int> { [berry] = berries };
        foreach (var (id, count) in extras?.ExtraBerries ?? []) berryCounts[id] = berryCounts.GetValueOrDefault(id) + count;
        var cards = berryCounts.Select(x => (ShopArt(x.Key), $"{BerryName(x.Key)} ×{x.Value}")).ToList();
        cards.Add(((ImageSource)FindResource("UiCoinImage"), Coins(coins + (extras?.ExtraCoins ?? 0))));
        foreach (var (id, count) in extras?.ExtraItems ?? []) cards.Add((ShopArt(id), $"{ItemName(id)} ×{count}"));
        if (xp is not null) cards.Add((null, xp));
        panel.Columns = 3;
        foreach (var (image, text) in cards) panel.Children.Add(RewardCard(image, text, null));
    }

    private void RefreshLandscape(string routeId)
    {
        LandscapeName.Text = RouteName(routeId);
        if (_displayedRoute == routeId) return;
        _displayedRoute = routeId;
        if (routeId == "grove")
        {
            LandscapeImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/PokeCorner;component/Assets/NightDiary/grove-environment.png"));
            return;
        }
        var path = _catalog is null ? null : System.IO.Path.Combine(_catalog.DataRoot, "expeditions", routeId + ".png");
        LandscapeImage.Source = path is not null && System.IO.File.Exists(path)
            ? SpriteImages.Decode(System.IO.File.ReadAllBytes(path)) : null;
        Landscape.Visibility = Visibility.Visible;
    }
    private void RefreshTrip(SaveData data, ExpeditionState trip)
    {
        var ready = trip.Status == "ready" || DateTimeOffset.UtcNow >= trip.ReadyAtUtc;
        HeaderSketch.Source = (ImageSource)FindResource(ready ? "NdBackpackImage" : "NdMapImage");
        TripProgress.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
        TripTitle.Text = ready ? "С возвращением!" : "Команда в походе";
        TripTitle.FontSize = ready ? 18 : 15;
        TripGlyph.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
        TripHeader.HorizontalAlignment = TripTerms.HorizontalAlignment = ready ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        TripTimeRow.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
        SubtitleText.Text = ready ? "Находки готовы и ждут получения." : "Условия похода сохранены. Команда вернётся сама.";
        TrainerNote.Text = ready ? "Вернулись! Рюкзак трещит, глаза горят." : "Ушли. Обещали писать. Не напишут.";
        var difficulty = DifficultyBox.Items.OfType<ListBoxItem>().FirstOrDefault(item => Equals(item.Tag, trip.Difficulty));
        TripTerms.Text = ready ? (trip.Rewards.HasEncounter ? "Поход завершён. В пути команда кого-то встретила." : "Поход завершён. Находки ждут получения.") : $"{RouteName(trip.LocationId)} · {difficulty?.Content ?? trip.Difficulty} · {trip.DurationMinutes} мин";
        var xpGain = new Dictionary<Guid, long>();
        TripTeam.Text = string.Join(" · ", trip.ParticipantIds.Select(id =>
        {
            var member = data.Individuals.FirstOrDefault(p => p.Id == id);
            return member is null ? "Участник" : $"{DisplayName(member, data)} · особенности {_selector.Traits(member)}";
        }));
        var reward = trip.Rewards;
        var berryRemainder = data.BerryRemainders[reward.PrimaryBerryId];
        var coinRemainder = data.CoinRemainder;
        var berries = new RationalRemainder { Numerator = berryRemainder.Numerator, Denominator = berryRemainder.Denominator }
            .Settle(reward.PrimaryNumerator, reward.PrimaryDenominator);
        var coins = new RationalRemainder { Numerator = coinRemainder.Numerator, Denominator = coinRemainder.Denominator }
            .Settle(reward.CoinNumerator, reward.CoinDenominator);
        var totalXp = 0;
        var xp = string.Join(", ", trip.ParticipantIds.Select(id =>
        {
            var member = data.Individuals.Single(p => p.Id == id);
            var amount = new RationalRemainder { Numerator = member.XpRemainder.Numerator, Denominator = member.XpRemainder.Denominator }.Settle(reward.XpNumerator, reward.XpDenominator);
            totalXp += checked((int)amount);
            xpGain[id] = amount;
            return $"{SpeciesName(member.CurrentSpeciesId)} +{amount} XP";
        }));
        RenderSlots(TripSlots, trip.ParticipantIds, data, false, ready ? xpGain : null);
        RenderRewards(TripRewardCards, reward.PrimaryBerryId, checked((int)berries), checked((int)coins), null, reward);
        TripRewardCards.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
        TripRewards.Text = $"Сохранённые условия: {berries} {BerryName(reward.PrimaryBerryId)}, {coins} монет; " +
            $"{xp}. Дополнительные находки и встреча откроются при получении. Награды не сгорают.";
        var returnsAt = trip.ReadyAtUtc.ToLocalTime();
        TripTime.Text = ready ? "Поход завершён. Находки ждут получения." :
            $"Осталось {Math.Ceiling((trip.ReadyAtUtc - DateTimeOffset.UtcNow).TotalMinutes)} мин";
        TripReturnText.Text = "Ожидаемое возвращение · " + (returnsAt.Date == DateTime.Today ? returnsAt.ToString("t") : returnsAt.ToString("g"));
        var total = (trip.ReadyAtUtc - trip.StartedAtUtc).TotalSeconds;
        TripProgress.Value = ready || total <= 0 ? 1 : Math.Clamp((DateTimeOffset.UtcNow - trip.StartedAtUtc).TotalSeconds / total, 0, 1);
        ClaimButton.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
        ClaimButton.IsEnabled = CancelButton.IsEnabled = !_busy;
        ClaimButton.Content = _busy ? "Получаем находки…" : trip.Rewards.HasEncounter ? "Забрать находки и открыть встречу" : "Забрать находки";
    }

    private void RefreshResult(SaveData data, ExpeditionState trip)
    {
        var reward = trip.Rewards;
        RenderSlots(ResultSlots, trip.ParticipantIds, data, false);
        ResultRewardCards.Children.Clear();
        if (trip.Receipt is { } quantities) RenderRewards(ResultRewardCards, reward.PrimaryBerryId, quantities.PrimaryBerries, quantities.Coins, $"{quantities.XpByParticipant.Values.Sum()} XP команде");
        var extras = reward.ExtraBerries.Select(x => $"{BerryName(x.Key)} ×{x.Value}")
            .Concat(reward.ExtraItems.Select(x => $"{ItemName(x.Key)} ×{x.Value}"))
            .Concat(reward.ExtraCoins > 0 ? [Coins(reward.ExtraCoins)] : []).ToArray();
        ResultExtrasText.Text = extras.Length == 0 ? "" : "Сверх обещанного: " + string.Join(", ", extras);
        ResultExtrasText.Visibility = extras.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        var receiptText = trip.Receipt is { } receipt
            ? $"Основная ягода: {BerryName(reward.PrimaryBerryId)} ×{receipt.PrimaryBerries}; монеты всего (включая дополнительные): {receipt.Coins}; опыт: " +
              string.Join(", ", receipt.XpByParticipant.Select(x =>
              {
                  var member = data.Individuals.FirstOrDefault(p => p.Id == x.Key);
                  return $"{(member is null ? "Участник" : DisplayName(member, data))} +{x.Value} XP";
              })) + ". "
            : "Точные количества ягод, монет и опыта этого старого похода не сохранены. ";
        ResultText.Text = $"{RouteName(trip.LocationId)} · {trip.DurationMinutes} мин · находки и XP сохранены. " +
            receiptText +
            "Состав дополнительных находок (уже включены в итог): " +
            (reward.ExtraBerries.Count == 0 && reward.ExtraItems.Count == 0 && reward.ExtraCoins == 0 ? "нет" :
                string.Join(", ", reward.ExtraBerries.Select(x => $"{BerryName(x.Key)} ×{x.Value}")
                    .Concat(reward.ExtraItems.Select(x => $"{ItemName(x.Key)} ×{x.Value}"))
                    .Concat(reward.ExtraCoins > 0 ? [$"{reward.ExtraCoins} монет"] : [])));
        var encounter = data.Encounters.FirstOrDefault(e => e.Source == "expedition" && e.Id == trip.Id);
        OpenEncounterButton.Visibility = encounter?.Phase == "resolved" ? Visibility.Visible : Visibility.Collapsed;
        OpenEncounterButton.IsEnabled = !_busy && encounter?.Phase == "resolved";
        OpenEncounterButton.Tag = encounter?.Id;
        EncounterPanel.Visibility = encounter?.Phase == "resolved" && encounter.Individual is not null ? Visibility.Visible : Visibility.Collapsed;
        if (encounter?.Individual is { } individual)
            ResultText.Text += $" Встреча: {SpeciesName(individual.CurrentSpeciesId)}{(individual.IsShiny ? " · шайни" : "")}. " +
                (encounter.Phase == "resolved" ? "Закрытая встреча сохранена. Откройте её, чтобы перейти к встрече." : "Встреча открыта.");
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _suspended || _pickerOpen || _currentPreview is null || _selected.Count is < 1 or > 3) return;
        var generation = _generation;
        _busy = true;
        Refresh();
        try
        {
            _pendingStart ??= Request(DateTimeOffset.UtcNow);
            var result = await _start(_pendingStart);
            if (!_suspended && generation == _generation) Show(result.Message);
            _pendingStart = null; _continueAfterResult = false;
        }
        catch (Exception ex) { if (!_suspended && generation == _generation) Show("Не удалось сохранить поход. Команда осталась дома, повторите попытку.", ex); }
        finally { _busy = false; Refresh(); }
    }

    private async void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _suspended || _snapshot().Expedition is not { Status: "running" } trip) return;
        var generation = _generation;
        _busy = true; Refresh();
        try
        {
            var confirm = _confirmDestructive ?? _confirmAsync;
            var accepted = confirm is not null
                ? await confirm("Прервать экспедицию?", "Команда вернётся без добычи и опыта.", "Прервать")
                : _confirmCancel is not null ? _confirmCancel() : await ConfirmLocallyAsync();
            if (!accepted || _suspended || generation != _generation) return;
            var now = DateTimeOffset.UtcNow;
            if (_snapshot().Expedition is not { Status: "running" } current || current.Id != trip.Id) return;
            if (now >= current.ReadyAtUtc) { Show("Поход уже завершён. Заберите находки."); return; }
            var result = await _cancel(current.Id, now);
            if (!_suspended && generation == _generation) Show(result.Message);
        }
        catch (Exception ex) { if (!_suspended && generation == _generation) Show("Не удалось прервать поход. Команда пока в пути, повторите попытку.", ex); }
        finally { _busy = false; Refresh(); }
    }

    private Task<bool> ConfirmLocallyAsync()
    {
        _localConfirmation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ScenarioScroll.IsEnabled = false;
        LocalConfirmation.Present("Прервать экспедицию?", "Команда вернётся без добычи и опыта.", "Прервать", destructive: true);
        return _localConfirmation.Task;
    }
    private void CompleteLocalConfirmation(bool accepted)
    {
        var pending = _localConfirmation; _localConfirmation = null;
        LocalConfirmation.Visibility = Visibility.Collapsed; ScenarioScroll.IsEnabled = true;
        pending?.TrySetResult(accepted);
    }

    private async void ClaimButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _suspended || _snapshot().Expedition is not { } trip) return;
        var generation = _generation;
        Guid? encounterId = null;
        _busy = true;
        Refresh();
        try
        {
            if (_pendingClaimTripId != trip.Id)
            {
                _pendingClaimTripId = trip.Id;
                _pendingClaimId = Guid.NewGuid();
            }
            var result = await _claim(trip.Id, _pendingClaimId);
            if (!_suspended && generation == _generation) Show(result.Encounter is { IsClosed: true } encounter
                ? $"{result.Message} Встреча с {SpeciesName(encounter.SpeciesId)} сохранена закрытой." : result.Message);
            _pendingClaimId = Guid.Empty;
            if (result.Encounter is { } met) encounterId = met.Id; // closed = species still sealed until opened
        }
        catch (Exception ex) { if (!_suspended && generation == _generation) Show("Не удалось забрать находки. Они никуда не денутся, повторите попытку.", ex); }
        finally { _busy = false; Refresh(); }
        // One click: the claim button already promised the encounter, so open it right away.
        if (encounterId is { } id && !_suspended && generation == _generation) await OpenEncounter(id);
    }

    private async void OpenEncounterButton_Click(object sender, RoutedEventArgs e)
    {
        if (OpenEncounterButton.Tag is Guid id) await OpenEncounter(id);
    }

    private async Task OpenEncounter(Guid id)
    {
        if (_busy || _suspended) return;
        if (_enterEncounter is not null) { _enterEncounter(id); return; }
        var generation = _generation;
        _busy = true;
        OpenEncounterButton.IsEnabled = false;
        try
        {
            var result = await _open(id);
            if (!_suspended && generation == _generation) Show(result.Repeated ? "Встреча уже открыта." : $"{SpeciesName(result.Individual.CurrentSpeciesId)} теперь в коллекции.");
        }
        catch (Exception ex) { if (!_suspended && generation == _generation) Show("Не удалось открыть встречу. Она сохранена, повторите попытку.", ex); }
        finally { _busy = false; Refresh(); }
    }
}
