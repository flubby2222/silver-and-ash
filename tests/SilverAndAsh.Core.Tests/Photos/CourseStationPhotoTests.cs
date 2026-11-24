using System.Numerics;
using SilverAndAsh.Core.Course;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.Showroom;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Photos;

/// <summary>
/// The camera stands of the station photos of the test course (issue #722): each picture shows what its station is for
/// and its sign. The tests record that without comparing pixels: what is to be in the picture lies in the view cone of
/// the camera as the game sets it up (70 degrees vertical, 854 x 480) and no wall stands in the line of sight.
/// </summary>
public class CourseStationPhotoTests
{
    private const float VerticalFovDegrees = 70f;
    private const float Aspect = 854f / 480f;

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

    private static void AssertShowsItsSign(GameWorld world, string text)
    {
        var sign = SignOf(text);
        AssertShows(world, $"the sign '{text}'", sign);
        Assert.True(ShowroomLabelSight.Sees(world.Level, world.Player.EyePosition, sign), $"The game does not draw the sign '{text}' from here.");
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
        AssertShows(world, "the ghoul", world.Ghouls.OrderBy(ghoul => Vector2.Distance(new Vector2(ghoul.Position.X, ghoul.Position.Z), new Vector2(25f, 11f))).First().Position + new Vector3(0f, 1f, 0f));
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
    public void The_arch_scene_looks_at_the_springer_with_the_leaves_half_open_and_the_barred_twin_is_gone()
    {
        var (setup, _) = Taken("door-normal-open-below");
        var world = setup.World;
        var door = world.Doors.Single();
        var middle = (door.BoxMin + door.BoxMax) / 2f;

        AssertShows(world, "the arch springer", new Vector3(middle.X, door.BoxMin.Y, middle.Z) + PhotoScenes.BelowTheArchTarget);
        Assert.True(door.OpenFraction >= PhotoScenes.BelowTheArchSwing);
        Assert.True(door.OpenFraction < 1f, "The leaves are open all the way and hide the springer.");
        Assert.Null(PhotoScenes.Find("door-barred-open-below"));
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
