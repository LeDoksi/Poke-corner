using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using PokeCorner.App.Overlay;
using PokeCorner.App.Tests.Theme;
using Xunit;

namespace PokeCorner.App.Tests.Overlay;

/// POKE-9 root cause, confirmed live on 2026-10-02: ShowInTaskbar="False" makes WPF give the
/// window a hidden, NON-topmost owner, and an owned window's z-order follows its owner — the pet
/// kept WS_EX_TOPMOST and still sat under ordinary Chrome windows. Detaching the owner on the
/// running app put it straight back above them. So: no owner, and WS_EX_TOOLWINDOW (not the
/// owner trick) keeps these windows off the taskbar and out of Alt+Tab.
public class OverlayOwnershipTests
{
    [Fact]
    public void OverlayWindow_HasNoOwnerAndIsAToolWindow() =>
        AssertUnownedToolWindow(() => new OverlayWindow());

    [Fact]
    public void ProgressPanelWindow_HasNoOwnerAndIsAToolWindow() =>
        AssertUnownedToolWindow(() => new ProgressPanelWindow());

    private static void AssertUnownedToolWindow(Func<Window> create)
    {
        WpfTestHost.Run(() =>
        {
            var window = create();
            try
            {
                var hwnd = new WindowInteropHelper(window).EnsureHandle();

                Assert.Equal(IntPtr.Zero, GetWindow(hwnd, GW_OWNER));
                Assert.NotEqual(0, GetWindowLong(hwnd, GWL_EXSTYLE) & WS_EX_TOOLWINDOW);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private const uint GW_OWNER = 4;
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
}
