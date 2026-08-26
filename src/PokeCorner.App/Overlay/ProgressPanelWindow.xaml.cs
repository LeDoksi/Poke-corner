using PokeCorner.App.Popup;

namespace PokeCorner.App.Overlay;

public partial class ProgressPanelWindow : System.Windows.Window
{
    public ProgressPanelWindow()
    {
        InitializeComponent();
    }

    public void UpdateProgress(ProgressInfo progress)
    {
        CatchBar.Value = progress.CatchFraction;
        EvolutionBar.Value = progress.EvolutionFraction;
        TreatBar.Value = progress.TreatFraction;
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
    }
}
