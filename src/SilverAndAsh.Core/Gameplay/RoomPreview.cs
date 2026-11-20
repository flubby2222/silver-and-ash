using System.Numerics;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// One room template on its own, to walk through and judge in the game (issue #528, <c>--room &lt;file&gt;</c>). The
/// level is the template inside a ring of wall. All door slots stay wall except the first: there a player starts, in the
/// doorway, looking straight into the room - as if they had just opened the door. Stepping in enters the room as in a
/// run (a combat room seals, its enemies come out). Enemies and chests are placed by the same rules as on level 0; with
/// <see cref="WithEnemies"/> false the room stays empty of enemies, to look at it in peace.
/// </summary>
public sealed class RoomPreview
{
    // Cells of wall around the template, so the doorway never lies on the edge of the level.
    private const int Ring = 1;

    // How far ahead of the eyes the start view looks, in metres (only the direction counts).
    private const float LookAhead = 10f;

    /// <summary>
    /// A preview of <paramref name="template"/>; with <paramref name="withEnemies"/> false it has no enemies. Every
    /// template has a door slot - <see cref="RoomTemplateParser"/> refuses one without.
    /// </summary>
    public RoomPreview(RoomTemplate template, bool withEnemies = true)
    {
        ArgumentNullException.ThrowIfNull(template);
        Template = template;
        WithEnemies = withEnemies;
        var slot = template.DoorSlots[0];
        Entrance = (slot.X + Ring, slot.Z + Ring);
        Inwards = slot.Side switch
        {
            RoomSide.North => (0, 1),
            RoomSide.South => (0, -1),
            RoomSide.West => (1, 0),
            _ => (-1, 0),
        };
    }

    /// <summary>The room shown.</summary>
    public RoomTemplate Template { get; }

    /// <summary>False: the room gets no enemies (chests are placed all the same).</summary>
    public bool WithEnemies { get; }

    /// <summary>The level cell of the doorway a player starts in: the template's first door slot.</summary>
    public (int X, int Z) Entrance { get; }

    /// <summary>One cell step from the doorway into the room.</summary>
    public (int X, int Z) Inwards { get; }

    /// <summary>
    /// Reads the template at <paramref name="path"/>. The folder it lies in names the kind of room, as in assets/rooms/
    /// (e.g. "combat"). A relative path that is not found is also looked for below <paramref name="baseDirectory"/> (the
    /// game's own folder, which holds a copy of assets/). Throws <see cref="FileNotFoundException"/> if there is no such
    /// file and <see cref="LevelFormatException"/> if it is no valid template.
    /// </summary>
    public static RoomTemplate Load(string path, string? baseDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var file = File.Exists(path) || baseDirectory is null || Path.IsPathRooted(path) ? path : Path.Combine(baseDirectory, path);
        if (!File.Exists(file))
        {
            throw new FileNotFoundException($"There is no room template '{path}'.", path);
        }

        var folder = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(file))) ?? string.Empty;
        if (!Enum.TryParse<RoomKind>(folder, ignoreCase: true, out var kind) || !Enum.IsDefined(kind) || int.TryParse(folder, out _))
        {
            var kinds = string.Join(", ", Enum.GetNames<RoomKind>().Select(name => name.ToLowerInvariant()));
            throw new LevelFormatException(
                Path.GetFileName(file), 0, $"The folder '{folder}' names no kind of room. A template lies in one of: {kinds}.");
        }

        return RoomTemplateParser.Parse(File.ReadAllText(file), kind, $"{folder}/{Path.GetFileName(file)}");
    }

    /// <summary>
    /// The level of this preview for <paramref name="seed"/>: the room (number 0) with its enemies - unless
    /// <see cref="WithEnemies"/> is false - and chests, placed as on level 0. The doorway is part of no room.
    /// </summary>
    public GeneratedLevel Build(int seed, Balance balance)
    {
        ArgumentNullException.ThrowIfNull(balance);
        var width = Template.Width + (2 * Ring);
        var depth = Template.Depth + (2 * Ring);
        var cells = new Cell[width, depth];
        var floor = new List<(int X, int Z)>();
        for (var x = 0; x < width; x++)
        {
            for (var z = 0; z < depth; z++)
            {
                var inside = x >= Ring && x < width - Ring && z >= Ring && z < depth - Ring;
                cells[x, z] = inside ? Template.CellAt(x - Ring, z - Ring) : Cell.Wall;
                if (!cells[x, z].IsSolid)
                {
                    floor.Add((x, z));
                }
            }
        }

        // The doorway: as high as a door, on the floor of the cell behind it.
        var behind = cells[Entrance.X + Inwards.X, Entrance.Z + Inwards.Z];
        cells[Entrance.X, Entrance.Z] = new Cell(false, behind.FloorHeight, Cell.DefaultCeilingHeight);

        static List<(int X, int Z)> Moved(IReadOnlyList<(int X, int Z)> slots) => slots.Select(cell => (cell.X + Ring, cell.Z + Ring)).ToList();

        var room = new PlacedRoom(
            0, Template.Kind, Template.Name, Ring, Ring, Template.Width, Template.Depth, DistanceFromStart: 0, floor,
            Moved(Template.GhoulSlots), Moved(Template.BatSlots), Moved(Template.ChestSlots), Moved(Template.BossSlots));
        var level = new Level(cells, Entrance.X, Entrance.Z);
        var generated = new GeneratedLevel(seed, level, [room], []);
        var populated = WithEnemies ? EnemyPlacement.Populate(generated, balance.Difficulty) : generated;
        return ChestPlacement.Place(populated, balance.Chests);
    }

    /// <summary>A world in this preview for <paramref name="seed"/>: a player in the doorway, looking into the room.</summary>
    public GameWorld NewWorld(int seed, Balance balance)
    {
        var generated = Build(seed, balance);
        var world = new GameWorld(generated.Level, seed, balance, generated.Doors, generated.Rooms, generated.Chests)
        {
            // The doorway looks like the room it opens into, as a door cell does.
            Surfaces = SurfaceVariants.For(generated with { Doors = [new LevelDoor(Entrance.X, Entrance.Z, 0, 0, SideOfRoom)] }),
        };
        var eyes = world.Player.EyePosition;
        PhotoStudio.Face(world.Player, eyes + (new Vector3(Inwards.X, 0f, Inwards.Z) * LookAhead));
        return world;
    }

    /// <summary>The world after <paramref name="finished"/> ended: the same room again, from the doorway.</summary>
    public GameWorld NextWorld(GameWorld finished)
    {
        ArgumentNullException.ThrowIfNull(finished);
        return NewWorld(finished.Seed, finished.Balance);
    }

    /// <summary>
    /// The photo of the entrance (<c>--room &lt;file&gt; --photo entrance</c>): the first picture a player sees in the
    /// doorway, for designers and testers. Its name - and so its file name - comes from the template, e.g.
    /// "room-combat-l-hall".
    /// </summary>
    public PhotoScene EntrancePhoto(int seed) => new(
        "room-" + Path.ChangeExtension(Template.Name, null).Replace('/', '-'),
        $"The entrance of the room template {Template.Name}, seen from its first door slot.",
        studio => new PhotoSetup(NewWorld(seed, studio.Balance)));

    private RoomSide SideOfRoom => Template.DoorSlots[0].Side;
}
