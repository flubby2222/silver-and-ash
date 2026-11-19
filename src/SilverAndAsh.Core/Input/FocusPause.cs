namespace SilverAndAsh.Core.Input;

/// <summary>
/// What a frame of the running game does when its window loses the focus, e.g. with Alt+Tab (issue #428): the Esc menu
/// opens, so the run holds (the tick gets <c>Paused</c>) and the mouse is free for the other window. Back in the game the
/// menu stays open until the player resumes. Pure rules, so they can be checked without a window.
/// </summary>
public static class FocusPause
{
    /// <summary>
    /// Whether the Esc menu is open after this frame: an open menu stays open, a window without focus opens it.
    /// </summary>
    public static bool MenuOpen(bool menuOpen, bool windowFocused) => menuOpen || !windowFocused;

    /// <summary>
    /// Whether the window captures the mouse: only while a run is played - not in the Esc menu, on the end screen, on the
    /// title screen or while an overlay (bonus choice, the player's menu) wants the cursor.
    /// </summary>
    public static bool CapturesMouse(bool menuOpen, bool onEndScreen, bool onTitle, bool overlayOpen) =>
        !menuOpen && !onEndScreen && !onTitle && !overlayOpen;
}
