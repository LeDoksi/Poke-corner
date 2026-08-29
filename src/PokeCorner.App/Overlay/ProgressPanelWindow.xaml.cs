using System.Windows.Media;
using System.Windows.Shapes;
using PokeCorner.App.Popup;

namespace PokeCorner.App.Overlay;

public partial class ProgressPanelWindow : System.Windows.Window
{
    // Must match the Width/Height and StrokeThickness of the gauge Ellipses in the XAML.
    private const double GaugeDiameter = 36;
    private const double GaugeStrokeThickness = 4.5;

    /// The length of one full turn of the arc, expressed in StrokeDashArray's units — which
    /// are multiples of StrokeThickness, not pixels. The stroke is centred on the ellipse's
    /// path, so the drawn circle's radius is (diameter - thickness) / 2.
    private static readonly double DashUnitsPerTurn =
        Math.PI * (GaugeDiameter - GaugeStrokeThickness) / GaugeStrokeThickness;

    /// Gap between the panel and the pet, and the margin kept from a screen edge when the
    /// panel has to be clamped against one.
    private const double Gap = 6;

    public ProgressPanelWindow()
    {
        InitializeComponent();
    }

    public void UpdateProgress(ProgressInfo progress)
    {
        SetGauge(CatchArc, progress.CatchFraction);
        SetGauge(TreatArc, progress.TreatFraction);

        // The evolution ring is the one gauge whose "full" is ambiguous, so it renders through
        // the shared display helper: empty and dimmed for a species that never evolves, full
        // for one that reached its final form, and the tooltip says which.
        SetGauge(EvolutionArc, EvolutionProgressDisplay.BarValue(progress.EvolutionFraction, progress.EvolutionState));
        EvolutionGauge.Opacity = EvolutionProgressDisplay.BarOpacity(progress.EvolutionState);
        EvolutionGauge.ToolTip = EvolutionProgressDisplay.ToolTip(progress.EvolutionFraction, progress.EvolutionState);
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

    /// Positions the panel immediately to the LEFT of the overlay sprite, vertically centered
    /// on it — called whenever the overlay is repositioned (corner/monitor/size settings).
    ///
    /// Renamed from PositionBelow along with the layout change: the panel is a vertical strip
    /// beside the pet now, and a method still called "below" would be actively misleading at
    /// its three call sites in App.ReconcileSettingsState/ApplyPetSizeAndPosition.
    public void PositionLeftOf(OverlayWindow overlay)
    {
        // SpriteBounds, not the overlay's own Left/Top/Width/Height: the overlay window is
        // deliberately larger than the pet (headroom so a scale-up reaction isn't clipped),
        // and anchoring to the window rect would leave a visible gap of exactly that padding
        // between the panel and the sprite it is supposed to sit against.
        var pet = overlay.SpriteBounds;

        // Width comes from the XAML (SizeToContent is Height only), so this never assigns it.
        Left = pet.Left - Width - Gap;

        // ActualHeight is 0 until the first layout pass, and the vertical centering below
        // needs it. App's ReconcileSettingsState calls this again immediately after Show()
        // — and on every growth tick after that — so the real value always lands shortly.
        if (ActualHeight <= 0)
        {
            Top = pet.Top;
            OverlayWindow.KeepAboveAppsBelowShell(this);
            return;
        }

        Top = pet.Top + (pet.Height - ActualHeight) / 2;

        // Clamp to the taskbar-aware working area — the same region OverlayWindow.PositionAt
        // anchors the sprite to, and with the same physical-pixels-vs-DIPs care.
        //
        // Both directions matter here. Screen.FromPoint takes PHYSICAL pixels, so the overlay's
        // DIP Left/Top has to be scaled UP to pick the right monitor (at 150% the raw DIPs
        // named a point two-thirds of the way in, which lands on the wrong monitor in a
        // multi-monitor setup and, worse, silently succeeds on a single one). And WorkingArea
        // comes back in physical pixels, so it has to come back DOWN to DIPs before it is
        // compared against Left/Top/Width below — otherwise every clamp here would shove the
        // panel to a coordinate 1.5x past the edge it was supposed to be held inside.
        var dpi = OverlayWindow.DpiFor(this);
        var bounds = OverlayWindow.ToDips(System.Windows.Forms.Screen
            .FromPoint(new System.Drawing.Point((int)(pet.Left * dpi.DpiScaleX), (int)(pet.Top * dpi.DpiScaleY)))
            .WorkingArea, dpi);

        // The left-edge case is this layout's counterpart to the old below/above flip: with
        // the pet parked in a LEFT screen corner there is no room beside it on that side, so
        // the panel goes to its right instead of running off-screen.
        if (Left < bounds.Left)
        {
            Left = pet.Right + Gap;
        }
        // ...and if the pet is wide enough (a large pet size) that neither side fits, clamp
        // rather than leaving it hanging off an edge. Left wins the tie-break: clamping to
        // the right edge first and then to the left leaves it on-screen either way.
        if (Left + Width > bounds.Right)
        {
            Left = bounds.Right - Width;
        }
        if (Left < bounds.Left)
        {
            Left = bounds.Left;
        }

        // Vertical centering can push the panel past the top or bottom of the working area
        // when the pet sits in a corner and the panel is taller than the sprite.
        if (Top + ActualHeight > bounds.Bottom)
        {
            Top = bounds.Bottom - ActualHeight;
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
