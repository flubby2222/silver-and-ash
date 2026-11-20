using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

/// <summary>
/// Every level file that ships with the game (issue #501): a new or changed file under assets/levels that the parser
/// rejects turns this red, not only the two levels the older tests name.
/// </summary>
public class ShippedLevelsTests
{
    public static TheoryData<string> LevelFiles()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(RepositoryFiles.Find("assets/levels")).Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(path));
        }

        return data;
    }

    [Fact]
    public void There_are_level_files()
    {
        Assert.NotEmpty(LevelFiles());
    }

    [Theory]
    [MemberData(nameof(LevelFiles))]
    public void Every_level_file_parses_and_starts_a_world(string name)
    {
        var level = LevelParser.ParseFile(Path.Combine(RepositoryFiles.Find("assets/levels"), name));

        Assert.False(level[level.PlayerStartX, level.PlayerStartZ].IsSolid);
        var world = new GameWorld(level);
        world.Tick(SilverAndAsh.Core.Input.PlayerInput.None);
        Assert.False(world.Player.IsDead);
    }

    [Fact]
    public void In_the_listening_level_the_ghoul_is_on_the_left_and_the_bat_on_the_right()
    {
        // The level for the listening test of directional sound (issue #491): the player looks along the hall.
        var world = new GameWorld(LevelParser.ParseFile(RepositoryFiles.Find("assets/levels/hoerprobe.txt")));
        var listener = SoundListener.Of(world.Player);
        var hearing = Balance.Default.Hearing;

        var ghoul = Hearing.Mix(listener.Ears, listener.Facing, Assert.Single(world.Ghouls).Body.Position, hearing);
        var bat = Hearing.Mix(listener.Ears, listener.Facing, Assert.Single(world.Bats).Body.Position, hearing);

        Assert.True(ghoul is { Pan: < 0f, Volume: > 0f }, $"ghoul {ghoul}");
        Assert.True(bat is { Pan: > 0f, Volume: > 0f }, $"bat {bat}");
    }
}
