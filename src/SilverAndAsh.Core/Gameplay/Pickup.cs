using System.Numerics;
using SilverAndAsh.Core.Physics;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// The kinds of consumables: they work the moment they are picked up and cannot be kept for later
/// (decision record 0004 - items and weapons are something else and come later).
/// </summary>
public enum ConsumableKind
{
    /// <summary>Heals (<see cref="BloodVialBalance.Heal"/>).</summary>
    BloodVial,
}

/// <summary>
/// A consumable lying in the level, dropped by a killed enemy. It falls to the floor and waits there until a living
/// player walks over it (and can use it - a blood vial stays while the player's health is full).
/// </summary>
public sealed class Pickup
{
    private Vector3 _velocity;

    /// <summary>Creates a pickup at <paramref name="position"/> (the bottom of its body; it falls from there).</summary>
    public Pickup(int id, ConsumableKind kind, Vector3 position)
    {
        Id = id;
        Kind = kind;
        Position = position;
        PreviousPosition = position;
    }

    public int Id { get; }

    public ConsumableKind Kind { get; }

    /// <summary>Bottom centre of its body in world coordinates.</summary>
    public Vector3 Position { get; private set; }

    /// <summary>The position one tick earlier, for smooth drawing between ticks.</summary>
    public Vector3 PreviousPosition { get; private set; }

    /// <summary>True once a player picked it up; it is removed from the world then.</summary>
    public bool IsTaken { get; internal set; }

    /// <summary>Its body, for touching players.</summary>
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
