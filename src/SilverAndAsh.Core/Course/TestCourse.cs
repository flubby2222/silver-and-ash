using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.Showroom;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Course;

/// <summary>
/// The test course (issue #718, docs/pruefstrecke.md, <c>--course</c>): one hand-built level with every station whose
/// look or sound still waits for a judgement, one after the other on a straight way from west to east. Unlike the
/// showroom everything follows the real rules - enemies live and attack, the portcullis bars, the boss door slams shut,
/// Gorm fights. Only the player cannot die: health stops at 1, so every hit can still be seen and heard.
/// </summary>
public static class TestCourse
{
    /// <summary>Shown briefly when the course is entered.</summary>
    public const string Name = "THE TEST COURSE";

    // The first floor column of the first station: one ring of wall west of it.
    private const int FirstColumn = 1;

    // Height of a sign over a station's entrance, in metres - below the 3.5 m doorway, above every head.
    private const float SignHeight = 3.1f;

    /// <summary>
    /// The stations, west to east. A new station is one more entry: a name, a kind of room, a width and what it holds.
    /// </summary>
    public static IReadOnlyList<CourseStation> Stations { get; } =
    [
        new("Arms", RoomKind.Start, 5, Arms),
        new("Columns", RoomKind.Start, 7, Columns),
        new("Cracked wall", RoomKind.Start, 5, room => room.SecretRoomNorth()),
        new("Portcullis", RoomKind.Combat, 7, Portcullis),
        new("Teleporter", RoomKind.Start, 5, TeleporterAndWell),
        new("Boss", RoomKind.Antechamber, Antechamber.Width, Boss),
    ];

    /// <summary>
    /// The course of <paramref name="stations"/> (all of <see cref="Stations"/> if none are given) with
    /// <paramref name="seed"/> for what is rolled - the secret room's chest - and the values of <paramref name="balance"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">If a station runs into another or a column does not fit.</exception>
    public static CourseLayout Build(int seed, Balance balance, IReadOnlyList<CourseStation>? stations = null)
    {
        ArgumentNullException.ThrowIfNull(balance);
        stations ??= Stations;
        var course = new CourseBuilder();
        var labels = new List<ShowroomLabel>();
        var rooms = new List<CourseRoom>();
        var x = FirstColumn;
        foreach (var station in stations)
        {
            var index = course.AddRoom(
                station.Kind, $"{rooms.Count + 1} - {station.Name}", x, CourseRoom.Top, x + station.Width - 1, CourseRoom.Bottom, rooms.Count);
            var room = new CourseRoom(course, station, rooms.Count + 1, index, x);
            if (rooms.Count > 0)
            {
                course.AddDoor(new LevelDoor(x - 1, CourseRoom.Way, rooms[^1].Index, index, RoomSide.East), room.Station.Name);
            }

            station.Furnish(room);
            labels.Add(new ShowroomLabel(
                $"{room.Number} - {station.Name}", new Vector3((x + 0.5f) * Level.CellSize, SignHeight, (CourseRoom.Way + 0.5f) * Level.CellSize)));
            rooms.Add(room);
            x = room.LastX + 2;
        }

        var level = WithColumns(course.ToLevel((FirstColumn, CourseRoom.Way)), course, balance.Columns);
        var generated = new GeneratedLevel(seed, level, course.Rooms, course.Doors, null, course.Teleporters, course.Well);
        generated = GratePlacement.Place(ChestPlacement.Place(BossRoomColumns.Place(generated, balance.Columns), balance.Chests));
        var finds = new GroundFinds(course.Loot, course.Consumables, course.Weapons, course.Items);
        return new CourseLayout(generated, labels, finds, rooms.Select(room => generated.Rooms[room.Index]).ToList());
    }

    /// <summary>
    /// A new world of the course: a player who cannot die, with every weapon (as in the showroom), at the west end
    /// looking east along the way.
    /// </summary>
    public static GameWorld NewWorld(int seed, Balance balance)
    {
        var course = Build(seed, balance);
        var generated = course.Generated;
        var world = new GameWorld(generated.Level, seed, balance, generated.Doors, generated.Rooms, generated.Chests, generated.Well, generated.Teleporters)
        {
            LevelName = Name,
            Surfaces = SurfaceVariants.For(generated),
            Finds = course.Finds,
            IsTestWorld = true,
            IsTestCourse = true,
            PlayersCannotDie = true,
        };
        foreach (var player in world.Players)
        {
            ShowroomLayout.ArmFully(player);
            PhotoStudio.Face(player, player.EyePosition + Vector3.UnitX);
        }

        return world;
    }

    /// <summary>The world after <paramref name="finished"/> ended: the course again from the start, same seed and values.</summary>
    public static GameWorld NextWorld(GameWorld finished)
    {
        ArgumentNullException.ThrowIfNull(finished);
        return NewWorld(finished.Seed, finished.Balance);
    }

    // Each column of the stations, checked by the rule of the generated levels: a column that does not fit is an error
    // of the course, not something to leave out quietly.
    private static Level WithColumns(Level level, CourseBuilder course, ColumnsBalance balance)
    {
        var doorCells = course.Doors.SelectMany(door => door.Cells).ToList();
        foreach (var (column, owner) in course.Columns)
        {
            var fit = ColumnPlacement.Check(level, column, doorCells, balance);
            if (fit != ColumnFit.Fits)
            {
                throw new InvalidOperationException($"The test course: a column of '{owner}' at {column.X} / {column.Z} m does not fit ({fit}).");
            }

            level = level.WithColumns([.. level.Columns, column]);
        }

        return level;
    }

    // 1: every weapon lying in a row along the north wall (and in the hands, see NewWorld), both vampiric items along
    // the south wall - each with its [F] prompt - and the teleporter of the start on the way.
    private static void Arms(CourseRoom room)
    {
        var step = 0;
        foreach (var kind in Enum.GetValues<WeaponKind>())
        {
            room.Weapon(kind, step++, CourseRoom.Top);
        }

        room.Item(ItemKind.Leech, 1, CourseRoom.Bottom);
        room.Item(ItemKind.AshenHeart, 3, CourseRoom.Bottom);
        room.Teleporter(2, CourseRoom.Way, TeleporterSite.Start);
    }

    // 2: two columns either side of the way, silver beside one of them, a ghoul beyond them - awake, as outside an arena.
    private static void Columns(CourseRoom room)
    {
        room.Column(3, CourseRoom.Way - 1);
        room.Column(3, CourseRoom.Way + 1);
        room.Silver(2, CourseRoom.Way - 1, 10);
        room.Ghoul(5, CourseRoom.Way - 1);
    }

    // 4: an arena - its grates fall when a player is inside - with a column, and a ghoul and a bat that come out near it.
    private static void Portcullis(CourseRoom room)
    {
        room.Column(3, CourseRoom.Way - 1);
        room.Ghoul(4, CourseRoom.Way + 1);
        room.Bat(2, CourseRoom.Way + 1);
    }

    // 5: a teleporter turned towards the way in (west), Sirius' Relief beside the way - and with the start's
    // teleporter, two to travel between.
    private static void TeleporterAndWell(CourseRoom room)
    {
        room.Teleporter(1, CourseRoom.Way, TeleporterSite.BossGate, -Vector2.UnitX);
        room.Well(3, CourseRoom.Way + 1);
    }

    // 6: the antechamber with silver and a blood vial before the portal, and the boss room behind it.
    private static void Boss(CourseRoom room)
    {
        room.Silver(1, CourseRoom.Bottom, 25);
        room.Consumable(ConsumableKind.BloodVial, 2, CourseRoom.Bottom);
        room.BossRoomSouth();
    }
}
