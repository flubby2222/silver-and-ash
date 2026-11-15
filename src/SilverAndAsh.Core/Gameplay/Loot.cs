using System.Numerics;
using SilverAndAsh.Core.Physics;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>What a piece of <see cref="Loot"/> is.</summary>
public enum LootKind
{
    /// <summary>Silver coins, the currency of a run (<see cref="Player.Silver"/>).</summary>
    Silver,

    /// <summary>A key: fits every lock and is used up (<see cref="Player.Keys"/>).</summary>
    Key,
}

/// <summary>
/// Something of value lying in the level - silver or a key, dropped by a killed enemy or a cleared room today, later
/// also by chests (decision records 0005 and 0006). It falls to the floor and waits there until a living player walks
/// over it; that player gets <see cref="Amount"/> of it. Unlike a consumable it is always taken.
/// </summary>
public sealed class Loot
{
    private Vector3 _velocity;

    /// <summary>Creates loot worth <paramref name="amount"/> at <paramref name="position"/> (the bottom of its body; it falls from there).</summary>
    public Loot(int id, LootKind kind, int amount, Vector3 position)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        Id = id;
        Kind = kind;
        Amount = amount;
        Position = position;
        PreviousPosition = position;
    }

    public int Id { get; }

    public LootKind Kind { get; }

    /// <summary>How much it is worth: silver coins, or keys.</summary>
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
