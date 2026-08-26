using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PokeCorner.App.Overlay;

public enum ScreenCorner { TopLeft, TopRight, BottomLeft, BottomRight }

public partial class OverlayWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;
    private static readonly TimeSpan IdleFrameDuration = TimeSpan.FromMilliseconds(150);

    private SpriteAnimator? _animator;
    private System.Windows.Point _mouseDownPosition;
    private bool _isDragging;

    /// Field, not a local: SetWinEventHook stores only the raw function pointer, so a
    /// delegate that the GC is free to collect would leave Windows calling into freed
    /// memory. Keeping the instance alive for the window's lifetime is the standard fix.
    private readonly WinEventDelegate _foregroundChangedCallback;
    private IntPtr _foregroundHook;

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
        _foregroundChangedCallback = (_, _, _, _, _, _, _) => ForegroundWindowChanged?.Invoke();
        SourceInitialized += (_, _) =>
        {
            MakeClickThroughExceptSprite();
            _foregroundHook = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _foregroundChangedCallback, 0, 0,
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        };
        Closed += (_, _) =>
        {
            if (_foregroundHook != IntPtr.Zero) UnhookWinEvent(_foregroundHook);
            _foregroundHook = IntPtr.Zero;
        };
    }

    /// Keeps `window` above ordinary application windows but permanently below the taskbar
    /// and the shell surfaces that sit with it — tray flyouts, the Start menu, notification
    /// centre. It does that by inserting the window into the topmost band DIRECTLY BEHIND
    /// Shell_TrayWnd, which is the taskbar's own window.
    ///
    /// Two approaches were tried before this one, and it is worth recording why both fail,
    /// because each looks obviously correct:
    ///
    ///   Topmost="True" — the original. The topmost band is flat: there is no "just below the
    ///   shell" position inside it, so the pet could and did render over the taskbar and over
    ///   tray flyouts.
    ///
    ///   SetWindowPos(HWND_TOP) with the window left non-topmost. This does keep the pet below
    ///   every shell surface, but it does NOT put it above the window the user is working in:
    ///   Windows refuses to raise a window above the foreground window on behalf of a process
    ///   that does not own the foreground, so the call silently does nothing useful and the pet
    ///   ends up buried under whatever was clicked last. Verified live — the pet sat below
    ///   three ordinary app windows while this was in place.
    ///
    /// Passing a topmost window as hWndInsertAfter makes `window` topmost too and places it
    /// immediately behind that window, which is exactly the "one rung below the shell" slot the
    /// flat topmost band otherwise doesn't offer. The taskbar therefore always covers the pet,
    /// and a flyout or the Start menu — which come up at the TOP of the topmost band when they
    /// open, above Shell_TrayWnd — covers it as well.
    ///
    /// SWP_NOACTIVATE is essential: without it this would steal focus from whatever the user
    /// just clicked on, which is the very thing that triggers this call.
    ///
    /// Static so ProgressPanelWindow — which needs the identical treatment for the identical
    /// reason — can reuse it rather than carrying its own copy of the P/Invoke.
    public static void KeepAboveAppsBelowShell(System.Windows.Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return; // not yet sourced; the SourceInitialized/Show path re-runs this

        // HWND_TOP as the fallback, never HWND_TOPMOST: if the taskbar can't be found there is
        // nothing to stay underneath, and defaulting to the topmost band would quietly restore
        // the exact over-the-taskbar bug this method exists to prevent.
        var taskbar = FindWindow("Shell_TrayWnd", null);
        var insertAfter = taskbar == IntPtr.Zero ? HWND_TOP : taskbar;

        SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    public void SetSpriteFrames(IReadOnlyList<ImageSource> frames)
    {
        _animator?.Stop();
        _animator = null;

        // A held (FillBehavior.HoldEnd) animation outranks any later plain property
        // assignment in WPF's value precedence, so PlayRanAwayReaction's fade/slide would
        // otherwise keep the sprite invisible and off-position forever — SetMoodTint's
        // assignments and every subsequent reveal would silently do nothing. Clearing the
        // clocks with BeginAnimation(property, null) restores local-value precedence, so
        // every fresh reveal starts from a clean, fully visible, un-translated state.
        SpriteImage.BeginAnimation(OpacityProperty, null);
        SpriteTranslateTransform.BeginAnimation(TranslateTransform.YProperty, null);
        SpriteImage.Opacity = 1.0;
        SpriteTranslateTransform.Y = 0;

        if (frames.Count == 0)
        {
            SpriteImage.Source = null;
            return;
        }

        // frameCount is always derived from frames.Count right here, so FrameSequencer
        // and SpriteAnimator can never disagree about how many frames exist — a single
        // frame (the common case until the animated-sprite follow-up plan lands) just
        // means FrameIndexAt always returns 0, i.e. a static image with no visible cycling.
        var sequencer = new FrameSequencer(frames.Count, IdleFrameDuration);
        _animator = new SpriteAnimator(SpriteImage, sequencer, frames);
        _animator.Start();
    }

    public void PositionAt(ScreenCorner corner, int monitorIndex, int sizePixels, int? rememberedX = null, int? rememberedY = null)
    {
        Width = sizePixels;
        Height = sizePixels;

        var screens = System.Windows.Forms.Screen.AllScreens;

        // A remembered spot from a manual drag wins over the corner setting — but only if it's
        // still somewhere on a currently-connected monitor (a disconnected second monitor
        // would otherwise strand the pet off-screen forever).
        if (rememberedX is { } x && rememberedY is { } y &&
            screens.Any(s => x >= s.WorkingArea.Left - sizePixels && x <= s.WorkingArea.Right &&
                              y >= s.WorkingArea.Top - sizePixels && y <= s.WorkingArea.Bottom))
        {
            (Left, Top) = (x, y);
            KeepAboveAppsBelowShell(this);
            return;
        }

        var screen = monitorIndex >= 0 && monitorIndex < screens.Length ? screens[monitorIndex] : System.Windows.Forms.Screen.PrimaryScreen!;
        var bounds = screen.WorkingArea;

        const int margin = 12;
        (Left, Top) = corner switch
        {
            ScreenCorner.TopLeft => (bounds.Left + margin, bounds.Top + margin),
            ScreenCorner.TopRight => (bounds.Right - sizePixels - margin, bounds.Top + margin),
            ScreenCorner.BottomLeft => (bounds.Left + margin, bounds.Bottom - sizePixels - margin),
            ScreenCorner.BottomRight => (bounds.Right - sizePixels - margin, bounds.Bottom - sizePixels - margin),
            _ => (bounds.Right - sizePixels - margin, bounds.Bottom - sizePixels - margin)
        };

        // Moving/resizing a window can drop it behind others; without this the pet silently
        // fell out of position in the z-order after a corner/monitor/size change and only
        // recovered on the next foreground switch.
        KeepAboveAppsBelowShell(this);
    }

    // Click-vs-drag detection via explicit mouse capture + MouseMove threshold, not a
    // before/after position comparison around DragMove(). That approach turned out
    // unreliable: DragMove() runs its own internal Win32 SC_MOVE modal loop that
    // bypasses WPF's normal input pipeline, so WPF's Mouse.GetPosition() afterward
    // isn't guaranteed to reflect what actually happened — in practice, clicks
    // stopped registering at all. This is the standard, well-tested pattern instead:
    // track real movement via WPF's own MouseMove stream, and only invoke DragMove()
    // once we're already sure it's a genuine drag.
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

    /// WS_EX_TRANSPARENT operates at the Win32 WM_NCHITTEST level — it makes Windows
    /// route ALL mouse input past the entire HWND (including its children) to whatever
    /// is behind it, before WPF's own hit-testing ever runs. So it is deliberately NOT
    /// applied here: only WS_EX_LAYERED is set, which is still needed (alongside
    /// AllowsTransparency="True") for proper alpha-blended transparency rendering.
    /// Instead, the window's own background is {x:Null} (see OverlayWindow.xaml) — a
    /// null background has no hit-test surface at all, so WPF's hit-testing only ever
    /// finds the SpriteImage control itself (hit-testable by default), meaning clicks on
    /// the pet land on it while clicks elsewhere in the window's bounding box fall through
    /// to whatever's behind it, all through WPF's normal hit-testing rather than Win32
    /// click-through.
    private void MakeClickThroughExceptSprite()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED);
    }

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    private static readonly IntPtr HWND_TOP = IntPtr.Zero;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    public void PlayPetReaction() => PlayBounce(peakScale: 1.15, durationMs: 300, glyph: "❤");

    public void PlayFeedReaction() => PlayBounce(peakScale: 1.3, durationMs: 400, glyph: "+2250");

    public void PlayEvolutionFlash()
    {
        KeepAboveAppsBelowShell(this); // scales to 1.4 — same clipping risk as PlayBounce

        // Same held-animation precedence problem as in SetSpriteFrames: a still-holding
        // bounce (or a previous flash) would make this one start from — and be fought by —
        // a stale animated value. Clearing first means the newest animation always wins.
        ClearScaleAnimations();

        var flash = new DoubleAnimation(1.0, 1.4, TimeSpan.FromMilliseconds(200)) { AutoReverse = true };
        SpriteScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, flash);
        SpriteScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, flash);
    }

    public void PlayRanAwayReaction()
    {
        var slide = new DoubleAnimation(0, Height, TimeSpan.FromMilliseconds(400));
        var fade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(400));
        SpriteTranslateTransform.BeginAnimation(TranslateTransform.YProperty, slide);
        SpriteImage.BeginAnimation(OpacityProperty, fade);
    }

    /// mood: 0.0 (long-neglected) to 1.0 (just interacted with) — CareModeService.ComputeMood's
    /// output, applied continuously so the sprite visibly droops/dims as Hardcore-mode
    /// neglect approaches the run-away threshold, not just at the moment it triggers.
    public void SetMoodTint(double mood)
    {
        var droopOffset = (1.0 - mood) * 10;
        SpriteTranslateTransform.Y = droopOffset;
        SpriteImage.Opacity = 0.5 + mood * 0.5;
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

    private void PlayBounce(double peakScale, int durationMs, string glyph)
    {
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

        // Same reason as above: the previous fade holds Opacity at 0, which would outrank
        // this plain assignment and make the glyph invisible on every bounce after the first.
        ReactionGlyphText.BeginAnimation(OpacityProperty, null);
        ReactionGlyphText.Text = glyph;
        ReactionGlyphText.Opacity = 1.0;
        var fadeUp = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(durationMs * 2));
        ReactionGlyphText.BeginAnimation(OpacityProperty, fadeUp);
    }
}
