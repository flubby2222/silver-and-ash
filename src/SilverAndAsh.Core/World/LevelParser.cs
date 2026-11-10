namespace SilverAndAsh.Core.World;

/// <summary>
/// Reads a level from text. Each character is one cell, each line one row:
/// <list type="bullet">
///   <item><c>#</c> – wall</item>
///   <item><c>' '</c> (space) – also wall, handy for irregular shapes</item>
///   <item><c>.</c> – floor</item>
///   <item><c>P</c> – floor where the player starts (exactly once)</item>
///   <item><c>1</c> / <c>2</c> / <c>3</c> – raised floor (platform) at 0.25 / 0.5 / 0.75 m</item>
///   <item><c>_</c> – pit, floor 1 m below normal</item>
/// </list>
/// The ceiling is always at the standard height. Lines may have different lengths; missing cells are walls.
/// Empty lines at the end are ignored. Full description: docs/level-format.md.
/// </summary>
public static class LevelParser
{
    /// <summary>Height of one platform step ('1' = 1 step, '2' = 2 steps, ...), in metres.</summary>
    public const float PlatformStep = 0.25f;

    /// <summary>Floor height of a pit ('_'), in metres.</summary>
    public const float PitFloorHeight = -1f;

    private const string AllowedSymbols = "'#', ' ', '.', 'P', '1', '2', '3', '_'";

    /// <summary>Reads the level file at <paramref name="path"/>.</summary>
    public static Level ParseFile(string path) => Parse(File.ReadAllText(path));

    /// <summary>Reads a level from <paramref name="text"/>. Throws <see cref="LevelFormatException"/> on errors.</summary>
    public static Level Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lines = text.ReplaceLineEndings("\n").Split('\n').ToList();
        while (lines.Count > 0 && lines[^1].Trim().Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count == 0)
        {
            throw new LevelFormatException(0, "The level is empty.");
        }

        var width = lines.Max(line => line.Length);
        var depth = lines.Count;
        var cells = new Cell[width, depth];
        (int X, int Z, int Line)? start = null;

        for (var z = 0; z < depth; z++)
        {
            var line = lines[z];
            var lineNumber = z + 1;

            for (var x = 0; x < width; x++)
            {
                var symbol = x < line.Length ? line[x] : '#';
                cells[x, z] = symbol switch
                {
                    '#' or ' ' => Cell.Wall,
                    '.' => Cell.Floor,
                    'P' => Cell.Floor,
                    '1' or '2' or '3' => FloorAt((symbol - '0') * PlatformStep),
                    '_' => FloorAt(PitFloorHeight),
                    _ => throw new LevelFormatException(
                        lineNumber, $"Unknown symbol '{symbol}' in column {x + 1}. Allowed: {AllowedSymbols}."),
                };

                if (symbol == 'P')
                {
                    if (start is { } first)
                    {
                        throw new LevelFormatException(
                            lineNumber, $"Second player start 'P' in column {x + 1} (the first one is in line {first.Line}).");
                    }

                    start = (x, z, lineNumber);
                }
            }
        }

        if (start is not { } playerStart)
        {
            throw new LevelFormatException(0, "The level has no player start 'P'.");
        }

        return new Level(cells, playerStart.X, playerStart.Z);
    }

    /// <summary>A walkable cell with its floor at <paramref name="floorHeight"/> and the standard ceiling.</summary>
    private static Cell FloorAt(float floorHeight) => new(false, floorHeight, Cell.DefaultCeilingHeight);
}
