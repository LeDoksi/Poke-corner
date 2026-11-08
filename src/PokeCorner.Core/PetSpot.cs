namespace PokeCorner.Core;

/// Where the player left the dragged pet, pinned to a MONITOR instead of to the virtual desktop.
///
/// A bare (x, y) on the virtual desktop is not stable on a multi-monitor PC: after a reboot the
/// second monitor can attach seconds after autostart, and a fullscreen game that switches the
/// primary monitor's resolution shifts every other monitor's origin. In both cases the old
/// absolute spot either lands nowhere (pet falls back to the primary corner) or on the wrong
/// monitor (Georgy 08.11: "каждый раз при перезагрузке покемон снова на экране 1", "за игру
/// тоже переносится на экран 1"). Remembering WHICH monitor plus the offset inside it survives both.
///
/// DeviceName and the monitor rect are what System.Windows.Forms.Screen reports (physical
/// pixels); OffsetX/OffsetY are the pet WINDOW's origin minus that monitor's working-area origin,
/// in DIPs — the same units as Window.Left/Top.
public sealed record PetSpot(string DeviceName, int MonitorX, int MonitorY, int MonitorWidth, int MonitorHeight,
    int OffsetX, int OffsetY);

/// One connected monitor as the matcher sees it: name and full bounds, physical pixels.
public readonly record struct MonitorDescriptor(string DeviceName, int X, int Y, int Width, int Height);

public static class PetSpotMatcher
{
    /// Index of the monitor `spot` belongs to, or -1 when that monitor is not connected (yet).
    ///
    /// Order matters. The same name AND the same rect is certainly it. The same rect alone still
    /// means "the screen in that place" even if Windows renumbered the DISPLAYn names after a
    /// reboot. The same name alone covers a resolution change (a game on that very monitor) or a
    /// shifted layout (a game changed the OTHER monitor's resolution). Nothing matching means the
    /// monitor is not attached — the caller keeps the pet parked and retries later rather than
    /// forgetting the spot.
    public static int FindMonitor(PetSpot spot, IReadOnlyList<MonitorDescriptor> monitors)
    {
        bool SameRect(MonitorDescriptor m) => m.X == spot.MonitorX && m.Y == spot.MonitorY
            && m.Width == spot.MonitorWidth && m.Height == spot.MonitorHeight;
        bool SameName(MonitorDescriptor m) => string.Equals(m.DeviceName, spot.DeviceName, StringComparison.OrdinalIgnoreCase);

        for (var i = 0; i < monitors.Count; i++) if (SameName(monitors[i]) && SameRect(monitors[i])) return i;
        for (var i = 0; i < monitors.Count; i++) if (SameRect(monitors[i])) return i;
        for (var i = 0; i < monitors.Count; i++) if (SameName(monitors[i])) return i;
        return -1;
    }
}
