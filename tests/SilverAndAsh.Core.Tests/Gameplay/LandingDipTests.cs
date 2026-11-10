using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class LandingDipTests
{
    private static readonly PlayerInput Jump = PlayerInput.None with { Jump = true };
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };

    private static readonly Level Room = LevelParser.Parse(".....\n.....\n..P..\n.....\n.....");

    [Fact]
    public void Standing_and_walking_cause_no_dip()
    {
        var player = new Player(Room);

        for (var i = 0; i < 60; i++)
        {
            player.Tick(i < 30 ? PlayerInput.None : Forward);
            Assert.Equal(0f, player.LandingDip.Depth);
        }
    }

    [Fact]
    public void Landing_after_a_jump_dips_the_eyes()
    {
        var player = new Player(Room);
        var maxDip = JumpAndLand(player);

        Assert.InRange(maxDip, 0.05f, Tuning.Player.LandingDipMax);
    }

    [Fact]
    public void The_dip_lowers_the_drawn_eye_position_and_tilts_the_view()
    {
        var player = new Player(Room);
        JumpAndLand(player);

        var normalEyes = player.Position.Y + Tuning.Player.EyeHeight;
        Assert.True(player.InterpolatedEyePosition(1f).Y < normalEyes);
        Assert.True(player.InterpolatedViewTilt(1f) > 0f);
    }

    [Fact]
    public void The_dip_springs_back_within_half_a_second()
    {
        var player = new Player(Room);
        JumpAndLand(player);

        RunTicks(player, PlayerInput.None, SimulationClock.SecondsToTicks(0.5f));

        Assert.Equal(0f, player.LandingDip.Depth);
    }

    [Fact]
    public void The_dip_never_grows_while_springing_back()
    {
        var player = new Player(Room);
        JumpAndLand(player);

        var previous = player.LandingDip.Depth;
        for (var i = 0; i < 30; i++)
        {
            player.Tick(PlayerInput.None);
            Assert.True(player.LandingDip.Depth <= previous);
            previous = player.LandingDip.Depth;
        }
    }

    [Fact]
    public void Harder_landings_dip_more_but_never_beyond_the_limit()
    {
        var soft = new LandingDip();
        var hard = new LandingDip();
        var extreme = new LandingDip();

        soft.Land(2f);
        hard.Land(6f);
        extreme.Land(100f);

        Assert.True(soft.Depth < hard.Depth);
        Assert.Equal(Tuning.Player.LandingDipMax, extreme.Depth);
    }

    [Fact]
    public void Walking_up_stairs_is_not_a_landing()
    {
        var player = new Player(LevelParser.Parse("P\n1\n2\n3"));

        for (var i = 0; i < 2 * SimulationClock.TicksPerSecond; i++)
        {
            player.Tick(Forward);
            Assert.Equal(0f, player.LandingDip.Depth);
        }
    }

    /// <summary>Jumps once and returns the deepest dip seen right after landing.</summary>
    private static float JumpAndLand(Player player)
    {
        player.Tick(Jump);
        var guard = 0;
        while (!player.IsGrounded && guard++ < 600)
        {
            player.Tick(PlayerInput.None);
        }

        return player.LandingDip.Depth;
    }

    private static void RunTicks(Player player, PlayerInput input, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            player.Tick(input);
        }
    }
}
