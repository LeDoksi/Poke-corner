using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PokeCorner.App.Theme;

/// Night Diary 2.0 motion (D-38): short, soft, and always optional. Everything here is skipped when Windows
/// animations are off, and while screenshots are being captured, so a frame is never caught half-way.
public static class Motion
{
    public static bool Enabled => SystemParameters.ClientAreaAnimation
        && Environment.GetEnvironmentVariable("POKE_SHELL_CAPTURE_DIR") is null;

    private static readonly IEasingFunction Out = Frozen(new CubicEase { EasingMode = EasingMode.EaseOut });
    private static readonly IEasingFunction Spring = Frozen(new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.7 });

    private static T Frozen<T>(T value) where T : Freezable { value.Freeze(); return value; }

    /// A page arriving: fades in while rising a few pixels.
    public static void Enter(FrameworkElement? element, double rise = 10, int milliseconds = 220)
    {
        if (element is null) return;
        if (element.RenderTransform is not TranslateTransform shift || shift.IsFrozen)
            element.RenderTransform = shift = new TranslateTransform();
        if (!Enabled)
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            shift.BeginAnimation(TranslateTransform.YProperty, null);
            return;
        }

        var time = TimeSpan.FromMilliseconds(milliseconds);
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, time) { EasingFunction = Out, FillBehavior = FillBehavior.Stop });
        shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(rise, 0.0, time) { EasingFunction = Out, FillBehavior = FillBehavior.Stop });
    }

    /// A number or badge that just changed: one quick bounce.
    public static void Pop(FrameworkElement? element, double from = 1.25)
    {
        if (element is null || !Enabled) return;
        if (element.RenderTransform is not ScaleTransform scale || scale.IsFrozen)
        {
            element.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            element.RenderTransform = scale = new ScaleTransform(1, 1);
        }

        var animation = new DoubleAnimation(from, 1.0, TimeSpan.FromMilliseconds(260)) { EasingFunction = Spring, FillBehavior = FillBehavior.Stop };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
    }

    private static readonly DependencyProperty BreathingProperty =
        DependencyProperty.RegisterAttached("Breathing", typeof(bool), typeof(Motion), new PropertyMetadata(false));

    /// Slow breathing for something that waits for the player. Idempotent: call it on every refresh.
    public static void Breathe(UIElement? element, bool on)
    {
        if (element is null) return;
        var running = (bool)element.GetValue(BreathingProperty);
        on = on && Enabled && element.Visibility == Visibility.Visible;
        if (on == running) return;
        element.SetValue(BreathingProperty, on);
        if (!on)
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            return;
        }

        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, 0.55, TimeSpan.FromMilliseconds(1100))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase()
        });
    }

    /// Whole-window scale for big windows (D-38, softened by D-51): a big window is shared between larger controls
    /// and more content. FullHD maximized gives x1.15 and about 1670x904 logical px (was x1.25 and 1536x832),
    /// 2560x1400 gives x1.4 and 1829x1000, so the Pokédex shows more tiles and the scenes are not cropped.
    public static double ShellScale(double width, double height)
    {
        if (double.IsNaN(width) || double.IsNaN(height)) return 1;
        var raw = Math.Min(width / 1440.0, height / 800.0);
        if (raw <= 1) return 1;
        return Math.Clamp(Math.Floor(Math.Pow(raw, .6) * 20 + 1e-9) / 20, 1.0, 1.75);
    }
}
