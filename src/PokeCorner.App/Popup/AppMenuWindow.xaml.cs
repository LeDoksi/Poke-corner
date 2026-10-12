using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace PokeCorner.App.Popup;

public enum MenuSection { Corner, Pokedex, Shop, Expeditions, Nursery, Settings, Catch }

public partial class AppMenuWindow : Window
{
    private readonly CatchView _catchView;
    private MenuSection _lastSection = MenuSection.Corner;

    /// Whether the badge's breathing pulse is currently running, so SetCatchReady doesn't
    /// restart the animation (and its phase) on every catch-ready refresh tick.
    private bool _catchBadgePulsing;

    public AppMenuWindow(CollectionView collectionView, SettingsView settingsView, ShopView shopView, CatchView catchView)
    {
        InitializeComponent();
        SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
        Closed += (_, _) => SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;
        var contentInk = (System.Windows.Media.Brush)FindResource("PrimaryTextBrush");
        collectionView.Foreground = contentInk;
        settingsView.Foreground = contentInk;
        shopView.Foreground = contentInk;
        catchView.Foreground = contentInk;
        CollectionTab.Content = collectionView;
        SettingsContent.Content = settingsView;
        ShopTab.Content = shopView;
        CatchTab.Content = catchView;
        _catchView = catchView;
    }

    /// Drag and double-click-to-maximize for the custom title bar: WindowChrome's CaptionHeight
    /// is 0, so the OS does none of this.
    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximized();
            return;
        }

        DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) => ToggleMaximized();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        if (SettingsContent.Visibility == Visibility.Visible) Navigate(_lastSection);
        else Close();
        e.Handled = true;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) =>
        Navigate(SettingsContent.Visibility == Visibility.Visible ? _lastSection : MenuSection.Settings);

    // Segoe MDL2 Assets glyphs, the same font (and codepoints) the real Windows title
    // bar draws its chrome buttons from.
    private const string MaximizeGlyph = "\uE922";
    private const string RestoreGlyph = "\uE923";

    private void ToggleMaximized()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        MaximizeButton.Content = WindowState == WindowState.Maximized ? RestoreGlyph : MaximizeGlyph;
        MaximizeButton.ToolTip = WindowState == WindowState.Maximized ? "Восстановить" : "Развернуть";
    }

    public void Navigate(MenuSection section)
    {
        if (section == MenuSection.Settings)
        {
            MenuTabControl.Visibility = Visibility.Collapsed;
            SettingsContent.Visibility = Visibility.Visible;
            SettingsButton.Content = "К разделам";
            System.Windows.Automation.AutomationProperties.SetName(SettingsButton, "Вернуться к разделам");
            return;
        }

        SettingsContent.Visibility = Visibility.Collapsed;
        MenuTabControl.Visibility = Visibility.Visible;
        _lastSection = section;
        SettingsButton.Content = "Настройки";
        System.Windows.Automation.AutomationProperties.SetName(SettingsButton, "Открыть настройки");
        if (section == MenuSection.Catch)
        {
            MenuTabControl.SelectedItem = CornerTab;
            CornerTabs.SelectedItem = CatchTab;
        }
        else
        {
            MenuTabControl.SelectedItem = section switch
            {
                MenuSection.Corner => CornerTab,
                MenuSection.Pokedex => CollectionTab,
                MenuSection.Shop => ShopTab,
                MenuSection.Expeditions => ExpeditionsTab,
                MenuSection.Nursery => NurseryTab,
                _ => CornerTab
            };
            if (section == MenuSection.Corner) CornerTabs.SelectedItem = CornerHomeTab;
        }
    }

    // Compatibility for callers compiled against the previous four-tab shell.
    public void SelectTab(int index)
    {
        if (index is < 0 or > 3) return;
        Navigate(index switch
        {
            0 => MenuSection.Pokedex,
            1 => MenuSection.Shop,
            2 => MenuSection.Catch,
            _ => MenuSection.Settings
        });
    }

    public async void ShowCatchReveal(CatchReveal reveal)
    {
        Navigate(MenuSection.Catch);
        await _catchView.PlayReveal(reveal);
    }

    /// Drives the Catch tab's badge + pulse from the same catch-ready-count signal the tray
    /// icon reacts to (App calls this alongside _trayIconService.SetCatchReady). Toggles
    /// on/off cleanly as the count crosses 0 — a queued catch shows the dot and starts the
    /// breathing pulse, claiming the last one hides both.
    public void SetCatchReady(int count)
    {
        var ready = count > 0;
        CatchBadge.Visibility = ready ? Visibility.Visible : Visibility.Collapsed;
        UpdateCatchBadgeAnimation();
    }

    private void SystemParameters_StaticPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SystemParameters.ClientAreaAnimation)) return;
        if (Dispatcher.CheckAccess()) UpdateCatchBadgeAnimation();
        else Dispatcher.BeginInvoke(UpdateCatchBadgeAnimation);
    }

    private void UpdateCatchBadgeAnimation()
    {
        var pulseEnabled = CatchBadge.Visibility == Visibility.Visible && SystemParameters.ClientAreaAnimation;
        if (pulseEnabled == _catchBadgePulsing) return;
        _catchBadgePulsing = pulseEnabled;
        if (!pulseEnabled)
        {
            CatchBadge.BeginAnimation(OpacityProperty, null);
            CatchBadge.Opacity = 1.0;
            return;
        }

        // Slow breathing pulse, not a strobe — subtle, plays only while the badge is visible.
        var pulse = new DoubleAnimation(1.0, 0.35, TimeSpan.FromMilliseconds(900))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase()
        };
        CatchBadge.BeginAnimation(OpacityProperty, pulse);
    }
}
