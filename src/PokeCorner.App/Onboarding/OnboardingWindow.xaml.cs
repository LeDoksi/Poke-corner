using System.Windows;
using System.Windows.Controls;

// Aliased because UseWindowsForms (needed for TrayIconService's NotifyIcon) pulls
// System.Drawing into scope project-wide, making a bare "Brush" ambiguous.
using Brush = System.Windows.Media.Brush;

namespace PokeCorner.App.Onboarding;

public partial class OnboardingWindow : Window
{
    private static readonly (string Title, string Body)[] Steps =
    {
        ("Добро пожаловать в уголок", "Спутник рядом, пока ты занимаешься своими делами. Нажми, чтобы погладить его; двойной клик открывает уголок."),
        ("Новые друзья ждут знакомства", "За обычную активность появляются покеболы. Открывай их, когда удобно."),
        ("Маленькие приключения", "Собери команду. Она привезёт ягоды и другие находки. Занятые питомцы вернутся после похода."),
        ("В гнезде — новое открытие", "Выбери совместимую пару и познакомься с малышом. Перед началом видны цена и варианты потомства."),
        ("Забота помогает расти", "Гладь и корми питомца. Со временем он будет готов к эволюции. Точные условия — в его карточке."),
        ("Подарок для первых открытий", "Подарочные покеболы хранятся в уголке. Знакомство можно повторить: подарок выдаётся один раз.")
    };

    /// The dot for the current step stretches into a short pill in the accent color; the rest
    /// stay small and muted. Cheaper to read at a glance than "3/7" alone, which is why both
    /// are shown — the counter for precision, the dots for shape.
    private const double InactiveDotWidth = 6;
    private const double ActiveDotWidth = 18;

    private int _stepIndex;
    private bool _closed;
    private bool _pending;
    private readonly string _dataRoot;
    private readonly Func<int>? _giftQueueCount;
    private readonly Func<int, string, Task>? _commitProgress;
    private readonly List<Border> _stepDots = new();

    public OnboardingWindow(int startStep = 1, Func<int, string, Task>? commitProgress = null, string? dataRoot = null, Func<int>? giftQueueCount = null)
    {
        InitializeComponent();
        Closed += (_, _) => _closed = true;
        _commitProgress = commitProgress;
        _dataRoot = dataRoot ?? System.IO.Path.Combine(AppContext.BaseDirectory, "Data");
        _giftQueueCount = giftQueueCount;
        BuildStepDots();
        ShowStep(startStep - 1);
    }

    /// One dot per entry in Steps, so the indicator can never drift out of sync with the
    /// actual number of onboarding steps.
    private void BuildStepDots()
    {
        foreach (var _ in Steps)
        {
            var dot = new Border
            {
                Height = 6,
                Width = InactiveDotWidth,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _stepDots.Add(dot);
            StepDotsPanel.Children.Add(dot);
        }
    }

    private void ShowStep(int index)
    {
        _stepIndex = Math.Clamp(index, 0, Steps.Length - 1);
        TitleText.Text = Steps[_stepIndex].Title;
        BodyText.Text = Steps[_stepIndex].Body;
        if (_stepIndex == 5 && _giftQueueCount is not null)
        {
            var count = _giftQueueCount();
            BodyText.Text = count > 0 ? $"Подарочные покеболы в очереди: {count}. Открой их в уголке, когда удобно. Повторное знакомство не выдаёт новый подарок."
                : "Все подарочные покеболы уже открыты. Повторное знакомство не выдаёт новый подарок.";
        }
        ShowIllustration();
        StepCounterText.Text = $"ШАГ {_stepIndex + 1} ИЗ {Steps.Length}";
        BackButton.IsEnabled = _stepIndex > 0;
        NextButton.Content = _stepIndex == Steps.Length - 1 ? "В мой уголок" : "Дальше";

        var activeBrush = (Brush)FindResource("UiForestBrush");
        var inactiveBrush = (Brush)FindResource("CardBorderBrush");
        for (var i = 0; i < _stepDots.Count; i++)
        {
            var isCurrent = i == _stepIndex;
            _stepDots[i].Background = isCurrent ? activeBrush : inactiveBrush;
            _stepDots[i].Width = isCurrent ? ActiveDotWidth : InactiveDotWidth;
        }
    }

    public FrameworkElement TakeHostedContent()
    {
        var content = (FrameworkElement)Content;
        Content = null;
        content.Unloaded += (_, _) => _closed = true;
        return content;
    }

    private void ShowIllustration()
    {
        Illustration.Children.Clear();
        System.Windows.Controls.Image Add(System.Windows.Media.ImageSource source, double width)
        {
            var image = new System.Windows.Controls.Image { Source = source, Width = width, Stretch = System.Windows.Media.Stretch.Uniform, Margin = new Thickness(8) };
            Illustration.Children.Add(image);
            return image;
        }
        void Packaged(string relative, double width)
        {
            var path = System.IO.Path.Combine(_dataRoot, relative);
            if (System.IO.File.Exists(path)) Add(PokeCorner.App.Imaging.SpriteImages.Decode(System.IO.File.ReadAllBytes(path)), width);
        }
        if (_stepIndex == 0)
            Packaged("art-production/last-attempt/final-assets/assets/room-master.png", 580);
        else if (_stepIndex == 2)
            Packaged("expeditions/grove.png", 580);
        else if (_stepIndex == 3)
            Add((System.Windows.Media.ImageSource)FindResource("UiNurseryWaitingImage"), 580);
        else if (_stepIndex == 4)
        {
            Packaged("catalog-package/assets/normal/1.gif", 100);
            for (var id = 2; id <= 3; id++)
            {
                var hidden = new StackPanel { Margin = new Thickness(20), VerticalAlignment = VerticalAlignment.Center };
                hidden.Children.Add(new TextBlock { Text = "→", FontSize = 32, HorizontalAlignment = System.Windows.HorizontalAlignment.Center });
                var path = System.IO.Path.Combine(_dataRoot, "catalog-package", "assets", "normal", id + ".gif");
                if (System.IO.File.Exists(path))
                    hidden.Children.Add(new Border { Width = 100, Height = 100, Background = (Brush)FindResource("UiSecondaryInkBrush"),
                        OpacityMask = new System.Windows.Media.ImageBrush(PokeCorner.App.Imaging.SpriteImages.Decode(System.IO.File.ReadAllBytes(path))) { Stretch = System.Windows.Media.Stretch.Uniform } });
                else hidden.Children.Add(new TextBlock { Text = "?", FontSize = 32, HorizontalAlignment = System.Windows.HorizontalAlignment.Center });
                Illustration.Children.Add(hidden);
            }
        }
        else
        {
            var count = _stepIndex == 5 ? Math.Clamp(_giftQueueCount?.Invoke() ?? 2, 0, 2) : 1;
            if (count == 0) Add((System.Windows.Media.ImageSource)FindResource("UiBallOpenImage"), 130);
            for (var i = 0; i < count; i++) Add((System.Windows.Media.ImageSource)FindResource("UiBallClosedImage"), 130);
        }
    }

    private async void BackButton_Click(object sender, RoutedEventArgs e) => await MoveAsync(_stepIndex - 1, "in-progress");

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_stepIndex == Steps.Length - 1)
        {
            await MoveAsync(_stepIndex, "completed");
            return;
        }
        await MoveAsync(_stepIndex + 1, "in-progress");
    }

    private async void SkipButton_Click(object sender, RoutedEventArgs e) => await MoveAsync(_stepIndex, "skipped");

    private async Task MoveAsync(int index, string status)
    {
        if (_pending || _closed) return;
        _pending = true;
        ErrorText.Text = "";
        SkipButton.IsEnabled = BackButton.IsEnabled = NextButton.IsEnabled = false;
        try
        {
            if (_commitProgress is not null) await _commitProgress(index + 1, status);
            if (_closed) return;
            if (status is "completed" or "skipped") Close();
            else ShowStep(index);
        }
        catch (Exception ex)
        {
            if (!_closed) ErrorText.Text = ex.Message + " Шаг не сохранён. Повторите действие или закройте знакомство.";
        }
        finally
        {
            _pending = false;
            if (!_closed)
            {
                SkipButton.IsEnabled = NextButton.IsEnabled = true;
                BackButton.IsEnabled = _stepIndex > 0;
            }
        }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Escape) return;
        Close();
        e.Handled = true;
    }
}
