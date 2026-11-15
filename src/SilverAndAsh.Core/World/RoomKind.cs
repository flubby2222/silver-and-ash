namespace SilverAndAsh.Core.World;

/// <summary>
/// What a room is for. The folder of a room template under assets/rooms/ names its kind
/// (e.g. assets/rooms/combat/pillars.txt is a combat room). More kinds come later (treasure, merchant, altar).
/// </summary>
public enum RoomKind
{
    /// <summary>Where the run begins; holds the player start 'P'.</summary>
    Start,

    /// <summary>The most common room; seals its doors and becomes an arena on first entry.</summary>
    Combat,

    /// <summary>A narrow passage between rooms, with the low standard ceiling.</summary>
    Corridor,

    /// <summary>The last room of a level; has exactly one door.</summary>
    Boss,
}

/// <summary>Rules that depend on the kind of room.</summary>
public static class RoomKindRules
{
    /// <summary>
    /// True for rooms that may only have one way in. Later also treasure, secret, merchant and altar rooms.
    /// The level generator never adds a second door or a loop to them.
    /// </summary>
    public static bool HasSingleEntrance(this RoomKind kind) => kind == RoomKind.Boss;
}
