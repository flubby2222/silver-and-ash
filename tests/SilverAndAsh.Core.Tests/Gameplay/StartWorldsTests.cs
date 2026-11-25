using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>Issue #534: what a start plays, and that <c>--room</c> loads only its own template and skips the title screen.</summary>
public class StartWorldsTests
{
    private const int Seed = 534;

    private const string LHallPath = "assets/rooms/combat/l-hall.txt";

    // The repository holds assets/ as the game's folder holds its copy.
    private static string Repository => Path.GetDirectoryName(Path.GetDirectoryName(RepositoryFiles.Find("assets/rooms")))!;

    [Fact]
    public void A_room_starts_in_the_room_without_the_title_screen()
    {
        var start = StartWorlds.For(LaunchOptions.Parse(["--room", LHallPath]), Repository);

        var world = start.NewWorld(Seed, Balance.Default);

        Assert.False(start.OpensOnTitle);
        Assert.Equal("combat/l-hall.txt", start.Preview?.Template.Name);
        Assert.Equal(start.Preview!.Entrance, Level.ToGrid(world.Player.Position.X, world.Player.Position.Z));
        Assert.Contains("combat/l-hall.txt", start.Description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("--seed 7")]
    [InlineData("--level test")]
    public void Every_other_start_opens_on_the_title_screen(string args)
    {
        var start = StartWorlds.For(LaunchOptions.Parse(args.Split(' ', StringSplitOptions.RemoveEmptyEntries)), Repository);

        Assert.True(start.OpensOnTitle);
        Assert.Null(start.Preview);
    }

    [Fact]
    public void Without_options_level_0_is_played()
    {
        var start = StartWorlds.For(LaunchOptions.Parse([]), Repository);

        var world = start.NewWorld(Seed, Balance.Default);

        Assert.Equal(LevelZero.Name, world.LevelName);
        Assert.StartsWith("Level 0 from", start.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void A_level_file_is_played_and_restarted()
    {
        var start = StartWorlds.For(LaunchOptions.Parse(["--level", "test"]), Repository);

        var world = start.NewWorld(Seed, Balance.Default);
        var next = start.NextWorld(world);

        Assert.Null(world.LevelName);
        Assert.Equal(world.Level.Width, next.Level.Width);
        Assert.Contains("test.txt", start.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void No_enemies_reaches_the_room()
    {
        var start = StartWorlds.For(LaunchOptions.Parse(["--room", LHallPath, "--no-enemies"]), Repository);

        Assert.False(start.Preview?.WithEnemies);
        Assert.Empty(start.NewWorld(Seed, Balance.Default).Ghouls);
        Assert.EndsWith("without enemies.", start.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void A_broken_template_elsewhere_does_not_stop_a_room()
    {
        using var folder = BrokenNextToLHall(out var lHall);

        var start = StartWorlds.For(LaunchOptions.Parse(["--room", lHall]), folder.Root);

        Assert.Equal("combat/l-hall.txt", start.Preview?.Template.Name);
        Assert.Single(start.NewWorld(Seed, Balance.Default).Players);
    }

    // The other side of the test above: the same folder does stop level 0, so the broken template really is broken.
    [Fact]
    public void A_broken_template_stops_level_0()
    {
        using var folder = BrokenNextToLHall(out _);

        var error = Assert.Throws<LevelFormatException>(() => StartWorlds.For(LaunchOptions.Parse([]), folder.Root));

        Assert.Contains("broken.txt", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_broken_room_template_itself_is_refused()
    {
        using var folder = BrokenNextToLHall(out var lHall);
        var broken = Path.Combine(Path.GetDirectoryName(lHall)!, "broken.txt");

        Assert.Throws<LevelFormatException>(() => StartWorlds.For(LaunchOptions.Parse(["--room", broken]), folder.Root));
    }

    [Fact]
    public void A_missing_room_template_is_named()
    {
        var error = Assert.Throws<FileNotFoundException>(
            () => StartWorlds.For(LaunchOptions.Parse(["--room", "assets/rooms/combat/no-such-room.txt"]), Repository));

        Assert.Contains("no-such-room.txt", error.Message, StringComparison.Ordinal);
    }

    // A game folder whose assets/rooms/combat holds l-hall and a broken copy of it (an unknown symbol 'Q').
    private static TemplateFolder BrokenNextToLHall(out string lHall)
    {
        var text = File.ReadAllText(RepositoryFiles.Find(LHallPath));
        var folder = new TemplateFolder(Path.Combine("assets", "rooms", "combat"));
        lHall = folder.Write("l-hall.txt", text);
        folder.Write("broken.txt", text.Replace("C", "Q", StringComparison.Ordinal));
        return folder;
    }
}
