using PokeCorner.App.Overlay;

namespace PokeCorner.App.Tests.Overlay;

public class ZSlotTests
{
    /// The final-review flip-flop: once the fallback has put the pet at the top of the topmost
    /// band, the taskbar is again the last topmost window. Re-slotting behind it would drop the
    /// flag and fire the fallback again, twice a second, forever.
    [Fact]
    public void ChooseZSlot_TaskbarIsLastTopmostAndWeAreAlreadyTopmost_LeavesAsIs()
    {
        Assert.Equal(ZSlot.LeaveAsIs, OverlayWindow.ChooseZSlot(taskbarFound: true, foreignTopmostBelowTaskbar: false, alreadyTopmost: true));
    }

    [Fact]
    public void ChooseZSlot_TaskbarIsLastTopmostAndWeLostTheFlag_GoesToTopmostBand()
    {
        Assert.Equal(ZSlot.TopOfTopmostBand, OverlayWindow.ChooseZSlot(taskbarFound: true, foreignTopmostBelowTaskbar: false, alreadyTopmost: false));
    }

    /// The normal case the original design relies on: the slot behind the taskbar is still
    /// inside the topmost band, so the pet stays topmost AND below the shell.
    [Fact]
    public void ChooseZSlot_ForeignTopmostBelowTaskbar_SlotsBehindTaskbar()
    {
        Assert.Equal(ZSlot.BehindTaskbar, OverlayWindow.ChooseZSlot(taskbarFound: true, foreignTopmostBelowTaskbar: true, alreadyTopmost: true));
        Assert.Equal(ZSlot.BehindTaskbar, OverlayWindow.ChooseZSlot(taskbarFound: true, foreignTopmostBelowTaskbar: true, alreadyTopmost: false));
    }

    [Fact]
    public void ChooseZSlot_NoTaskbar_KeepsOrRestoresTopmost()
    {
        Assert.Equal(ZSlot.LeaveAsIs, OverlayWindow.ChooseZSlot(taskbarFound: false, foreignTopmostBelowTaskbar: false, alreadyTopmost: true));
        Assert.Equal(ZSlot.TopOfTopmostBand, OverlayWindow.ChooseZSlot(taskbarFound: false, foreignTopmostBelowTaskbar: false, alreadyTopmost: false));
    }
}
