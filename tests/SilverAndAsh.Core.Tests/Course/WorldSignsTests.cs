using SilverAndAsh.Core.Course;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.Showroom;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Course;

/// <summary>
/// Which signs a world shows (issue #722, review of PR #728): the world decides, not the launch option - so the photos
/// of the course, which build their world without <c>--course</c>, show the station signs as the game does.
/// </summary>
public class WorldSignsTests
{
    private const int Seed = 1;

    [Fact]
    public void The_test_course_shows_its_station_signs()
    {
        var world = TestCourse.NewWorld(Seed, Balance.Default);

        var signs = WorldSigns.Of(world);

        Assert.NotNull(signs);
        Assert.True(world.IsTestCourse);
        Assert.Equal(TestCourse.Build(Seed, Balance.Default).Labels.Select(label => label.Text), signs.Select(label => label.Text));
        Assert.Equal("1 - Arms", signs[0].Text);
    }

    [Fact]
    public void The_course_after_a_finished_one_shows_its_station_signs_too()
    {
        var again = TestCourse.NextWorld(TestCourse.NewWorld(Seed, Balance.Default));

        Assert.True(again.IsTestCourse);
        Assert.Equal(TestCourse.Stations.Count, WorldSigns.Of(again)!.Count);
    }

    [Fact]
    public void The_showroom_shows_the_labels_of_its_exhibits()
    {
        var world = ShowroomLayout.Build().NewWorld(Balance.Default);

        var signs = WorldSigns.Of(world);

        Assert.NotNull(signs);
        Assert.False(world.IsTestCourse);
        Assert.Equal(ShowroomLayout.Build().Labels.Select(label => label.Text), signs.Select(label => label.Text));
    }

    [Fact]
    public void A_run_on_a_generated_level_shows_no_signs()
    {
        var world = new LevelZero(RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"))).NewRun(77, Balance.Default);

        Assert.False(world.IsTestCourse);
        Assert.Null(WorldSigns.Of(world));
    }

    /// <summary>A test world is not always the course: one that is neither course nor showroom has no signs.</summary>
    [Fact]
    public void A_test_world_that_is_not_the_course_shows_no_signs()
    {
        var world = new GameWorld(LevelParser.Parse("#P#\n#.#\n#.#")) { IsTestWorld = true };

        Assert.True(world.IsTestWorld);
        Assert.Null(WorldSigns.Of(world));
    }

    /// <summary>
    /// The bug of PR #728: the photos of the course build their world themselves, not through <c>--course</c>, and lost
    /// their signs. Each of them must show the sign of its station.
    /// </summary>
    [Theory]
    [InlineData("course-1-arms", "1 - Arms")]
    [InlineData("course-2-columns", "2 - Columns")]
    [InlineData("course-3-cracked-wall", "3 - Cracked wall")]
    [InlineData("course-4-portcullis", "4 - Portcullis")]
    [InlineData("course-5-teleporter", "5 - Teleporter")]
    [InlineData("course-6-boss", "6 - Boss")]
    public void Each_photo_of_the_course_shows_the_sign_of_its_station(string scene, string sign)
    {
        var studio = new PhotoStudio(RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms")), Balance.Default);

        var world = PhotoScenes.Find(scene)!.Build(studio).World;

        Assert.Contains(sign, WorldSigns.Of(world)!.Select(label => label.Text));
    }

    [Fact]
    public void A_photo_of_a_generated_level_shows_no_course_signs()
    {
        var studio = new PhotoStudio(RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms")), Balance.Default);

        var world = PhotoScenes.Find("room-boss")!.Build(studio).World;

        Assert.Null(WorldSigns.Of(world));
    }
}
