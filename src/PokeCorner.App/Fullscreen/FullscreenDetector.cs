namespace PokeCorner.App.Fullscreen;

public readonly record struct ScreenRect(double X, double Y, double Width, double Height);

public static class FullscreenDetector
{
    private const double ToleranceScreenUnits = 0.5;

    public static bool IsFullscreen(ScreenRect windowRect, ScreenRect monitorRect, bool windowHasBorder)
    {
        if (windowHasBorder) return false;

        return Math.Abs(windowRect.X - monitorRect.X) < ToleranceScreenUnits
            && Math.Abs(windowRect.Y - monitorRect.Y) < ToleranceScreenUnits
            && Math.Abs(windowRect.Width - monitorRect.Width) < ToleranceScreenUnits
            && Math.Abs(windowRect.Height - monitorRect.Height) < ToleranceScreenUnits;
    }
}
