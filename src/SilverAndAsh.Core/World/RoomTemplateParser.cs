namespace SilverAndAsh.Core.World;

/// <summary>
/// Reads room templates. The symbols are those of the level format (see <see cref="LevelParser"/>) plus:
/// <list type="bullet">
///   <item><c>+</c> – door slot: a place on the outer wall where the generator may put a door.
///   Must sit on a side (not in a corner) with floor right behind it. Unused slots stay walls.</item>
/// </list>
/// 'G' and 'B' mark places for enemies, 'P' the player start (only in the start room).
/// Training dummies ('D') are not allowed in rooms. The ceiling height comes from the kind of room.
/// Full description: docs/level-format.md.
/// </summary>
public static class RoomTemplateParser
{
    private const string AllowedSymbols = "'#', ' ', '.', '+', 'P', '1', '2', '3', '_', 'G', 'B'";

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
        (int X, int Z)? playerStart = null;

        for (var z = 0; z < depth; z++)
        {
            for (var x = 0; x < width; x++)
            {
                var symbol = x < lines[z].Length ? lines[z][x] : '#';
                var floor = symbol == 'D' ? null : LevelParser.FloorHeightOf(symbol);

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

        var doorSlots = doorCells.Select(cell => ToDoorSlot(cells, cell, name)).ToList();

        if (doorSlots.Count == 0)
        {
            throw new LevelFormatException(name, 0, "The room has no door slot '+'.");
        }

        if (kind == RoomKind.Boss && doorSlots.Count != 1)
        {
            throw new LevelFormatException(
                name, 0, $"A boss room needs exactly one door slot '+', but this one has {doorSlots.Count}.");
        }

        CheckFloorIsConnected(cells, name);

        return new RoomTemplate(name, kind, cells, doorSlots, ghoulSlots, batSlots, playerStart);
    }

    private static DoorSlot ToDoorSlot(Cell[,] cells, (int X, int Z) cell, string name)
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

        return new DoorSlot(x, z, side);
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
