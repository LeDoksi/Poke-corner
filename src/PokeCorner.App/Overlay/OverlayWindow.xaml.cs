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
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private static readonly TimeSpan IdleFrameDuration = TimeSpan.FromMilliseconds(150);

    private SpriteAnimator? _animator;

    public event Action? PetClicked;

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

    private void SpriteImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            PetClicked?.Invoke();
        }
        else if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    /// The window itself stays WS_EX_LAYERED | WS_EX_TRANSPARENT (click-through) at
    /// all times; the sprite Image re-enables hit-testing locally via IsHitTestVisible,
    /// so clicks land on the pet but pass through everywhere else in the window's bounds.
    private void MakeClickThroughExceptSprite()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
        SpriteImage.IsHitTestVisible = true;
    }

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
