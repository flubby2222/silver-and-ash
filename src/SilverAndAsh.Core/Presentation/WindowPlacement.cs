namespace SilverAndAsh.Core.Presentation;

/// <summary>A rectangle on the desktop in pixels: top-left corner plus size. Used for monitors and the window.</summary>
public readonly record struct DesktopRect(int X, int Y, int Width, int Height)
{
    /// <summary>True if the point lies inside (left and top edges count, right and bottom do not).</summary>
    public bool Contains(int x, int y) => x >= X && x < X + Width && y >= Y && y < Y + Height;
}

/// <summary>
/// Makes sure the game window can be seen. Found while testing v0.3.0: started on a monitor left of the
/// main monitor (negative desktop coordinates), Raylib/GLFW put the window far outside every monitor
/// (x = 32767) - invisible, while the game had already captured the mouse.
/// </summary>
public static class WindowPlacement
{
    /// <summary>
    /// Where the window has to move so it can be seen, or null if it is fine where it is.
    /// The window counts as visible when its centre lies on any monitor. Otherwise it is centred on
    /// <paramref name="mainMonitor"/>, but never further up or left than that monitor's corner,
    /// so the title bar stays reachable even if the window is bigger than the monitor.
    /// </summary>
    public static (int X, int Y)? CorrectedPosition(DesktopRect window, IReadOnlyList<DesktopRect> monitors, DesktopRect mainMonitor)
    {
        ArgumentNullException.ThrowIfNull(monitors);

        // In long: the broken positions are close to int.MaxValue, adding half the width would overflow.
        var centreX = (int)Math.Clamp((long)window.X + window.Width / 2, int.MinValue, int.MaxValue);
        var centreY = (int)Math.Clamp((long)window.Y + window.Height / 2, int.MinValue, int.MaxValue);
        if (monitors.Any(monitor => monitor.Contains(centreX, centreY)))
        {
            return null;
        }

        var x = mainMonitor.X + Math.Max(0, (mainMonitor.Width - window.Width) / 2);
        var y = mainMonitor.Y + Math.Max(0, (mainMonitor.Height - window.Height) / 2);
        return (x, y);
    }
}
