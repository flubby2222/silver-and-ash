using System.Numerics;

namespace SilverAndAsh.Core.World;

/// <summary>The kind of special place a teleporter stands at (issue #436). More come with new room kinds (merchant).</summary>
public enum TeleporterSite
{
    /// <summary>In the start room, where every run begins.</summary>
    Start,

    /// <summary>In the room in front of the boss door, just before the boss fight - the antechamber (issue #565).</summary>
    BossGate,
}

/// <summary>
/// A teleporter of a generated level, decided when the level is made (see <see cref="TeleporterPlacement"/>).
/// </summary>
/// <param name="X">Column of its cell.</param>
/// <param name="Z">Row of its cell.</param>
/// <param name="Room">The room it stands in (number in <see cref="GeneratedLevel.Rooms"/>).</param>
/// <param name="Site">Which special place it marks.</param>
public sealed record TeleporterPlan(int X, int Z, int Room, TeleporterSite Site)
{
    /// <summary>
    /// Where its show side looks on the ground plane (X = east, Y = south, i.e. level Z), as a unit vector - or zero for
    /// "not turned" (the show side looks along +Z, as in the model file). Only the boss-gate teleporter in the antechamber
    /// is turned, towards the middle of the room (issue #565).
    /// </summary>
    public Vector2 Facing { get; init; }

    /// <summary>
    /// A name of its own for the travel choice, or null for the name of its <see cref="Site"/> (every generated level).
    /// The test course names each teleporter after its station (issue #722), so the second one is not called "Boss Gate".
    /// </summary>
    public string? Name { get; init; }
}
