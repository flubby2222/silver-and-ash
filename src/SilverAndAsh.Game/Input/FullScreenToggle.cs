using Raylib_cs;

namespace SilverAndAsh.Game.Input;

/// <summary>
/// Alt + Enter switches between the window and borderless full screen in the desktop's resolution (issue #350).
/// Borderless: a window without frame as large as the monitor - no change of the monitor's mode, so switching is quick
/// and other windows stay where they were. Pure presentation, never tick input.
/// </summary>
internal static class FullScreenToggle
{
    /// <summary>True while an Alt key is held - Enter then belongs to the switch, not to a menu.</summary>
    public static bool AltHeld => Raylib.IsKeyDown(KeyboardKey.LeftAlt) || Raylib.IsKeyDown(KeyboardKey.RightAlt);

    /// <summary>True while the game shows borderless full screen, false in a window.</summary>
    public static bool IsFullScreen => Raylib.IsWindowState(ConfigFlags.BorderlessWindowMode);

    /// <summary>Switches to full screen or to the window (the settings menu, issue #349); true if that changed anything.</summary>
    public static bool Set(bool fullScreen)
    {
        if (fullScreen == IsFullScreen)
        {
            return false;
        }

        Raylib.ToggleBorderlessWindowed();
        return true;
    }

    /// <summary>Switches if Alt + Enter was pressed this frame; true if it did.</summary>
    public static bool Update()
    {
        if (!AltHeld || !(Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter)))
        {
            return false;
        }

        // Back in the window, Raylib restores its old size and position; it stays freely resizable.
        Raylib.ToggleBorderlessWindowed();
        return true;
    }
}
