using System.Numerics;
using Raylib_cs;

namespace SilverAndAsh.Game.Input;

/// <summary>
/// Alt + Enter switches between the window and borderless full screen in the desktop's resolution (issue #350).
/// Borderless: a window without frame as large as the monitor - no change of the monitor's mode, so switching is quick
/// and other windows stay where they were. Pure presentation, never tick input.
/// </summary>
/// <remarks>
/// Not Raylib's own <c>ToggleBorderlessWindowed</c> (issue #428): its window is "always on top" (WS_EX_TOPMOST) and stays
/// so even after <c>ClearWindowState(TopmostWindow)</c> - so Alt+Tab gave the focus to another window, which stayed hidden
/// behind the game. Here it stays a normal window without frame, as large as the monitor.
/// </remarks>
internal static class FullScreenToggle
{
    // Where the window was and how large, to come back to it from full screen.
    private static Vector2 _windowPosition;
    private static int _windowWidth;
    private static int _windowHeight;

    /// <summary>True while an Alt key is held - Enter then belongs to the switch, not to a menu.</summary>
    public static bool AltHeld => Raylib.IsKeyDown(KeyboardKey.LeftAlt) || Raylib.IsKeyDown(KeyboardKey.RightAlt);

    /// <summary>True while the game shows borderless full screen, false in a window (the game uses no frameless window else).</summary>
    public static bool IsFullScreen => Raylib.IsWindowState(ConfigFlags.UndecoratedWindow);

    /// <summary>Switches to full screen or to the window (the settings menu, issue #349); true if that changed anything.</summary>
    public static bool Set(bool fullScreen)
    {
        if (fullScreen == IsFullScreen)
        {
            return false;
        }

        Toggle();
        return true;
    }

    /// <summary>Switches if Alt + Enter was pressed this frame; true if it did.</summary>
    public static bool Update()
    {
        if (!AltHeld || !(Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter)))
        {
            return false;
        }

        Toggle();
        return true;
    }

    private static void Toggle()
    {
        if (IsFullScreen)
        {
            // Back to the window's old size and place; it stays freely resizable.
            Raylib.ClearWindowState(ConfigFlags.UndecoratedWindow);
            Raylib.SetWindowSize(_windowWidth, _windowHeight);
            Raylib.SetWindowPosition((int)_windowPosition.X, (int)_windowPosition.Y);
            return;
        }

        _windowPosition = Raylib.GetWindowPosition();
        _windowWidth = Raylib.GetScreenWidth();
        _windowHeight = Raylib.GetScreenHeight();

        var monitor = Raylib.GetCurrentMonitor();
        var monitorPosition = Raylib.GetMonitorPosition(monitor);
        Raylib.SetWindowState(ConfigFlags.UndecoratedWindow);
        Raylib.SetWindowPosition((int)monitorPosition.X, (int)monitorPosition.Y);
        Raylib.SetWindowSize(Raylib.GetMonitorWidth(monitor), Raylib.GetMonitorHeight(monitor));
    }
}
