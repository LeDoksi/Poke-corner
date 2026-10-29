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
            var button = new Button
            {
                Content = route.Id switch { "grove" => "Роща", "lake" => "Озеро", "coast" => "Море", "mountains" => "Горы", "desert" => "Пустыня", "volcano" => "Вулкан", "snow" => "Снег", _ => "Руины" }, Tag = route.Id, MinWidth = 58, FontSize = 13, Padding = new Thickness(4, 5, 4, 5), Margin = new Thickness(0, 0, 6, 6),
                Style = (Style)FindResource("SecondaryButtonStyle"), HorizontalContentAlignment = HorizontalAlignment.Center
            };
            button.Click += (_, _) => { if (_busy) return; _route = route.Id; _pendingStart = null; Refresh(); };
            RouteButtons.Children.Add(button);
        }
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
            // Side margins exist only when the window is wider than the 968 column plus gutters.
            var margin = Math.Max(0, (ActualWidth - 968 - 48) / 2);
            MarginArt.Visibility = margin >= 150 ? Visibility.Visible : Visibility.Collapsed;
            MarginArtLeft.Width = MarginArtRight.Width = margin;
            Refresh();
            if (_pickerOpen) RefreshPicker();
        };
        PreparationFooter.SizeChanged += (_, _) => FitScene();
        ActionFooter.SizeChanged += (_, _) => FitScene();
        ScenarioContent.SizeChanged += (_, _) => FitScene();
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) => { _suspended = false; _timer.Start(); Refresh(); };
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
        _route = routeId; _pendingStart = null;
        if (_pickerOpen) ClosePicker();
        Refresh();
    }

    private string SpeciesName(int speciesId) =>
        _catalog?.Species.FirstOrDefault(s => s.Id == speciesId)?.DisplayName ?? $"№{speciesId}";

    private static string RouteName(string id) => Routes.FirstOrDefault(r => r.Id == id).Name ?? id;

    private ExpeditionRequest Request(DateTimeOffset now) => new(_route,
        (string)((ListBoxItem)DifficultyBox.SelectedItem).Tag,
        int.Parse((string)((ListBoxItem)DurationBox.SelectedItem).Tag, CultureInfo.InvariantCulture),
        _selected.ToArray(), Guid.NewGuid(), now);

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
        foreach (Button routeButton in RouteButtons.Children) routeButton.IsEnabled = !_busy && !active;
        DifficultyBox.IsEnabled = DurationBox.IsEnabled = !_busy && !active;
        // ponytail: result screen retired (loot is on the ready screen, encounter opens straight from the claim); its panel stays only because receipt tests still read ResultText.
        var showResult = false;
        var compact = ActualWidth < 760;
        Grid.SetColumnSpan(DifficultyGroup, 1);
        Grid.SetColumnSpan(DurationGroup, 1);
        Grid.SetColumn(DurationGroup, 1);
        Grid.SetRow(DurationGroup, 0);
        PreparationPanel.Visibility = PreparationDetailsPanel.Visibility = RouteSelectionPanel.Visibility = active || showResult ? Visibility.Collapsed : Visibility.Visible;
        PreparationFooter.Visibility = _pickerOpen || active || showResult ? Visibility.Collapsed : Visibility.Visible;
        ActionFooter.Visibility = !_pickerOpen && (active || showResult) ? Visibility.Visible : Visibility.Collapsed;
        ContinueButton.Visibility = showResult ? Visibility.Visible : Visibility.Collapsed;
        ContinueButton.IsEnabled = !_busy;
        if (!active) ClaimButton.Visibility = CancelButton.Visibility = Visibility.Collapsed;
        if (showResult) SceneFrame.Height = 82; else FitScene();
        TripPanel.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        ResultPanel.Visibility = showResult ? Visibility.Visible : Visibility.Collapsed;
        EncounterPanel.Visibility = Visibility.Collapsed; // RefreshResult re-shows it for a claimed trip with an unopened encounter
        if (!active) {
            SubtitleText.Text = showResult ? "Поход завершён." : "Выберите маршрут, время и команду.";
            TrainerNote.Text = RouteNote(_route);
            HeaderSketch.Source = (ImageSource)FindResource("NdMapImage");
        }
        RefreshLandscape(active || showResult ? trip!.LocationId : _route);
        RouteHintText.Text = _selector.RouteHint(active || showResult ? trip!.LocationId : _route, active || showResult ? trip!.Difficulty : Difficulty);
        if (active)
        {
            RefreshTrip(data, trip!);
            return;
        }

        foreach (Button button in RouteButtons.Children)
        {
            var selected = Equals(button.Tag, _route);
            button.Style = (Style)FindResource(selected ? "NdRouteSelectedStyle" : "SecondaryButtonStyle");
            button.MinHeight = 36;
            System.Windows.Automation.AutomationProperties.SetName(button, RouteName((string)button.Tag) + (selected ? " · выбран" : ""));
        }
        if (!_busy && _selected.RemoveWhere(id => data.Individuals.FirstOrDefault(p => p.Id == id) is not { } member ||
                _selector.Unavailable(member, data) is not null) > 0) _pendingStart = null;
        ChooseTeamButton.IsEnabled = !_busy;
        TeamHeading.Text = $"Команда · {_selected.Count} из 3";
        StartButton.Content = _busy ? "Отправляем команду…" : _pendingStart is not null ? "Повторить отправку" : "Отправить команду";
        ChooseTeamButton.Content = _selected.Count == 0 ? "Выбрать команду" : "Изменить команду";
        RenderSlots(TeamSlots, _selected, data, false);
        RouteHintText.Text = _selector.RouteHint(_route, Difficulty);
        if (_pickerOpen) {
            var signature = string.Join("|", data.Individuals.Select(p => $"{p.Id}:{p.TotalXp}:{p.Nickname}")) + data.ActiveIndividualId +
                data.Nest?.Status + string.Join(",", data.Nest?.ParticipantIds ?? []);
            if (_pickerState != signature) { _pickerState = signature; RefreshPicker(); }
        }
        if (_selected.Count == 0)
        {
            PreviewText.Text = "Выберите хотя бы одного участника.";
            ContributionsText.Text = "Выберите команду из 1–3 питомцев. Полная тройка может принести дополнительные находки и встречу.";
            _currentPreview = null;
            PreviewRewardCards.Children.Clear();
            LootLabel.Text = "Добыча появится после выбора команды.";
            StartButton.IsEnabled = false;
        }
        else
        {
            try
            {
                var preview = _currentPreview = _preview(Request(DateTimeOffset.UtcNow));
                LootLabel.Text = "Гарантированная добыча:";
                RenderRewards(PreviewRewardCards, preview.PrimaryBerryId, preview.PrimaryBerries, preview.Coins, $"{preview.XpPerParticipant} XP каждому");
                PreviewText.Text = "Вернутся через " + ((ListBoxItem)DurationBox.SelectedItem).Content;
                PreviewText.ToolTip = $"Ожидаемое возвращение: {preview.ReadyAtUtc.ToLocalTime():g}";
                ContributionsText.Text = _selected.Count < 3
                    ? "Добыча и опыт доступны. Для дополнительных находок и встречи нужны трое."
                    : "Полная команда: возможны находки и встреча.";
                StartButton.IsEnabled = !_busy;
            }
            catch (Exception ex)
            {
                _currentPreview = null; PreviewRewardCards.Children.Clear();
                PreviewText.Text = ex.Message;
                ContributionsText.Text = "";
                StartButton.IsEnabled = false;
            }
        }
        if (trip?.Status == "claimed") RefreshResult(data, trip);
    }

    // The scene takes whatever height the preparation footer leaves, so no dead band opens between them.
    private void FitScene()
    {
        if (_pickerOpen || ResultPanel.Visibility == Visibility.Visible || ActualHeight <= 0) return;
        var footer = PreparationFooter.Visibility == Visibility.Visible ? PreparationFooter : ActionFooter;
        var others = ScenarioContent.ActualHeight - SceneFrame.ActualHeight;
        var free = ActualHeight - 32 - footer.ActualHeight - footer.Margin.Top - others;
        // The scene is the elastic part: it gives way first at the 1200x660 minimum and takes the extra height when maximized.
        var height = Math.Clamp(Math.Floor(free), 110, 560);
        if (Math.Abs(SceneFrame.Height - height) >= 1) SceneFrame.Height = height;
    }

    // Trainer's field note for the chosen place: flavour drawn from each route's art brief, never a mechanic.
    private static string RouteNote(string route) => route switch
    {
        "grove" => "Под ногами сухая листва — шаги слышно издалека.",
        "lake" => "У воды тихо. Камыши шуршат даже без ветра.",
        "coast" => "Ветер с моря. Карту держать двумя руками.",
        "mountains" => "Тропа узкая, щебень сыплется из-под ботинок.",
        "desert" => "Следы заметает за минуты. Ориентир — гребень дюны.",
        "volcano" => "Жарко даже ночью. Базальт тёплый сквозь подошву.",
        "snow" => "Карнизы нависают. Говорить вполголоса.",
        _ => "Из тёмных проходов кто-то смотрит. Не задерживаться."
    };

    private string Difficulty => (DifficultyBox.SelectedItem as ListBoxItem)?.Tag as string ?? "normal";
    private string DisplayName(PokemonIndividual pet, SaveData data) => _selector.Name(pet) + (pet.Nickname is null ? "" : $" · {pet.Nickname}") + (pet.IsShiny ? " · шайни" : "")
        + (IndividualDisplay.Discriminator(data.Individuals, pet.Id) is { Length: > 0 } label ? "\n" + label : "");
    public static string Coins(long n) => n + (n % 10 == 1 && n % 100 != 11 ? " монета" : n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 ? " монеты" : " монет");
    internal static string BerryName(string id) => id switch { "rawst" => "Равст", "oran" => "Оран", "pecha" => "Печа", "cheri" => "Чери", "chesto" => "Често", "aspear" => "Аспир", _ => "ягоды" };
    private static string ItemName(string id) => id switch { "evolution-stone" => "Камень эволюции", "offspring-charm" => "Талисман потомства", "lucky-charm" => "Талисман удачи", _ => "Предмет" };
    private ImageSource? Sprite(PokemonIndividual pet)
    {
        var key = (pet.CurrentSpeciesId, pet.IsShiny);
        if (_spriteCache.TryGetValue(key, out var cached)) return cached;
        ImageSource? image = null;
        try { var path = _catalog?.GetAssetPath(key.CurrentSpeciesId, key.IsShiny);
            if (path is not null && File.Exists(path)) image = SpriteImages.Decode(File.ReadAllBytes(path)); }
        catch (Exception error) when (error is IOException or InvalidOperationException or NotSupportedException or ArgumentException) { }
        if (_spriteCache.Count >= 128) _spriteCache.Clear();
        _spriteCache[key] = image; return image;
    }
    private void RenderSlots(UniformGrid panel, IEnumerable<Guid> ids, SaveData data, bool editable, IReadOnlyDictionary<Guid, long>? xpGain = null)
    {
        panel.Children.Clear(); var members = ids.ToArray();
        for (var i = 0; i < 3; i++) {
            var member = i < members.Length ? data.Individuals.FirstOrDefault(p => p.Id == members[i]) : null;
            var content = new StackPanel { Orientation = Orientation.Horizontal, MinHeight = 52 };
            content.Children.Add(new Image { Source = member is null ? null : Sprite(member), Width = 48, Height = 48, Margin = new Thickness(0, 0, 8, 0) });
            var text = new TextBlock { Text = member is null ? $"Место {i + 1}\nдля питомца" : $"{(editable ? DisplayName(member, data) : _selector.Name(member))}\nУр. {_selector.Level(member)}", TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, MaxWidth = Math.Max(80, (ActualWidth - 160) / 3 - 75) };
            content.Children.Add(text);
            if (member is not null) content.ToolTip = $"{DisplayName(member, data)}\nМаршрут: {(_selector.Matches(member, _route) ? "подходящий тип — помогает команде" : "без совпадения типа")}\nОсобенности: {_selector.Traits(member)}" +
                (_currentPreview?.XpByParticipant.TryGetValue(member.Id, out var amount) == true ? $"\n+{amount} XP" : "");
            if (editable && member is not null) {
                var remove = new Button { Content = "×", Tag = member.Id, ToolTip = "Убрать из команды", MinWidth = 22, MinHeight = 22, Height = 22, Width = 22, VerticalAlignment = VerticalAlignment.Top, Padding = new Thickness(0), Style = (Style)FindResource("SecondaryButtonStyle") };
                remove.Click += (_, _) => { _draft.Remove((Guid)remove.Tag); RefreshPicker(); };
                content.Children.Add(remove);
            }
            System.Windows.Automation.AutomationProperties.SetName(content, member is null ? $"Пустое место {i + 1}" : DisplayName(member, data));
            if (member is null && !editable && ReferenceEquals(panel, TeamSlots)) {
                var invite = new Grid { MinHeight = 72 };
                invite.Children.Add(new System.Windows.Shapes.Rectangle { Stroke = (Brush)FindResource("NdBorderBrush"), StrokeThickness = 1.2, StrokeDashArray = [4, 3], RadiusX = 9, RadiusY = 9 });
                invite.Children.Add(new TextBlock { Text = "+\nВыбрать покемона", TextAlignment = TextAlignment.Center, Foreground = (Brush)FindResource("NdLavenderBrush"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                var slot = new Button { Content = invite, Style = (Style)FindResource("NdLinkButtonStyle"), Padding = new Thickness(0), Margin = new Thickness(0, 0, 7, 0), HorizontalContentAlignment = HorizontalAlignment.Stretch, IsEnabled = !_busy };
                System.Windows.Automation.AutomationProperties.SetName(slot, $"Выбрать покемона на место {i + 1}");
                slot.Click += ChooseTeam_Click;
                panel.Children.Add(slot);
                continue;
            }
            UIElement child = content;
            if (member is not null && xpGain?.TryGetValue(member.Id, out var gain) == true) {
                var dock = new DockPanel();
                var chip = new TextBlock { Text = $"+{gain} XP", FontSize = 17, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("NdApricotBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) };
                DockPanel.SetDock(chip, Dock.Right);
                dock.Children.Add(chip); dock.Children.Add(content);
                child = dock;
            }
            panel.Children.Add(new Border { Child = child, Padding = new Thickness(10), Margin = new Thickness(0, 0, 7, 0), CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(1), BorderBrush = (Brush)FindResource("CardBorderBrush"), Background = (Brush)FindResource("CardBackgroundBrush") });
        }
    }
    private void ChooseTeam_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _suspended || _snapshot().Expedition?.Status is "running" or "ready") return;
        _draft.Clear(); _draft.UnionWith(_selected); _pickerOpen = true;
        PickerPanel.Visibility = Visibility.Visible; ScenarioScroll.Visibility = PreparationFooter.Visibility = Visibility.Collapsed;
        ResetPickerFilters(); RefreshPicker(); PickerSearchBox.Focus();
    }
    private void ClosePicker()
    {
        _pickerOpen = false; _draft.Clear(); PickerPanel.Visibility = Visibility.Collapsed; ScenarioScroll.Visibility = PreparationFooter.Visibility = Visibility.Visible;
        if (!_suspended) ChooseTeamButton.Focus();
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
    private void RefreshPicker()
    {
        if (!_pickerOpen || _suspended) return;
        var data = _snapshot();
        _draft.RemoveWhere(id => data.Individuals.FirstOrDefault(p => p.Id == id) is not { } p || _selector.Unavailable(p, data) is not null);
        var rows = _selector.Query(data, _route, Difficulty, PickerSearchBox.Text,
            (PickerTypeBox.SelectedItem as ComboBoxItem)?.Tag as string, (PickerTraitBox.SelectedItem as ComboBoxItem)?.Tag as string,
            PickerAvailableBox.IsChecked == true, PickerRouteBox.IsChecked == true, PickerSortBox.SelectedIndex);
        // D-17 tiles, same family as the Pokédex grid: sprite, name, level, plus the two facts a team
        // pick needs (route fit, trait). Pages hold three rows of whatever width the window gives.
        var viewportWidth = PickerScroll.ViewportWidth > 0 ? PickerScroll.ViewportWidth - 2 : Math.Max(400, ActualWidth - 48); // viewport, not ActualWidth: the scrollbar would push the last tile to the next row
        var columns = Math.Max(2, (int)((viewportWidth - 8) / 150));
        var width = Math.Floor((viewportWidth - 8) / columns) - 8;
        var pageSize = Math.Max(12, columns * 3);
        var pages = Math.Max(1, (rows.Count + pageSize - 1) / pageSize); _pickerPage = Math.Clamp(_pickerPage, 0, pages - 1);
        PickerMembers.Children.Clear();
        foreach (var member in rows.Skip(_pickerPage * pageSize).Take(pageSize)) {
            var reason = _selector.Unavailable(member, data); var chosen = _draft.Contains(member.Id);
            var content = new StackPanel();
            var well = new Grid { Height = 64 };
            well.Children.Add(new Image { Source = Sprite(member), Width = 56, Height = 56, HorizontalAlignment = HorizontalAlignment.Center });
            if (chosen) well.Children.Add(new TextBlock { Text = "✓", FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("NdLavenderBrush"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top });
            content.Children.Add(well);
            content.Children.Add(new TextBlock { Text = DisplayName(member, data).Split('\n')[0], FontWeight = FontWeights.Bold, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 0) });
            content.Children.Add(new TextBlock { Text = $"Ур. {_selector.Level(member)}  #{member.CurrentSpeciesId:D4}", FontSize = 12, Foreground = (Brush)FindResource("NdMutedBrush") });
            var fits = _selector.Matches(member, _route);
            content.Children.Add(new TextBlock { Text = fits ? "✓ Подходит маршруту" : string.Join(" / ", _selector.Types(member).Select(ExpeditionTeamSelector.TypeName)), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = (Brush)FindResource(fits ? "NdLavenderBrush" : "NdMutedBrush"), Margin = new Thickness(0, 2, 0, 0) });
            content.Children.Add(new TextBlock { Text = string.Join(" · ", new[] { member.FirstTraitId, _selector.Level(member) >= _selector.SecondTraitLevel ? member.SecondTraitId : null }.Where(t => !string.IsNullOrWhiteSpace(t)).Select(CollectionView.TraitLabel)), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = (Brush)FindResource("NdMutedBrush") });
            if (reason is not null) content.Children.Add(new TextBlock { Text = reason, TextWrapping = TextWrapping.Wrap, FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 2, 0, 0) });
            var button = new Button { Content = content, Tag = member.Id, Width = width, MinHeight = 140,
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top,
                Margin = new Thickness(4), Padding = new Thickness(8), Style = (Style)FindResource("SecondaryButtonStyle"),
                IsEnabled = reason is null && (chosen || _draft.Count < 3),
                ToolTip = (reason ?? (chosen ? "Убрать из команды" : _draft.Count >= 3 ? "В команде уже трое. Уберите участника, чтобы выбрать другого." : "Выбрать питомца")) + "\n" + DisplayName(member, data) + "\n" + _selector.Traits(member),
                BorderThickness = new Thickness(chosen ? 2 : 1), BorderBrush = (Brush)FindResource(chosen ? "NdLavenderBrush" : "CardBorderBrush") };
            System.Windows.Automation.AutomationProperties.SetName(button, DisplayName(member, data) + " · " + (reason ?? (chosen ? "выбран" : "выбрать")));
            button.Click += (_, _) => { if (!button.IsEnabled || !_pickerOpen) return; if (!_draft.Remove(member.Id) && _draft.Count < 3) _draft.Add(member.Id); RefreshPicker(); };
            PickerMembers.Children.Add(button);
        }
        RenderSlots(PickerSlots, _draft, data, true);
        PickerRouteHintText.Text = $"{RouteName(_route)} · {((ListBoxItem)DifficultyBox.SelectedItem).Content} · {((ListBoxItem)DurationBox.SelectedItem).Content} · {_selector.RouteHint(_route, Difficulty)}";
        PickerCountText.Text = $"Выбрано {_draft.Count} / 3 · найдено {rows.Count}";
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
    private void RenderRewards(UniformGrid panel, string berry, int berries, int coins, string? xp, ExpeditionRewards? extras = null)
    {
        panel.Children.Clear();
        var berryCounts = new Dictionary<string, int> { [berry] = berries };
        foreach (var (id, count) in extras?.ExtraBerries ?? []) berryCounts[id] = berryCounts.GetValueOrDefault(id) + count;
        var cards = berryCounts.Select(x => (ShopArt(x.Key), $"{BerryName(x.Key)} ×{x.Value}")).ToList();
        cards.Add(((ImageSource)FindResource("UiCoinImage"), Coins(coins + (extras?.ExtraCoins ?? 0))));
        foreach (var (id, count) in extras?.ExtraItems ?? []) cards.Add((ShopArt(id), $"{ItemName(id)} ×{count}"));
        if (xp is not null) cards.Add((null, xp));
        var compact = ReferenceEquals(panel, PreviewRewardCards);
        if (!compact) panel.Columns = Math.Min(cards.Count, 4);
        foreach (var (image, text) in cards) {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            if (image is not null) row.Children.Add(new Image { Source = image, Width = 26, Height = 26, Margin = new Thickness(0, 0, 6, 0) });
            else row.Children.Add(new TextBlock { Text = "XP", FontWeight = FontWeights.ExtraBold, Foreground = (Brush)FindResource("UiForestBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            row.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = compact ? 145 : 180, TextAlignment = TextAlignment.Left, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, ToolTip = text });
            panel.Children.Add(new Border { Child = row, Padding = new Thickness(compact ? 2 : 8), Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(8), Background = compact ? System.Windows.Media.Brushes.Transparent : (Brush)FindResource("SurfaceBackgroundBrush") });
        }
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
        TripTitle.FontSize = ready ? 22 : 16;
        TripGlyph.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
        TripHeader.HorizontalAlignment = TripTerms.HorizontalAlignment = ready ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        TripTimeRow.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
        SubtitleText.Text = ready ? "Находки готовы и ждут получения." : "Условия похода сохранены. Команда вернётся сама.";
        TrainerNote.Text = ready ? "Вернулись! Рюкзак тяжелее, чем уходил." : "Ушли по тропе. Ждать у окна не нужно.";
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
