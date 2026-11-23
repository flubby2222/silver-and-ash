using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// A teleporter at a special place of the level (issue #436). It is unlocked the first time a player steps onto it -
/// for all players together, like the map: the level is shared, and in online co-op one player's find helps the others.
/// From an unlocked teleporter a player can travel to any other unlocked one (input <c>TeleportTo</c>), but never out of
/// a sealed room: there it rests until the fight is over.
/// </summary>
public sealed class Teleporter
{
    internal Teleporter(int id, TeleporterPlan plan, Level level)
    {
        Id = id;
        Plan = plan;
        Position = level.CellCenter(plan.X, plan.Z);
    }

    /// <summary>Its number: the place in <see cref="GameWorld.Teleporters"/>, and what <c>TeleportTo</c> names.</summary>
    public int Id { get; }

    /// <summary>Where it stands and which special place it marks.</summary>
    public TeleporterPlan Plan { get; }

    /// <summary>The middle of its cell, on the floor - where a player arrives.</summary>
    public Vector3 Position { get; }

    /// <summary>Where its show side looks, on the ground plane (Y = 0); zero if it is not turned (see <see cref="TeleporterPlan.Facing"/>).</summary>
    public Vector3 Facing => new(Plan.Facing.X, 0f, Plan.Facing.Y);

    /// <summary>True once a player has stepped onto it. It stays unlocked for the rest of the run.</summary>
    public bool IsUnlocked { get; private set; }

    /// <summary>
    /// True if the feet at <paramref name="feet"/> are within <paramref name="radius"/> of its middle on the ground plane
    /// and stand on its floor or up to <paramref name="height"/> above it (issue #480: a ledge above or a pit below does
    /// not count; a jump does).
    /// </summary>
    public bool Reaches(Vector3 feet, float radius, float height) =>
        feet.Y >= Position.Y - FloorTolerance
        && feet.Y <= Position.Y + height
        && new Vector2(feet.X - Position.X, feet.Z - Position.Z).LengthSquared() <= radius * radius;

    // Feet resting on the floor may sit a hair below it after a landing.
    private const float FloorTolerance = 0.05f;

    internal void Unlock() => IsUnlocked = true;
}
