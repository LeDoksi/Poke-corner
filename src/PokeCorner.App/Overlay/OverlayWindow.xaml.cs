using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

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

    public event Action? PetClicked;
    public event Action? MenuRequested;

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => MakeClickThroughExceptSprite();
    }

    public void SetSpriteFrames(IReadOnlyList<ImageSource> frames)
    {
        _animator?.Stop();
        _animator = null;

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

    public void PositionAt(ScreenCorner corner, int monitorIndex, int sizePixels)
    {
        Width = sizePixels;
        Height = sizePixels;

        var screens = System.Windows.Forms.Screen.AllScreens;
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
        }
    }

    private void SpriteImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (SpriteImage.IsMouseCaptured)
        {
            SpriteImage.ReleaseMouseCapture();
        }

        if (!_isDragging)
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
}
