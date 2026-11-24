using System.Numerics;
using SilverAndAsh.Core.Course;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.Showroom;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Photos;

/// <summary>
/// The camera stands of the station photos of the test course (issue #722): each picture shows what its station is for
/// and its sign. The tests record that without comparing pixels: what is to be in the picture lies in the view cone of
/// the camera as the game sets it up (CameraFrame: its field of view and the shape of a photo's picture) and no wall stands in the line of sight.
/// </summary>
public class CourseStationPhotoTests
{
    private const float VerticalFovDegrees = CameraFrame.FieldOfViewDegrees;
    private static readonly float Aspect = CameraFrame.PhotoAspect;

    // How much of the half-picture a point may use: it must lie well inside, not on the edge.
    private const float Margin = 0.9f;

    private static readonly CourseLayout Course = TestCourse.Build(PhotoStudio.Seed, Balance.Default);

    private static (PhotoSetup Setup, int Ran) Taken(string scene)
    {
        var studio = new PhotoStudio(RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms")), Balance.Default);
        var setup = PhotoScenes.Find(scene)!.Build(studio);
        var ran = setup.Run();
        return (setup, ran);
    }

    private static Vector3 SignOf(string text) => Course.Labels.Single(label => label.Text == text).Position;

    /// <summary>True when the point lies inside the picture (with the margin) and no wall or floor blocks the way to it (the rule the game applies to its signs; columns do not block it).</summary>
    private static bool Shows(GameWorld world, Vector3 point)
    {
        var eye = world.Player.EyePosition;
        var forward = world.Player.View.Forward;
        var right = world.Player.View.FlatRight;
        var up = Vector3.Cross(right, forward);
        var d = point - eye;
        var depth = Vector3.Dot(d, forward);
        if (depth <= 0.1f)
        {
            return false;
        }

        var tanV = MathF.Tan(VerticalFovDegrees * MathF.PI / 360f);
        var inside = MathF.Abs(Vector3.Dot(d, up) / depth) <= tanV * Margin
            && MathF.Abs(Vector3.Dot(d, right) / depth) <= tanV * Aspect * Margin;
        return inside && ShowroomLabelSight.Sees(world.Level, eye, point);
    }

    private static void AssertShows(GameWorld world, string what, Vector3 point) =>
        Assert.True(Shows(world, point), $"{what} at {point} is not in the picture (eye {world.Player.EyePosition}, looking {world.Player.View.Forward}).");

    /// <summary>Where a point lies across the picture: -1 at the left edge, 0 in the middle, 1 at the right edge.</summary>
    private static float AcrossThePicture(GameWorld world, Vector3 point)
    {
        var d = point - world.Player.EyePosition;
        var depth = Vector3.Dot(d, world.Player.View.Forward);
        var tanV = MathF.Tan(VerticalFovDegrees * MathF.PI / 360f);
        return Vector3.Dot(d, world.Player.View.FlatRight) / depth / (tanV * Aspect);
    }

    // Where the player's hands begin, down the picture (see DownThePicture): below 60 % of its height.
    private const float HandsTop = 0.2f;

    /// <summary>Where a point lies down the picture: -1 at the top edge, 0 in the middle, 1 at the bottom edge.</summary>
    private static float DownThePicture(GameWorld world, Vector3 point)
    {
        var d = point - world.Player.EyePosition;
        var forward = world.Player.View.Forward;
        var up = Vector3.Cross(world.Player.View.FlatRight, forward);
        var tanV = MathF.Tan(VerticalFovDegrees * MathF.PI / 360f);
        return -Vector3.Dot(d, up) / Vector3.Dot(d, forward) / tanV;
    }

    private static void AssertShowsItsSign(GameWorld world, string text)
    {
        var sign = SignOf(text);
        AssertShows(world, $"the sign '{text}'", sign); // includes the game's own rule (ShowroomLabelSight.Sees)
    }

    [Fact]
    public void Station_1_shows_its_sign_the_five_weapons_and_an_item()
    {
        var (setup, _) = Taken("course-1-arms");
        var world = setup.World;

        AssertShowsItsSign(world, "1 - Arms");
        Assert.Equal(5, Course.Finds.Weapons.Count);
        foreach (var weapon in Course.Finds.Weapons)
        {
            AssertShows(world, "a weapon", weapon.Position + new Vector3(0f, 0.2f, 0f));
        }

        AssertShows(world, "the Leech", Course.Finds.Items.First(item => item.Kind == ItemKind.Leech).Position + new Vector3(0f, 0.2f, 0f));
    }

    [Fact]
    public void The_second_photo_of_station_1_shows_every_weapon_nearer_than_the_first_and_above_the_hands()
    {
        // Issue #736: in the first photo the weapons are small specks. The second stands close to the row; the sign is in
        // the first (with it, this one would stand as far off).
        var overview = Taken("course-1-arms").Setup.World;
        var (setup, ran) = Taken("course-1-arms-row");
        var world = setup.World;

        Assert.True(ran >= CourseScenes.BannerTicks, "The level's name still covers the picture.");
        Assert.Equal(5, Course.Finds.Weapons.Count);
        foreach (var weapon in Course.Finds.Weapons)
        {
            var point = weapon.Position + new Vector3(0f, 0.2f, 0f);
            AssertShows(world, "a weapon", point);
            Assert.True(
                Vector3.Distance(world.Player.EyePosition, point) < Vector3.Distance(overview.Player.EyePosition, point) - 1f,
                $"The weapon at {weapon.Position} is not nearer than in the first photo.");
            // The hands of the player cover the lower part of the picture.
            Assert.True(DownThePicture(world, point) < HandsTop, $"The weapon at {weapon.Position} lies behind the hands.");
        }

        Assert.True(Course.Finds.Weapons.Min(weapon => Vector3.Distance(world.Player.EyePosition, weapon.Position)) < 4f, "The camera is too far from the row.");
    }

    [Fact]
    public void Station_2_shows_its_sign_both_columns_the_silver_and_the_ghoul()
    {
        var (setup, _) = Taken("course-2-columns");
        var world = setup.World;

        AssertShowsItsSign(world, "2 - Columns");
        Assert.True(world.Level.Columns.Count >= 2);
        foreach (var column in world.Level.Columns.Where(column => column.X is > 18f and < 24f))
        {
            AssertShows(world, "a column", new Vector3(column.X, 1.5f, column.Z));
        }

        AssertShows(world, "the silver", Course.Finds.Loot.First().Position + new Vector3(0f, 0.3f, 0f));
        var ghoul = world.Ghouls.OrderBy(ghoul => Vector2.Distance(new Vector2(ghoul.Position.X, ghoul.Position.Z), new Vector2(25f, 11f))).First().Position + new Vector3(0f, 1f, 0f);
        AssertShows(world, "the ghoul", ghoul);

        // The composition, not only "somewhere in view": the sign over the entrance stands left of the middle, the columns
        // in the middle and the ghoul at the right. A camera turned by 40 degrees moves them out of these bands.
        Assert.InRange(AcrossThePicture(world, SignOf("2 - Columns")), -0.4f, 0.3f);
        foreach (var column in world.Level.Columns.Where(column => column.X is > 18f and < 24f))
        {
            Assert.InRange(AcrossThePicture(world, new Vector3(column.X, 1.5f, column.Z)), -0.5f, 0.5f);
        }

        Assert.InRange(AcrossThePicture(world, ghoul), 0.5f, 0.9f);
    }

    [Fact]
    public void Station_3_shows_its_sign_and_the_cracked_wall_from_closer_than_before()
    {
        var (setup, _) = Taken("course-3-cracked-wall");
        var world = setup.World;

        AssertShowsItsSign(world, "3 - Cracked wall");
        var crack = world.Doors.Single(door => door.Kind == DoorKind.Cracked);
        var middle = (crack.BoxMin + crack.BoxMax) / 2f;
        AssertShows(world, "the cracked wall", new Vector3(middle.X, middle.Y, crack.BoxMax.Z + 0.1f));
        Assert.True(Vector3.Distance(world.Player.Position, new Vector3(middle.X, 0f, crack.BoxMax.Z)) < 6f, "The camera is too far from the crack.");
    }

    [Fact]
    public void Station_3_looks_at_the_crack_from_near_and_at_a_slant()
    {
        // Issue #736: from 5 m straight on, the crack was a thin line. Nearer, and at a slant, its relief shows.
        var world = Taken("course-3-cracked-wall").Setup.World;
        var crack = world.Doors.Single(door => door.Kind == DoorKind.Cracked);
        var face = new Vector3((crack.BoxMin.X + crack.BoxMax.X) / 2f, world.Player.EyePosition.Y, crack.BoxMax.Z);

        var toCrack = face - world.Player.EyePosition;
        Assert.True(toCrack.Length() < 4f, $"The camera is {toCrack.Length()} m from the crack.");
        // The wall faces south (+Z): the angle between the line of sight and the wall's normal.
        var slant = MathF.Acos(Vector3.Dot(Vector3.Normalize(-toCrack), Vector3.UnitZ)) * 180f / MathF.PI;
        Assert.InRange(slant, 30f, 70f);
    }

    [Fact]
    public void The_banner_has_faded_after_the_ticks_the_photos_wait_for_it()
    {
        // As the photo counts it down: from its full seconds, one tick's seconds at a time (Program, --photo).
        var left = LevelNameBanner.Seconds;
        for (var tick = 0; tick < CourseScenes.BannerTicks; tick++)
        {
            left = LevelNameBanner.After(left, SimulationClock.SecondsPerTick);
        }

        Assert.False(LevelNameBanner.IsShown(left), $"The banner still shows after {CourseScenes.BannerTicks} ticks ({left} s left).");
    }

    [Fact]
    public void The_banner_still_shows_a_tick_before_its_seconds_are_up()
    {
        // The other side of the countdown: the photos do not wait for nothing.
        var left = LevelNameBanner.Seconds;
        for (var tick = 0; tick < SimulationClock.SecondsToTicks(LevelNameBanner.Seconds) - 1; tick++)
        {
            left = LevelNameBanner.After(left, SimulationClock.SecondsPerTick);
        }

        Assert.True(LevelNameBanner.IsShown(left));
    }

    [Theory]
    [MemberData(nameof(Shots))]
    public void A_camera_stand_moves_along_when_a_station_before_it_gets_wider(string name, int index)
    {
        // Issue #736: the stands are given from the corner of their station, not in world metres.
        var shot = ShotNamed(name);
        var wider = TestCourse.Stations.Select((station, i) => i == 0 ? station with { Width = station.Width + 2 } : station).ToList();
        var widerCourse = TestCourse.Build(PhotoStudio.Seed, Balance.Default, wider);

        var before = CourseScenes.FeetOf(Course.Stations[index], shot);
        var after = CourseScenes.FeetOf(widerCourse.Stations[index], shot);

        var moved = index == 0 ? 0f : 2 * Level.CellSize;
        Assert.Equal(before.X + moved, after.X, 3);
        Assert.Equal(before.Z, after.Z, 3);
    }

    public static TheoryData<string, int> Shots() => new()
    {
        { nameof(CourseScenes.ArmsShot), 0 },
        { nameof(CourseScenes.ArmsRowShot), 0 },
        { nameof(CourseScenes.ColumnsShot), 1 },
        { nameof(CourseScenes.CrackedWallShot), 2 },
    };

    private static CourseScenes.Shot ShotNamed(string name) => name switch
    {
        nameof(CourseScenes.ArmsShot) => CourseScenes.ArmsShot,
        nameof(CourseScenes.ArmsRowShot) => CourseScenes.ArmsRowShot,
        nameof(CourseScenes.ColumnsShot) => CourseScenes.ColumnsShot,
        _ => CourseScenes.CrackedWallShot,
    };

    [Fact]
    public void The_station_photos_stand_where_their_shot_says()
    {
        var world = Taken("course-3-cracked-wall").Setup.World;
        var feet = CourseScenes.FeetOf(Course.Stations[2], CourseScenes.CrackedWallShot);

        Assert.Equal(feet.X, world.Player.Position.X, 3);
        Assert.Equal(feet.Z, world.Player.Position.Z, 3);
    }

    [Fact]
    public void Station_4_shows_its_sign_the_fallen_grate_and_the_bat_after_the_name_has_faded()
    {
        var (setup, ran) = Taken("course-4-portcullis");
        var world = setup.World;

        AssertShowsItsSign(world, "4 - Portcullis");
        var grate = world.Doors.Single(door => door.HasGrate && door.X < world.Player.Position.X / Level.CellSize && door.IsBarred);
        Assert.True(grate.GrateHeight <= 0f, "The grate has not fallen yet.");
        AssertShows(world, "the fallen grate", new Vector3(grate.BoxMax.X + 0.1f, (grate.BoxMin.Y + grate.BoxMax.Y) / 2f, (grate.BoxMin.Z + grate.BoxMax.Z) / 2f));
        AssertShows(world, "the bat", world.Bats.First().Position);
        Assert.True(ran >= CourseScenes.BannerTicks, "The level's name still covers the picture.");
    }

    [Fact]
    public void Station_5_shows_its_sign_after_the_name_has_faded()
    {
        var (setup, ran) = Taken("course-5-teleporter");

        AssertShowsItsSign(setup.World, "5 - Teleporter");
        Assert.True(ran >= CourseScenes.BannerTicks);
    }

    [Fact]
    public void Station_6_shows_its_sign_and_the_portal_after_the_name_has_faded()
    {
        var (setup, ran) = Taken("course-6-boss");
        var world = setup.World;

        AssertShowsItsSign(world, "6 - Boss");
        var portal = world.Doors.Single(door => door.Kind == DoorKind.Boss);
        AssertShows(world, "the boss portal", new Vector3((portal.BoxMin.X + portal.BoxMax.X) / 2f, (portal.BoxMin.Y + portal.BoxMax.Y) / 2f, portal.BoxMin.Z - 0.1f));
        Assert.True(ran >= CourseScenes.BannerTicks);
    }

    [Fact]
    public void The_boss_room_scene_shows_the_staircase_after_the_name_has_faded()
    {
        var (setup, ran) = Taken("course-boss-stairs");
        var world = setup.World;

        Assert.True(world.Stairs is { CanInteract: true });
        Assert.True(ran >= CourseScenes.BannerTicks, "The level's name still covers the picture.");
        var boss = world.Rooms.Single(room => room.Kind == RoomKind.Boss);
        var (x, z) = Stairs.CellIn(boss.Cells);
        AssertShows(world, "the staircase", world.Level.CellCenter(x, z) + new Vector3(0f, 1.5f, 0f));
    }

    [Fact]
    public void The_stairs_scene_cannot_show_the_sign_because_it_hangs_out_of_sight()
    {
        // Recorded so nobody looks for a camera stand that shows both: the sign hangs in the antechamber, behind the south
        // wall of the boss room, and the game does not draw it from there (issue #722).
        var (setup, _) = Taken("course-boss-stairs");

        Assert.False(ShowroomLabelSight.Sees(setup.World.Level, setup.World.Player.EyePosition, SignOf("6 - Boss")));
    }

    [Fact]
    public void The_arch_scene_looks_at_the_springer_with_the_leaves_half_open()
    {
        var (setup, _) = Taken("door-normal-open-below");
        var world = setup.World;
        var door = world.Doors.Single();
        var middle = (door.BoxMin + door.BoxMax) / 2f;

        AssertShows(world, "the arch springer", new Vector3(middle.X, door.BoxMin.Y, middle.Z) + PhotoScenes.BelowTheArchTarget);
        Assert.True(door.OpenFraction >= PhotoScenes.BelowTheArchSwing);
        Assert.True(door.OpenFraction < 1f, "The leaves are open all the way and hide the springer.");
    }

    [Fact]
    public void The_two_arch_scenes_differ_in_the_door_with_a_grate_and_in_nothing_else()
    {
        // The guide ring of a portcullis shows at the arch springer even with the grate up (review of PR #734), so the
        // barred scene is a picture of its own: the same camera, the same swing, only the door has a grate.
        var (plain, plainRan) = Taken("door-normal-open-below");
        var (barred, barredRan) = Taken("door-barred-open-below");

        Assert.False(plain.World.Doors.Single().HasGrate);
        Assert.True(barred.World.Doors.Single().HasGrate);
        Assert.Equal(plain.World.Player.EyePosition, barred.World.Player.EyePosition);
        Assert.Equal(plain.World.Player.View.Forward, barred.World.Player.View.Forward);
        Assert.Equal(plainRan, barredRan);
        var door = barred.World.Doors.Single();
        Assert.Equal(plain.World.Doors.Single().OpenFraction, door.OpenFraction);
        AssertShows(barred.World, "the arch springer", new Vector3((door.BoxMin.X + door.BoxMax.X) / 2f, door.BoxMin.Y, (door.BoxMin.Z + door.BoxMax.Z) / 2f) + PhotoScenes.BelowTheArchTarget);
    }

    [Fact]
    public void The_scenes_with_awake_ghouls_are_taken_at_once_and_the_others_after_the_name_has_faded()
    {
        // Settling an awake ghoul for 215 ticks lets it walk up to the camera; scenes 2 and 3 therefore stand at once and
        // keep the sign above the faint ghost of the name.
        Assert.Equal(1, Taken("course-2-columns").Ran);
        Assert.Equal(1, Taken("course-3-cracked-wall").Ran);
        Assert.True(Taken("course-1-arms").Ran >= CourseScenes.BannerTicks);
    }
}
