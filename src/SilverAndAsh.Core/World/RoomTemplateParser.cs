namespace SilverAndAsh.Core.World;

/// <summary>
/// Reads room templates. The symbols are those of the level format (see <see cref="LevelParser"/>) plus:
/// <list type="bullet">
///   <item><c>+</c> – door slot: a place on the outer wall where the generator may put a door.
///   Must sit on a side (not in a corner) with floor right behind it - in rooms (not corridors) also to the
///   left and right of that cell, so a door never sits flush with a side wall. Unused slots stay walls.</item>
/// </list>
/// The outer ring of a template is always wall or door slot. Rooms with a single entrance (the boss room) have
/// exactly one door slot.
/// 'G' and 'B' mark places for enemies, 'C' places for a chest (only in combat rooms), 'P' the player start (only in
/// the start room).
/// Training dummies ('D') are not allowed in rooms. The ceiling height comes from the kind of room.
/// Full description: docs/level-format.md.
/// </summary>
public static class RoomTemplateParser
{
    private const string AllowedSymbols = "'#', ' ', '.', '+', 'P', '1', '2', '3', '_', 'G', 'B', 'C'";

    /// <summary>
    /// Reads every template below <paramref name="directory"/>. Each sub-folder names the kind of room
    /// (start, combat, corridor, boss), each .txt file in it is one template.
    /// Throws <see cref="LevelFormatException"/> for an unknown folder or a broken template.
    /// </summary>
    public static IReadOnlyList<RoomTemplate> LoadAll(string directory)
    {
        var rooms = new List<RoomTemplate>();
        foreach (var folder in Directory.GetDirectories(directory).Order(StringComparer.Ordinal))
        {
            var folderName = Path.GetFileName(folder);
            if (!Enum.TryParse<RoomKind>(folderName, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
            {
                var kinds = string.Join(", ", Enum.GetNames<RoomKind>().Select(name => name.ToLowerInvariant()));
                throw new LevelFormatException(folderName, 0, $"Unknown kind of room. Folders may be: {kinds}.");
            }

            foreach (var file in Directory.GetFiles(folder, "*.txt").Order(StringComparer.Ordinal))
            {
                rooms.Add(Parse(File.ReadAllText(file), kind, $"{folderName}/{Path.GetFileName(file)}"));
            }
        }

        return rooms;
    }

    /// <summary>
    /// Reads one template of kind <paramref name="kind"/> from <paramref name="text"/>.
    /// <paramref name="name"/> (e.g. "combat/pillars.txt") appears in error messages.
    /// </summary>
    public static RoomTemplate Parse(string text, RoomKind kind, string name)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(name);

        var lines = text.ReplaceLineEndings("\n").Split('\n').ToList();
        while (lines.Count > 0 && lines[^1].Trim().Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count == 0)
        {
            throw new LevelFormatException(name, 0, "The room is empty.");
        }

        var width = lines.Max(line => line.Length);
        var depth = lines.Count;
        var ceiling = RoomTemplate.CeilingFor(kind);
        var cells = new Cell[width, depth];
        var doorCells = new List<(int X, int Z)>();
        var ghoulSlots = new List<(int X, int Z)>();
        var batSlots = new List<(int X, int Z)>();
        var chestSlots = new List<(int X, int Z)>();
        (int X, int Z)? playerStart = null;

        for (var z = 0; z < depth; z++)
        {
            for (var x = 0; x < width; x++)
            {
                var symbol = x < lines[z].Length ? lines[z][x] : '#';
                var floor = symbol switch
                {
                    'D' => null,
                    'C' => Cell.DefaultFloorHeight,
                    _ => LevelParser.FloorHeightOf(symbol),
                };

                if (symbol is '#' or ' ' or '+')
                {
                    cells[x, z] = Cell.Wall;
                }
                else if (floor is { } height)
                {
                    cells[x, z] = new Cell(false, height, ceiling);
                }
                else
                {
                    throw Error(name, x, z, $"Unknown symbol '{symbol}' in column {x + 1}. Allowed: {AllowedSymbols}.");
                }

                switch (symbol)
                {
                    case '+':
                        doorCells.Add((x, z));
                        break;
                    case 'G':
                        ghoulSlots.Add((x, z));
                        break;
                    case 'B':
                        batSlots.Add((x, z));
                        break;
                    case 'C' when kind != RoomKind.Combat:
                        throw Error(name, x, z, $"Only combat rooms may have a chest place 'C' (column {x + 1}).");
                    case 'C':
                        chestSlots.Add((x, z));
                        break;
                    case 'P' when kind != RoomKind.Start:
                        throw Error(name, x, z, $"Only the start room may have a player start 'P' (column {x + 1}).");
                    case 'P' when playerStart is not null:
                        throw Error(name, x, z, $"Second player start 'P' in column {x + 1}.");
                    case 'P':
                        playerStart = (x, z);
                        break;
                }
            }
        }

        if (kind == RoomKind.Start && playerStart is null)
        {
            throw new LevelFormatException(name, 0, "A start room needs a player start 'P'.");
        }

        CheckEdgeIsClosed(cells, name);

        var doorSlots = doorCells.Select(cell => ToDoorSlot(cells, cell, kind, name)).ToList();

        if (doorSlots.Count == 0)
        {
            throw new LevelFormatException(name, 0, "The room has no door slot '+'.");
        }

        if (kind.HasSingleEntrance() && doorSlots.Count != 1)
        {
            throw new LevelFormatException(
                name, 0, $"A {kind.ToString().ToLowerInvariant()} room needs exactly one door slot '+', but this one has {doorSlots.Count}.");
        }

        CheckFloorIsConnected(cells, name);

        return new RoomTemplate(name, kind, cells, doorSlots, ghoulSlots, batSlots, chestSlots, playerStart);
    }

    private static DoorSlot ToDoorSlot(Cell[,] cells, (int X, int Z) cell, RoomKind kind, string name)
    {
        var (x, z) = cell;
        var lastX = cells.GetLength(0) - 1;
        var lastZ = cells.GetLength(1) - 1;
        var onWestOrEast = x == 0 || x == lastX;
        var onNorthOrSouth = z == 0 || z == lastZ;

        if (onWestOrEast && onNorthOrSouth)
        {
            throw Error(name, x, z, $"Door slot '+' in column {x + 1} sits in a corner; a door needs a side.");
        }

        if (!onWestOrEast && !onNorthOrSouth)
        {
            throw Error(name, x, z, $"Door slot '+' in column {x + 1} is not on the edge of the room.");
        }

        var (side, behindX, behindZ) =
            z == 0 ? (RoomSide.North, x, z + 1)
            : z == lastZ ? (RoomSide.South, x, z - 1)
            : x == 0 ? (RoomSide.West, x + 1, z)
            : (RoomSide.East, x - 1, z);

        if (cells[behindX, behindZ].IsSolid)
        {
            throw Error(name, x, z, $"The cell behind door slot '+' in column {x + 1} must be floor.");
        }

        // In a room you never walk in right along a side wall: the cells left and right behind the door are floor
        // too. Corridors are one cell wide, there the door sits between the corridor walls.
        var (besideX, besideZ) = side is RoomSide.North or RoomSide.South ? (1, 0) : (0, 1);
        if (kind != RoomKind.Corridor
            && (cells[behindX - besideX, behindZ - besideZ].IsSolid || cells[behindX + besideX, behindZ + besideZ].IsSolid))
        {
            throw Error(name, x, z, $"The cells beside the floor behind door slot '+' in column {x + 1} must be floor, too - a door may not sit flush with a side wall.");
        }

        return new DoorSlot(x, z, side);
    }

    // The outer ring is wall or door slot: rooms never open onto the outside, and joined rooms meet wall on wall.
    private static void CheckEdgeIsClosed(Cell[,] cells, string name)
    {
        var lastX = cells.GetLength(0) - 1;
        var lastZ = cells.GetLength(1) - 1;
        for (var z = 0; z <= lastZ; z++)
        {
            for (var x = 0; x <= lastX; x++)
            {
                if ((x == 0 || x == lastX || z == 0 || z == lastZ) && !cells[x, z].IsSolid)
                {
                    throw Error(name, x, z, $"Floor on the edge of the room in column {x + 1}; the edge must be wall or a door slot '+'.");
                }
            }
        }
    }

    // Every walkable cell must be reachable from every other one, or enemies and loot could end up out of reach.
    private static void CheckFloorIsConnected(Cell[,] cells, string name)
    {
        var width = cells.GetLength(0);
        var depth = cells.GetLength(1);
        var floorCells = new List<(int X, int Z)>();
        for (var z = 0; z < depth; z++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!cells[x, z].IsSolid)
                {
                    floorCells.Add((x, z));
                }
            }
        }

        if (floorCells.Count == 0)
        {
            return;
        }

        var reached = new HashSet<(int X, int Z)> { floorCells[0] };
        var open = new Queue<(int X, int Z)>(reached);
        while (open.TryDequeue(out var cell))
        {
            foreach (var next in new[]
                     {
                         (cell.X + 1, cell.Z), (cell.X - 1, cell.Z), (cell.X, cell.Z + 1), (cell.X, cell.Z - 1),
                     })
            {
                if (next.Item1 >= 0 && next.Item1 < width && next.Item2 >= 0 && next.Item2 < depth
                    && !cells[next.Item1, next.Item2].IsSolid && reached.Add(next))
                {
                    open.Enqueue(next);
                }
            }
        }

        foreach (var cut in floorCells.Where(cell => !reached.Contains(cell)).Take(1))
        {
            throw Error(
                name, cut.X, cut.Z, $"The floor is not connected: column {cut.X + 1} can't be reached from the rest.");
        }
    }

    private static LevelFormatException Error(string name, int x, int z, string problem) =>
        new(name, z + 1, problem);
}
