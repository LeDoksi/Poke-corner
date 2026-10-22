using System.Windows;
using System.Windows.Threading;
using PokeCorner.Core.Persistence;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace PokeCorner.App.Popup;

public enum MenuSection { Corner, Pokedex, Shop, Expeditions, Nursery, Settings, Catch, Achievements }

public partial class AppMenuWindow : Window
{
    public event Action<MenuSection>? SectionViewed;
    private readonly CatchView _catchView;
    private MenuSection _lastSection = MenuSection.Corner;

    /// Whether the badge's breathing pulse is currently running, so SetCatchReady doesn't
    /// restart the animation (and its phase) on every catch-ready refresh tick.
    private bool _catchBadgePulsing;
    private readonly Func<SaveData>? _getSnapshot;
    private readonly DispatcherTimer _snapshotTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private TaskCompletionSource<bool>? _confirmation;
    private IInputElement? _previousFocus;
    private bool _closed;
    private TaskCompletionSource? _hosted;
    private bool _hostedFromSettings;


    public AppMenuWindow(CollectionView collectionView, SettingsView settingsView, ShopView shopView, CatchView catchView,
        ExpeditionView? expeditionView = null, NurseryView? nurseryView = null,
        RoomView? roomView = null, AchievementsView? achievementsView = null, Func<SaveData>? getSnapshot = null)
    {
        InitializeComponent();
        MenuTabControl.SelectionChanged += (_, e) => { if (ReferenceEquals(e.OriginalSource, MenuTabControl)) UpdateRailSketch(); };
        ShopTab.SizeChanged += (_, _) => UpdateRailSketch();
        _getSnapshot = getSnapshot;
        var workArea = SystemParameters.WorkArea;
        MinWidth = Math.Min(MinWidth, workArea.Width);
        MinHeight = Math.Min(MinHeight, workArea.Height);
        Width = Math.Min(Width, workArea.Width);
        Height = Math.Min(Height, workArea.Height);
        ConfirmationHost.UseNightDiaryTheme();
        ConfirmationHost.Completed += CompleteConfirmation;
        _snapshotTimer.Tick += SnapshotTimer_Tick;
        Loaded += (_, _) => { RefreshSnapshot(); UpdateCatchBadgeAnimation(); _snapshotTimer.Start(); };
        Unloaded += (_, _) => { StopPresentation(); CompleteConfirmation(false); CompleteHostedContent(); };
        Closed += (_, _) =>
        {
            _closed = true;
            StopPresentation();
            CompleteConfirmation(false);
            CompleteHostedContent();
        };
        RefreshSnapshot();
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
        if (roomView is not null) { roomView.Foreground = contentInk; CornerHomeTab.Content = roomView; }
        if (achievementsView is not null) { achievementsView.Foreground = contentInk; AlbumTab.Content = achievementsView; }
        if (expeditionView is not null)
        {
            expeditionView.Foreground = (System.Windows.Media.Brush)FindResource("NdTextBrush");
            ExpeditionsTab.Content = expeditionView;
        }
        if (nurseryView is not null)
        {
            nurseryView.Foreground = contentInk;
            NurseryTab.Content = nurseryView;
        }
        _catchView = catchView;
        MenuTabControl.SelectionChanged += (_, _) =>
        {
            RefreshSnapshot();
            if (IsLoaded) SectionViewed?.Invoke(SelectedSection());
        };
    }

    public Task ShowHostedAsync(FrameworkElement content)
    {
        Dispatcher.VerifyAccess();
        if (_closed) return Task.CompletedTask;
        CompleteHostedContent();
        _hostedFromSettings = SettingsContent.Visibility == Visibility.Visible;
        _hosted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MenuTabControl.Visibility = SettingsContent.Visibility = Visibility.Collapsed;
        HostedContent.Content = content;
        HostedContent.Visibility = Visibility.Visible;
        SettingsButton.Content = "К разделам";
        SettingsButton.ToolTip = "Назад";
        System.Windows.Automation.AutomationProperties.SetName(SettingsButton, "Закрыть открытый экран");
        return _hosted.Task;
    }

    public void CompleteHostedContent(FrameworkElement? expectedContent = null)
    {
        if (expectedContent is not null && !ReferenceEquals(HostedContent.Content, expectedContent)) return;
        var pending = _hosted;
        if (pending is null) return;
        _hosted = null;
        CompleteConfirmation(false);
        HostedContent.Visibility = Visibility.Collapsed;
        HostedContent.Content = null;
        SettingsContent.Visibility = _hostedFromSettings ? Visibility.Visible : Visibility.Collapsed;
        MenuTabControl.Visibility = _hostedFromSettings ? Visibility.Collapsed : Visibility.Visible;
        SettingsButton.Content = _hostedFromSettings ? "К разделам" : "Настройки";
        SettingsButton.ToolTip = _hostedFromSettings ? "К разделам" : "Настройки";
        System.Windows.Automation.AutomationProperties.SetName(SettingsButton, _hostedFromSettings ? "Вернуться к разделам" : "Открыть настройки");
        pending.TrySetResult();
    }

    public Task<bool> ConfirmAsync(string title, string body, string confirmCaption) => PresentConfirmation(title, body, confirmCaption, false);

    public Task<bool> ConfirmDestructiveAsync(string title, string body, string confirmCaption) => PresentConfirmation(title, body, confirmCaption, true);

    private Task<bool> PresentConfirmation(string title, string body, string confirmCaption, bool destructive)
    {
        Dispatcher.VerifyAccess();
        if (_closed) return Task.FromResult(false);
        if (_confirmation is not null) throw new InvalidOperationException("A confirmation is already active.");
        _previousFocus = Keyboard.FocusedElement;
        _confirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = _confirmation.Task;
        ShellContent.IsEnabled = false;
        ConfirmationHost.Present(title, body, confirmCaption, destructive);
        return task;
    }

    private void CompleteConfirmation(bool result)
    {
        var pending = _confirmation;
        if (pending is null) return;
        _confirmation = null;
        ConfirmationHost.Visibility = Visibility.Collapsed;
        ShellContent.IsEnabled = true;
        if (!_closed && _previousFocus is not null) Keyboard.Focus(_previousFocus);
        _previousFocus = null;
        pending.TrySetResult(result);
    }

    private void EncountersButton_Click(object sender, RoutedEventArgs e) => Navigate(MenuSection.Catch);

    private void SnapshotTimer_Tick(object? sender, EventArgs e) => RefreshSnapshot();

    private void RefreshSnapshot()
    {
        var snapshot = _getSnapshot?.Invoke();
        WalletText.Text = snapshot?.ShopCoins.ToString(System.Globalization.CultureInfo.CurrentCulture) ?? "—";
        var trip = snapshot?.Expedition;
        ExpeditionReadyBadge.Visibility = trip is not null && (trip.Status == "ready" || trip.Status == "running" && DateTimeOffset.UtcNow >= trip.ReadyAtUtc)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void StopPresentation()
    {
        _snapshotTimer.Stop();
        CatchBadge.BeginAnimation(OpacityProperty, null);
        CatchBadge.Opacity = 1;
        _catchBadgePulsing = false;
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

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = ActualWidth < 920;
        Resources["NdRailWidth"] = new GridLength(compact ? 112 : 136);
        TitleBar.Height = compact ? 40 : 44;
        BrandSubtitle.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        SettingsButton.Width = compact ? 96 : 120;
        SettingsContent.Margin = HostedContent.Margin = new Thickness(compact ? 112 : 136, 0, 0, 0);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) => ToggleMaximized();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        if (_confirmation is not null)
        {
            CompleteConfirmation(false);
            e.Handled = true;
            return;
        }
        if (_hosted is not null) CompleteHostedContent();
        else if (SettingsContent.Visibility == Visibility.Visible) Navigate(_lastSection);
        else Close();
        e.Handled = true;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_confirmation is not null) return;
        if (_hosted is not null) { CompleteHostedContent(); return; }
        Navigate(SettingsContent.Visibility == Visibility.Visible ? _lastSection : MenuSection.Settings);
    }

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
        if (_confirmation is not null) return;
        CompleteHostedContent();
        RefreshSnapshot();
        if (section == MenuSection.Settings)
        {
            RailSketch.Visibility = Visibility.Collapsed;
            if (SettingsContent.Visibility != Visibility.Visible) _lastSection = SelectedSection();
            MenuTabControl.Visibility = Visibility.Collapsed;
            SettingsContent.Visibility = Visibility.Visible;
            SettingsButton.Content = "К разделам";
            SettingsButton.ToolTip = "К разделам";
            System.Windows.Automation.AutomationProperties.SetName(SettingsButton, "Вернуться к разделам");
            return;
        }

        SettingsContent.Visibility = Visibility.Collapsed;
        MenuTabControl.Visibility = Visibility.Visible;
        _lastSection = section;
        SettingsButton.Content = "Настройки";
        SettingsButton.ToolTip = "Настройки";
        System.Windows.Automation.AutomationProperties.SetName(SettingsButton, "Открыть настройки");
        if (section is MenuSection.Catch or MenuSection.Achievements)
        {
            MenuTabControl.SelectedItem = CornerTab;
            CornerTabs.SelectedItem = section == MenuSection.Catch ? CatchTab : AlbumTab;
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
        UpdateRailSketch();
    }

    // Only sections with a drawn rail sketch show one; the rest keep the rail clean until their pass.
    private void UpdateRailSketch()
    {
        var tab = MenuTabControl.SelectedItem;
        var pokedex = tab == CollectionTab;
        RailSketch.Visibility = MenuTabControl.Visibility == Visibility.Visible && (tab == ExpeditionsTab || pokedex)
            ? Visibility.Visible : Visibility.Collapsed;
        RailCompass.Visibility = pokedex ? Visibility.Collapsed : Visibility.Visible;
        RailMagnifier.Visibility = pokedex ? Visibility.Visible : Visibility.Collapsed;
        RailSketchNote.Text = pokedex ? "У каждого своя страница." : "Компас старый, но не врёт.";
        // Sits right under the last section (Магазин), where the rail otherwise goes empty.
        if (ShopTab.ActualHeight > 0 && RailSketch.Parent is System.Windows.Media.Visual host)
            RailSketch.Margin = new Thickness(8, ShopTab.TransformToAncestor(host).Transform(new System.Windows.Point(0, ShopTab.ActualHeight)).Y + 16, 0, 0);
    }

    private MenuSection SelectedSection() => MenuTabControl.SelectedItem switch
    {
        var tab when tab == CollectionTab => MenuSection.Pokedex,
        var tab when tab == ShopTab => MenuSection.Shop,
        var tab when tab == ExpeditionsTab => MenuSection.Expeditions,
        var tab when tab == NurseryTab => MenuSection.Nursery,
        _ => CornerTabs.SelectedItem == CatchTab ? MenuSection.Catch :
            CornerTabs.SelectedItem == AlbumTab ? MenuSection.Achievements : MenuSection.Corner
    };

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
        EncountersButton.Content = $"Встречи · {Math.Max(0, count)}";
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
        var pulseEnabled = IsLoaded && CatchBadge.Visibility == Visibility.Visible && SystemParameters.ClientAreaAnimation;
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
