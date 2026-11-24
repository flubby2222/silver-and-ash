using System.Numerics;
using SilverAndAsh.Core.Course;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Photos;

/// <summary>
/// The stations of the test course (issue #718, docs/pruefstrecke.md), one picture each - with the signs, as
/// <c>--course</c> shows them. The doors on the way are opened with F, as a player would, before the picture.
/// </summary>
internal static class CourseScenes
{
    // Eye level of the point the entrance pictures look at, far across the station.
    private const float LookHeight = 1.2f;

    /// <summary>One scene per station, in the order of <see cref="TestCourse.Stations"/>.</summary>
    public static PhotoScene[] All() =>
    [
        new("course-1-arms", "Station 1 of the test course from its south-east corner back to its sign: the weapons along the north wall, the start teleporter on the way (issue #718).",
            studio => new PhotoSetup(Within(studio, 0, room => (LastX(room), CourseRoom.Bottom), room => SignOf(room)))),
        new("course-2-columns", "Station 2 of the test course from just inside its entrance: two columns either side of the way, silver beside one, a ghoul beyond (issue #718).",
            studio => new PhotoSetup(Within(studio, 1, room => (FirstX(room), CourseRoom.Way), room => Across(room)))),
        new("course-3-cracked-wall", "Station 3 of the test course from its south-east corner: the cracked wall in the middle of its north wall (issue #718).",
            studio => new PhotoSetup(Within(studio, 2, room => (LastX(room), CourseRoom.Bottom), room => NorthWallMiddle(room)))),
        new("course-4-portcullis", "Station 4 of the test course, sealed: from inside the arena back at the fallen grate, the ghoul and the bat coming out by the column (issue #718).",
            SealedArena),
        new("course-5-teleporter", "Station 5 of the test course from its north-east corner back to its sign: the teleporter turned towards the way in, the Well of Sirius beside the way (issue #718).",
            studio => new PhotoSetup(Within(studio, 4, room => (LastX(room), CourseRoom.Top), room => SignOf(room)))),
        new("course-6-boss", "Station 6 of the test course from its north-east corner: the antechamber, silver and a blood vial before the boss portal (issue #718).",
            studio => new PhotoSetup(Within(studio, 5, room => (LastX(room), CourseRoom.Top), room => PortalOf(room)))),
    ];

    // The course with every door on the way into station `index` opened and swung open, the player standing on the cell
    // `place` says and looking at what `lookAt` says.
    private static GameWorld Within(PhotoStudio studio, int index, Func<PlacedRoom, (int X, int Z)> place, Func<PlacedRoom, Vector3> lookAt)
    {
        var (world, course) = OpenedUpTo(studio, index);
        var room = course.Stations[index];
        var (x, z) = place(room);
        world.Player.PlaceAt(world.Level.CellCenter(x, z));
        PhotoStudio.Face(world.Player, lookAt(room));
        return world;
    }

    private static int FirstX(PlacedRoom room) => room.FloorCells.Min(cell => cell.X);

    private static int LastX(PlacedRoom room) => room.FloorCells.Max(cell => cell.X);

    // Where the sign of a station hangs: over its first cell on the way, a little below its height (3.1 m).
    private static Vector3 SignOf(PlacedRoom room) =>
        new((FirstX(room) + 0.5f) * Level.CellSize, 2.6f, (CourseRoom.Way + 0.5f) * Level.CellSize);

    // Station 4 a moment after a player walked in: the arena sealed, its grates down, its enemies out.
    private static PhotoSetup SealedArena(PhotoStudio studio)
    {
        var (world, course) = OpenedUpTo(studio, 3);
        var room = course.Stations[3];
        world.Player.PlaceAt(world.Level.CellCenter(LastX(room) - 1, CourseRoom.Way));
        // Low, so the sign over the grate stands clear above the level's name, which is still shown.
        PhotoStudio.Face(world.Player, world.Level.CellCenter(FirstX(room) - 1, CourseRoom.Way) + new Vector3(0f, 0.3f, 0f));
        var settle = Math.Max(studio.Balance.Grates.FallTicks, SimulationClock.SecondsToTicks(studio.Balance.Difficulty.EmergeTime)) + 2;
        return new PhotoSetup(world) { Ticks = settle };
    }

    // A new course world with the doors into stations 2 .. index + 1 opened with F from in front of each, then left to
    // swing all the way open.
    private static (GameWorld World, CourseLayout Course) OpenedUpTo(PhotoStudio studio, int index)
    {
        var course = TestCourse.Build(PhotoStudio.Seed, studio.Balance);
        var world = TestCourse.NewWorld(PhotoStudio.Seed, studio.Balance);
        var doors = new List<Door>();
        for (var station = 1; station <= index; station++)
        {
            var first = course.Stations[station].FloorCells.Min(cell => cell.X);
            var door = world.Doors.Single(door => door.X == first - 1 && door.Z == CourseRoom.Way);
            // From the station before - unless that is an arena, which would seal and drop its grates: then from behind.
            var side = course.Stations[station - 1].Kind == RoomKind.Combat ? 1 : -1;
            world.Player.PlaceAt(world.Level.CellCenter(door.X + side, CourseRoom.Way));
            PhotoStudio.Face(world.Player, (door.BoxMin + door.BoxMax) / 2f);
            world.Tick(PlayerInput.None with { Interact = true });
            if (!door.IsOpen)
            {
                throw new InvalidOperationException($"The door into station {station + 1} of the test course did not open with F.");
            }

            doors.Add(door);
        }

        world.Player.PlaceAt(world.Level.PlayerStartPosition);
        for (var tick = 0; tick < 10 * SimulationClock.TicksPerSecond && doors.Any(door => door.OpenFraction < 1f); tick++)
        {
            world.Tick(PlayerInput.None);
        }

        return (world, course);
    }

    // The far end of the station along the way.
    private static Vector3 Across(PlacedRoom room) =>
        new((LastX(room) + 0.5f) * Level.CellSize, LookHeight, (CourseRoom.Way + 0.5f) * Level.CellSize);

    // The middle of the station's north wall.
    private static Vector3 NorthWallMiddle(PlacedRoom room) =>
        new(((float)room.FloorCells.Average(cell => cell.X) + 0.5f) * Level.CellSize, LookHeight, CourseRoom.Top * Level.CellSize);

    // The middle of the boss portal in the station's south wall, where the portal pictures look (PortalLookHeight).
    private static Vector3 PortalOf(PlacedRoom room) =>
        new(((float)room.FloorCells.Average(cell => cell.X) + 0.5f) * Level.CellSize, PhotoStudio.PortalLookHeight, (CourseRoom.Bottom + 1) * Level.CellSize);
}
