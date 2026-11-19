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

    /// <summary>True once a player has stepped onto it. It stays unlocked for the rest of the run.</summary>
    public bool IsUnlocked { get; private set; }

    /// <summary>True if the feet at <paramref name="feet"/> are within <paramref name="radius"/> of its middle (on the ground plane).</summary>
    public bool Reaches(Vector3 feet, float radius) =>
        new Vector2(feet.X - Position.X, feet.Z - Position.Z).LengthSquared() <= radius * radius;

    internal void Unlock() => IsUnlocked = true;
}
