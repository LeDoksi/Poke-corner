using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;
using Button = System.Windows.Controls.Button;
using Image = System.Windows.Controls.Image;
using Rectangle = System.Windows.Shapes.Rectangle;
using Orientation = System.Windows.Controls.Orientation;
using UserControl = System.Windows.Controls.UserControl;

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
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private Guid _first;
    private Guid _second;
    private int? _choice;
    private NurseryRequest? _pendingStart;
    private Guid _pendingHatchNest;
    private Guid _pendingHatchOperation;
    private string? _shortageBerry;
    private int _shortageRequired;
    private bool _busy;
    private bool _refreshingReady;
    private int _selectionGeneration;

    public NurseryView(Func<SaveData> snapshot, Func<IReadOnlyList<Guid>, int?, NurseryPreview> preview,
        Func<NurseryRequest, Task<NurseryAction>> start, Func<Guid, Guid, Task<NurseryAction>> hatch,
        Func<DateTimeOffset, Task<bool>> refreshReady, CatalogPackage catalog, Action<string, int> shopShortage,
        Action<Guid> openPokedex, Func<Guid, Task> activate)
    {
        _snapshot = snapshot;
        _preview = preview;
        _start = start;
        _hatch = hatch;
        _refreshReady = refreshReady;
        _catalog = catalog;
        _recipes = new NurseryCatalog(catalog);
        _shopShortage = shopShortage;
        _openPokedex = openPokedex;
        _activate = activate;
        InitializeComponent();
        _timer.Tick += async (_, _) => await TickAsync();
        Loaded += (_, _) => { _timer.Start(); Refresh(); };
        Unloaded += (_, _) => _timer.Stop();
        Refresh();
    }

    private string SpeciesName(int id) => _catalog.Species.FirstOrDefault(s => s.Id == id)?.DisplayName ?? $"№{id}";
    private string ParentName(SaveData data, Guid id)
    {
        var parent = data.Individuals.FirstOrDefault(p => p.Id == id);
        return parent is null ? "Родитель" : $"{parent.Nickname ?? SpeciesName(parent.CurrentSpeciesId)} · {id.ToString("N")[..6]}";
    }

    private void Show(string message)
    {
        MessageText.Text = message;
        MessagePanel.Visibility = Visibility.Visible;
    }

    private void SelectFirst(Guid id)
    {
        if (_busy) return;
        _first = id;
        _second = Guid.Empty;
        _choice = null;
        _pendingStart = null;
        _selectionGeneration++;
        Refresh();
        FirstParentsPanel.Children.OfType<Button>().FirstOrDefault(b => Equals(b.Tag, id))?.Focus();
    }

    private void SelectSecond(Guid id)
    {
        if (_busy || id == _first) return;
        _second = id;
        _choice = null;
        _pendingStart = null;
        _selectionGeneration++;
        Refresh();
        SecondParentsPanel.Children.OfType<Button>().FirstOrDefault(b => Equals(b.Tag, id))?.Focus();
    }

    private Button ParentButton(SaveData data, PokemonIndividual parent, bool selected, bool enabled, Action select)
    {
        var button = new Button
        {
            Content = ParentName(data, parent.Id), Tag = parent.Id, IsEnabled = enabled,
            Margin = new Thickness(0, 0, 6, 6), MinHeight = 36,
            Style = (Style)FindResource("SecondaryButtonStyle"),
            FontWeight = selected ? FontWeights.Bold : FontWeights.Normal,
            BorderBrush = selected ? (Brush)FindResource("AccentRedBrush") : (Brush)FindResource("CardBorderBrush"),
            BorderThickness = selected ? new Thickness(2) : new Thickness(1)
        };
        button.Click += (_, _) => select();
        return button;
    }

    public void Refresh()
    {
        var data = _snapshot();
        var nest = data.Nest;
        var active = nest?.Status is "running" or "ready";
        PreparationPanel.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        NestPanel.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        ResultPanel.Visibility = !active && nest?.Status == "hatched" ? Visibility.Visible : Visibility.Collapsed;
        OpenPokedexButton.IsEnabled = ActivateButton.IsEnabled = !_busy && nest?.Status == "hatched";
        if (active)
        {
            RefreshNest(data, nest!);
            return;
        }
        if (nest?.Status == "hatched")
        {
            var child = data.HatchedNurseryResults.GetValueOrDefault(nest.Id) ?? nest.Child;
            var owned = data.Individuals.Count(p => p.SpeciesId == child.SpeciesId);
            ResultText.Text = $"№{child.SpeciesId} · {SpeciesName(child.SpeciesId)}{(child.IsShiny ? " · шайни" : "")} · " +
                $"характер {child.Nature} · особенность {CollectionView.TraitLabel(child.FirstTraitId)}. В коллекции сохранён один малыш. " +
                $"{(owned == 1 ? "Первый экземпляр этого вида." : $"Экземпляров этого вида: {owned}.")} " +
                $"Удача питомника: {data.NurseryLuckBonus:P2}.";
        }
        var busy = ExpeditionService.BusyIndividualIds(data);
        if (!_busy && (_first != Guid.Empty && (busy.Contains(_first) || data.Individuals.All(p => p.Id != _first)) ||
            _second != Guid.Empty && (busy.Contains(_second) || data.Individuals.All(p => p.Id != _second))))
        {
            _first = _second = Guid.Empty;
            _choice = null;
            _pendingStart = null;
            _selectionGeneration++;
        }
        FirstParentsPanel.Children.Clear();
        SecondParentsPanel.Children.Clear();
        foreach (var parent in data.Individuals)
        {
            var unavailable = busy.Contains(parent.Id);
            var firstButton = ParentButton(data, parent, parent.Id == _first, !_busy && !unavailable,
                () => SelectFirst(parent.Id));
            firstButton.ToolTip = unavailable ? "Родитель уже занят" : "Выбрать первым родителем";
            FirstParentsPanel.Children.Add(firstButton);
            if (_first == Guid.Empty || parent.Id == _first ||
                !_recipes.Partners(data.Individuals.Single(p => p.Id == _first).CurrentSpeciesId).Contains(parent.CurrentSpeciesId)) continue;
            var secondButton = ParentButton(data, parent, parent.Id == _second, !_busy && !unavailable,
                () => SelectSecond(parent.Id));
            secondButton.ToolTip = unavailable ? "Родитель уже занят" : "Выбрать вторым родителем";
            SecondParentsPanel.Children.Add(secondButton);
        }
        if (data.Individuals.Count == 0) FirstParentsPanel.Children.Add(new TextBlock { Text = "Пока нет покемонов." });
        if (_first != Guid.Empty && SecondParentsPanel.Children.Count == 0)
            SecondParentsPanel.Children.Add(new TextBlock { Text = "Среди ваших покемонов нет свободного совместимого партнёра." });
        OutcomePanel.Children.Clear();
        ShopButton.Visibility = Visibility.Collapsed;
        _shortageBerry = null;
        StartButton.IsEnabled = false;
        CostText.Text = ShortageText.Text = "";
        if (_first == Guid.Empty || _second == Guid.Empty)
        {
            PreviewText.Text = "Выберите двух разных родителей.";
            return;
        }
        try
        {
            var ids = new[] { _first, _second };
            var preview = _preview(ids, _choice);
            PreviewText.Text = $"Исходы — корневые виды семей, включая малышей и одиночные виды. " +
                $"Случайный результат станет известен только после вылупления. Удача: {data.NurseryLuckBonus:P2}.";
            if (preview.SelectionUseful)
            {
                OutcomePanel.Children.Add(ChoiceButton("Случайный исход", null));
                foreach (var outcome in preview.Outcomes)
                    OutcomePanel.Children.Add(ChoiceButton($"{OutcomeName(data, outcome.SpeciesId)} · {preview.OutcomeChances[outcome.SpeciesId]:P0}", outcome.SpeciesId));
            }
            foreach (var outcome in preview.Outcomes)
            {
                var id = outcome.SpeciesId;
                var normalPool = preview.TraitsBySpecies[id];
                var shinyPool = preview.ShinyTraitsBySpecies[id];
                var traits = string.Join(", ", normalPool.Select(t => $"{CollectionView.TraitLabel(t)} {1d / normalPool.Count:P0}"));
                var shinyTraits = string.Join(", ", shinyPool.Select(t => $"{CollectionView.TraitLabel(t)} {1d / shinyPool.Count:P0}"));
                var card = new StackPanel { Orientation = Orientation.Horizontal, Width = 270, Margin = new Thickness(0, 4, 10, 4) };
                var asset = _catalog.GetAssetPath(id, false);
                if (asset is not null && File.Exists(asset))
                {
                    var sprite = new BitmapImage(new Uri(asset, UriKind.Absolute));
                    card.Children.Add(data.DiscoveredSpeciesIds.Contains(id)
                        ? new Image { Source = sprite, Width = 48, Height = 48, Margin = new Thickness(0, 0, 7, 0) }
                        : new Rectangle { Fill = (Brush)FindResource("PrimaryTextBrush"), OpacityMask = new ImageBrush(sprite),
                            Width = 48, Height = 48, Margin = new Thickness(0, 0, 7, 0) });
                }
                card.Children.Add(new TextBlock
                {
                    Text = $"{OutcomeName(data, id)} · {preview.OutcomeChances[id]:P0} · shiny {preview.ShinyChances[id]:P2} · " +
                        $"особенности: {traits}" + (shinyTraits != traits && shinyTraits.Length > 0 ? $" (shiny: {shinyTraits})" : ""),
                    TextWrapping = TextWrapping.Wrap, Width = 210
                });
                OutcomePanel.Children.Add(card);
            }
            CostText.Text = $"Нужно: {string.Join(", ", preview.Berries.Select(x => $"{x.Key} ×{x.Value}"))}. " +
                $"Ожидание: {preview.Hours} ч, с учётом родителей до {preview.ReadyAtUtc.ToLocalTime():g}." +
                (_choice is null ? "" : " Талисман потомства ×1.");
            var shortage = preview.Berries.Where(x => data.Berries.GetValueOrDefault(x.Key) < x.Value).ToArray();
            if (shortage.Length > 0)
            {
                ShortageText.Text = "Не хватает: " + string.Join(", ", shortage.Select(x =>
                    $"{x.Key} ×{x.Value - data.Berries.GetValueOrDefault(x.Key)}")) + ". Можно докупить или найти в походе.";
                _shortageBerry = shortage[0].Key;
                _shortageRequired = shortage[0].Value;
                ShopButton.Content = "Докупить ягоды";
                ShopButton.Visibility = Visibility.Visible;
            }
            else if (_choice is not null && data.Items.GetValueOrDefault("offspring-charm") < 1)
            {
                ShortageText.Text = "Не хватает талисмана потомства. Его можно купить в магазине.";
                _shortageBerry = "offspring-charm";
                _shortageRequired = 1;
                ShopButton.Content = "Купить талисман";
                ShopButton.Visibility = Visibility.Visible;
            }
            else StartButton.IsEnabled = !_busy;
        }
        catch (Exception ex)
        {
            PreviewText.Text = ex.Message;
        }
    }

    private string OutcomeName(SaveData data, int id) => data.DiscoveredSpeciesIds.Contains(id)
        ? $"№{id} · {SpeciesName(id)}" : $"№{id} · неизвестный вид";

    private Button ChoiceButton(string title, int? choice)
    {
        var button = new Button
        {
            Content = title, Tag = choice, IsEnabled = !_busy, Margin = new Thickness(0, 0, 6, 6),
            FontWeight = _choice == choice ? FontWeights.Bold : FontWeights.Normal,
            Style = (Style)FindResource("SecondaryButtonStyle")
        };
        button.Click += (_, _) =>
        {
            if (_busy) return;
            _choice = choice;
            _pendingStart = null;
            _selectionGeneration++;
            Refresh();
            OutcomePanel.Children.OfType<Button>().FirstOrDefault(b => Equals(b.Tag, choice))?.Focus();
        };
        return button;
    }

    private void RefreshNest(SaveData data, NurseryState nest)
    {
        var ready = nest.Status == "ready";
        NestTitle.Text = ready ? "Малыш готов появиться" : "Яйцо в гнезде";
        NestParents.Text = "Родители: " + string.Join(" + ", nest.ParticipantIds.Select(id => ParentName(data, id)));
        NestTerms.Text = $"Сохранённая цена: {string.Join(", ", nest.ResourcePrices.Select(x => $"{x.Key} ×{x.Value}"))}. " +
            $"{(nest.SelectedSpeciesId is { } selected ? $"Выбранный вид: {SpeciesName(selected)}." : "Случайный исход скрыт до вылупления.")} " +
            $"Удача: {data.NurseryLuckBonus:P2}.";
        NestTime.Text = ready ? "Яйцо ждёт встречи. Родители заняты до вылупления." :
            $"Готовность: {nest.ReadyAtUtc.ToLocalTime():g} · осталось {Math.Max(0, Math.Ceiling((nest.ReadyAtUtc - DateTimeOffset.UtcNow).TotalMinutes))} мин";
        var total = (nest.ReadyAtUtc - nest.StartedAtUtc).TotalSeconds;
        NestProgress.Value = ready || total <= 0 ? 1 : Math.Clamp((DateTimeOffset.UtcNow - nest.StartedAtUtc).TotalSeconds / total, 0, 1);
        HatchButton.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
        HatchButton.IsEnabled = !_busy;
    }

    private async Task TickAsync()
    {
        if (_busy || _refreshingReady) return;
        var nest = _snapshot().Nest;
        if (nest?.Status != "running" || DateTimeOffset.UtcNow < nest.ReadyAtUtc)
        {
            if (nest is not null) Refresh();
            return;
        }
        _refreshingReady = true;
        try { await _refreshReady(DateTimeOffset.UtcNow); }
        catch (Exception ex) { Show(ex.Message); }
        finally { _refreshingReady = false; Refresh(); }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _first == Guid.Empty || _second == Guid.Empty) return;
        var generation = _selectionGeneration;
        _pendingStart ??= new NurseryRequest([_first, _second], _choice, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var request = _pendingStart;
        _busy = true;
        Refresh();
        try
        {
            var result = await _start(request);
            if (generation != _selectionGeneration) return;
            if (_snapshot().Nest?.Id != request.OperationId || !result.Success)
                throw new InvalidOperationException("Запуск не подтверждён сохранением.");
            _pendingStart = null;
            Show(result.Message);
        }
        catch (Exception ex) { if (generation == _selectionGeneration) Show(ex.Message); }
        finally { _busy = false; Refresh(); }
    }

    private async void HatchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _snapshot().Nest is not { Status: "ready" } nest) return;
        if (_pendingHatchNest != nest.Id)
        {
            _pendingHatchNest = nest.Id;
            _pendingHatchOperation = Guid.NewGuid();
        }
        _busy = true;
        Refresh();
        try
        {
            var result = await _hatch(nest.Id, _pendingHatchOperation);
            var current = _snapshot().Nest;
            if (!result.Success || current?.Id != nest.Id || current.Status != "hatched" ||
                result.Child is null || !_snapshot().HatchedNurseryResults.ContainsKey(nest.Id))
                throw new InvalidOperationException("Вылупление не подтверждено сохранением.");
            _pendingHatchNest = _pendingHatchOperation = Guid.Empty;
            Show(result.Message);
        }
        catch (Exception ex) { Show(ex.Message); }
        finally { _busy = false; Refresh(); }
    }

    private void ShopButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_busy && _shortageBerry is { } berry) _shopShortage(berry, _shortageRequired);
    }

    private void OpenPokedexButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_busy && _snapshot().Nest is { Status: "hatched" } nest) _openPokedex(nest.Child.Id);
    }

    private async void ActivateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _snapshot().Nest is not { Status: "hatched" } nest) return;
        var id = nest.Child.Id;
        _busy = true;
        Refresh();
        try
        {
            await _activate(id);
            if (_snapshot().ActiveIndividualId != id) throw new InvalidOperationException("Спутник не подтверждён сохранением.");
            Show($"{SpeciesName(nest.Child.SpeciesId)} теперь на рабочем столе.");
        }
        catch (Exception ex) { Show(ex.Message); }
        finally { _busy = false; Refresh(); }
    }
}
