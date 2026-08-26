using System.Windows.Media;
using System.Windows.Shapes;
using PokeCorner.App.Popup;

namespace PokeCorner.App.Overlay;

public partial class ProgressPanelWindow : System.Windows.Window
{
    // Must match the Width/Height and StrokeThickness of the gauge Ellipses in the XAML.
    private const double GaugeDiameter = 30;
    private const double GaugeStrokeThickness = 4.5;

    /// The length of one full turn of the arc, expressed in StrokeDashArray's units — which
    /// are multiples of StrokeThickness, not pixels. The stroke is centred on the ellipse's
    /// path, so the drawn circle's radius is (diameter - thickness) / 2.
    private static readonly double DashUnitsPerTurn =
        Math.PI * (GaugeDiameter - GaugeStrokeThickness) / GaugeStrokeThickness;

    public ProgressPanelWindow()
    {
        InitializeComponent();
    }

    public void UpdateProgress(ProgressInfo progress)
    {
        SetGauge(CatchArc, progress.CatchFraction);
        SetGauge(EvolutionArc, progress.EvolutionFraction);
        SetGauge(TreatArc, progress.TreatFraction);
    }

    /// Draws `fraction` of a ring by making the stroke a single dash that covers that much of
    /// the circumference followed by a gap covering the rest — the standard donut-gauge trick,
    /// and the reason these are plain Ellipses instead of a Path whose ArcSegment would have
    /// to be recomputed (with its own sweep-direction and large-arc bookkeeping) every second.
    private static void SetGauge(Ellipse arc, double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);

        // A zero-length dash with StrokeDashCap="Round" still paints a round dot, so an empty
        // gauge would show a stray pip at 12 o'clock instead of nothing. Hide the arc instead.
        if (fraction <= 0)
        {
            arc.Visibility = System.Windows.Visibility.Hidden;
            return;
        }
        arc.Visibility = System.Windows.Visibility.Visible;

        var filled = DashUnitsPerTurn * fraction;
        // The trailing +0.001 keeps the gap strictly positive at fraction == 1; a zero-length
        // gap makes WPF fall back to rendering the stroke as if undashed anyway, but an
        // explicit small value keeps the collection well-formed rather than relying on that.
        arc.StrokeDashArray = new DoubleCollection { filled, DashUnitsPerTurn - filled + 0.001 };
    }

    /// Positions the panel directly under the overlay sprite, horizontally centered on it —
    /// called whenever the overlay itself is repositioned (corner/monitor/size settings change).
    public void PositionBelow(OverlayWindow overlay)
    {
        // Width comes from the XAML (SizeToContent is Height only now), so this no longer
        // assigns it — the old `Width = 100` here was silently discarded by the content
        // measure anyway, which is what collapsed the panel into an unreadable sliver.
        Left = overlay.Left + (overlay.Width - Width) / 2;
        Top = overlay.Top + overlay.Height + 4;

        // Clamp to the taskbar-aware working area — the same region OverlayWindow.PositionAt
        // anchors the sprite to. Without this, the pet's default bottom-right corner leaves no
        // room below it and the panel rendered on top of (or behind) the taskbar. ActualHeight
        // is 0 until the first layout pass; App's ReconcileSettingsState calls this again
        // immediately after Show(), when it is real, so the flip below always gets applied.
        if (ActualHeight <= 0) return;

        var bounds = System.Windows.Forms.Screen
            .FromPoint(new System.Drawing.Point((int)overlay.Left, (int)overlay.Top))
            .WorkingArea;

        if (Top + ActualHeight > bounds.Bottom)
        {
            // No room below — sit above the sprite instead of overlapping it.
            Top = overlay.Top - ActualHeight - 4;
        }
        if (Top < bounds.Top)
        {
            Top = bounds.Top;
        }

        // The panel needs the same z-order slot as the pet it hangs off — see
        // KeepAboveAppsBelowShell's comment for why neither is Topmost="True" any more.
        OverlayWindow.KeepAboveAppsBelowShell(this);
    }
}
