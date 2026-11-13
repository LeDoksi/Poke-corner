using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PokeCorner.Core.Domain;
using PokeCorner.Core.Persistence;
using Brush = System.Windows.Media.Brush;
using Button = System.Windows.Controls.Button;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;

namespace PokeCorner.App.Popup;

/// <summary>
/// «Сегодня» (D-38): the right-hand diary page of a big window. What is waiting, what is under way and what has
/// happened since midnight. It only repeats what other screens know (ReadyItems, DayJournal), so it never has
/// state of its own, and a click on a waiting line goes where the rail badge would.
/// </summary>
public sealed class TodayPanel : Border
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly string[] Notes =
    [
        "План на день: погладить всех. Остальное подождёт.", "Ягоды сами себя не купят.", "Записал. Перечитал. Ничего не понял. Оставлю.",
        "Кто рано встаёт, тому покебол.", "Тихо. Подозрительно тихо.", "Питомник сам себя не проверит.", "Пахнет дождём и приключением."
    ];

    private readonly StackPanel _ready = new();
    private readonly StackPanel _running = new();
    private readonly StackPanel _events = new();
    private readonly TextBlock _date = new();
    private readonly TextBlock _note = new();
    private readonly TextBlock _readyTitle;
    private readonly TextBlock _runningTitle;
    private string _signature = "";

    public Action<ReadyItem>? OpenReady { get; set; }
    public Func<int, string>? SpeciesName { get; set; }

    public TodayPanel()
    {
        Width = 288;
        Margin = new Thickness(0, 14, 16, 14);
        Padding = new Thickness(16, 14, 16, 14);
        CornerRadius = new CornerRadius(14);
        BorderThickness = new Thickness(1);
        SetResourceReference(BackgroundProperty, "NdPanelBrush");
        SetResourceReference(BorderBrushProperty, "NdPanelLineBrush");
        System.Windows.Automation.AutomationProperties.SetName(this, "Сегодня");

        var title = new TextBlock { Text = "Сегодня", FontSize = 17, FontWeight = FontWeights.Bold };
        title.SetResourceReference(TextBlock.FontFamilyProperty, "NdDisplayFont");
        title.SetResourceReference(TextBlock.ForegroundProperty, "NdTextBrush");
        _date.FontSize = 22; _date.Margin = new Thickness(0, -2, 0, 6); _date.Opacity = 0.9;
        _date.SetResourceReference(TextBlock.FontFamilyProperty, "NdHandFont");
        _date.SetResourceReference(TextBlock.ForegroundProperty, "NdLavenderBrush");

        _readyTitle = Section("Ждёт тебя");
        _runningTitle = Section("В пути");

        // The trainer's line of the day on a scrap of paper (D-43).
        _note.TextWrapping = TextWrapping.Wrap; _note.TextAlignment = TextAlignment.Center; _note.FontSize = 20;
        _note.SetResourceReference(TextBlock.FontFamilyProperty, "NdHandFont");
        _note.SetResourceReference(TextBlock.ForegroundProperty, "NdPaperInkBrush");
        var tape = new Border { Width = 54, Height = 14, Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x99, 0xFF, 0xD0, 0xA0)),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -7, 0, 0),
            RenderTransform = new RotateTransform(-4), RenderTransformOrigin = new System.Windows.Point(0.5, 0.5), IsHitTestVisible = false };
        var paper = new Border { CornerRadius = new CornerRadius(3), Padding = new Thickness(12, 14, 12, 10), Child = _note };
        paper.SetResourceReference(BackgroundProperty, "NdPaperBrush");
        var scrap = new Grid { Margin = new Thickness(6, 18, 6, 4), RenderTransform = new RotateTransform(-1.5), RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
            VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false };
        scrap.Children.Add(paper); scrap.Children.Add(tape);

        var list = new StackPanel();
        list.Children.Add(title); list.Children.Add(_date);
        list.Children.Add(_readyTitle); list.Children.Add(_ready);
        list.Children.Add(_runningTitle); list.Children.Add(_running);
        list.Children.Add(Section("За день")); list.Children.Add(_events);
        var scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
        var layout = new DockPanel();
        DockPanel.SetDock(scrap, Dock.Bottom);
        layout.Children.Add(scrap); layout.Children.Add(scroll);
        Child = layout;
    }

    private static TextBlock Section(string text)
    {
        var block = new TextBlock { Text = text.ToUpper(Russian), FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 12, 0, 6) };
        block.SetResourceReference(TextBlock.ForegroundProperty, "NdMutedBrush");
        return block;
    }

    public void Refresh(SaveData? data, IReadOnlyList<ReadyItem> ready, DateTimeOffset now)
    {
        var local = now.ToLocalTime();
        IReadOnlyList<DayEvent> running = data is null ? Array.Empty<DayEvent>() : DayJournal.Running(data, now);
        IReadOnlyList<DayEvent> events = data is null ? Array.Empty<DayEvent>() : DayJournal.Today(data, now, id => SpeciesName?.Invoke(id) ?? $"№{id:D4}");
        var signature = local.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "|" + string.Join(";", ready.Select(r => r.Kind + r.Text))
            + "|" + string.Join(";", running.Select(r => r.Text + DayJournal.Remaining(r.AtUtc, now)))
            + "|" + string.Join(";", events.Select(e => e.Text + e.AtUtc.Ticks));
        if (signature == _signature) return;
        _signature = signature;

        _date.Text = local.ToString("d MMMM, dddd", Russian);
        _note.Text = Notes[local.DayOfYear % Notes.Length];

        _ready.Children.Clear();
        foreach (var item in ready) _ready.Children.Add(ReadyRow(item));
        if (ready.Count == 0) _ready.Children.Add(Quiet("Пока никто не ждёт."));

        _running.Children.Clear();
        foreach (var item in running) _running.Children.Add(Row(item.Text, DayJournal.Remaining(item.AtUtc, now), "NdLavenderBrush", "NdTextBrush"));
        _runningTitle.Visibility = _running.Visibility = running.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        _events.Children.Clear();
        foreach (var item in events.Take(12))
            _events.Children.Add(Row(item.Text, item.AtUtc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture),
                item.IsShiny ? "NdShinyBrush" : item.Kind == DayEventKind.Award ? "NdApricotBrush" : "NdSuccessBrush", "NdMutedBrush"));
        if (events.Count == 0) _events.Children.Add(Quiet("Страница пока чистая."));
    }

    private UIElement ReadyRow(ReadyItem item)
    {
        var text = new TextBlock { Text = item.Text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        var dot = new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(4.5), Margin = new Thickness(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center };
        dot.SetResourceReference(BackgroundProperty, "NdApricotBrush");
        dot.SetResourceReference(EffectProperty, "NdApricotGlow");
        var content = new DockPanel();
        DockPanel.SetDock(dot, Dock.Left);
        content.Children.Add(dot); content.Children.Add(text);
        var button = new Button { Content = content, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 6),
            Padding = new Thickness(11, 8, 11, 8), MinHeight = 38, FontSize = 13.5 };
        button.SetResourceReference(StyleProperty, "SecondaryButtonStyle");
        System.Windows.Automation.AutomationProperties.SetName(button, item.Text);
        button.Click += (_, _) => OpenReady?.Invoke(item);
        return button;
    }

    private static UIElement Row(string text, string trailing, string dotBrush, string trailingBrush)
    {
        var dot = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(3.5), Margin = new Thickness(2, 6, 9, 0), VerticalAlignment = VerticalAlignment.Top };
        dot.SetResourceReference(BackgroundProperty, dotBrush);
        var time = new TextBlock { Text = trailing, FontSize = 11.5, Margin = new Thickness(8, 2, 0, 0), VerticalAlignment = VerticalAlignment.Top };
        time.SetResourceReference(TextBlock.ForegroundProperty, trailingBrush);
        time.SetResourceReference(TextBlock.FontFamilyProperty, "NdDisplayFont");
        var body = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 13.5 };
        body.SetResourceReference(TextBlock.ForegroundProperty, "NdTextBrush");
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(dot, Dock.Left); DockPanel.SetDock(time, Dock.Right);
        row.Children.Add(dot); row.Children.Add(time); row.Children.Add(body);
        return row;
    }

    private static UIElement Quiet(string text)
    {
        var block = new TextBlock { Text = text, FontSize = 20, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 };
        block.SetResourceReference(TextBlock.FontFamilyProperty, "NdHandFont");
        block.SetResourceReference(TextBlock.ForegroundProperty, "NdLavenderBrush");
        return block;
    }
}
