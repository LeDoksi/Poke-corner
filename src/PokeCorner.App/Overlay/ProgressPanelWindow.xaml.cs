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
        Width = 100;
        Left = overlay.Left + (overlay.Width - Width) / 2;
        Top = overlay.Top + overlay.Height + 4;
    }
}
