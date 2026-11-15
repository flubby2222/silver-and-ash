namespace SilverAndAsh.Core.World;

/// <summary>A level built by the <see cref="LevelGenerator"/>: the cells plus which room and door is where.</summary>
/// <param name="Seed">The seed it was built from. The same seed and rooms always give the same level.</param>
/// <param name="Level">The cells, ready to play.</param>
/// <param name="Rooms">All rooms, the start room first (index 0). Corridors count as rooms.</param>
/// <param name="Doors">All doors; each joins two rooms.</param>
public sealed record GeneratedLevel(int Seed, Level Level, IReadOnlyList<PlacedRoom> Rooms, IReadOnlyList<LevelDoor> Doors);

/// <summary>A room template placed in a generated level. All cells are level cells.</summary>
/// <param name="Index">Position in <see cref="GeneratedLevel.Rooms"/>.</param>
/// <param name="Kind">What the room is for.</param>
/// <param name="TemplateName">The template it was made from, e.g. "combat/pillars.txt".</param>
/// <param name="X">Column of the template's top left corner (after turning).</param>
/// <param name="Z">Row of the template's top left corner.</param>
/// <param name="Width">Cells from west to east (after turning).</param>
/// <param name="Depth">Cells from north to south.</param>
/// <param name="DistanceFromStart">Rooms to go through on the shortest way from the start room (start = 0).</param>
/// <param name="FloorCells">The walkable cells of the room - not its walls, not its doors.</param>
/// <param name="GhoulSlots">Places where a ghoul may start.</param>
/// <param name="BatSlots">Places above which a bat may hover.</param>
public sealed record PlacedRoom(
    int Index,
    RoomKind Kind,
    string TemplateName,
    int X,
    int Z,
    int Width,
    int Depth,
    int DistanceFromStart,
    IReadOnlyList<(int X, int Z)> FloorCells,
    IReadOnlyList<(int X, int Z)> GhoulSlots,
    IReadOnlyList<(int X, int Z)> BatSlots);

/// <summary>A door between two rooms of a generated level.</summary>
/// <param name="X">Column of the door cell.</param>
/// <param name="Z">Row of the door cell.</param>
/// <param name="RoomA">The room on the inner side of the door.</param>
/// <param name="RoomB">The room on the outer side.</param>
/// <param name="Side">The wall of room A the door sits in: room B lies in this direction.</param>
/// <param name="Kind">What kind of door it is (the door of the boss room is a boss door).</param>
public sealed record LevelDoor(int X, int Z, int RoomA, int RoomB, RoomSide Side, DoorKind Kind = DoorKind.Normal);
