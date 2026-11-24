using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.Showroom;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Showroom;

/// <summary>
/// Issue #588: <see cref="ShowroomLayout.Build"/> became a table of its building blocks. The recording
/// (showroom-layout.txt) holds everything the build makes, taken before that change - cells, looks, sections, pieces
/// and their places, doors, chests, enemies, exhibits and labels, in their order - so the change is shown to give the
/// same showroom. A new piece or building block changes the recording on purpose: then the new lines belong in the
/// same pull request, and a changed line of anything else is a warning sign, not something to nod through.
/// </summary>
public class ShowroomRecordingTests
{
    private const string RecordingPath = "tests/SilverAndAsh.Core.Tests/Showroom/showroom-layout.txt";

    // One missing line in the middle shifts every line after it; the first differences say what happened.
    private const int ShownDifferences = 5;

    [Fact]
    public void The_showroom_is_built_as_recorded()
    {
        var recorded = File.ReadAllLines(RepositoryFiles.Find(RecordingPath));

        var now = Recording(ShowroomLayout.Build());

        var differences = Enumerable.Range(0, Math.Max(recorded.Length, now.Count))
            .Where(line => line >= recorded.Length || line >= now.Count || recorded[line] != now[line])
            .Select(line => $"line {line + 1}:\n  recorded: {(line < recorded.Length ? recorded[line] : "(none)")}\n  now:      {(line < now.Count ? now[line] : "(none)")}")
            .ToList();
        var shown = string.Join("\n", differences.Take(ShownDifferences));
        var more = differences.Count > ShownDifferences ? $"\n... and {differences.Count - ShownDifferences} more" : "";
        Assert.True(differences.Count == 0, $"showroom-layout.txt differs from ShowroomLayout.Build():\n{shown}{more}");
    }

    [Fact]
    public void The_recording_shows_each_cell_and_look()
    {
        // The map in the recording is read by eye: one letter per cell. These are its keys.
        var layout = ShowroomLayout.Build();
        var first = layout.Sections[0];
        var second = layout.Sections[1];

        Assert.Equal('#', CellLetter(layout.Level[0, 0]));
        Assert.Equal('c', CellLetter(layout.Level[first.CorridorX, first.NorthRow]));
        Assert.Equal('R', CellLetter(layout.Level[first.RoomX, first.NorthRow]));
        Assert.Equal('?', CellLetter(Cell.Floor with { FloorHeight = 0.4f }));
        Assert.Equal('?', CellLetter(Cell.Wall with { CeilingHeight = 6f }));
        Assert.Equal('A', LookLetter(layout.Surfaces, first.RoomX, first.NorthRow));
        Assert.Equal('B', LookLetter(layout.Surfaces, second.RoomX, second.NorthRow));
    }

    /// <summary>Everything <paramref name="layout"/> holds, as lines of text.</summary>
    private static List<string> Recording(ShowroomLayout layout)
    {
        var lines = new List<string>();
        var level = layout.Level;
        foreach (var property in PropertiesOf(level))
        {
            AddProperty(lines, "level", property.Name, property.GetValue(level));
        }

        // The cells as a map, north at the top: '#' wall, 'c' floor under the corridor ceiling, 'R' under the room's.
        // Any other cell is a '?' with its own line below the map.
        for (var z = 0; z < level.Depth; z++)
        {
            lines.Add($"cells {z,2} " + string.Concat(Enumerable.Range(0, level.Width).Select(x => CellLetter(level[x, z]))));
        }

        for (var z = 0; z < level.Depth; z++)
        {
            for (var x = 0; x < level.Width; x++)
            {
                if (CellLetter(level[x, z]) == '?')
                {
                    lines.Add($"cell {x} {z} {Show(level[x, z])}");
                }
            }
        }

        // The looks as a map: the variant of walls, floor and ceiling, or '*' where they differ (a line below the map).
        lines.AddRange(layout.Surfaces.Rooms.Select(look => $"surfaces room {Show(look)}"));
        for (var z = 0; z < level.Depth; z++)
        {
            lines.Add($"looks {z,2} " + string.Concat(Enumerable.Range(0, level.Width).Select(x => LookLetter(layout.Surfaces, x, z))));
        }

        for (var z = 0; z < level.Depth; z++)
        {
            for (var x = 0; x < level.Width; x++)
            {
                if (LookLetter(layout.Surfaces, x, z) == '*')
                {
                    lines.Add($"look {x} {z} {string.Join(" ", Enum.GetValues<SurfaceKind>().Select(kind => layout.Surfaces.At(x, z, kind)))}");
                }
            }
        }

        lines.AddRange(layout.Sections.Select(section => $"section {Show(section)}"));
        lines.AddRange(layout.Placed.Select(place => $"placed {Show(place)}"));
        lines.AddRange(layout.Doors.Select(door => $"door {Show(door)}"));
        lines.AddRange(layout.Chests.Select(chest => $"chest {Show(chest)}"));
        foreach (var property in PropertiesOf(layout.Exhibits))
        {
            AddProperty(lines, "exhibit", property.Name, property.GetValue(layout.Exhibits));
        }

        lines.AddRange(layout.Labels.Select(label => $"label {Show(label)}"));
        return lines;
    }

    // A list gets one line per entry, anything else one line.
    private static void AddProperty(List<string> lines, string prefix, string name, object? value)
    {
        if (value is IEnumerable list and not string)
        {
            lines.AddRange(list.Cast<object?>().Select(entry => $"{prefix} {name} {Show(entry)}"));
        }
        else
        {
            lines.Add($"{prefix} {name} {Show(value)}");
        }
    }

    private static char CellLetter(Cell cell) => cell switch
    {
        _ when cell == Cell.Wall => '#',
        { IsSolid: false, FloorHeight: Cell.DefaultFloorHeight, CeilingHeight: Cell.DefaultCeilingHeight } => 'c',
        { IsSolid: false, FloorHeight: Cell.DefaultFloorHeight, CeilingHeight: RoomTemplate.RoomCeilingHeight } => 'R',
        _ => '?',
    };

    private static char LookLetter(SurfaceVariants surfaces, int x, int z)
    {
        var variants = Enum.GetValues<SurfaceKind>().Select(kind => surfaces.At(x, z, kind)).Distinct().ToList();
        return variants.Count == 1 ? variants[0].ToString()[0] : '*';
    }

    // Public instance properties without an index, in the order the type declares them.
    private static IEnumerable<PropertyInfo> PropertiesOf(object value) => value.GetType()
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => property.GetIndexParameters().Length == 0)
        .OrderBy(property => property.MetadataToken);

    // Numbers to 4 places, so a last bit of sine or cosine on another system does not show; no culture; -0 as 0.
    private static string Show(object? value) => value switch
    {
        null => "null",
        float number => (MathF.Round(number, 4) + 0f).ToString("F4", CultureInfo.InvariantCulture),
        Vector3 vector => $"({Show(vector.X)}, {Show(vector.Y)}, {Show(vector.Z)})",
        Vector2 flat => $"({Show(flat.X)}, {Show(flat.Y)})",
        ITuple tuple => "(" + string.Join(", ", Enumerable.Range(0, tuple.Length).Select(item => Show(tuple[item]))) + ")",
        string text => text,
        Enum or int or bool => Convert.ToString(value, CultureInfo.InvariantCulture)!,
        IEnumerable list => "[" + string.Join(", ", list.Cast<object?>().Select(Show)) + "]",
        _ => $"{value.GetType().Name} {{ {string.Join(", ", PropertiesOf(value).Select(property => $"{property.Name} = {Show(property.GetValue(value))}"))} }}",
    };
}
