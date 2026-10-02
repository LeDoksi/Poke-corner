using PokeCorner.App.Overlay;
using PokeCorner.App.Tests.Theme;

namespace PokeCorner.App.Tests.Overlay;

/// POKE-22: closing the pet window (Alt+F4 while it has focus, or the 1.0.15 taskbar button)
/// used to leave the process running with no pet — and the next launch made a second copy.
public class OverlayCloseTests
{
    [Fact]
    public void Close_WithoutAllowClose_KeepsThePetOnScreen()
    {
        WpfTestHost.Run(() =>
        {
            var window = new OverlayWindow { ShowActivated = false };
            window.Show();

            window.Close();

            Assert.True(window.IsVisible);
            window.AllowClose = true;
            window.Close();
            Assert.False(window.IsVisible);
        });
    }

    /// Same for the progress panel: closed by Alt+F4, it stayed referenced by App, and the next
    /// Show() on the closed window threw and crashed the app.
    [Fact]
    public void ProgressPanel_CloseWithoutAllowClose_StaysOpen()
    {
        WpfTestHost.Run(() =>
        {
            var window = new ProgressPanelWindow { ShowActivated = false };
            window.Show();

            window.Close();

            Assert.True(window.IsVisible);
            window.AllowClose = true;
            window.Close();
            Assert.False(window.IsVisible);
        });
    }
}
