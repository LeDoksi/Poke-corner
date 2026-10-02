using PokeCorner.App.Fullscreen;

namespace PokeCorner.App.Tests.Fullscreen;

public class FullscreenDetectorTests
{
    private static readonly ScreenRect Monitor = new(0, 0, 1920, 1080);

    [Fact]
    public void IsFullscreen_WindowExactlyMatchesMonitorAndHasNoBorder_ReturnsTrue()
    {
        var window = new ScreenRect(0, 0, 1920, 1080);

        Assert.True(FullscreenDetector.IsFullscreen(window, Monitor, windowHasBorder: false));
    }

    [Fact]
    public void IsFullscreen_WindowHasBorder_ReturnsFalseEvenIfSameSize()
    {
        var window = new ScreenRect(0, 0, 1920, 1080);

        Assert.False(FullscreenDetector.IsFullscreen(window, Monitor, windowHasBorder: true));
    }

    [Fact]
    public void IsFullscreen_WindowSmallerThanMonitor_ReturnsFalse()
    {
        var window = new ScreenRect(0, 0, 1280, 800);

        Assert.False(FullscreenDetector.IsFullscreen(window, Monitor, windowHasBorder: false));
    }

    [Fact]
    public void IsFullscreen_WithinSubPixelTolerance_StillReturnsTrue()
    {
        var window = new ScreenRect(0.2, -0.1, 1919.9, 1080.1);

        Assert.True(FullscreenDetector.IsFullscreen(window, Monitor, windowHasBorder: false));
    }
}
