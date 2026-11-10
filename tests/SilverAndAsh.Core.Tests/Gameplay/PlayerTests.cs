using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class PlayerTests
{
    [Fact]
    public void Player_starts_on_the_start_cell_with_eyes_at_eye_height()
    {
        var level = LevelParser.Parse("#P#");

        var player = new Player(level);

        Assert.Equal(level.PlayerStartPosition, player.Position);
        Assert.Equal(level.PlayerStartPosition + new Vector3(0f, Tuning.Player.EyeHeight, 0f), player.EyePosition);
    }

    [Fact]
    public void Look_input_turns_the_view()
    {
        var player = new Player(LevelParser.Parse("P"));

        player.Look(PlayerInput.None with { Look = new Vector2(50f, 0f) });

        Assert.NotEqual(0f, player.View.YawRadians);
    }
}
