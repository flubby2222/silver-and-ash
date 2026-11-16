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

    // The left click that captures the mouse must not also swing the blade.
    private readonly CaptureClickFilter _captureClick = new();

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
        _captureClick.MouseCaptured();
    }

    /// <summary>Releases the mouse (normal cursor again, the view stays still).</summary>
    public void ReleaseMouse()
    {
        Raylib.EnableCursor();
        IsMouseCaptured = false;
        _ignoreNextMouseDelta = true;
    }

    /// <summary>
    /// Reads the input of the current frame. Whether the mouse is captured decides the game (captured while
    /// playing; free on the title screen, in the Esc menu and while a bonus is chosen).
    /// <paramref name="keysWithFreeMouse"/>: the mouse is free for a window in the running game (the bonus choice) -
    /// keys still count (you walk on while choosing), the mouse neither turns the view nor attacks.
    /// </summary>
    public PlayerInput Read(bool keysWithFreeMouse = false)
    {
        var look = Vector2.Zero;
        if (IsMouseCaptured && !_ignoreNextMouseDelta)
        {
            look = Raylib.GetMouseDelta();
        }

        _ignoreNextMouseDelta = false;

        if (!IsMouseCaptured && keysWithFreeMouse)
        {
            return new PlayerInput(
                ReadMoveKeys(), Vector2.Zero,
                Jump: Raylib.IsKeyDown(KeyboardKey.Space),
                Dash: Raylib.IsKeyDown(KeyboardKey.LeftShift),
                Interact: Raylib.IsKeyPressed(KeyboardKey.F),
                Reload: Raylib.IsKeyDown(KeyboardKey.R));
        }

        // With the mouse released the player is "away from the game": no walking or jumping either.
        if (!IsMouseCaptured)
        {
            return PlayerInput.None;
        }

        return new PlayerInput(
            ReadMoveKeys(),
            look,
            Jump: Raylib.IsKeyDown(KeyboardKey.Space),
            Dash: Raylib.IsKeyDown(KeyboardKey.LeftShift),
            Melee: _captureClick.Filter(Raylib.IsMouseButtonDown(MouseButton.Left)),
            Shoot: Raylib.IsMouseButtonDown(MouseButton.Right),
            Interact: Raylib.IsKeyPressed(KeyboardKey.F),
            Reload: Raylib.IsKeyDown(KeyboardKey.R),
            SelectSlot: ReadSlotKeys(),
            CycleSlot: ReadWheel());
    }

    // 1 / 2 / 3 pressed this frame: that ranged weapon place (0, 1, 2).
    private static int? ReadSlotKeys() =>
        Raylib.IsKeyPressed(KeyboardKey.One) ? 0
        : Raylib.IsKeyPressed(KeyboardKey.Two) ? 1
        : Raylib.IsKeyPressed(KeyboardKey.Three) ? 2
        : null;

    // Mouse wheel this frame: towards you (down) = the next weapon, away (up) = the one before.
    private static int ReadWheel()
    {
        var wheel = Raylib.GetMouseWheelMove();
        return wheel < 0f ? 1 : wheel > 0f ? -1 : 0;
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
