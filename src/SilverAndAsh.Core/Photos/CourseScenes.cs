using System.Numerics;
using SilverAndAsh.Core.Course;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Photos;

/// <summary>
/// The stations of the test course (issue #718, docs/pruefstrecke.md), one picture each - with the signs, as
/// <c>--course</c> shows them. The doors on the way are opened with F, as a player would, before the picture.
/// Each picture shows what the station is for and its sign (issue #722); where the camera stands is a table entry
/// (<see cref="Shot"/>), found by projecting the things to show through the game's camera.
/// </summary>
internal static class CourseScenes
{
    // A few ticks more than the banner's seconds, so a crumb left by counting down in floats cannot show.
    private const int BannerMarginTicks = 5;

    /// <summary>
    /// Ticks until the level's name has faded, counted in the photo's own ticks: the banner's seconds
    /// (<see cref="LevelNameBanner.Seconds"/>) and a few more. A picture that must be free of it runs this long.
    /// </summary>
    public static int BannerTicks { get; } = SimulationClock.SecondsToTicks(LevelNameBanner.Seconds) + BannerMarginTicks;

    // Where the camera stands - its feet, in metres east and south of the north-west corner of the station's floor, so it
    // moves along when a station before it gets wider - and where it looks, in degrees (yaw 0 = towards +Z, 90 = towards
    // +X; pitch negative = down). Eye height is the player's.
    internal readonly record struct Shot(float East, float South, float YawDegrees, float PitchDegrees);

    // Station 1: from its south-east corner up to the north-west, so the whole row of weapons on the north wall, the
    // items on the south wall and the sign over the first cell are in view.
    // Turned and tilted so that no weapon lies behind the teleporter's pillars or under the sign, each one wholly in the
    // picture and above the hands (#201). With the whip (#202) and the stake crossbow (#203) the station grew a cell each time: the camera moves east with it.
    internal static readonly Shot ArmsShot = new(15f, 8.5f, 227f, -20f);

    // Station 1 again, close to the row of weapons (issue #736): from the east end of the way, down at the row, every
    // weapon between the hands. The sign is not in this one: with it, the row would be as far off as in the first.
    // Steeper than the hands need, so that the longest weapon at the west end is not cut off at the edge (#201).
    internal static readonly Shot ArmsRowShot = new(14.5f, 4f, 215f, -30f);

    // Station 2: from the far south-east corner back west, the columns and the silver in front, the ghoul beyond, the sign
    // over the entrance.
    internal static readonly Shot ColumnsShot = new(13.5f, 9.5f, 245f, -13f);

    // Station 3: near the crack in the north wall and at a slant to it (issue #736), the sign over the first cell beyond.
    internal static readonly Shot CrackedWallShot = new(7.5f, 2f, 262f, -10f);

    /// <summary>Where the feet of a <see cref="Shot"/> stand in <paramref name="station"/>, in world metres.</summary>
    internal static Vector3 FeetOf(PlacedRoom station, Shot shot) =>
        new(FirstX(station) * Level.CellSize + shot.East, 0f, station.FloorCells.Min(cell => cell.Z) * Level.CellSize + shot.South);

    /// <summary>One scene per station, in the order of <see cref="TestCourse.Stations"/>.</summary>
    public static PhotoScene[] All() =>
    [
        new("course-1-arms", "Station 1 of the test course from its south-east corner: every weapon along the north wall, the items on the south wall, the start teleporter on the way and the sign (issues #718, #722).",
            studio => Settled(Within(studio, 0, ArmsShot))),
        new("course-1-arms-row", "Station 1 of the test course close to its row of weapons on the north wall, from the east end of the way to the sign over the first cell (issue #736).",
            studio => Settled(Within(studio, 0, ArmsRowShot))),
        new("course-1-axe", "Like course-1-arms-row, the hunter's axe in hand: its model in the light of the test course (issue #776).",
            studio => Settled(AxeInHand(Within(studio, 0, ArmsRowShot)))),
        new("course-1-crossbow", "Like course-1-arms-row, the stake crossbow in the first place: its placeholder boxes in the light of the test course (issue #203).",
            studio => ArmsRowWith(studio, CrossbowInHand)),
        new("course-2-columns", "Station 2 of the test course from its far corner back to its sign: two columns either side of the way, silver beside one, a ghoul beyond (issues #718, #722).",
            studio => new PhotoSetup(Within(studio, 1, ColumnsShot))),
        new("course-3-cracked-wall", "Station 3 of the test course close to and at a slant to the cracked wall in the middle of its north wall, the sign over the first cell beyond (issues #718, #722, #736).",
            studio => new PhotoSetup(Within(studio, 2, CrackedWallShot))),
        new("course-4-portcullis", "Station 4 of the test course, sealed: from inside the arena back at the fallen grate and its sign, the bat circling by the column; the player is hurt by then, the enemies attack while the level's name fades (issues #718, #722).",
            SealedArena),
        new("course-5-teleporter", "Station 5 of the test course from its north-east corner back to its sign: the teleporter turned towards the way in, Sirius' Relief beside the way (issue #718).",
            studio => Settled(Within(studio, 4, room => (LastX(room), CourseRoom.Top), room => SignOf(room)))),
        new("course-6-boss", "Station 6 of the test course from its north-east corner: the antechamber, silver and a blood vial before the boss portal (issue #718).",
            studio => Settled(Within(studio, 5, room => (LastX(room), CourseRoom.Top), room => PortalOf(room)))),
        new("course-boss-stairs", "The boss room of the test course after the victory: Gorm dead, the spiral staircase risen in its middle and its lamp lit, from 6 m towards the portal, up to under the 6 m ceiling, the level's name faded (issues #727, #722).",
            StairsAfterVictory),
    ];

    /// <summary>Like course-1-arms-row, with what <paramref name="inHand"/> puts in a player's hand (issue #786: the whip).</summary>
    internal static PhotoSetup ArmsRowWith(PhotoStudio studio, Func<GameWorld, GameWorld> inHand) =>
        Settled(inHand(Within(studio, 0, ArmsRowShot)));

    // The picture is taken after the level's name has faded, so it covers nothing.
    private static PhotoSetup Settled(GameWorld world) => new(world) { Ticks = BannerTicks };

    private static GameWorld AxeInHand(GameWorld world)
    {
        world.Player.EquipMelee(WeaponKind.HuntersAxe);
        return world;
    }

    // In the first place, the one a run starts with in hand.
    private static GameWorld CrossbowInHand(GameWorld world)
    {
        world.Player.Equip(0, WeaponKind.StakeCrossbow);
        return world;
    }

    // Cells between a player and the staircase in the course's boss room, towards the portal (north).
    private const int StairsDistanceCells = 3;

    // The boss room behind station 6 after the fight: Gorm dead before the first tick, a player in the room between the
    // portal and the staircase, looking at it two thirds up. The scene runs until it stands, as stairs-4m does, and
    // until the level's name has faded. The sign of station 6 hangs in the antechamber, behind the south wall of the boss
    // room and more than the 16 m a sign shows from (ShowroomLabelSight): it cannot be in this picture.
    private static PhotoSetup StairsAfterVictory(PhotoStudio studio)
    {
        var (world, _) = OpenedUpTo(studio, 5);
        var boss = world.Rooms.Single(room => room.Kind == RoomKind.Boss);
        var (x, z) = Stairs.CellIn(boss.Cells);
        world.Player.PlaceAt(world.Level.CellCenter(x, z - StairsDistanceCells));
        world.Gorm!.TakeDamage(world.Gorm.Health, new List<GameEvent>());
        PhotoStudio.Face(world.Player, world.Level.CellCenter(x, z) + new Vector3(0f, Stairs.Height * 2f / 3f, 0f));
        var start = world.Ticks;
        return new PhotoSetup(world)
        {
            Ticks = 600,
            StopWhen = world => world.Ticks - start >= BannerTicks && world.Stairs is { CanInteract: true },
        };
    }

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

    // The same, the player standing and looking as `shot` says.
    private static GameWorld Within(PhotoStudio studio, int index, Shot shot)
    {
        var (world, course) = OpenedUpTo(studio, index);
        world.Player.PlaceAt(FeetOf(course.Stations[index], shot));
        PhotoStudio.Face(world.Player, world.Player.EyePosition + Direction(shot) * 10f);
        return world;
    }

    /// <summary>The direction a <see cref="Shot"/> looks in, as a unit vector.</summary>
    internal static Vector3 Direction(Shot shot)
    {
        var yaw = shot.YawDegrees * MathF.PI / 180f;
        var pitch = shot.PitchDegrees * MathF.PI / 180f;
        return new Vector3(MathF.Sin(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), MathF.Cos(yaw) * MathF.Cos(pitch));
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
        return new PhotoSetup(world) { Ticks = Math.Max(settle, BannerTicks) };
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

    // The middle of the boss portal in the station's south wall, where the portal pictures look (PortalLookHeight).
    private static Vector3 PortalOf(PlacedRoom room) =>
        new(((float)room.FloorCells.Average(cell => cell.X) + 0.5f) * Level.CellSize, PhotoStudio.PortalLookHeight, (CourseRoom.Bottom + 1) * Level.CellSize);
}
