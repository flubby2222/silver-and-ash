using System.Numerics;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>The player character: where it stands, how it moves and where it looks.</summary>
public sealed class Player
{
    private readonly Level _level;

    /// <summary>Creates the player standing on the start cell of <paramref name="level"/>.</summary>
    public Player(Level level)
    {
        ArgumentNullException.ThrowIfNull(level);
        _level = level;
        Position = level.PlayerStartPosition;
        PreviousPosition = Position;
    }

    /// <summary>Position of the feet in world coordinates (metres) after the latest tick.</summary>
    public Vector3 Position { get; private set; }

    /// <summary>Position of the feet one tick earlier. Needed for smooth drawing between ticks.</summary>
    public Vector3 PreviousPosition { get; private set; }

    /// <summary>Current movement in metres per second.</summary>
    public Vector3 Velocity { get; private set; }

    /// <summary>Where the player is looking.</summary>
    public FirstPersonView View { get; } = new();

    /// <summary>Position of the eyes after the latest tick.</summary>
    public Vector3 EyePosition => Position + EyeOffset;

    private static Vector3 EyeOffset => new(0f, Tuning.Player.EyeHeight, 0f);

    /// <summary>
    /// Applies the look part of the input. Called once per drawn frame (not per tick),
    /// so turning the view feels immediate even at high frame rates.
    /// </summary>
    public void Look(PlayerInput input) => View.ApplyMouse(input.Look);

    /// <summary>True while the feet are on the ground. Jumping is only possible from here.</summary>
    public bool IsGrounded { get; private set; } = true;

    // Was the jump button already held in the previous tick? A jump needs a fresh press,
    // so holding the button down does not make the player hop again and again.
    private bool _jumpWasHeld;

    /// <summary>
    /// Advances the player by one simulation tick: walking (with less control in the air), jumping,
    /// gravity, walls, floor and ceiling.
    /// </summary>
    public void Tick(PlayerInput input)
    {
        const float Dt = SimulationClock.SecondsPerTick;
        PreviousPosition = Position;

        // Horizontal: accelerate towards the wished velocity - in the air only with part of the strength.
        var acceleration = Tuning.Player.Acceleration * (IsGrounded ? 1f : Tuning.Player.AirControl);
        var targetVelocity = WishDirection(input) * Tuning.Player.MaxSpeed;
        var horizontal = MoveTowards(new Vector3(Velocity.X, 0f, Velocity.Z), targetVelocity, acceleration * Dt);

        // Vertical: jump on a fresh press while standing. Gravity is applied in MoveVertically.
        var vertical = Velocity.Y;
        var jumpPressed = input.Jump && !_jumpWasHeld;
        _jumpWasHeld = input.Jump;
        if (jumpPressed && IsGrounded)
        {
            vertical = Tuning.Player.JumpSpeed;
        }

        Velocity = horizontal with { Y = vertical };

        // Walls first (sideways), then gravity, floor and ceiling (up and down).
        (Position, Velocity) = CharacterCollision.Move(_level, Position, Velocity, Tuning.Player.Radius, Dt);
        (Position, Velocity, var grounded) = CharacterCollision.MoveVertically(
            _level, Position, Velocity, Tuning.Player.Radius, Tuning.Player.Height, Tuning.Player.Gravity, Dt);
        IsGrounded = grounded;
    }

    /// <summary>
    /// Eye position for drawing, blended between the previous and the latest tick.
    /// <paramref name="alpha"/> 0 = previous tick, 1 = latest tick.
    /// </summary>
    public Vector3 InterpolatedEyePosition(float alpha) =>
        Vector3.Lerp(PreviousPosition, Position, Math.Clamp(alpha, 0f, 1f)) + EyeOffset;

    /// <summary>Desired walking direction on the ground, relative to the view. Length 1 at most.</summary>
    private Vector3 WishDirection(PlayerInput input)
    {
        var direction = View.FlatForward * input.Move.Y + View.FlatRight * input.Move.X;

        // Without this, walking diagonally (W + D) would be about 41 % faster than straight.
        return direction.LengthSquared() > 1f ? Vector3.Normalize(direction) : direction;
    }

    /// <summary>Changes <paramref name="current"/> towards <paramref name="target"/> by at most <paramref name="maxChange"/>.</summary>
    private static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxChange)
    {
        var difference = target - current;
        var distance = difference.Length();
        return distance <= maxChange ? target : current + difference / distance * maxChange;
    }
}
