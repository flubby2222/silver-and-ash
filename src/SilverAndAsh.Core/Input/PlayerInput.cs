using System.Numerics;

namespace SilverAndAsh.Core.Input;

/// <summary>
/// What the player wants to do, independent of keyboard, mouse or gamepad.
/// The Game project fills this from Raylib; Core only ever sees this struct.
/// </summary>
/// <param name="Move">
/// Desired walking direction relative to the view: X = right (+) / left (-), Y = forward (+) / back (-).
/// Each component between -1 and 1.
/// </param>
/// <param name="Look">Mouse movement since the last frame in pixels: X = right (+), Y = down (+).</param>
/// <param name="Jump">True while the jump button is held.</param>
/// <param name="Dash">True while the dash button is held.</param>
public readonly record struct PlayerInput(Vector2 Move, Vector2 Look, bool Jump, bool Dash = false)
{
    /// <summary>No input at all.</summary>
    public static PlayerInput None { get; } = new(Vector2.Zero, Vector2.Zero, false);
}
