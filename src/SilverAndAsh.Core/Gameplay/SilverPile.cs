using System.Numerics;
using SilverAndAsh.Core.Physics;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// A few silver coins lying in the level - dropped by a killed enemy today, later also by chests. Silver is the
/// currency of a run (decision records 0005 and 0006): it falls to the floor and waits there until a living player
/// walks over it; that player's <see cref="Player.Silver"/> goes up by <see cref="Amount"/>. Unlike a consumable it
/// is always taken.
/// </summary>
public sealed class SilverPile
{
    private Vector3 _velocity;

    /// <summary>Creates a pile worth <paramref name="amount"/> at <paramref name="position"/> (the bottom of its body; it falls from there).</summary>
    public SilverPile(int id, int amount, Vector3 position)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        Id = id;
        Amount = amount;
        Position = position;
        PreviousPosition = position;
    }

    public int Id { get; }

    /// <summary>How much silver it is worth.</summary>
    public int Amount { get; }

    /// <summary>Bottom centre of its body in world coordinates.</summary>
    public Vector3 Position { get; private set; }

    /// <summary>The position one tick earlier, for smooth drawing between ticks.</summary>
    public Vector3 PreviousPosition { get; private set; }

    /// <summary>True once a player picked it up; it is removed from the world then.</summary>
    public bool IsTaken { get; internal set; }

    /// <summary>Its body, for touching players (as big as a consumable).</summary>
    public CylinderBody Body => new(Position, BodySizes.Pickup.Radius, BodySizes.Pickup.Height);

    /// <summary>One tick of falling (with <paramref name="gravity"/>) until it lies on the floor.</summary>
    public void Fall(World.Level level, float gravity)
    {
        ArgumentNullException.ThrowIfNull(level);
        PreviousPosition = Position;
        (Position, _velocity, _) = CharacterCollision.MoveVertically(
            level, Position, _velocity, BodySizes.Pickup.Radius, BodySizes.Pickup.Height, gravity, SimulationClock.SecondsPerTick);
    }
}
