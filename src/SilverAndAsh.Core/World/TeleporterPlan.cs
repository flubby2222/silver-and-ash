namespace SilverAndAsh.Core.World;

/// <summary>The kind of special place a teleporter stands at (issue #436). More come with new room kinds (merchant).</summary>
public enum TeleporterSite
{
    /// <summary>In the start room, where every run begins.</summary>
    Start,

    /// <summary>In the room in front of the boss door, just before the boss fight.</summary>
    BossGate,
}

/// <summary>
/// A teleporter of a generated level, decided when the level is made (see <see cref="TeleporterPlacement"/>).
/// </summary>
/// <param name="X">Column of its cell.</param>
/// <param name="Z">Row of its cell.</param>
/// <param name="Room">The room it stands in (number in <see cref="GeneratedLevel.Rooms"/>).</param>
/// <param name="Site">Which special place it marks.</param>
public sealed record TeleporterPlan(int X, int Z, int Room, TeleporterSite Site);
