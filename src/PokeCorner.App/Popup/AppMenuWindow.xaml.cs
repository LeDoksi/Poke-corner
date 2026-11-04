using System.Windows;
using System.Windows.Threading;
using PokeCorner.Core.Domain;
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
    private readonly Func<IReadOnlyList<ReadyItem>>? _readyItems;
    private MenuSection? _railSection;
    private readonly Dictionary<MenuSection, int> _railVisits = [];
    private readonly DispatcherTimer _snapshotTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private TaskCompletionSource<bool>? _confirmation;
    private IInputElement? _previousFocus;
    private bool _closed;
    private TaskCompletionSource? _hosted;
    private bool _hostedFromSettings;


    public AppMenuWindow(CollectionView collectionView, SettingsView settingsView, ShopView shopView, CatchView catchView,
        ExpeditionView? expeditionView = null, NurseryView? nurseryView = null,
        RoomView? roomView = null, AchievementsView? achievementsView = null, Func<SaveData>? getSnapshot = null,
        Func<IReadOnlyList<ReadyItem>>? readyItems = null)
    {
        _readyItems = readyItems;
        InitializeComponent();
        MenuTabControl.SelectionChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.OriginalSource, MenuTabControl)) return;
            // Arrow keys on the rail while Settings is open leave Settings for the newly picked section.
            if (SettingsContent.Visibility == Visibility.Visible && _hosted is null) Navigate(SelectedSection());
            UpdateRailSketch();
        };
        MenuTabControl.PreviewMouseLeftButtonDown += RailTab_PreviewMouseLeftButtonDown;
        ShopTab.SizeChanged += (_, _) => UpdateRailSketch();
        SizeChanged += (_, _) => UpdateRailSketch(); // window height decides whether the note still fits
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
        // A chrome-less maximized window overhangs its monitor by the resize border; FitToMonitor pulls the
        // content in from every overhanging edge. No WM_GETMINMAXINFO hook: Windows rescales ptMaxSize by the
        // primary monitor, which pushed the window off a 150% laptop screen in 1.1.14.
        SourceInitialized += (_, _) =>
        {
            FitToMonitor();
        };
        // SystemParameters.WorkArea above is the PRIMARY monitor's; a laptop at 150% next to a bigger
        // primary screen got a window larger than itself (Georgy 04.11). Re-fit on the real monitor.
        Loaded += (_, _) => FitToMonitor();
        StateChanged += (_, _) => FitToMonitor();
        DpiChanged += (_, _) => FitToMonitor();
        LocationChanged += (_, _) => { if (!_fitPending) { _fitPending = true; Dispatcher.BeginInvoke(() => { _fitPending = false; FitToMonitor(); }, System.Windows.Threading.DispatcherPriority.Background); } };
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
        catchView.BackRequested += () => Navigate(MenuSection.Corner);
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
        // Tour and save history carry their own exit («Позже», «Отмена»); a second shell «Назад» beside them was noise.
        SettingsButton.Visibility = Visibility.Collapsed;
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
        MenuTabControl.Visibility = Visibility.Visible;
        CoverSection(_hostedFromSettings);
        SettingsButton.Visibility = Visibility.Visible;
        UpdateRailSketch();
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
        // D-31: the same "ready" list as the corner strip and the badge over the desktop pet.
        var ready = _readyItems?.Invoke() ?? [];
        PokedexReadyBadge.Visibility = ready.Any(x => x.Kind == ReadyKind.Evolution) ? Visibility.Visible : Visibility.Collapsed;
        NurseryReadyBadge.Visibility = ready.Any(x => x.Kind == ReadyKind.Nursery) ? Visibility.Visible : Visibility.Collapsed;
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
    // Esc closes the topmost layer. Preview (tunnels window-first) only for the shell's own confirmation,
    // which sits above everything. The rest waits for the bubbling KeyDown, so an open pet page, picker,
    // local confirmation or ComboBox drop-down handles Esc first and only an unclaimed Esc leaves the section.
    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _confirmation is null) return;
        CompleteConfirmation(false);
        e.Handled = true;
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _confirmation is not null) return;
        if (_hosted is not null) CompleteHostedContent();
        else if (SettingsContent.Visibility == Visibility.Visible) Navigate(_lastSection);
        else Close();
        e.Handled = true;
    }

    private UIElement? _coveredSection;

    // Hidden, not just painted over: a section behind Settings must not take Tab focus or clicks.
    private void CoverSection(bool covered)
    {
        MenuTabControl.Tag = covered ? "settings" : null;
        if (_coveredSection is not null) { _coveredSection.Visibility = Visibility.Visible; _coveredSection = null; }
        if (covered && MenuTabControl.SelectedItem is System.Windows.Controls.TabItem { Content: UIElement content })
        {
            _coveredSection = content;
            content.Visibility = Visibility.Hidden;
        }
    }

    // A click on the rail tab that is already highlighted raises no SelectionChanged. It still has to work:
    // from Settings it opens that section, and «Уголок» returns from its inner pages (Поимка, Достижения).
    private void RailTab_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_confirmation is not null || _hosted is not null) return;
        for (var node = e.OriginalSource as DependencyObject; node is not null; node = System.Windows.Media.VisualTreeHelper.GetParent(node))
        {
            if (node is not System.Windows.Controls.TabItem item || System.Windows.Controls.ItemsControl.ItemsControlFromItemContainer(item) != MenuTabControl) continue;
            if (SettingsContent.Visibility != Visibility.Visible)
            {
                if (item == CornerTab && MenuTabControl.SelectedItem == CornerTab && CornerTabs.SelectedItem != CornerHomeTab)
                {
                    Navigate(MenuSection.Corner);
                    e.Handled = true;
                }
                return;
            }
            MenuTabControl.SelectedItem = item;
            if (SettingsContent.Visibility == Visibility.Visible) Navigate(SelectedSection());
            e.Handled = true;
            return;
        }
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

    private const uint MonitorDefaultToNearest = 2;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    private bool _fitPending;
    private bool _fitting;

    /// Keeps the whole window on the monitor it is on: a normal window no bigger than that monitor's
    /// work area, and a maximized one with its content pulled in from any edge that overhangs it
    /// (frame overhang or taskbar), so the title-bar buttons and the bottom buttons stay visible.
    private void FitToMonitor()
    {
        if (_fitting || Content is not FrameworkElement root) return;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info) || !GetWindowRect(hwnd, out var rect)) return;
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        double sx = dpi.DpiScaleX, sy = dpi.DpiScaleY;
        var work = info.Work;
        _fitting = true;
        try
        {
            if (WindowState == WindowState.Maximized)
            {
                root.Margin = new Thickness(Math.Max(0, work.Left - rect.Left) / sx, Math.Max(0, work.Top - rect.Top) / sy,
                    Math.Max(0, rect.Right - work.Right) / sx, Math.Max(0, rect.Bottom - work.Bottom) / sy);
                return;
            }
            root.Margin = new Thickness(0);
            if (WindowState != WindowState.Normal) return;
            double workW = (work.Right - work.Left) / sx, workH = (work.Bottom - work.Top) / sy;
            MinWidth = Math.Min(MinWidth, workW);
            MinHeight = Math.Min(MinHeight, workH);
            if (Width > workW) Width = workW;
            if (Height > workH) Height = workH;
            // Window.Left/Top are DIPs of the same (system) DPI as these physical numbers.
            double left = work.Left / sx, top = work.Top / sy, right = work.Right / sx, bottom = work.Bottom / sy;
            if (Left + Width > right) Left = Math.Max(left, right - Width);
            if (Top + Height > bottom) Top = Math.Max(top, bottom - Height);
            if (Left < left) Left = left;
            if (Top < top) Top = top;
        }
        finally { _fitting = false; }
    }

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
            // The rail stays: Settings replaces only the content area, so any section is one click away.
            SettingsContent.Visibility = Visibility.Visible;
            CoverSection(true);
            SettingsButton.Content = "К разделам";
            SettingsButton.ToolTip = "К разделам";
            System.Windows.Automation.AutomationProperties.SetName(SettingsButton, "Вернуться к разделам");
            return;
        }

        SettingsContent.Visibility = Visibility.Collapsed;
        MenuTabControl.Visibility = Visibility.Visible;
        CoverSection(false);
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

    // Every section has its own sketch and note at the foot of the rail (D-25); settings and hosted pages stay clean.
    private void UpdateRailSketch()
    {
        RailSketch.Visibility = MenuTabControl.Visibility == Visibility.Visible && SettingsContent.Visibility != Visibility.Visible
            && HostedContent.Visibility != Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
        var section = SelectedSection();
        // Screens without a side margin (corner, Pokédex, catch, album) take turns between their sketches
        // at the foot of the rail: a new one on each visit, never on a mere resize (Astra margins 02.11).
        if (section != _railSection) { _railSection = section; _railVisits[section] = _railVisits.GetValueOrDefault(section) + 1; }
        var pieces = section switch
        {
            MenuSection.Pokedex => new[] { ("rail-pokedex", "Кто этот покемон? Узнаю — запишу."),
                ("pokedex-footprint", "Чей это след? Узнаю — запишу."), ("pokedex-pencil", "Карандаш точу: записей всё больше.") },
            MenuSection.Expeditions => new[] { ("rail-expeditions", "Компас старый, но не врёт.") },
            MenuSection.Nursery => new[] { ("rail-nursery", "Яйца любят тепло и терпение.") },
            MenuSection.Shop => new[] { ("rail-shop", "Список: ягоды, косточки, не забыть сдачу.") },
            MenuSection.Achievements => new[] { ("rail-album", "Значки собираются медленно. Зато навсегда."),
                ("album-star", "Ещё одна звёздочка в дневник."), ("album-ribbon", "Лучшее — на полку, остальное — в память.") },
            MenuSection.Catch => new[] { ("catch-leaves", "В траве кто-то шевелится."), ("catch-campfire", "У костра лучше слышно шорохи.") },
            _ => new[] { ("rail-corner", "Дома всегда кто-то ждёт."), ("corner-cup", "Чай остыл, пока мы гладились."),
                ("corner-yarn", "Клубок опять укатился под стол.") }
        };
        var (sketch, note) = pieces[(_railVisits.GetValueOrDefault(section) + pieces.Length - 1) % pieces.Length];
        RailSketchImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri($"pack://application:,,,/PokeCorner;component/Assets/NightDiary/sketch-{sketch}.png"));
        RailSketchNote.Text = note;
        // Sits right under the last section (Магазин), where the rail otherwise goes empty.
        if (ShopTab.ActualHeight > 0 && RailSketch.Parent is System.Windows.Media.Visual host)
        {
            var top = ShopTab.TransformToAncestor(host).Transform(new System.Windows.Point(0, ShopTab.ActualHeight)).Y + 16;
            RailSketch.Margin = new Thickness(8, top, 0, 0);
            // Like a doodle at the foot of a page: it sits low, just above «Настройки», not glued under the last tab.
            if (SettingsButton.ActualHeight > 0)
            {
                RailSketchNote.Visibility = Visibility.Visible; RailSketchImage.Visibility = Visibility.Visible;
                RailSketch.Measure(new System.Windows.Size(RailSketch.Width, double.PositiveInfinity));
                var bottom = SettingsButton.TransformToAncestor(host).Transform(new System.Windows.Point(0, 0)).Y - 12;
                top = Math.Max(top, bottom - RailSketch.DesiredSize.Height);
                RailSketch.Margin = new Thickness(8, top, 0, 0);
            }
            // At 1200x660 the rail is short: the note goes first, then the sketch, so neither runs into «Настройки».
            if (SettingsButton.ActualHeight > 0)
            {
                var room = SettingsButton.TransformToAncestor(host).Transform(new System.Windows.Point(0, 0)).Y - 8 - top;
                RailSketchNote.Visibility = Visibility.Visible; RailSketchImage.Visibility = Visibility.Visible;
                RailSketch.Measure(new System.Windows.Size(RailSketch.Width, double.PositiveInfinity));
                if (RailSketch.DesiredSize.Height > room) RailSketchNote.Visibility = Visibility.Collapsed;
                if (RailSketchImage.Height > room) RailSketchImage.Visibility = Visibility.Collapsed;
            }
        }
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
