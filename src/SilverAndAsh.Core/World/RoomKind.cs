namespace SilverAndAsh.Core.World;

/// <summary>
/// What a room is for. The folder of a room template under assets/rooms/ names its kind
/// (e.g. assets/rooms/combat/pillars.txt is a combat room). More kinds come later (merchant, altar).
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

    /// <summary>A dead end without enemies, holding 1-2 chests; one per level is behind a locked door.</summary>
    Treasure,

    /// <summary>A hidden dead end next to a combat room, behind a cracked wall; holds one chest.</summary>
    Secret,

    /// <summary>
    /// The quiet room right in front of the boss room (issue #565): no enemies, no chests, never an arena. Its layout is
    /// built in (<see cref="World.Antechamber"/>), so there is no folder for it under assets/rooms/.
    /// </summary>
    Antechamber,
}

/// <summary>Rules that depend on the kind of room.</summary>
public static class RoomKindRules
{
    /// <summary>
    /// True for rooms that may only have one way in: boss, treasure and secret rooms (later also merchant and altar
    /// rooms). The level generator never adds a second door or a loop to them.
    /// </summary>
    public static bool HasSingleEntrance(this RoomKind kind) => kind is RoomKind.Boss or RoomKind.Treasure or RoomKind.Secret;

    /// <summary>True for rooms that hold chests but no enemies: treasure and secret rooms.</summary>
    public static bool IsHoard(this RoomKind kind) => kind is RoomKind.Treasure or RoomKind.Secret;
}
