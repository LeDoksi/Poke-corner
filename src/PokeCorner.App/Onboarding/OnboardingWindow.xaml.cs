using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// Aliased because UseWindowsForms (needed for TrayIconService's NotifyIcon) pulls
// System.Drawing into scope project-wide, making a bare "Brush" ambiguous.
using Brush = System.Windows.Media.Brush;

namespace PokeCorner.App.Onboarding;

public partial class OnboardingWindow : Window
{
    private static readonly (string Title, string Body)[] Steps =
    {
        ("Добро пожаловать в PokéCorner!", "Это твой личный покемон-питомец, который живёт в углу экрана и растёт вместе с тобой."),
        ("Рост от активности", "Питомец растёт от твоей активности за ПК — кликов мышкой, клавиатуры и геймпада. Чем больше ты играешь и работаешь, тем быстрее он эволюционирует."),
        ("Погладить и покормить", "Клик по питомцу на рабочем столе гладит его — бесплатно и всегда доступно. Кормление даёт больше очков роста, но требует лакомство — открой меню и загляни на вкладку «Коллекция»."),
        ("Поимка новых покемонов", "Когда шкала поимки заполнится, в трее появится пункт «Поймать!» — открой его и лови нового покемона на вкладке «Поймать»."),
        ("Коллекция и Pokédex", "Все пойманные покемоны хранятся в «Коллекции». Вкладка «Pokédex» показывает все виды, которых ты когда-либо поймал."),
        ("Магазин", "Зарабатывай монеты активностью и трать их на лакомства, бустеры или яйца редких покемонов — яйца вылупляются через час."),
        ("Меню приложения", "Двойной клик по питомцу на рабочем столе или иконка в трее открывают это меню в любой момент — все вкладки в одном окне.")
    };

    /// The dot for the current step stretches into a short pill in the accent color; the rest
    /// stay small and muted. Cheaper to read at a glance than "3/7" alone, which is why both
    /// are shown — the counter for precision, the dots for shape.
    private const double InactiveDotWidth = 6;
    private const double ActiveDotWidth = 18;

    private int _stepIndex;
    private readonly List<Border> _stepDots = new();

    public OnboardingWindow()
    {
        InitializeComponent();
        BuildStepDots();
        ShowStep(0);
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
        StepCounterText.Text = $"ШАГ {_stepIndex + 1} ИЗ {Steps.Length}";
        BackButton.IsEnabled = _stepIndex > 0;
        NextButton.Content = _stepIndex == Steps.Length - 1 ? "Готово" : "Далее";

        var activeBrush = (Brush)FindResource("AccentRedBrush");
        var inactiveBrush = (Brush)FindResource("CardBorderBrush");
        for (var i = 0; i < _stepDots.Count; i++)
        {
            var isCurrent = i == _stepIndex;
            _stepDots[i].Background = isCurrent ? activeBrush : inactiveBrush;
            _stepDots[i].Width = isCurrent ? ActiveDotWidth : InactiveDotWidth;
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => ShowStep(_stepIndex - 1);

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_stepIndex == Steps.Length - 1)
        {
            Close();
            return;
        }
        ShowStep(_stepIndex + 1);
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e) => Close();
}
