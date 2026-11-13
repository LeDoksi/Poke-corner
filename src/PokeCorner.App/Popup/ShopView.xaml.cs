using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using Image = System.Windows.Controls.Image;
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
    private sealed record Tile(Button Button, Image Icon, TextBlock Name, TextBlock Price, Border Badge, TextBlock Stock);
    private readonly Dictionary<string, Tile> _tiles = [];
    private readonly Dictionary<string, string> _quantities = [];
    private readonly Dictionary<string, BitmapImage?> _art = [];
    private ShopGood _selected;
    private string _category = "all";
    private int _step = 1;
    private bool _syncing;
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
        _selected = shop.Goods[0];
        foreach (var good in shop.Goods) AddTile(good);
        QuantityBox.GotKeyboardFocus += (_, _) => QuantityBox.SelectAll();
        _syncing = true; CategoryBox.SelectedIndex = 0; StepBox.SelectedIndex = 0; _syncing = false;
        Select(_selected);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Escape || _localConfirmation is null) return;
            CompleteLocalConfirmation(false); e.Handled = true;
        };
        Loaded += (_, _) => { _suspended = false; StartRefresh(); Refresh(); };
        Unloaded += (_, _) => { ClearReturn(); _suspended = true; _generation++; _timer?.Stop(); _timer = null; CompleteLocalConfirmation(false); };
        Refresh();
    }

    private void StartRefresh()
    {
        if (_timer is not null) return;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
    }

    private BitmapImage? Art(string id)
    {
        if (_art.TryGetValue(id, out var cached)) return cached;
        var path = Path.Combine(_shop.DataRoot, "final", "assets", "shop", id + ".png");
        BitmapImage? bitmap = null;
        if (File.Exists(path))
        {
            using var stream = File.OpenRead(path);
            bitmap = new BitmapImage();
            bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
        }
        return _art[id] = bitmap;
    }

    private static string Category(ShopGood good) => good.IsBerry ? "berries" : good.Id is "lucky-charm" or "offspring-charm" ? "charms" : "pet";

    private static string Effect(ShopGood good) => good.Id switch
    {
        "care-booster" => "Помогает подготовиться к эволюции.",
        "lucky-charm" => "Помогает встретить шайни-покемона. Активируется в поимке.",
        _ => good.Effect
    };

    private static string Unit(ShopGood good) => good.Id == "treats" ? "пачка" : "шт.";

    private int Stock(ShopGood good) => good.Id == "treats" ? _state.Feeding.Treats
        : (good.IsBerry ? _state.Shop.Berries : _state.Shop.Items).GetValueOrDefault(good.Id);

    private void AddTile(ShopGood good)
    {
        _quantities[good.Id] = "1";
        var icon = new Image { Source = Art(good.Id), Width = 60, Height = 60, HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
        System.Windows.Media.RenderOptions.SetBitmapScalingMode(icon, System.Windows.Media.BitmapScalingMode.HighQuality);
        var name = new TextBlock { Text = good.Name, FontWeight = FontWeights.SemiBold, FontSize = 13, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 36, Margin = new Thickness(0, 6, 0, 2) };
        var price = new TextBlock { Text = $"{good.Price} монет", FontSize = 12.5, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Foreground = (System.Windows.Media.Brush)FindResource("NdApricotBrush") };
        var stock = new TextBlock { FontSize = 11.5, FontWeight = FontWeights.Bold, Foreground = (System.Windows.Media.Brush)FindResource("NdTextBrush") };
        // What is already owned sits in the corner of the tile, the way a shopkeeper marks a shelf.
        var badge = new Border { Child = stock, CornerRadius = new CornerRadius(9), Padding = new Thickness(7, 1, 7, 2), HorizontalAlignment = System.Windows.HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xCC, 0x14, 0x17, 0x2B)), BorderBrush = (System.Windows.Media.Brush)FindResource("NdBorderBrush"), BorderThickness = new Thickness(1), ToolTip = "В запасе" };
        var body = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        body.Children.Add(icon); body.Children.Add(name); body.Children.Add(price);
        var content = new Grid();
        content.Children.Add(body); content.Children.Add(badge);
        var tile = new Button { Style = (Style)FindResource("NdTileButtonStyle"), Tag = good.Id, Content = content, Padding = new Thickness(8), Margin = new Thickness(5),
            HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, Width = 150, Height = 140 };
        tile.Click += (_, _) => Select(good);
        GoodsPanel.Children.Add(tile);
        _tiles.Add(good.Id, new Tile(tile, icon, name, price, badge, stock));
    }

    private void Select(ShopGood good)
    {
        _selected = good;
        DetailArt.Source = Art(good.Id);
        DetailNameText.Text = good.Name;
        DetailEffectText.Text = Effect(good);
        UseButton.Content = good.Id == "lucky-charm" ? "К поимке →" : "Выбрать питомца →";
        UseButton.Visibility = good.Id is "xp-potion" or "care-booster" or "lucky-charm" ? Visibility.Visible : Visibility.Collapsed;
        QuantityBox.ToolTip = good.Id == "treats" ? "Количество пачек" : "Количество предметов";
        System.Windows.Automation.AutomationProperties.SetName(QuantityBox, $"Количество: {good.Name}" + (good.Id == "treats" ? " · пачки" : " · штуки"));
        System.Windows.Automation.AutomationProperties.SetName(BuyButton, $"Купить: {good.Name}");
        _syncing = true; QuantityBox.Text = _quantities[good.Id]; _syncing = false;
        var accent = (System.Windows.Media.Brush)FindResource("NdApricotBrush");
        var line = (System.Windows.Media.Brush)FindResource("NdPanelLineBrush");
        foreach (var (id, tile) in _tiles)
        {
            tile.Button.BorderBrush = id == good.Id ? accent : line;
            tile.Button.BorderThickness = new Thickness(id == good.Id ? 2 : 1);
            System.Windows.Automation.AutomationProperties.SetName(tile.Button, _shop.Goods.First(g => g.Id == id).Name + (id == good.Id ? " · выбрано" : ""));
        }
        Refresh();
    }

    private void CategoryBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || CategoryBox.SelectedItem is not ListBoxItem { Tag: string category }) return;
        _category = category;
        foreach (var good in _shop.Goods)
            _tiles[good.Id].Button.Visibility = category == "all" || Category(good) == category ? Visibility.Visible : Visibility.Collapsed;
        LayoutTiles();
        // The detail panel never shows a good the shelf has just hidden.
        if (category != "all" && Category(_selected) != category) Select(_shop.Goods.First(g => Category(g) == category));
    }

    private void StepBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (StepBox.SelectedItem is ListBoxItem { Tag: string step }) _step = int.Parse(step);
    }

    private void QuantityBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing || _tiles.Count == 0) return;
        _quantities[_selected.Id] = QuantityBox.Text;
        UpdateTotal();
    }

    private void DecreaseButton_Click(object sender, RoutedEventArgs e) => AdjustQuantity(-1);
    private void IncreaseButton_Click(object sender, RoutedEventArgs e) => AdjustQuantity(1);
    private async void BuyButton_Click(object sender, RoutedEventArgs e) => await Buy(_selected);

    private async void UseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selected.Id == "lucky-charm") await ActivateLuckyCharm();
        else OpenPetItems(_selected.Id);
    }

    private int MaximumQuantity(ShopGood good) => Math.Min((int.MaxValue - Stock(good)) / good.Units, int.MaxValue / good.Price);

    // With a step of 5 or 10 the buttons walk the round numbers: 1 → 5 → 10, and back down to 1.
    private void AdjustQuantity(int direction)
    {
        var maximum = MaximumQuantity(_selected);
        if (_busy || _suspended || maximum < 1) return;
        long current = int.TryParse(QuantityBox.Text, out var count) ? count : 0;
        var next = direction > 0 ? (current / _step + 1) * _step : (Math.Max(1, current) - 1) / _step * _step;
        QuantityBox.Text = Math.Clamp(next, 1, maximum).ToString();
    }

    private void UpdateTotal()
    {
        var good = _selected;
        var maximum = MaximumQuantity(good);
        var parsed = int.TryParse(_quantities[good.Id], out var count);
        var valid = parsed && count > 0 && count <= maximum;
        var cost = (long)good.Price * count;
        TotalText.Text = !parsed || count <= 0 ? "Укажите целое количество от 1."
            : count > maximum ? maximum == 0 ? "В запасе больше нет места." : $"Можно купить не больше {maximum}."
            : cost > _state.Shop.Coins ? $"Итого {cost} монет · не хватает {cost - _state.Shop.Coins}."
            : $"Итого {cost} монет";
        TotalText.Foreground = (System.Windows.Media.Brush)FindResource(valid && cost <= _state.Shop.Coins ? "SecondaryTextBrush" : "UiDangerBrush");
        BuyButton.IsEnabled = !_busy && valid && cost <= _state.Shop.Coins;
        QuantityBox.IsEnabled = !_busy;
        DecreaseButton.IsEnabled = !_busy && parsed && count > 1;
        IncreaseButton.IsEnabled = !_busy && maximum > 0 && (!parsed || count < maximum);
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
        if (!int.TryParse(_quantities[good.Id], out var count) || count <= 0 || count > MaximumQuantity(good))
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

    public void SelectBerryShortage(string berryId, int required) => SelectShortage(berryId, required, true);

    public void SelectItemShortage(string itemId, int required) => SelectShortage(itemId, required, false);

    // A shortage elsewhere opens the shop on the missing good with the missing amount already typed in.
    private void SelectShortage(string id, int required, bool berry)
    {
        if (_shop.Goods.FirstOrDefault(g => g.Id == id && g.IsBerry == berry) is not { } good) return;
        if (_category != "all") CategoryBox.SelectedIndex = 0;
        (berry ? _state.Shop.Berries : _state.Shop.Items).TryGetValue(id, out var owned);
        _quantities[id] = Math.Max(0, required - owned).ToString();
        Select(good);
        _tiles[id].Button.BringIntoView();
        QuantityBox.Focus();
    }

    private Action? _return;

    /// A shortage elsewhere sent the player here; offer the way back until they leave the shop.
    public void SetReturn(string caption, Action back)
    {
        _return = back;
        ReturnButton.Content = "← " + caption;
        ReturnButton.Visibility = Visibility.Visible;
    }

    private void ClearReturn()
    {
        _return = null;
        ReturnButton.Visibility = Visibility.Collapsed;
    }

    private void ReturnButton_Click(object sender, RoutedEventArgs e)
    {
        var back = _return;
        ClearReturn();
        back?.Invoke();
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
            var tile = _tiles[good.Id];
            var stock = Stock(good);
            tile.Stock.Text = "×" + stock;
            tile.Badge.Opacity = stock > 0 ? 1 : .45;
            // A price the purse cannot cover right now goes quiet, so the affordable shelf reads at a glance.
            tile.Price.Opacity = good.Price <= _state.Shop.Coins ? 1 : .55;
        }
        var owned = Stock(_selected);
        StockText.Text = _selected.Id == "lucky-charm" ? $"{owned} · {(_state.Shop.CatchLuckyCharmActive ? "активен" : "не активен")}" : owned.ToString();
        PriceText.Text = $"{_selected.Price} монет/{Unit(_selected)}";
        UpdateTotal();
    }

    private static readonly DiaryPiece[] MarginPieces =
    [
        new("shop-list", "Список: ягоды. Ниже: ещё ягоды."), new("shop-coins"),
        new("shop-berries", "Чери, Оран, Печа. Звучит как заклинание."), new("shop-receipt", "Чек потерян. Сдача, видимо, тоже."),
    ];

    private void Root_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DetailColumn.Width = new GridLength(e.NewSize.Width >= 1500 ? 380 : e.NewSize.Width >= 900 ? 320 : 280);
        LayoutTiles();
        ShopMargin.Show("shop", MarginPieces);
    }

    /// The shelf fills the page: the widest tiles (up to 320 px) whose rows still fit without scrolling;
    /// when nothing fits, the smallest readable tile and the shelf scrolls.
    private void LayoutTiles()
    {
        double width = ActualWidth - 48 - DetailColumn.Width.Value - 8 + 5, height = GoodsScroll.ActualHeight + 5;
        if (width < 200 || _tiles.Count == 0) return;
        var shown = Math.Max(1, _tiles.Values.Count(t => t.Button.Visibility == Visibility.Visible));
        double best = 0;
        for (var columns = 2; columns <= 6; columns++)
        {
            var w = Math.Min(320, Math.Floor(width / columns));
            if (w < 130) break;
            var rows = Math.Ceiling(shown / (double)columns);
            if (rows * Math.Round(w * .92) <= height && w > best) best = w;
        }
        if (best == 0) best = Math.Max(130, Math.Min(170, Math.Floor((width - 14) / Math.Max(2, Math.Floor((width - 14) / 150)))));
        foreach (var tile in _tiles.Values)
        {
            tile.Button.Width = best - 10; tile.Button.Height = Math.Round(best * .92) - 10;
            tile.Icon.Width = tile.Icon.Height = Math.Round(Math.Clamp(best * .4, 52, 104));
            tile.Name.FontSize = best >= 200 ? 14.5 : 13;
        }
    }
}
