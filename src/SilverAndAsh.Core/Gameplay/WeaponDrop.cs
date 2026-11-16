using System.Numerics;
using SilverAndAsh.Core.Physics;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// A weapon lying in the level - dropped from the inventory (right click, issue #193). It falls to the floor and stays
/// there. Taking it up again with F comes with the loot system (#194); walking over it does nothing.
/// </summary>
public sealed class WeaponDrop
{
    private Vector3 _velocity;

    /// <summary>Creates <paramref name="kind"/> lying at <paramref name="position"/> (the bottom of its body; it falls from there); a ranged one as <paramref name="ranged"/>, with its magazine.</summary>
    public WeaponDrop(int id, WeaponKind kind, Vector3 position, RangedWeapon? ranged = null)
    {
        if (ranged is not null && ranged.Kind != kind)
        {
            throw new ArgumentException($"The weapon {ranged.Kind} is no {kind}.", nameof(ranged));
        }

        _ = Weapons.Info(kind);
        Ranged = ranged;
        Id = id;
        Kind = kind;
        Position = position;
        PreviousPosition = position;
    }

    public int Id { get; }

    public WeaponKind Kind { get; }

    /// <summary>A ranged weapon as it was dropped, with its magazine (null for a melee weapon) - taken up again with #194.</summary>
    public RangedWeapon? Ranged { get; }

    /// <summary>Its name, hand and tier.</summary>
    public WeaponInfo Info => Weapons.Info(Kind);

    /// <summary>Bottom centre of its body in world coordinates.</summary>
    public Vector3 Position { get; private set; }

    /// <summary>The position one tick earlier, for smooth drawing between ticks.</summary>
    public Vector3 PreviousPosition { get; private set; }

    /// <summary>One tick of falling (with <paramref name="gravity"/>) until it lies on the floor.</summary>
    public void Fall(World.Level level, float gravity)
    {
        ArgumentNullException.ThrowIfNull(level);
        PreviousPosition = Position;
        (Position, _velocity, _) = CharacterCollision.MoveVertically(
            level, Position, _velocity, BodySizes.Pickup.Radius, BodySizes.Pickup.Height, gravity, SimulationClock.SecondsPerTick);
    }
}
