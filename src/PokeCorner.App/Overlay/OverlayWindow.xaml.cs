using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PokeCorner.App.Overlay;

public enum ScreenCorner { TopLeft, TopRight, BottomLeft, BottomRight }

public partial class OverlayWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;
    private static readonly TimeSpan IdleFrameDuration = TimeSpan.FromMilliseconds(150);

    private SpriteAnimator? _animator;
    private System.Windows.Point _mouseDownPosition;
    private bool _isDragging;
    private readonly DispatcherTimer _stillEffectTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private int _thoughtStage;
    private bool _thoughtAnimated;

    /// Field, not a local: SetWinEventHook stores only the raw function pointer, so a
    /// delegate that the GC is free to collect would leave Windows calling into freed
    /// memory. Keeping the instance alive for the window's lifetime is the standard fix.
    private readonly WinEventDelegate _foregroundChangedCallback;
    private IntPtr _foregroundHook;

    /// False keeps the pet on screen when something tries to close it (Alt+F4 while it has
    /// focus): with ShutdownMode=OnExplicitShutdown the process would otherwise keep running
    /// with no pet. The app sets it right before shutting down.
    public bool AllowClose { get; set; }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!AllowClose) e.Cancel = true;
        base.OnClosing(e);
    }

    public event Action? PetClicked;
    public event Action? MenuRequested;

    /// Another process's window just became the foreground window. App subscribes to re-top
    /// the overlay AND the progress panel together, so the pair never gets separated by a
    /// newly activated app window.
    public event Action? ForegroundWindowChanged;

    /// Fired once a manual drag ends, so the new spot can be remembered and restored on the
    /// next launch instead of always snapping back to the configured corner.
    public event Action? PositionChanged;

    public OverlayWindow()
    {
        InitializeComponent();
        ThoughtBubble.Tag = 0;
        _stillEffectTimer.Tick += (_, _) =>
        {
            _stillEffectTimer.Stop();
            foreach (var shape in HeartEffects.Children.OfType<Shape>().Concat(CrumbEffects.Children.OfType<Shape>()))
                shape.Opacity = 0;
        };
        Loaded += (_, _) => SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
        Unloaded += (_, _) => SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;
        _foregroundChangedCallback = (_, _, _, _, _, _, _) => ForegroundWindowChanged?.Invoke();
        SourceInitialized += (_, _) =>
        {
            MakeClickThroughExceptSprite();
            HideFromTaskbarWithoutOwner(this);
            _foregroundHook = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _foregroundChangedCallback, 0, 0,
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        };
        Closed += (_, _) =>
        {
            _stillEffectTimer.Stop();
            if (_foregroundHook != IntPtr.Zero) UnhookWinEvent(_foregroundHook);
            _foregroundHook = IntPtr.Zero;
        };
    }

    /// Keeps `window` above ordinary app windows but below the taskbar and the shell surfaces
    /// that sit with it (tray flyouts, Start, notification centre), by inserting it into the
    /// topmost band directly behind Shell_TrayWnd. Plain Topmost="True" can't do that: the
    /// topmost band is flat, so the pet rendered over the taskbar and tray flyouts.
    ///
    /// SWP_NOACTIVATE is essential: this runs right after the user clicks something and must not
    /// steal focus from it. Static so ProgressPanelWindow can reuse it.
    public static void KeepAboveAppsBelowShell(System.Windows.Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return; // not yet sourced; the SourceInitialized/Show path re-runs this

        var taskbar = FindWindow("Shell_TrayWnd", null);
        var slot = ChooseZSlot(
            taskbarFound: taskbar != IntPtr.Zero,
            foreignTopmostBelowTaskbar: taskbar != IntPtr.Zero && HasForeignTopmostBelow(taskbar),
            alreadyTopmost: IsTopmost(hwnd));

        if (slot == ZSlot.BehindTaskbar)
        {
            SetWindowPos(hwnd, taskbar, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            // Belt and braces: the slot was judged to be inside the topmost band, but if Windows
            // dropped the flag anyway, being buried is the one outcome that must not stand.
            if (!IsTopmost(hwnd)) slot = ZSlot.TopOfTopmostBand;
        }

        if (slot == ZSlot.TopOfTopmostBand)
        {
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            TopmostFallbackCount++;
        }
    }

    /// Where to put the pet this round. "Behind Shell_TrayWnd" keeps the topmost flag only while
    /// some OTHER topmost window sits below the taskbar; when the taskbar is the last one, that
    /// slot is the top of the normal band and the flag would be dropped. Then the pet goes to the
    /// top of the topmost band instead — and is LEFT there while it stays topmost, because
    /// re-slotting it behind the taskbar every tick would drop the flag again and flip-flop twice
    /// a second. The price: while no other topmost window exists, the pet can draw over a shell
    /// flyout near it.
    public static ZSlot ChooseZSlot(bool taskbarFound, bool foreignTopmostBelowTaskbar, bool alreadyTopmost)
    {
        if (taskbarFound && foreignTopmostBelowTaskbar) return ZSlot.BehindTaskbar;
        return alreadyTopmost ? ZSlot.LeaveAsIs : ZSlot.TopOfTopmostBand;
    }

    private static bool IsTopmost(IntPtr hwnd) => (NativeMethods.GetWindowLong(hwnd, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0;

    /// Whether a topmost window of another process sits below the taskbar — our own windows
    /// (pet, progress panel) are skipped, since they are what is being placed.
    private static bool HasForeignTopmostBelow(IntPtr taskbar)
    {
        var ownPid = (uint)Environment.ProcessId;
        for (var next = GetWindow(taskbar, GW_HWNDNEXT); next != IntPtr.Zero; next = GetWindow(next, GW_HWNDNEXT))
        {
            NativeMethods.GetWindowThreadProcessId(next, out var pid);
            if (pid == ownPid) continue;
            return IsTopmost(next);
        }
        return false;
    }

    /// How many times the fallback above fired this session — in zorder.log, so a log sent in
    /// by a user shows whether it was involved.
    public static int TopmostFallbackCount { get; private set; }

    public void SetSpriteFrames(IReadOnlyList<ImageSource> frames)
    {
        _animator?.Stop();
        _animator = null;

        // Clear held animations before assigning the next sprite; held WPF values outrank
        // plain property assignments and could otherwise leave it invisible or displaced.
        SpriteImage.BeginAnimation(OpacityProperty, null);
        SpriteImage.Opacity = 1.0;

        if (frames.Count == 0)
        {
            SpriteImage.Source = null;
            return;
        }

        // frameCount comes from frames.Count right here, so FrameSequencer and SpriteAnimator can
        // never disagree; a single frame just means a static image.
        var sequencer = new FrameSequencer(frames.Count, IdleFrameDuration);
        _animator = new SpriteAnimator(SpriteImage, sequencer, frames);
        _animator.Start();
    }

    /// The gap kept between the pet and the working-area edge it is anchored to. DIPs, like
    /// every other number that reaches Left/Top/Width/Height.
    private const double CornerMargin = 12;

    /// THE UNIT BOUNDARY. System.Windows.Forms.Screen reports PHYSICAL pixels; WPF's
    /// Left/Top/Width/Height take DIPs (1/96"), i.e. physical = DIP * scale. At 100% scaling the
    /// two are numerically identical, which hides the mistake — at 150% a raw physical value put
    /// the pet far off-screen. Every value crossing that boundary goes through here.
    public static Rect ToDips(System.Drawing.Rectangle physical, DpiScale dpi) =>
        new(physical.Left / dpi.DpiScaleX, physical.Top / dpi.DpiScaleY,
            physical.Width / dpi.DpiScaleX, physical.Height / dpi.DpiScaleY);

    /// The scale factor WPF is actually applying to `window`'s Left/Top right now.
    ///
    /// Read from the window's own composition target when it has one, because that matrix IS
    /// the conversion WPF performs — no assumption about awareness mode can disagree with it.
    /// Before the window is sourced (PositionAt runs once during startup, ahead of Show())
    /// there is no such matrix, so it falls back to the process's system DPI: WPF on .NET is
    /// System-DPI-aware unless an app.manifest opts into PerMonitorV2, and this app ships no
    /// manifest, so a single process-wide scale is the whole truth and the two agree.
    ///
    /// Static and window-taking so ProgressPanelWindow can reuse it, same as
    /// KeepAboveAppsBelowShell.
    public static DpiScale DpiFor(System.Windows.Window window)
    {
        var transform = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformToDevice;
        if (transform is { } t && t.M11 > 0 && t.M22 > 0) return new DpiScale(t.M11, t.M22);

        var systemScale = GetDpiForSystem() / 96.0;
        return new DpiScale(systemScale, systemScale);
    }

    /// Where a `sizeDips`-square pet sits in `corner` of an already-converted working area.
    /// Pure, so the DIP arithmetic is testable without a live window or monitor.
    public static System.Windows.Point CornerPosition(ScreenCorner corner, Rect workAreaDips, double sizeDips) =>
        corner switch
        {
            ScreenCorner.TopLeft => new(workAreaDips.Left + CornerMargin, workAreaDips.Top + CornerMargin),
            ScreenCorner.TopRight => new(workAreaDips.Right - sizeDips - CornerMargin, workAreaDips.Top + CornerMargin),
            ScreenCorner.BottomLeft => new(workAreaDips.Left + CornerMargin, workAreaDips.Bottom - sizeDips - CornerMargin),
            _ => new(workAreaDips.Right - sizeDips - CornerMargin, workAreaDips.Bottom - sizeDips - CornerMargin)
        };

    /// Whether a remembered drag position still lands on `workAreaDips`. Both sides must be
    /// DIPs (Window.Left/Top are DIPs); against a raw physical WorkingArea the "still on a
    /// connected monitor" check misjudges at any scaling other than 100%.
    public static bool IsOnWorkArea(double xDips, double yDips, double sizeDips, Rect workAreaDips) =>
        xDips >= workAreaDips.Left - sizeDips && xDips <= workAreaDips.Right &&
        yDips >= workAreaDips.Top - sizeDips && yDips <= workAreaDips.Bottom;

    /// How much bigger than the configured pet size the WINDOW is made, so a reaction that
    /// scales the sprite up has somewhere to grow into instead of being shaved off at the
    /// window's own edge (a WPF window clips its content to its client area).
    ///
    /// 1.4 is not a round number picked for comfort: it is exactly PlayEvolutionFlash's peak
    /// scale, the largest of the three reactions (pet 1.15, feed 1.3, flash 1.4). The sprite
    /// is centred in the window and scales about its own centre, so a peak of 1.4 needs
    /// 0.2 * size of clearance on every side — which is precisely what a 1.4x window gives.
    /// Raising any animation's peak scale past this constant re-introduces the clipping.
    public const double SpriteScaleHeadroom = 1.4;

    /// The size the sprite itself is drawn at, in DIPs — the configured pet size, not the
    /// padded window. Zero until the first PositionAt.
    private double _spriteSizeDips;

    /// The rect the user actually SEES, i.e. the window rect minus the invisible headroom.
    /// Everything that anchors to the pet — the progress panel beside it — must use this
    /// rather than Left/Top/Width/Height, or it anchors to transparent air.
    public Rect SpriteBounds
    {
        get
        {
            if (_spriteSizeDips <= 0) return new Rect(Left, Top, Width, Height);
            var pad = (Width - _spriteSizeDips) / 2;
            return new Rect(Left + pad, Top + pad, _spriteSizeDips, _spriteSizeDips);
        }
    }

    /// sizePixels is treated as DIPs on purpose: the setting is a perceived size, so the pet
    /// should look the same next to everything else on a 150% display rather than shrinking
    /// to two-thirds of it.
    public void PositionAt(ScreenCorner corner, int monitorIndex, int sizePixels, int? rememberedX = null, int? rememberedY = null)
    {
        // The sprite keeps the configured size; only the window around it grows, so the pet
        // looks identical to before — it simply is no longer clipped when it scales up.
        _spriteSizeDips = sizePixels;
        SpriteImage.Width = sizePixels;
        SpriteImage.Height = sizePixels;
        HeartEffects.Width = HeartEffects.Height = sizePixels;
        CrumbEffects.Width = CrumbEffects.Height = sizePixels;

        var windowSize = sizePixels * SpriteScaleHeadroom;
        Width = windowSize;
        Height = windowSize;

        // Corner anchoring must place the VISIBLE sprite in the corner, so the window origin
        // sits one headroom-pad up and to the left of where the sprite is wanted. Without this
        // the pet would drift inward from every corner by that pad.
        var headroomPad = (windowSize - sizePixels) / 2;

        var dpi = DpiFor(this);
        var screens = System.Windows.Forms.Screen.AllScreens;

        // A remembered spot from a manual drag wins over the corner setting — but only if it's
        // still somewhere on a currently-connected monitor (a disconnected second monitor
        // would otherwise strand the pet off-screen forever).
        // The remembered pair is a WINDOW origin on both sides of the round trip — App stores
        // Left/Top and hands the same numbers back — so it is restored verbatim and only the
        // on-a-connected-monitor test needs the window's (padded) size.
        var rememberedArea = screens.Select(s => (Rect?)ToDips(s.WorkingArea, dpi))
            .FirstOrDefault(area => area is { } bounds && rememberedX is { } x && rememberedY is { } y
                && IsOnWorkArea(x, y, windowSize, bounds));
        if (rememberedX is { } rememberedLeft && rememberedY is { } rememberedTop && rememberedArea is { } area)
        {
            (Left, Top) = (Math.Clamp(rememberedLeft, area.Left, Math.Max(area.Left, area.Right - windowSize)),
                Math.Clamp(rememberedTop, area.Top, Math.Max(area.Top, area.Bottom - windowSize)));
            KeepAboveAppsBelowShell(this);
            return;
        }

        var screen = monitorIndex >= 0 && monitorIndex < screens.Length ? screens[monitorIndex] : System.Windows.Forms.Screen.PrimaryScreen!;
        var workArea = ToDips(screen.WorkingArea, dpi);
        var position = CornerPosition(corner, workArea, sizePixels);
        (Left, Top) = (Math.Clamp(position.X - headroomPad, workArea.Left, Math.Max(workArea.Left, workArea.Right - windowSize)),
            Math.Clamp(position.Y - headroomPad, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - windowSize)));

        // Moving/resizing a window can drop it behind others; re-top it right away instead of
        // waiting for the next foreground switch.
        KeepAboveAppsBelowShell(this);
    }

    // Click-vs-drag detection via explicit mouse capture + a MouseMove threshold, not a
    // before/after position comparison around DragMove(): DragMove() runs its own Win32
    // SC_MOVE modal loop that bypasses WPF's input pipeline, so positions read afterwards are
    // unreliable (clicks stopped registering). DragMove() starts only once it's surely a drag.
    private void SpriteImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MenuRequested?.Invoke();
            return;
        }

        _mouseDownPosition = e.GetPosition(this);
        _isDragging = false;
        SpriteImage.CaptureMouse();
    }

    private void SpriteImage_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _isDragging) return;

        var current = e.GetPosition(this);
        var delta = current - _mouseDownPosition;
        if (Math.Abs(delta.X) > 4 || Math.Abs(delta.Y) > 4)
        {
            _isDragging = true;
            SpriteImage.ReleaseMouseCapture();
            DragMove(); // blocks until mouse-up; this call is now only reached for a real drag.
            PositionChanged?.Invoke();
        }
    }

    private void SpriteImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (SpriteImage.IsMouseCaptured)
        {
            SpriteImage.ReleaseMouseCapture();
        }

        // e.ClickCount < 2 excludes the mouse-up half of a double-click (Down1/Up1 already
        // fired PetClicked for the first click before WPF could know a second was coming;
        // Down2 short-circuits into MenuRequested and never touches _isDragging/capture, so
        // without this check Up2 would fall through here and fire PetClicked a second time).
        if (!_isDragging && e.ClickCount < 2)
        {
            PetClicked?.Invoke();
        }
        _isDragging = false;
    }

    /// Keeps a desk window off the taskbar and out of Alt+Tab WITHOUT ShowInTaskbar="False".
    /// That property gives the window a hidden, non-topmost owner, and an owned window's z-order
    /// follows its owner: the pet kept WS_EX_TOPMOST yet sat under ordinary app windows (POKE-9).
    /// WS_EX_TOOLWINDOW hides it with no owner involved — but WPF puts WS_EX_APPWINDOW on every
    /// ShowInTaskbar window and APPWINDOW overrides TOOLWINDOW, so that has to come off too.
    /// Call from SourceInitialized, i.e. before the window is first shown.
    public static void HideFromTaskbarWithoutOwner(System.Windows.Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        SetWindowLong(hwnd, GWL_EXSTYLE, (NativeMethods.GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
    }

    /// Only WS_EX_LAYERED (needed with AllowsTransparency for alpha-blended rendering), NOT
    /// WS_EX_TRANSPARENT: that works at the Win32 WM_NCHITTEST level and would route ALL mouse
    /// input past the window, the pet included. Click-through instead comes from the window's
    /// {x:Null} background, which has no hit-test surface: clicks land on the sprite and fall
    /// through everywhere else in the window's rectangle.
    private void MakeClickThroughExceptSprite()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var extendedStyle = NativeMethods.GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED);
    }

    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    private const uint GW_HWNDNEXT = 2;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const int WS_EX_TOPMOST = 0x00000008;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    /// The process's system DPI, honouring its declared awareness. Win10 1607+, which the
    /// installer already requires. Used only as DpiFor's pre-Show fallback.
    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();

    public void PlayPetReaction()
    {
        PlayBounce(peakScale: 1.15, durationMs: 300);
        PlayParticles(HeartEffects, HeartLeft, HeartMiddle, HeartRight);
    }

    public void PlayFeedReaction()
    {
        PlayBounce(peakScale: 1.3, durationMs: 400);
        PlayParticles(CrumbEffects, CrumbLeft, CrumbMiddle, CrumbRight);
    }

    public void UpdateThought(DateTimeOffset lastInteractionUtc, DateTimeOffset nowUtc)
    {
        var idle = nowUtc - lastInteractionUtc;
        var stage = idle >= TimeSpan.FromHours(5.75) ? 3 :
            idle >= TimeSpan.FromHours(4.5) ? 2 : idle >= TimeSpan.FromHours(3) ? 1 : 0;
        if (stage != _thoughtStage)
        {
            _thoughtStage = stage;
            ThoughtBubble.Tag = stage;
            ThoughtBubble.Visibility = stage == 0 ? Visibility.Collapsed : Visibility.Visible;
            ThoughtStage.Text = new string('⌁', stage);
        }
        UpdateThoughtMotion();
    }

    private void SystemParameters_StaticPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SystemParameters.ClientAreaAnimation)) return;
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(UpdateMotion); return; }
        UpdateMotion();
    }

    private void UpdateMotion()
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            ClearScaleAnimations();
            foreach (var shape in HeartEffects.Children.OfType<Shape>().Concat(CrumbEffects.Children.OfType<Shape>()))
            {
                shape.BeginAnimation(OpacityProperty, null);
                shape.Opacity = 0;
            }
        }
        UpdateThoughtMotion();
    }

    private void UpdateThoughtMotion()
    {
        var animate = _thoughtStage > 0 && SystemParameters.ClientAreaAnimation;
        if (animate == _thoughtAnimated) return;
        _thoughtAnimated = animate;
        ThoughtStage.BeginAnimation(OpacityProperty, animate
            ? new DoubleAnimation(.65, 1, TimeSpan.FromSeconds(1))
                { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever }
            : null);
        if (!animate) ThoughtStage.Opacity = 1;
    }

    private void PlayParticles(Canvas canvas, params Shape[] particles)
    {
        var size = _spriteSizeDips > 0 ? _spriteSizeDips : 96;
        canvas.Width = canvas.Height = size;
        for (var i = 0; i < particles.Length; i++)
        {
            var particle = particles[i];
            Canvas.SetLeft(particle, size * (.32 + .16 * i));
            Canvas.SetTop(particle, size * (canvas == HeartEffects ? .12 : .38));
            particle.BeginAnimation(OpacityProperty, null);
            var shift = particle.RenderTransform as TranslateTransform ?? new TranslateTransform();
            particle.RenderTransform = shift;
            shift.BeginAnimation(TranslateTransform.XProperty, null);
            shift.BeginAnimation(TranslateTransform.YProperty, null);
            shift.X = shift.Y = 0;
            if (!SystemParameters.ClientAreaAnimation)
            {
                particle.Opacity = 1;
                continue;
            }
            var opacity = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(650) };
            opacity.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            opacity.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
            particle.BeginAnimation(OpacityProperty, opacity);
            shift.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(0, (i - 1) * 14, TimeSpan.FromMilliseconds(650)));
            shift.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(0, -Math.Min(24, size * .2), TimeSpan.FromMilliseconds(650)));
        }
        if (!SystemParameters.ClientAreaAnimation)
        {
            _stillEffectTimer.Stop();
            _stillEffectTimer.Start();
        }
    }

    public void PlayEvolutionFlash()
    {
        if (!SystemParameters.ClientAreaAnimation) return;
        KeepAboveAppsBelowShell(this); // scales to 1.4 — same clipping risk as PlayBounce

        // Same held-animation precedence problem as in SetSpriteFrames: a still-holding
        // bounce (or a previous flash) would make this one start from — and be fought by —
        // a stale animated value. Clearing first means the newest animation always wins.
        ClearScaleAnimations();

        var flash = new DoubleAnimation(1.0, 1.4, TimeSpan.FromMilliseconds(200)) { AutoReverse = true };
        SpriteScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, flash);
        SpriteScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, flash);
    }

    /// Removes any still-holding scale animation clock (and the glyph fade's) so a newly
    /// started bounce/flash isn't silently outranked by the previous one's held end value.
    private void ClearScaleAnimations()
    {
        SpriteScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        SpriteScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        SpriteScaleTransform.ScaleX = 1.0;
        SpriteScaleTransform.ScaleY = 1.0;
    }

    private void PlayBounce(double peakScale, int durationMs)
    {
        if (!SystemParameters.ClientAreaAnimation) return;
        // The sprite scales up past its normal footprint here, into the space the progress
        // panel occupies. These are two independent top-level windows with independent
        // z-order, so being merely "usually above" the panel is not enough — a bounce that
        // starts while the panel happens to sit on top gets visibly clipped. App's periodic
        // re-top already orders the pair correctly; this pins it at the one moment it shows.
        KeepAboveAppsBelowShell(this);

        ClearScaleAnimations();

        var bounce = new DoubleAnimation(1.0, peakScale, TimeSpan.FromMilliseconds(durationMs / 2.0))
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase()
        };
        SpriteScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, bounce);
        SpriteScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, bounce);

    }
}

public enum ZSlot { BehindTaskbar, TopOfTopmostBand, LeaveAsIs }
