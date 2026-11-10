using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Input;

namespace SilverAndAsh.Game.Input;

/// <summary>
/// Translates keyboard and mouse (via Raylib) into a <see cref="PlayerInput"/> for Core,
/// and manages whether the mouse is "captured" by the window.
/// </summary>
internal sealed class RaylibInput
{
    // After capturing or releasing the mouse, Raylib reports one big jump in mouse movement.
    // Ignoring the very next frame's delta avoids the view snapping around.
    private bool _ignoreNextMouseDelta = true;

    /// <summary>
    /// True while the mouse is captured: the cursor is hidden and every movement turns the view.
    /// </summary>
    public bool IsMouseCaptured { get; private set; }

    /// <summary>Captures the mouse (hides the cursor, mouse movement turns the view).</summary>
    public void CaptureMouse()
    {
        Raylib.DisableCursor();
        IsMouseCaptured = true;
        _ignoreNextMouseDelta = true;
    }

    /// <summary>Releases the mouse (normal cursor again, the view stays still).</summary>
    public void ReleaseMouse()
    {
        Raylib.EnableCursor();
        IsMouseCaptured = false;
        _ignoreNextMouseDelta = true;
    }

    /// <summary>
    /// Reads the input of the current frame. Esc releases the mouse, a left click captures it again;
    /// losing the window focus (e.g. Alt+Tab) releases it, too.
    /// </summary>
    public PlayerInput Read()
    {
        if (IsMouseCaptured && (Raylib.IsKeyPressed(KeyboardKey.Escape) || !Raylib.IsWindowFocused()))
        {
            ReleaseMouse();
        }
        else if (!IsMouseCaptured && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            CaptureMouse();
        }

        var look = Vector2.Zero;
        if (IsMouseCaptured && !_ignoreNextMouseDelta)
        {
            look = Raylib.GetMouseDelta();
        }

        _ignoreNextMouseDelta = false;

        return new PlayerInput(ReadMoveKeys(), look, Raylib.IsKeyDown(KeyboardKey.Space));
    }

    private static Vector2 ReadMoveKeys()
    {
        var move = Vector2.Zero;
        if (Raylib.IsKeyDown(KeyboardKey.W))
        {
            move.Y += 1f;
        }

        if (Raylib.IsKeyDown(KeyboardKey.S))
        {
            move.Y -= 1f;
        }

        if (Raylib.IsKeyDown(KeyboardKey.D))
        {
            move.X += 1f;
        }

        if (Raylib.IsKeyDown(KeyboardKey.A))
        {
            move.X -= 1f;
        }

        return move;
    }
}
