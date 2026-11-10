using System.Numerics;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>The player character: where it stands, how it moves and where it looks.</summary>
public sealed class Player
{
    /// <summary>Creates the player standing on the start cell of <paramref name="level"/>.</summary>
    public Player(Level level)
    {
        ArgumentNullException.ThrowIfNull(level);
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

    /// <summary>Advances the player by one simulation tick: walking with acceleration and braking.</summary>
    public void Tick(PlayerInput input)
    {
        PreviousPosition = Position;

        var targetVelocity = WishDirection(input) * Tuning.Player.MaxSpeed;
        Velocity = MoveTowards(Velocity, targetVelocity, Tuning.Player.Acceleration * SimulationClock.SecondsPerTick);

        Position += Velocity * SimulationClock.SecondsPerTick;
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
