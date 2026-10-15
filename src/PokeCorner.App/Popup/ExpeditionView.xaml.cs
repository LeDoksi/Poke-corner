using System.Globalization;
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
    private static readonly (string Id, string Name, Color Sky, Color Ground)[] Routes =
    [
        ("grove", "Лесная роща", Colors.SkyBlue, Color.FromRgb(43, 96, 57)),
        ("lake", "Озёрный берег", Color.FromRgb(131, 187, 209), Color.FromRgb(41, 108, 133)),
        ("coast", "Морское побережье", Color.FromRgb(132, 199, 226), Color.FromRgb(34, 115, 142)),
        ("mountains", "Горные тропы", Color.FromRgb(154, 185, 201), Color.FromRgb(84, 104, 98)),
        ("desert", "Пустынные дюны", Color.FromRgb(233, 195, 141), Color.FromRgb(166, 111, 61)),
        ("volcano", "Вулканическая долина", Color.FromRgb(161, 122, 113), Color.FromRgb(89, 64, 64)),
        ("snow", "Снежный перевал", Color.FromRgb(169, 207, 218), Color.FromRgb(122, 158, 168)),
        ("ruins", "Древние руины", Color.FromRgb(184, 166, 137), Color.FromRgb(104, 91, 79))
    ];
    private readonly Func<SaveData> _snapshot;
    private readonly Func<ExpeditionRequest, ExpeditionPreview> _preview;
    private readonly Func<ExpeditionRequest, Task<ExpeditionAction>> _start;
    private readonly Func<Guid, DateTimeOffset, Task<ExpeditionAction>> _cancel;
    private readonly Func<Guid, Guid, Task<ExpeditionAction>> _claim;
    private readonly Func<Guid, Task<EncounterOpenResult>> _open;
    private readonly CatalogPackage? _catalog;
    private readonly LevelProgression? _levels;
    private readonly Func<bool> _confirmCancel;
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
        CatalogPackage? catalog = null, Func<bool>? confirmCancel = null)
    {
        _snapshot = snapshot;
        _preview = preview;
        _start = start;
        _cancel = cancel;
        _claim = claim;
        _open = open;
        _catalog = catalog;
        _levels = catalog is null ? null : new LevelProgression(catalog.DataRoot);
        _confirmCancel = confirmCancel ?? (() => MessageBox.Show("Прервать поход? Добыча и опыт не выдаются.",
            "Экспедиция", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
        _initializing = true;
        InitializeComponent();
        foreach (var route in Routes)
        {
            var button = new Button
            {
                Content = route.Name, Tag = route.Id, MinWidth = 122, Margin = new Thickness(0, 0, 6, 6),
                Style = (Style)FindResource("SecondaryButtonStyle")
            };
            button.Click += (_, _) => { if (_busy) return; _route = route.Id; _pendingStart = null; Refresh(); };
            RouteButtons.Children.Add(button);
        }
        DifficultyBox.SelectedIndex = 1;
        DurationBox.SelectedIndex = 1;
        _initializing = false;
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) => { _timer.Start(); Refresh(); };
        Unloaded += (_, _) => _timer.Stop();
        Refresh();
    }

    private string SpeciesName(int speciesId) =>
        _catalog?.Species.FirstOrDefault(s => s.Id == speciesId)?.DisplayName ?? $"№{speciesId}";

    private static string RouteName(string id) => Routes.FirstOrDefault(r => r.Id == id).Name ?? id;

    private ExpeditionRequest Request(DateTimeOffset now) => new(_route,
        (string)((ComboBoxItem)DifficultyBox.SelectedItem).Tag,
        int.Parse((string)((ComboBoxItem)DurationBox.SelectedItem).Tag, CultureInfo.InvariantCulture),
        _selected.ToArray(), Guid.NewGuid(), now);

    private void ChoiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initializing && DifficultyBox.SelectedItem is not null && DurationBox.SelectedItem is not null)
        {
            if (_busy && _pendingStart is { } pending)
            {
                _initializing = true;
                DifficultyBox.SelectedItem = DifficultyBox.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Tag, pending.Difficulty));
                DurationBox.SelectedItem = DurationBox.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Tag, pending.DurationMinutes.ToString(CultureInfo.InvariantCulture)));
                _initializing = false;
                return;
            }
            _pendingStart = null;
            Refresh();
        }
    }

    private void Show(string message)
    {
        MessageText.Text = message;
        MessagePanel.Visibility = Visibility.Visible;
    }

    public void Refresh()
    {
        if (_initializing) return;
        var data = _snapshot();
        var trip = data.Expedition;
        var active = trip?.Status is "running" or "ready";
        foreach (Button routeButton in RouteButtons.Children) routeButton.IsEnabled = !_busy && !active;
        DifficultyBox.IsEnabled = DurationBox.IsEnabled = !_busy && !active;
        PreparationPanel.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        TripPanel.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        ResultPanel.Visibility = !active && trip?.Status == "claimed" ? Visibility.Visible : Visibility.Collapsed;
        if (active)
        {
            RefreshTrip(data, trip!);
            return;
        }

        var route = Routes.First(r => r.Id == _route);
        LandscapeName.Text = route.Name;
        SkyTop.Color = route.Sky;
        SkyBottom.Color = route.Ground;
        foreach (Button button in RouteButtons.Children)
        {
            button.FontWeight = Equals(button.Tag, _route) ? FontWeights.Bold : FontWeights.Normal;
            button.BorderBrush = Equals(button.Tag, _route) ? (Brush)FindResource("AccentRedBrush") : (Brush)FindResource("CardBorderBrush");
            button.BorderThickness = Equals(button.Tag, _route) ? new Thickness(2) : new Thickness(1);
        }
        var busy = ExpeditionService.BusyIndividualIds(data);
        if (!_busy && _selected.RemoveWhere(id => busy.Contains(id) || data.Individuals.All(p => p.Id != id)) > 0) _pendingStart = null;
        TeamPanel.Children.Clear();
        foreach (var member in data.Individuals)
        {
            var unavailable = busy.Contains(member.Id);
            var button = new Button
            {
                Tag = member.Id,
                Content = $"{SpeciesName(member.CurrentSpeciesId)} · {member.Id.ToString("N")[..6]}{(unavailable ? " · занят" : "")}",
                ToolTip = unavailable ? "Уже занят в походе или питомнике" : $"Выбрать {SpeciesName(member.CurrentSpeciesId)}",
                IsEnabled = !unavailable && !_busy,
                Margin = new Thickness(0, 0, 6, 6),
                FontWeight = _selected.Contains(member.Id) ? FontWeights.Bold : FontWeights.Normal,
                Style = (Style)FindResource("SecondaryButtonStyle")
            };
            button.Click += (_, _) =>
            {
                if (_busy) return;
                if (!_selected.Remove(member.Id) && _selected.Count < 3) _selected.Add(member.Id);
                _pendingStart = null;
                Refresh();
            };
            TeamPanel.Children.Add(button);
        }
        if (data.Individuals.Count == 0) TeamPanel.Children.Add(new TextBlock { Text = "Пока нет покемонов для похода." });
        if (_selected.Count == 0)
        {
            PreviewText.Text = "Выберите хотя бы одного участника.";
            ContributionsText.Text = "Один участник: −20% добычи. Двое: без встречи и дополнительных находок. Трое: открывают шансы на них.";
            StartButton.IsEnabled = false;
        }
        else
        {
            try
            {
                var preview = _preview(Request(DateTimeOffset.UtcNow));
                PreviewText.Text = $"Гарантировано: {preview.PrimaryBerries} {preview.PrimaryBerryId}, {preview.Coins} монет, " +
                    $"{preview.XpPerParticipant} XP каждому. Готовность: {preview.ReadyAtUtc.ToLocalTime():g}. " +
                    $"Шанс встречи: {preview.EncounterChance:P0}; дополнительная проверка: {preview.ExtraChancePerCheck:P0}.";
                ContributionsText.Text = string.Join(" · ", _selected.Select(id =>
                {
                    var member = data.Individuals.Single(p => p.Id == id);
                    return $"{SpeciesName(member.CurrentSpeciesId)} · ур. {_levels?.LevelForXp(member.TotalXp) ?? 1}: {preview.XpByParticipant[id]} XP, " +
                        $"особенности {member.FirstTraitId ?? "нет"}{(member.SecondTraitId is null ? "" : $", {member.SecondTraitId}")}";
                })) + (_selected.Count == 1 ? " · Один участник: −20% добычи, без встречи и дополнительных находок." :
                    _selected.Count == 2 ? " · Двое: без встречи и дополнительных находок." :
                    " · Полная тройка: встреча и дополнительные находки возможны.");
                StartButton.IsEnabled = !_busy;
            }
            catch (Exception ex)
            {
                PreviewText.Text = ex.Message;
                ContributionsText.Text = "";
                StartButton.IsEnabled = false;
            }
        }
        if (trip?.Status == "claimed") RefreshResult(data, trip);
    }

    private void RefreshTrip(SaveData data, ExpeditionState trip)
    {
        var ready = trip.Status == "ready" || DateTimeOffset.UtcNow >= trip.ReadyAtUtc;
        TripTitle.Text = ready ? "С возвращением!" : "Команда в походе";
        TripTerms.Text = $"{RouteName(trip.LocationId)} · {trip.Difficulty} · {trip.DurationMinutes} мин";
        TripTeam.Text = string.Join(" · ", trip.ParticipantIds.Select(id =>
        {
            var member = data.Individuals.FirstOrDefault(p => p.Id == id);
            return member is null ? "Участник" : SpeciesName(member.CurrentSpeciesId);
        }));
        var reward = trip.Rewards;
        var berryRemainder = data.BerryRemainders[reward.PrimaryBerryId];
        var coinRemainder = data.CoinRemainder;
        var berries = new RationalRemainder { Numerator = berryRemainder.Numerator, Denominator = berryRemainder.Denominator }
            .Settle(reward.PrimaryNumerator, reward.PrimaryDenominator);
        var coins = new RationalRemainder { Numerator = coinRemainder.Numerator, Denominator = coinRemainder.Denominator }
            .Settle(reward.CoinNumerator, reward.CoinDenominator);
        var xp = string.Join(", ", trip.ParticipantIds.Select(id =>
        {
            var member = data.Individuals.Single(p => p.Id == id);
            return $"{SpeciesName(member.CurrentSpeciesId)} +{new RationalRemainder { Numerator = member.XpRemainder.Numerator, Denominator = member.XpRemainder.Denominator }.Settle(reward.XpNumerator, reward.XpDenominator)} XP";
        }));
        TripRewards.Text = $"Сохранённые условия: {berries} {reward.PrimaryBerryId}, {coins} монет; " +
            $"{xp}. Дополнительные находки и встреча откроются при получении.";
        TripTime.Text = ready ? "Поход завершён. Находки ждут получения." :
            $"Вернутся: {trip.ReadyAtUtc.ToLocalTime():g} · осталось {Math.Ceiling((trip.ReadyAtUtc - DateTimeOffset.UtcNow).TotalMinutes)} мин";
        var total = (trip.ReadyAtUtc - trip.StartedAtUtc).TotalSeconds;
        TripProgress.Value = ready || total <= 0 ? 1 : Math.Clamp((DateTimeOffset.UtcNow - trip.StartedAtUtc).TotalSeconds / total, 0, 1);
        ClaimButton.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.Visibility = ready ? Visibility.Collapsed : Visibility.Visible;
        ClaimButton.IsEnabled = CancelButton.IsEnabled = !_busy;
    }

    private void RefreshResult(SaveData data, ExpeditionState trip)
    {
        var reward = trip.Rewards;
        var receiptText = trip.Receipt is { } receipt
            ? $"Основная ягода: {reward.PrimaryBerryId} ×{receipt.PrimaryBerries}; монеты: {receipt.Coins}; опыт: " +
              string.Join(", ", receipt.XpByParticipant.Select(x =>
              {
                  var member = data.Individuals.FirstOrDefault(p => p.Id == x.Key);
                  return $"{(member is null ? x.Key.ToString("N")[..6] : SpeciesName(member.CurrentSpeciesId))} +{x.Value} XP";
              })) + ". "
            : "Точные количества ягод, монет и опыта этого старого похода не сохранены. ";
        ResultText.Text = $"{RouteName(trip.LocationId)} · {trip.DurationMinutes} мин · находки и XP сохранены. " +
            receiptText +
            "Дополнительные: " +
            (reward.ExtraBerries.Count == 0 && reward.ExtraItems.Count == 0 && reward.ExtraCoins == 0 ? "нет" :
                string.Join(", ", reward.ExtraBerries.Select(x => $"{x.Key} ×{x.Value}")
                    .Concat(reward.ExtraItems.Select(x => $"{x.Key} ×{x.Value}"))
                    .Concat(reward.ExtraCoins > 0 ? [$"{reward.ExtraCoins} монет"] : [])));
        var encounter = data.Encounters.FirstOrDefault(e => e.Source == "expedition" && e.Id == trip.Id);
        OpenEncounterButton.Visibility = encounter?.Phase == "resolved" ? Visibility.Visible : Visibility.Collapsed;
        OpenEncounterButton.IsEnabled = !_busy && encounter?.Phase == "resolved";
        OpenEncounterButton.Tag = encounter?.Id;
        if (encounter?.Individual is { } individual)
            ResultText.Text += $" Встреча: {SpeciesName(individual.CurrentSpeciesId)}{(individual.IsShiny ? " · шайни" : "")}. " +
                (encounter.Phase == "resolved" ? "Откройте встречу, чтобы добавить покемона." : "Встреча открыта.");
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _selected.Count is < 1 or > 3) return;
        _busy = true;
        Refresh();
        try
        {
            _pendingStart ??= Request(DateTimeOffset.UtcNow);
            Show((await _start(_pendingStart)).Message);
            _pendingStart = null;
        }
        catch (Exception ex) { Show(ex.Message); }
        finally { _busy = false; Refresh(); }
    }

    private async void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _snapshot().Expedition is not { Status: "running" } trip) return;
        if (!_confirmCancel()) return;
        var now = DateTimeOffset.UtcNow;
        if (_snapshot().Expedition is not { Status: "running" } current || current.Id != trip.Id) { Refresh(); return; }
        if (now >= current.ReadyAtUtc) { Show("Поход уже завершён. Заберите находки."); Refresh(); return; }
        _busy = true;
        Refresh();
        try { Show((await _cancel(current.Id, now)).Message); }
        catch (Exception ex) { Show(ex.Message); }
        finally { _busy = false; Refresh(); }
    }

    private async void ClaimButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _snapshot().Expedition is not { } trip) return;
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
            Show(result.Encounter is { IsClosed: true } encounter
                ? $"{result.Message} Встреча с {SpeciesName(encounter.SpeciesId)} сохранена закрытой." : result.Message);
            _pendingClaimId = Guid.Empty;
        }
        catch (Exception ex) { Show(ex.Message); }
        finally { _busy = false; Refresh(); }
    }

    private async void OpenEncounterButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || OpenEncounterButton.Tag is not Guid id) return;
        _busy = true;
        OpenEncounterButton.IsEnabled = false;
        try
        {
            var result = await _open(id);
            Show(result.Repeated ? "Встреча уже открыта." : $"{SpeciesName(result.Individual.CurrentSpeciesId)} теперь в коллекции.");
        }
        catch (Exception ex) { Show(ex.Message); }
        finally { _busy = false; Refresh(); }
    }
}
