using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using Image = System.Windows.Controls.Image;
using Panel = System.Windows.Controls.Panel;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;
using PokeCorner.Core;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using PokeCorner.Core.PokeApi;

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
    private bool _suspended;
    private int _generation;
    private readonly Func<string, string, string, Task<bool>> _confirm;
    private readonly Action<string>? _openPetItems;
    private readonly Action? _openCharm;
    private TaskCompletionSource<bool>? _localConfirmation;
    private readonly Dictionary<(string Item, int Quantity), Guid> _purchaseOperations = [];

    public ShopView(AppState state, ShopService shop, Func<Action<SaveData>, Task> commit, Action onChanged, CatalogPackage? catalog = null,
        Func<string, string, string, Task<bool>>? confirmAsync = null, Action<string>? openPetItems = null,
        Action? openCharm = null)
    {
        InitializeComponent();
        _state = state;
        _shop = shop;
        _confirm = confirmAsync ?? ConfirmLocally;
        _openPetItems = openPetItems;
        _openCharm = openCharm;
        LocalConfirmationHost.Completed += result => CompleteLocalConfirmation(result);
        _commit = commit;
        _onChanged = onChanged;
        foreach (var good in shop.Goods) AddRow(good, good.IsBerry ? BerriesPanel : ItemsPanel);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Escape || _localConfirmation is null) return;
            CompleteLocalConfirmation(false); e.Handled = true;
        };
        Loaded += (_, _) => { _suspended = false; StartRefresh(); Refresh(); };
        Unloaded += (_, _) => { _suspended = true; _generation++; _timer?.Stop(); _timer = null; CompleteLocalConfirmation(false); };
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
        var card = new Border { Style = (Style)FindResource("CardStyle"), Padding = new Thickness(10, 16, 10, 16), Margin = new Thickness(0, 0, 12, 8) };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var path = Path.Combine(_shop.DataRoot, "final", "assets", "shop", good.Id + ".png");
        var icon = new Image { Width = 64, Height = 64, Margin = new Thickness(0, 0, 10, 0) };
        if (File.Exists(path))
        {
            using var stream = File.OpenRead(path);
            var bitmap = new BitmapImage();
            bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
            icon.Source = bitmap;
        }
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var detail = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        detail.Children.Add(new TextBlock { Text = good.Name, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        detail.Children.Add(new TextBlock { Text = good.Id switch
        {
            "care-booster" => "Помогает подготовиться к эволюции.",
            "lucky-charm" => "Помогает встретить shiny-покемона. Активируется в поимке.",
            _ => good.Effect
        }, Style = (Style)FindResource("CaptionTextStyle"), TextWrapping = TextWrapping.Wrap });
        var stock = new TextBlock { Style = (Style)FindResource("CaptionTextStyle") };
        detail.Children.Add(stock);
        Grid.SetColumn(detail, 1);
        grid.Children.Add(detail);

        var action = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        var quantity = new TextBox { Text = "1", Width = 38, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Количество" };
        var total = new TextBlock { Width = 84, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        var buy = new Button { Content = "Купить", Padding = new Thickness(8, 5, 8, 5), MinHeight = 34, Style = (Style)FindResource("UiPrimaryButtonStyle"), Tag = good.Id };
        System.Windows.Automation.AutomationProperties.SetName(quantity, $"Количество: {good.Name}");
        System.Windows.Automation.AutomationProperties.SetName(buy, $"Купить: {good.Name}");
        quantity.TextChanged += (_, _) => UpdateTotal(good);
        buy.Click += async (_, _) => await Buy(good);
        action.Children.Add(quantity);
        action.Children.Add(total);
        action.Children.Add(buy);
        if (good.Id == "xp-potion")
        {
            var use = new Button { Content = "Выбрать питомца", Padding = new Thickness(5), Margin = new Thickness(0, 4, 0, 0) };
            use.Click += (_, _) => OpenPetItems("xp-potion");
            detail.Children.Add(use);
        }
        if (good.Id == "care-booster")
        {
            var use = new Button { Content = "Выбрать питомца", Padding = new Thickness(5), Margin = new Thickness(0, 4, 0, 0) };
            use.Click += (_, _) => OpenPetItems("care-booster");
            detail.Children.Add(use);
        }
        if (good.Id == "lucky-charm")
        {
            var activate = new Button { Content = "К поимке", Padding = new Thickness(5), Margin = new Thickness(0, 4, 0, 0) };
            activate.Click += async (_, _) => await ActivateLuckyCharm();
            detail.Children.Add(activate);
        }
        Grid.SetColumn(action, 1);
        Grid.SetRow(action, 1);
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

    private Task<bool> ConfirmLocally(string title, string body, string caption)
    {
        _localConfirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        LocalConfirmationHost.Present(title, body, caption);
        return _localConfirmation.Task;
    }

    private void CompleteLocalConfirmation(bool result)
    {
        var pending = _localConfirmation; _localConfirmation = null;
        LocalConfirmationHost.Visibility = Visibility.Collapsed;
        pending?.TrySetResult(result);
    }

    private async Task Buy(ShopGood good)
    {
        if (_busy || _suspended) return;
        if (!int.TryParse(_rows[good.Id].Quantity.Text, out var count) || count <= 0)
        { Show("Укажите положительное количество."); return; }
        var total = (long)good.Price * count;
        if (total > _state.Shop.Coins)
        { Show($"Не хватает {total - _state.Shop.Coins} монет."); return; }
        _busy = true;
        var generation = _generation;
        Refresh();
        var key = (good.Id, count);
        if (!_purchaseOperations.TryGetValue(key, out var id)) _purchaseOperations[key] = id = Guid.NewGuid();
        PurchaseResult? result = null;
        var committed = false;
        try
        {
            if (!await _confirm("Купить предметы?", $"{good.Name} × {count} · {total} монет. Покупка попадёт в общий запас.", "Купить")
                || _suspended || generation != _generation) return;
            await _commit(next =>
            {
                result = _shop.TryPurchase(next, good.Id, count, id);
                if (!result.Success) throw new InvalidOperationException(result.Message);
            });
            committed = true;
            _purchaseOperations.Remove(key);
            if (!_suspended && generation == _generation) Show(result?.Message ?? "Покупка сохранена.");
            _onChanged();
        }
        catch (Exception ex)
        {
            if (!_suspended && generation == _generation)
                Show(committed ? "Покупка сохранена. Не удалось обновить экран: " + ex.Message
                    : ex is IOException or UnauthorizedAccessException
                        ? "Покупка не сохранена. Откройте историю сохранений в настройках для восстановления. " + ex.Message
                        : ex.Message + " Повторите покупку.");
        }
        finally { _busy = false; if (!_suspended) Refresh(); }
    }

    private void OpenPetItems(string item)
    {
        if (_busy || _suspended) return;
        if (_openPetItems is null) { Show("Откройте карточку нужного питомца в Покедексе: там видны запас и эффект предмета."); return; }
        _openPetItems(item);
    }

    private async Task ActivateLuckyCharm()
    {
        if (_busy || _suspended) return;
        if (_openCharm is not null) { _openCharm(); return; }
        _busy = true;
        Refresh();
        PurchaseResult? result = null;
        try
        {
            await _commit(next =>
            {
                result = _shop.TryActivateLuckyCharm(next);
                if (!result.Success) throw new InvalidOperationException(result.Message);
            });
            Show(result?.Message ?? "Талисман удачи активирован.");
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

    public void SelectItemShortage(string itemId, int required)
    {
        if (!_rows.TryGetValue(itemId, out var row) || !_shop.Goods.Any(g => g.Id == itemId && !g.IsBerry)) return;
        _state.Shop.Items.TryGetValue(itemId, out var owned);
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
        if (_suspended) return;
        CoinsText.Text = _state.Shop.Coins.ToString(System.Globalization.CultureInfo.CurrentCulture);
        CoinProgressBar.Value = ProgressCalculator.ComputeCoinFraction(_state.Shop);
        foreach (var good in _shop.Goods)
        {
            var row = _rows[good.Id];
            var stock = good.Id == "treats" ? _state.Feeding.Treats
                : (good.IsBerry ? _state.Shop.Berries : _state.Shop.Items).GetValueOrDefault(good.Id);
            row.Stock.Text = good.Id == "lucky-charm"
                ? $"Запас: {stock} · {(_state.Shop.CatchLuckyCharmActive ? "активен" : "не активен")} · {good.Price} монет/шт."
                : $"Запас: {stock} · {good.Price} монет/{(good.Id == "treats" ? "пачка" : "шт.")}";
            row.Buy.IsEnabled = row.Quantity.IsEnabled = !_busy;
            if (int.TryParse(row.Quantity.Text, out var count) && count > 0 && (long)good.Price * count > _state.Shop.Coins)
                row.Total.Text = $"Не хватает {(long)good.Price * count - _state.Shop.Coins} монет";
            else UpdateTotal(good);
        }
    }
}
