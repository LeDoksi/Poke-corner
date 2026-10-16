using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using UserControl = System.Windows.Controls.UserControl;
using Image = System.Windows.Controls.Image;
using Button = System.Windows.Controls.Button;
using Rectangle = System.Windows.Shapes.Rectangle;
using System.Windows.Media;
using System.Windows.Shapes;
using PokeCorner.Core.Persistence;
using Brush = System.Windows.Media.Brush;

namespace PokeCorner.App.Popup;

public partial class AchievementsView : UserControl
{
    private readonly RoomArt _art;
    private readonly Func<SaveData> _snapshot;
    private readonly Func<Action<SaveData>, Task> _commit;
    private readonly Action _back;
    private string? _selectedAwardId;
    private bool _busy;

    private static readonly string[] Names =
    [
        "Полевой альбом", "Большая компания", "Рады знакомству", "В путь", "Карта воспоминаний",
        "Растём вместе", "Рядом", "Новая жизнь", "Время расти", "Особенная встреча",
        "Теперь ты Росток", "Любимчик", "На своём месте", "Слаженная работа", "Из гнезда — в путь",
        "Семейное дело", "Новая глава", "Полная корзинка", "По рецепту", "Осознанный выбор",
        "Ждали именно тебя", "Легенды рядом", "Второе призвание", "Новые привычки",
        "В добрые руки", "Сила в капле"
    ];

    public AchievementsView(RoomArt art, Func<SaveData> snapshot, Func<Action<SaveData>, Task> commit, Action back)
    {
        _art = art;
        _snapshot = snapshot;
        _commit = commit;
        _back = back;
        InitializeComponent();
        Loaded += (_, _) => Refresh();
        Refresh();
    }

    public static void ApplyShelfSelection(SaveData data, RoomArt art, string? awardId, int slot)
    {
        if (slot is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(slot));
        if (awardId is not null)
        {
            var card = art.Cards.FirstOrDefault(id => art.LatestEarned(data, id) == awardId);
            if (card is null || !data.AwardLedger.ContainsKey(awardId))
                throw new InvalidOperationException("Награда ещё не открыта.");
        }
        while (data.TrophyShelfIds.Count <= slot) data.TrophyShelfIds.Add(null);
        if (awardId is not null)
            for (var i = 0; i < data.TrophyShelfIds.Count; i++)
                if (i != slot && data.TrophyShelfIds[i] == awardId) data.TrophyShelfIds[i] = null;
        data.TrophyShelfIds[slot] = awardId;
    }

    public void Refresh()
    {
        var data = _snapshot();
        ShelfPanel.Children.Clear();
        for (var slot = 0; slot < 3; slot++)
        {
            var index = slot;
            var id = slot < data.TrophyShelfIds.Count ? data.TrophyShelfIds[slot] : null;
            var panel = new StackPanel { Width = 174, Margin = new Thickness(0, 0, 10, 10) };
            if (id is not null && _art.Awards.TryGetValue(id, out var award))
                panel.Children.Add(new Image { Source = RoomView.LoadImage(_art.PathFor(award.File)),
                    Width = 132, Height = 94, Stretch = Stretch.Uniform });
            else
                panel.Children.Add(new TextBlock { Text = "Свободно", Height = 94,
                    TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            var button = new Button
            {
                Content = $"Место {slot + 1} · " + (_selectedAwardId is not null ? "выставить" : id is null ? "свободно" : "снять"),
                Tag = slot, IsEnabled = !_busy && (_selectedAwardId is not null || id is not null),
                Style = (Style)FindResource("SecondaryButtonStyle"),
                ToolTip = _selectedAwardId is not null ? "Выставить выбранную награду" :
                    id is null ? "Сначала выберите открытую награду" : "Снять награду бесплатно"
            };
            ToolTipService.SetShowOnDisabled(button, true);
            AutomationProperties.SetName(button, $"Место {slot + 1}: " + (_selectedAwardId is not null ? "выставить выбранную награду" : id is null ? "свободно" : "снять награду"));
            button.Click += async (_, _) => await ChangeSlotAsync(index);
            panel.Children.Add(button);
            ShelfPanel.Children.Add(panel);
        }
        CardsPanel.Children.Clear();
        for (var i = 0; i < _art.Cards.Count; i++)
        {
            var card = _art.Cards[i];
            var earned = _art.LatestEarned(data, card);
            var appearance = _art.Awards[earned ?? card];
            var panel = new StackPanel { Width = 270, Margin = new Thickness(0, 0, 14, 14) };
            if (earned is null)
                panel.Children.Add(new Rectangle
                {
                    Width = 132, Height = 94, HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    Fill = (Brush)FindResource("PrimaryTextBrush"),
                    OpacityMask = new ImageBrush(RoomView.LoadImage(_art.PathFor(appearance.MaskFile)))
                    { Stretch = Stretch.Uniform, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }
                });
            else
                panel.Children.Add(new Image { Source = RoomView.LoadImage(_art.PathFor(appearance.File)),
                    Width = 132, Height = 94, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Stretch = Stretch.Uniform });
            panel.Children.Add(new TextBlock { Text = Names[i], FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
            panel.Children.Add(new TextBlock
            {
                Text = earned is null ? "Ещё не открыто" :
                    appearance.Id.StartsWith("trophy-special-", StringComparison.Ordinal) ? "Открыто" :
                    $"Ступень {appearance.Id[^1]} открыта",
                TextWrapping = TextWrapping.Wrap
            });
            var button = new Button
            {
                Content = earned is null ? "Закрыто" : _selectedAwardId == earned ? "Отменить выбор" : "Выбрать для полки",
                Tag = card, IsEnabled = !_busy && earned is not null,
                Style = (Style)FindResource("SecondaryButtonStyle"),
                ToolTip = earned is null ? "Эта награда ещё не открыта" : "Выбрать награду для места на полке",
                Margin = new Thickness(0, 6, 0, 0)
            };
            ToolTipService.SetShowOnDisabled(button, true);
            AutomationProperties.SetName(button, Names[i] + (earned is null ? ": ещё не открыто" : ": выбрать для полки"));
            if (earned is not null)
                button.Click += (_, _) =>
                {
                    _selectedAwardId = _selectedAwardId == earned ? null : earned;
                    MessageText.Text = _selectedAwardId is null
                        ? "Выбор отменён. Нажмите занятое место, чтобы снять награду."
                        : "Выберите одно из трёх мест полки. Выбор и замена бесплатны.";
                    Refresh();
                };
            panel.Children.Add(button);
            CardsPanel.Children.Add(panel);
        }
    }

    private async Task ChangeSlotAsync(int slot)
    {
        if (_busy) return;
        var selected = _selectedAwardId;
        _busy = true;
        try
        {
            await _commit(next => ApplyShelfSelection(next, _art, selected, slot));
            _selectedAwardId = null;
            MessageText.Text = selected is null ? "Награда снята с полки." : "Награда выставлена на полку.";
        }
        catch (Exception ex)
        {
            MessageText.Text = "Полка не изменена: " + ex.Message;
        }
        finally
        {
            _busy = false;
            Refresh();
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => _back();
}
