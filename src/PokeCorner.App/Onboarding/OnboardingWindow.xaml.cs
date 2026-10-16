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
        ("Питомец на рабочем столе", "Нажми на питомца, чтобы погладить его. Двойной клик открывает меню. Кольца прогресса можно включить в настройках. Подарок к крупному обновлению — два закрытых покебола — уже сохранён отдельно от обучения."),
        ("Уголок", "Здесь видны активность, уход, закрытые покеболы и заработанные лакомства. Поимка раскрывает вид только после броска; делать это сейчас не нужно. В комнате можно бесплатно выставить три открытых трофея."),
        ("Покедекс", "Смотри экземпляры и открытые виды, ищи и фильтруй коллекцию. У питомца можно выбрать кличку и избранное, покормить его и узнать условия эволюции. Здесь только пример: ничего менять не нужно."),
        ("Экспедиции", "Одновременно проходит одна экспедиция. Выбери команду, бонусы и сложность; путь длится 30 минут, 1,5 часа, 3 часа или 6 часов. Основная награда гарантирована, встреча бывает бонусом. Начинать поход сейчас не нужно."),
        ("Питомник", "Совместимая пара может дать несколько вариантов потомства. Перед запуском видны цена, случайный исход и предмет выбора. Яйца в магазине больше не продаются; во время обучения ресурсы не тратятся."),
        ("Предметы и открытия", "Лакомства и шесть видов ягод — разные запасы. Рецепты эволюции покажут условия и камень выбора ветки. В магазине есть расходники, а в альбоме — награды и три места на полке. Покупать и эволюционировать для обучения не нужно.")
    };

    /// The dot for the current step stretches into a short pill in the accent color; the rest
    /// stay small and muted. Cheaper to read at a glance than "3/7" alone, which is why both
    /// are shown — the counter for precision, the dots for shape.
    private const double InactiveDotWidth = 6;
    private const double ActiveDotWidth = 18;

    private int _stepIndex;
    private bool _closed;
    private readonly Func<int, string, Task>? _commitProgress;
    private readonly List<Border> _stepDots = new();

    public OnboardingWindow(int startStep = 1, Func<int, string, Task>? commitProgress = null)
    {
        InitializeComponent();
        Closed += (_, _) => _closed = true;
        _commitProgress = commitProgress;
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
            if (!_closed) System.Windows.MessageBox.Show(this, ex.Message + "\nШаг не сохранён. Повторите действие или закройте окно.", "PokéCorner — ошибка сохранения");
        }
        finally
        {
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
