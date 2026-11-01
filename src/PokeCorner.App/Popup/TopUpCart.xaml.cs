using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PokeCorner.App.Imaging;
using PokeCorner.Core.Domain;
using Brush = System.Windows.Media.Brush;
using Image = System.Windows.Controls.Image;
using Button = System.Windows.Controls.Button;
using UserControl = System.Windows.Controls.UserControl;

namespace PokeCorner.App.Popup;

/// One good the cart offers. Need is how many are missing (the count starts there and can't go lower);
/// an optional good (Need 0) starts at zero and the player adds it with "+".
public sealed record TopUpNeed(string Id, int Need, int Have, string? Note = null);

/// The in-place top-up from the nursery (D-27), reused by evolution and retraining in the pet card:
/// the missing goods are already counted, one button buys them all in one save, no confirmation.
public partial class TopUpCart : UserControl
{
    private readonly Dictionary<string, int> _cart = [];
    private readonly Dictionary<(string, int), Guid> _operations = [];
    private readonly Dictionary<string, ImageSource?> _art = [];
    private IReadOnlyList<TopUpNeed> _needs = [];
    private long _coins;
    private ShopService _shop = new();
    private bool _busy;

    public TopUpCart()
    {
        InitializeComponent();
        // A click on the cart is not a click on the dimmed backdrop behind a card.
        MouseLeftButtonDown += (_, e) => e.Handled = true;
    }

    /// Buys the listed goods in one commit; throws with a readable message when the shop refuses.
    public Func<IReadOnlyList<(string Item, int Quantity, Guid Operation)>, Task>? Buy { get; set; }
    /// "Открыть магазин": the old way, kept as a way out of the cart.
    public event Action<string?>? ShopRequested;
    public event Action<string>? Bought;
    public event Action<string>? Failed;
    public event Action? Closed;

    public bool IsOpen => Visibility == Visibility.Visible;
    public string? Owner { get; private set; }

    public void UseShop(ShopService shop) { _shop = shop; _art.Clear(); }

    // A missing or broken price list must not take the pet card down with it: the cart then simply has nothing to sell.
    private IReadOnlyList<ShopGood> Goods
    {
        get
        {
            try { return _shop.Goods; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException) { return []; }
        }
    }

    public int Price(string id) => Goods.FirstOrDefault(g => g.Id == id)?.Price ?? 0;
    public long Cost(IEnumerable<TopUpNeed> needs) => needs.Sum(n => (long)Price(n.Id) * n.Need);

    public string GoodName(string id) => Goods.FirstOrDefault(g => g.Id == id) is { IsBerry: false } good ? good.Name : ExpeditionView.BerryName(id);

    /// Opens the cart for one screen part (owner), starting from exactly what is missing.
    public void Open(string owner, string title, IReadOnlyList<TopUpNeed> needs, long coins)
    {
        if (Owner != owner) _cart.Clear();
        Owner = owner;
        TitleText.Text = title;
        Visibility = Visibility.Visible;
        Render(needs, coins);
        BuyButton.Focus();
    }

    /// Re-reads stock and coins while open (after a save elsewhere) and keeps the chosen counts.
    public void Update(IReadOnlyList<TopUpNeed> needs, long coins)
    {
        if (!IsOpen || _busy) return;
        if (needs.Count == 0) { Close(); return; }
        if (coins == _coins && needs.SequenceEqual(_needs)) return;
        Render(needs, coins);
    }

    public void Close()
    {
        if (!IsOpen) return;
        Visibility = Visibility.Collapsed;
        _cart.Clear();
        Owner = null;
        Closed?.Invoke();
    }

    private void Render(IReadOnlyList<TopUpNeed> needs, long coins)
    {
        _needs = needs; _coins = coins;
        foreach (var need in needs) if (!_cart.ContainsKey(need.Id) || _cart[need.Id] < need.Need) _cart[need.Id] = need.Need;
        foreach (var id in _cart.Keys.Where(id => needs.All(n => n.Id != id)).ToArray()) _cart.Remove(id);
        RowsPanel.Children.Clear();
        foreach (var need in needs)
        {
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
            var icon = new Image { Source = Art(need.Id), Width = 30, Height = 30, Margin = new Thickness(0, 0, 8, 0) };
            DockPanel.SetDock(icon, Dock.Left); row.Children.Add(icon);
            var cost = new TextBlock { Text = $"{(long)Price(need.Id) * _cart[need.Id]} ●", Width = 52, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("MutedTextBrush") };
            var plus = Step("+", need.Id, +1); var minus = Step("−", need.Id, -1); minus.IsEnabled = _cart[need.Id] > need.Need && !_busy;
            var count = new TextBlock { Text = _cart[need.Id].ToString(CultureInfo.CurrentCulture), FontWeight = FontWeights.Bold, Width = 30, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            foreach (var element in new UIElement[] { cost, plus, count, minus }) { DockPanel.SetDock(element, Dock.Right); row.Children.Add(element); }
            var name = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            name.Children.Add(new TextBlock { Text = GoodName(need.Id), FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            name.Children.Add(new TextBlock { Text = need.Note ?? $"в запасе {need.Have} · нужно {need.Have + need.Need}", FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("MutedTextBrush") });
            row.Children.Add(name);
            RowsPanel.Children.Add(row);
        }
        var total = _cart.Sum(x => (long)Price(x.Key) * x.Value);
        var lacking = total - coins;
        TotalText.Text = lacking > 0
            ? $"Итого {total} ● · у вас {coins}. Не хватает {lacking} монет: ягоды можно найти в походе."
            : $"Итого {total} ● · у вас {coins}";
        BuyButton.Content = $"Купить · {total} ●";
        BuyButton.IsEnabled = !_busy && Buy is not null && total > 0 && lacking <= 0;
        CancelButton.IsEnabled = ShopButton.IsEnabled = !_busy;
    }

    private Button Step(string text, string id, int delta)
    {
        var button = new Button { Content = text, MinWidth = 28, Padding = new Thickness(4, 0, 4, 0), Style = (Style)FindResource("SecondaryButtonStyle"), IsEnabled = !_busy,
            ToolTip = delta > 0 ? "Купить на одну больше" : "На одну меньше" };
        button.Click += (_, _) =>
        {
            if (_busy || !_cart.ContainsKey(id)) return;
            var floor = _needs.FirstOrDefault(n => n.Id == id)?.Need ?? 0;
            _cart[id] = Math.Clamp(_cart[id] + delta, floor, 999);
            Render(_needs, _coins);
        };
        return button;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Shop_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var owner = Owner;
        Close();
        ShopRequested?.Invoke(owner);
    }

    private async void Buy_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || Buy is null) return;
        var items = _cart.Where(x => x.Value > 0).Select(x =>
        {
            if (!_operations.TryGetValue((x.Key, x.Value), out var op)) _operations[(x.Key, x.Value)] = op = Guid.NewGuid();
            return (x.Key, x.Value, op);
        }).ToArray();
        if (items.Length == 0) return;
        _busy = true;
        Render(_needs, _coins);
        try
        {
            await Buy(items);
            foreach (var (item, quantity, _) in items) _operations.Remove((item, quantity));
            _busy = false;
            Close();
            Bought?.Invoke("Куплено: " + string.Join(", ", items.Select(x => $"{GoodName(x.Item1).ToLower(CultureInfo.CurrentCulture)} ×{x.Item2}")) + ".");
        }
        catch (Exception ex)
        {
            _busy = false;
            if (IsOpen) Render(_needs, _coins);
            Failed?.Invoke(ex.Message + " Покупка не прошла, можно повторить.");
        }
    }

    private ImageSource? Art(string id)
    {
        if (_art.TryGetValue(id, out var image)) return image;
        try
        {
            var path = Path.Combine(_shop.DataRoot, "final", "assets", "shop", id + ".png");
            if (File.Exists(path)) image = SpriteImages.Decode(File.ReadAllBytes(path));
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException or ArgumentException)
        {
            image = null;
        }
        return _art[id] = image;
    }
}
