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
    public void Landing_after_a_jump_dips_the_eyes_a_few_centimetres()
    {
        var player = new Player(Room);
        JumpAndLand(player);

        var (deepest, _) = WatchDip(player, SimulationClock.TicksPerSecond);

        Assert.InRange(deepest, 0.04f, 0.12f);
    }

    [Fact]
    public void The_dip_lowers_the_drawn_eye_position_and_tilts_the_view()
    {
        var player = new Player(Room);
        JumpAndLand(player);
        RunTicks(player, PlayerInput.None, 5);

        var normalEyes = player.Position.Y + Tuning.Player.EyeHeight;
        Assert.True(player.InterpolatedEyePosition(1f).Y < normalEyes);
        Assert.True(player.InterpolatedViewTilt(1f) > 0f);
    }

    [Fact]
    public void The_dip_is_soft_no_sudden_jumps_between_ticks()
    {
        // Softness = the camera never moves more than 1 cm per tick (at 60 ticks per second).
        // The first version moved the full dip in a single tick - that was the unpleasant twitch.
        var player = new Player(Room);
        JumpAndLand(player);

        var (_, largestStep) = WatchDip(player, SimulationClock.TicksPerSecond);

        Assert.True(largestStep <= 0.01f, $"Largest change per tick was {largestStep:0.0000} m.");
    }

    [Fact]
    public void The_lowest_point_comes_after_a_moment_not_instantly()
    {
        var player = new Player(Room);
        JumpAndLand(player);

        var deepestTick = 0;
        var deepest = 0f;
        for (var tick = 1; tick <= 60; tick++)
        {
            player.Tick(PlayerInput.None);
            if (player.LandingDip.Depth > deepest)
            {
                deepest = player.LandingDip.Depth;
                deepestTick = tick;
            }
        }

        // Halfway through the nod.
        var expected = SimulationClock.SecondsToTicks(Tuning.Player.LandingDipDuration / 2f);
        Assert.InRange(deepestTick, expected - 3, expected + 3);
    }

    [Fact]
    public void The_camera_comes_back_without_overshooting_and_comes_to_rest()
    {
        var player = new Player(Room);
        JumpAndLand(player);

        for (var i = 0; i < SimulationClock.TicksPerSecond; i++)
        {
            player.Tick(PlayerInput.None);

            // Never above the normal eye height ("no bouncing").
            Assert.True(player.LandingDip.Depth >= 0f);
        }

        Assert.Equal(0f, player.LandingDip.Depth);
    }

    [Fact]
    public void A_second_landing_during_a_nod_does_not_restart_it()
    {
        var dip = new LandingDip();
        dip.Land(6f);
        for (var i = 0; i < 5; i++)
        {
            dip.Tick();
        }

        var before = dip.Depth;
        dip.Land(10f);
        dip.Tick();

        // Still on the same smooth wave - no jump.
        Assert.InRange(dip.Depth - before, 0f, 0.01f);
    }

    [Fact]
    public void Harder_landings_dip_more_but_never_beyond_the_limit()
    {
        Assert.True(PeakDepth(2f) < PeakDepth(6f));
        Assert.True(PeakDepth(100f) <= Tuning.Player.LandingDipMax + 0.001f);
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

    private static float PeakDepth(float fallSpeed)
    {
        var dip = new LandingDip();
        dip.Land(fallSpeed);
        var peak = 0f;
        for (var i = 0; i < 60; i++)
        {
            dip.Tick();
            peak = MathF.Max(peak, dip.Depth);
        }

        return peak;
    }

    /// <summary>Jumps once and waits until the player has landed.</summary>
    private static void JumpAndLand(Player player)
    {
        player.Tick(Jump);
        var guard = 0;
        while (!player.IsGrounded && guard++ < 600)
        {
            player.Tick(PlayerInput.None);
        }
    }

    /// <summary>Watches the dip for some ticks: deepest point and largest change between two ticks.</summary>
    private static (float Deepest, float LargestStep) WatchDip(Player player, int ticks)
    {
        var deepest = player.LandingDip.Depth;
        var largestStep = 0f;
        var previous = player.LandingDip.Depth;

        for (var i = 0; i < ticks; i++)
        {
            player.Tick(PlayerInput.None);
            var depth = player.LandingDip.Depth;
            deepest = MathF.Max(deepest, depth);
            largestStep = MathF.Max(largestStep, MathF.Abs(depth - previous));
            previous = depth;
        }

        return (deepest, largestStep);
    }

    private static void RunTicks(Player player, PlayerInput input, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            player.Tick(input);
        }
    }
}
