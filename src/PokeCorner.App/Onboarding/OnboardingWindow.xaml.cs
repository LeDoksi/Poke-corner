using System.Windows;

namespace PokeCorner.App.Onboarding;

public partial class OnboardingWindow : Window
{
    private static readonly (string Title, string Body)[] Steps =
    {
        ("Добро пожаловать в PokéCorner!", "Это твой личный покемон-питомец, который живёт в углу экрана и растёт вместе с тобой."),
        ("Рост от активности", "Питомец растёт от твоей активности за ПК — кликов мышкой и клавиатуры. Чем больше ты играешь и работаешь, тем быстрее он эволюционирует."),
        ("Погладить и покормить", "Клик по питомцу на рабочем столе гладит его — бесплатно и всегда доступно. Кормление даёт больше очков роста, но требует лакомство — открой меню и загляни на вкладку «Коллекция»."),
        ("Поимка новых покемонов", "Когда шкала поимки заполнится, в трее появится пункт «Поймать!» — открой его и лови нового покемона на вкладке «Поймать»."),
        ("Коллекция и Pokédex", "Все пойманные покемоны хранятся в «Коллекции». Вкладка «Pokédex» показывает все виды, которых ты когда-либо поймал."),
        ("Магазин", "Зарабатывай монеты активностью и трать их на лакомства, бустеры или яйца редких покемонов — яйца вылупляются через час."),
        ("Меню приложения", "Двойной клик по питомцу на рабочем столе или иконка в трее открывают это меню в любой момент — все вкладки в одном окне.")
    };

    private int _stepIndex;

    public OnboardingWindow()
    {
        InitializeComponent();
        ShowStep(0);
    }

    private void ShowStep(int index)
    {
        _stepIndex = Math.Clamp(index, 0, Steps.Length - 1);
        TitleText.Text = Steps[_stepIndex].Title;
        BodyText.Text = Steps[_stepIndex].Body;
        BackButton.IsEnabled = _stepIndex > 0;
        NextButton.Content = _stepIndex == Steps.Length - 1 ? "Готово" : "Далее";
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
