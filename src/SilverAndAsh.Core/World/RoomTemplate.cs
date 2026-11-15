namespace SilverAndAsh.Core.World;

/// <summary>
/// A hand-built room read from a file in assets/rooms/. The level generator picks templates with the
/// world's seed, turns or mirrors them and joins them at their door slots. Immutable: turning or
/// mirroring returns a new template. Read templates with <see cref="RoomTemplateParser"/>.
/// </summary>
public sealed class RoomTemplate
{
    /// <summary>Ceiling height of rooms (high enough for the air dash), in metres. Corridors use the standard height.</summary>
    public const float RoomCeilingHeight = 6f;

    private readonly Cell[,] _cells;

    internal RoomTemplate(
        string name,
        RoomKind kind,
        Cell[,] cells,
        IReadOnlyList<DoorSlot> doorSlots,
        IReadOnlyList<(int X, int Z)> ghoulSlots,
        IReadOnlyList<(int X, int Z)> batSlots,
        IReadOnlyList<(int X, int Z)> chestSlots,
        (int X, int Z)? playerStart)
    {
        Name = name;
        Kind = kind;
        _cells = cells;
        DoorSlots = doorSlots;
        GhoulSlots = ghoulSlots;
        BatSlots = batSlots;
        ChestSlots = chestSlots;
        PlayerStart = playerStart;
    }

    /// <summary>Where the template came from, e.g. "combat/pillars.txt". Used in messages and logs.</summary>
    public string Name { get; }

    public RoomKind Kind { get; }

    /// <summary>Number of cells from west to east.</summary>
    public int Width => _cells.GetLength(0);

    /// <summary>Number of cells from north to south.</summary>
    public int Depth => _cells.GetLength(1);

    /// <summary>The ceiling height of every walkable cell, fixed by the kind of room.</summary>
    public float CeilingHeight => CeilingFor(Kind);

    /// <summary>Places on the outer wall where a door may go.</summary>
    public IReadOnlyList<DoorSlot> DoorSlots { get; }

    /// <summary>Cells where a ghoul may start ('G').</summary>
    public IReadOnlyList<(int X, int Z)> GhoulSlots { get; }

    /// <summary>Cells above which a bat may hover ('B').</summary>
    public IReadOnlyList<(int X, int Z)> BatSlots { get; }

    /// <summary>Cells where a chest may stand ('C'); only combat rooms have them for now.</summary>
    public IReadOnlyList<(int X, int Z)> ChestSlots { get; }

    /// <summary>The player start ('P'); only the start room has one.</summary>
    public (int X, int Z)? PlayerStart { get; }

    /// <summary>The ceiling height used for walkable cells of rooms of <paramref name="kind"/>.</summary>
    public static float CeilingFor(RoomKind kind) =>
        kind == RoomKind.Corridor ? Cell.DefaultCeilingHeight : RoomCeilingHeight;

    /// <summary>The cell at column <paramref name="x"/>, row <paramref name="z"/> (both 0-based).</summary>
    public Cell CellAt(int x, int z) => _cells[x, z];

    /// <summary>
    /// The template turned clockwise (seen from above) by <paramref name="quarterTurns"/> quarter turns.
    /// Negative values turn anticlockwise. Door slots turn with the room: a north door becomes an east door.
    /// </summary>
    public RoomTemplate Rotated(int quarterTurns)
    {
        var turns = ((quarterTurns % 4) + 4) % 4;
        var room = this;
        for (var i = 0; i < turns; i++)
        {
            room = room.TurnedOnce();
        }

        return room;
    }

    /// <summary>The template mirrored from west to east. East and west doors swap sides.</summary>
    public RoomTemplate Mirrored()
    {
        var width = Width;
        return Transformed(
            width,
            Depth,
            (x, z) => (width - 1 - x, z),
            side => side switch
            {
                RoomSide.East => RoomSide.West,
                RoomSide.West => RoomSide.East,
                _ => side,
            });
    }

    // One clockwise quarter turn: the top row becomes the right column.
    private RoomTemplate TurnedOnce()
    {
        var depth = Depth;
        return Transformed(depth, Width, (x, z) => (depth - 1 - z, x), side => (RoomSide)(((int)side + 1) % 4));
    }

    private RoomTemplate Transformed(
        int newWidth, int newDepth, Func<int, int, (int X, int Z)> move, Func<RoomSide, RoomSide> turnSide)
    {
        var cells = new Cell[newWidth, newDepth];
        for (var x = 0; x < Width; x++)
        {
            for (var z = 0; z < Depth; z++)
            {
                var (newX, newZ) = move(x, z);
                cells[newX, newZ] = _cells[x, z];
            }
        }

        (int X, int Z) Move((int X, int Z) cell) => move(cell.X, cell.Z);

        return new RoomTemplate(
            Name,
            Kind,
            cells,
            DoorSlots.Select(slot =>
            {
                var (newX, newZ) = move(slot.X, slot.Z);
                return new DoorSlot(newX, newZ, turnSide(slot.Side));
            }).ToList(),
            GhoulSlots.Select(Move).ToList(),
            BatSlots.Select(Move).ToList(),
            ChestSlots.Select(Move).ToList(),
            PlayerStart is { } start ? Move(start) : null);
    }
}
