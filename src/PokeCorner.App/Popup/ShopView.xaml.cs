using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using Image = System.Windows.Controls.Image;
using MessageBox = System.Windows.MessageBox;
using Panel = System.Windows.Controls.Panel;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;

namespace PokeCorner.App.Popup;

public partial class ShopView : UserControl
{
    private readonly AppState _state;
    private readonly ShopService _shop;
    private readonly Func<Action<SaveData>, Task> _commit;
    private readonly Action _onChanged;
    private readonly Dictionary<string, (TextBlock Stock, TextBlock Total, TextBox Quantity, Button Buy)> _rows = [];
    private DispatcherTimer? _timer;
    private bool _busy;

    public ShopView(AppState state, ShopService shop, Func<Action<SaveData>, Task> commit, Action onChanged)
    {
        InitializeComponent();
        _state = state;
        _shop = shop;
        _commit = commit;
        _onChanged = onChanged;
        foreach (var good in shop.Goods) AddRow(good, good.IsBerry ? BerriesPanel : ItemsPanel);
        Loaded += (_, _) => StartRefresh();
        Unloaded += (_, _) => { _timer?.Stop(); _timer = null; };
        Refresh();
    }

    private void StartRefresh()
    {
        if (_timer is not null) return;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
    }

    private void AddRow(ShopGood good, Panel panel)
    {
        var card = new Border { Style = (Style)FindResource("CardStyle"), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 8) };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var path = Path.Combine(_shop.DataRoot, "final", "assets", "shop", good.Id + ".png");
        var icon = new Image { Width = 64, Height = 64, Margin = new Thickness(0, 0, 10, 0) };
        icon.Source = new BitmapImage(new Uri(path));
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var detail = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        detail.Children.Add(new TextBlock { Text = good.Name, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        detail.Children.Add(new TextBlock { Text = good.Effect, Style = (Style)FindResource("CaptionTextStyle"), TextWrapping = TextWrapping.Wrap });
        var stock = new TextBlock { Style = (Style)FindResource("CaptionTextStyle") };
        detail.Children.Add(stock);
        Grid.SetColumn(detail, 1);
        grid.Children.Add(detail);

        var action = new StackPanel { Width = 110, Margin = new Thickness(8, 0, 0, 0) };
        var quantity = new TextBox { Text = "1", Width = 54, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, ToolTip = "Положительное количество" };
        var total = new TextBlock { TextAlignment = TextAlignment.Right, TextWrapping = TextWrapping.Wrap };
        var buy = new Button { Content = "Купить", Padding = new Thickness(5), Tag = good.Id };
        quantity.TextChanged += (_, _) => UpdateTotal(good);
        buy.Click += async (_, _) => await Buy(good);
        action.Children.Add(quantity);
        action.Children.Add(total);
        action.Children.Add(buy);
        if (good.Id == "xp-potion")
        {
            var use = new Button { Content = "Применить", Padding = new Thickness(5), Margin = new Thickness(0, 4, 0, 0) };
            use.Click += async (_, _) => await UsePotion();
            action.Children.Add(use);
        }
        if (good.Id == "care-booster")
        {
            var use = new Button { Content = "Применить", Padding = new Thickness(5), Margin = new Thickness(0, 4, 0, 0) };
            use.Click += async (_, _) => await UseCareBooster();
            action.Children.Add(use);
        }
        Grid.SetColumn(action, 2);
        grid.Children.Add(action);
        card.Child = grid;
        panel.Children.Add(card);
        _rows.Add(good.Id, (stock, total, quantity, buy));
        UpdateTotal(good);
    }

    private void UpdateTotal(ShopGood good)
    {
        if (!_rows.TryGetValue(good.Id, out var row)) return;
        row.Total.Text = int.TryParse(row.Quantity.Text, out var count) && count > 0
            ? $"{(long)good.Price * count} монет"
            : "Укажите количество";
    }

    private async Task Buy(ShopGood good)
    {
        if (_busy) return;
        if (!int.TryParse(_rows[good.Id].Quantity.Text, out var count) || count <= 0)
        {
            Show("Укажите положительное количество.");
            return;
        }
        _busy = true;
        Refresh();
        var id = Guid.NewGuid();
        PurchaseResult? result = null;
        try
        {
            await _commit(next =>
            {
                result = _shop.TryPurchase(next, good.Id, count, id);
                if (!result.Success) throw new InvalidOperationException(result.Message);
            });
            Show(result?.Message ?? "Покупка сохранена.");
            _onChanged();
        }
        catch (Exception ex) { Show(ex.Message); }
        finally { _busy = false; Refresh(); }
    }

    private async Task UsePotion()
    {
        if (_busy) return;
        var active = _state.Collection.GetActive();
        if (active is null) { Show("Выберите питомца."); return; }
        var levels = new LevelProgression(_shop.DataRoot);
        var (useful, excess) = _shop.PotionPreview(active, levels);
        if (useful == 0) { Show("Максимальный уровень: зелье не расходуется."); return; }
        if (MessageBox.Show($"Полезно {useful} XP; излишек {excess} XP. Применить одно зелье?", "Зелье опыта",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _busy = true;
        Refresh();
        var id = Guid.NewGuid();
        PurchaseResult? result = null;
        try
        {
            await _commit(next =>
            {
                result = _shop.TryUseXpPotion(next, active.Id, id, levels);
                if (!result.Success) throw new InvalidOperationException(result.Message);
            });
            Show(result?.Message ?? "Зелье применено.");
            _onChanged();
        }
        catch (Exception ex) { Show(ex.Message); }
        finally { _busy = false; Refresh(); }
    }

    private async Task UseCareBooster()
    {
        if (_busy) return;
        var active = _state.Collection.GetActive();
        if (active is null) { Show("Выберите питомца."); return; }
        _busy = true;
        Refresh();
        var id = Guid.NewGuid();
        PurchaseResult? result = null;
        try
        {
            await _commit(next =>
            {
                result = _shop.TryUseCareBooster(next, active.Id, id);
                if (!result.Success) throw new InvalidOperationException(result.Message);
            });
            Show(result?.Message ?? "Бустер применён.");
            _onChanged();
        }
        catch (Exception ex) { Show(ex.Message); }
        finally { _busy = false; Refresh(); }
    }

    public void SelectBerryShortage(string berryId, int required)
    {
        if (!_rows.TryGetValue(berryId, out var row) || !_shop.Goods.Any(g => g.Id == berryId && g.IsBerry)) return;
        _state.Shop.Berries.TryGetValue(berryId, out var owned);
        row.Quantity.Text = Math.Max(0, required - owned).ToString();
        row.Quantity.Focus();
    }

    private void Show(string message)
    {
        MessageText.Text = message;
        MessagePanel.Visibility = Visibility.Visible;
    }

    public void Refresh()
    {
        CoinsText.Text = _state.Shop.Coins.ToString(System.Globalization.CultureInfo.CurrentCulture);
        CoinProgressBar.Value = ProgressCalculator.ComputeCoinFraction(_state.Shop);
        foreach (var good in _shop.Goods)
        {
            var row = _rows[good.Id];
            var stock = good.Id == "treats" ? _state.Feeding.Treats
                : (good.IsBerry ? _state.Shop.Berries : _state.Shop.Items).GetValueOrDefault(good.Id);
            row.Stock.Text = $"Запас: {stock} · {good.Price} монет/{(good.Id == "treats" ? "пачка" : "шт.")}";
            row.Buy.IsEnabled = !_busy;
            if (int.TryParse(row.Quantity.Text, out var count) && count > 0 && (long)good.Price * count > _state.Shop.Coins)
                row.Total.Text = $"Не хватает {(long)good.Price * count - _state.Shop.Coins} монет";
            else UpdateTotal(good);
        }
    }
}
