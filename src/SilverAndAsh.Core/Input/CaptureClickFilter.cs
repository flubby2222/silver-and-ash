namespace SilverAndAsh.Core.Input;

/// <summary>
/// The click that captures the mouse must not also swing the blade (both are the left mouse button).
/// After capturing, the button counts as "not pressed" until it has been let go once.
/// </summary>
public sealed class CaptureClickFilter
{
    private bool _waitingForRelease;

    /// <summary>Call when the mouse has just been captured by a click.</summary>
    public void MouseCaptured() => _waitingForRelease = true;

    /// <summary>
    /// Returns what the game should see of the button this frame: false while the capture click is still held.
    /// </summary>
    public bool Filter(bool buttonDown)
    {
        if (!buttonDown)
        {
            _waitingForRelease = false;
        }

        return buttonDown && !_waitingForRelease;
    }
}
