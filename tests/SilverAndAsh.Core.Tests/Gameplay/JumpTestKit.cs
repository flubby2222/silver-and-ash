using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>Shared helpers for tests about jumping, perfect jumps and stamina.</summary>
internal static class JumpTestKit
{
    public static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    public static readonly PlayerInput ForwardJump = Forward with { Jump = true };

    /// <summary>The perfect window in ticks.</summary>
    public static readonly int Window = SimulationClock.SecondsToTicks(Tuning.Player.PerfectJumpWindow);

    /// <summary>Ticks from pressing jump until the player is on the ground again (same for every jump on flat ground).</summary>
    public static readonly int AirTicks = MeasureAirTicks();

    public static float NormalAirSpeed => Tuning.Player.MaxSpeed * Tuning.Player.AirSpeedMultiplier;

    public static float BonusAirSpeed => NormalAirSpeed * (1f + Tuning.Player.PerfectJumpSpeedBonus);

    // Exhausted: no air boost, the penalty applies to the walking speed.
    public static float ExhaustedAirSpeed => Tuning.Player.MaxSpeed * (1f - Tuning.Player.ExhaustedJumpSpeedPenalty);

    /// <summary>A player at full running speed in a long corridor (no walls in the way).</summary>
    public static Player RunningPlayer()
    {
        var corridor = string.Join('\n', Enumerable.Repeat(".", 120).Prepend("P"));
        var player = new Player(LevelParser.Parse(corridor));
        RunTicks(player, Forward, 30);
        return player;
    }

    /// <summary>
    /// Starts right after take-off (the first tick in the air). Presses jump a few ticks before the landing,
    /// so the jump is queued; stops after the landing, before the queued jump takes off.
    /// </summary>
    public static void PressShortlyBeforeLanding(Player player)
    {
        RunTicks(player, Forward, AirTicks - Window / 2 - 1);
        player.Tick(ForwardJump);
        while (!player.IsGrounded)
        {
            player.Tick(Forward);
        }
    }

    /// <summary>
    /// Starts right after take-off: presses jump shortly before each landing, <paramref name="hops"/> times.
    /// Counted from each take-off, so the timing does not drift from hop to hop.
    /// Afterwards the player has just taken off with the last timed jump.
    /// </summary>
    public static void ChainTimedJumps(Player player, int hops)
    {
        for (var hop = 0; hop < hops; hop++)
        {
            PressShortlyBeforeLanding(player);
            while (player.IsGrounded)
            {
                player.Tick(Forward);                       // the queued jump takes off
            }
        }
    }

    /// <summary>Runs until the player goes from "in the air" to "on the ground".</summary>
    public static void RunUntilLanded(Player player, PlayerInput? input = null)
    {
        var tickInput = input ?? Forward;
        var guard = 0;
        while (player.IsGrounded && guard++ < 300)
        {
            player.Tick(tickInput);
        }

        while (!player.IsGrounded && guard++ < 600)
        {
            player.Tick(tickInput);
        }
    }

    /// <summary>Runs 40 ticks and returns the deepest landing dip seen.</summary>
    public static float DeepestDip(Player player)
    {
        var deepest = 0f;
        for (var i = 0; i < 40; i++)
        {
            deepest = MathF.Max(deepest, player.LandingDip.Depth);
            player.Tick(Forward);
        }

        return deepest;
    }

    public static float HorizontalSpeed(Player player) => new Vector2(player.Velocity.X, player.Velocity.Z).Length();

    public static void RunTicks(Player player, PlayerInput input, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            player.Tick(input);
        }
    }

    private static int MeasureAirTicks()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        var ticks = 1;
        while (!player.IsGrounded)
        {
            player.Tick(Forward);
            ticks++;
        }

        return ticks;
    }
}
