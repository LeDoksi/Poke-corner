using PokeCorner.App.Popup;

namespace PokeCorner.App.Overlay;

public partial class ProgressPanelWindow : System.Windows.Window
{
    /// Must match the Height of the bar Grids in the XAML — the fill is a bottom-anchored
    /// Border sized as a fraction of this, so the two numbers have to agree.
    private const double BarHeight = 46;

    /// Gap between the panel and the pet, and the margin kept from a screen edge when the
    /// panel has to be clamped against one.
    private const double Gap = 6;

    public ProgressPanelWindow()
    {
        InitializeComponent();
    }

    public void UpdateProgress(ProgressInfo progress)
    {
        SetBar(CatchFill, progress.CatchFraction);
        SetBar(EvolutionFill, progress.EvolutionFraction);
        SetBar(TreatFill, progress.TreatFraction);
    }

    /// Grows the fill upward from the floor of its track. Plain Height on a bottom-aligned
    /// Border rather than a ScaleTransform: a scaled Border would squash its CornerRadius
    /// along with it, so an almost-empty bar would end up with visibly flattened ends.
    private static void SetBar(System.Windows.Controls.Border fill, double fraction) =>
        fill.Height = BarHeight * Math.Clamp(fraction, 0, 1);

    /// Positions the panel immediately to the LEFT of the overlay sprite, vertically centered
    /// on it — called whenever the overlay is repositioned (corner/monitor/size settings).
    ///
    /// Renamed from PositionBelow along with the layout change: the panel is a vertical strip
    /// beside the pet now, and a method still called "below" would be actively misleading at
    /// its three call sites in App.ReconcileSettingsState/ApplyPetSizeAndPosition.
    public void PositionLeftOf(OverlayWindow overlay)
    {
        // Width comes from the XAML (SizeToContent is Height only), so this never assigns it.
        Left = overlay.Left - Width - Gap;

        // ActualHeight is 0 until the first layout pass, and the vertical centering below
        // needs it. App's ReconcileSettingsState calls this again immediately after Show()
        // — and on every growth tick after that — so the real value always lands shortly.
        if (ActualHeight <= 0)
        {
            Top = overlay.Top;
            OverlayWindow.KeepAboveAppsBelowShell(this);
            return;
        }

        Top = overlay.Top + (overlay.Height - ActualHeight) / 2;

        // Clamp to the taskbar-aware working area — the same region OverlayWindow.PositionAt
        // anchors the sprite to.
        var bounds = System.Windows.Forms.Screen
            .FromPoint(new System.Drawing.Point((int)overlay.Left, (int)overlay.Top))
            .WorkingArea;

        // The left-edge case is this layout's counterpart to the old below/above flip: with
        // the pet parked in a LEFT screen corner there is no room beside it on that side, so
        // the panel goes to its right instead of running off-screen.
        if (Left < bounds.Left)
        {
            Left = overlay.Left + overlay.Width + Gap;
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
