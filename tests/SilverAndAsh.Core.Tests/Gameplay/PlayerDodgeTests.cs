using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.Gameplay.JumpTestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class PlayerDodgeTests
{
    private const float Tolerance = 0.01f;

    private static readonly int DodgeTicks = SimulationClock.SecondsToTicks(Tuning.Player.DodgeDuration);

    private static readonly PlayerInput Shift = PlayerInput.None with { Dash = true };

    private static float SegmentFraction => 1f / Tuning.Player.StaminaJumps;

    [Fact]
    public void Shift_on_the_ground_starts_a_dodge_that_costs_two_segments()
    {
        var player = StandingPlayer();

        player.Tick(Shift);

        Assert.True(player.IsDodging);
        Assert.False(player.IsDashing);
        Assert.Equal(1f - Tuning.Player.DodgeStaminaSegments * SegmentFraction, player.Stamina.Fraction, Tolerance);
    }

    [Fact]
    public void Shift_in_the_air_is_still_the_dash()
    {
        var player = StandingPlayer();
        player.Tick(PlayerInput.None with { Jump = true });
        RunTicks(player, PlayerInput.None, 5);

        player.Tick(Shift);

        Assert.True(player.IsDashing);
        Assert.False(player.IsDodging);
    }

    [Fact]
    public void A_dodge_covers_duration_times_speed()
    {
        var player = StandingPlayer();
        var start = player.Position;

        player.Tick(Shift);
        RunTicks(player, PlayerInput.None, DodgeTicks - 1);

        Assert.False(player.IsDodging);
        var expected = Tuning.Player.DodgeDuration * Tuning.Player.DodgeSpeed;
        Assert.Equal(expected, Vector3.Distance(start, player.Position), Tolerance);
    }

    [Fact]
    public void Without_a_walking_direction_the_dodge_goes_forward()
    {
        var player = StandingPlayer();
        var start = player.Position;

        player.Tick(Shift);
        RunTicks(player, PlayerInput.None, DodgeTicks - 1);

        var moved = Vector3.Normalize(player.Position - start);
        Assert.Equal(1f, Vector3.Dot(moved, player.View.FlatForward), Tolerance);
    }

    [Fact]
    public void The_dodge_follows_the_walking_direction_freely()
    {
        // Wish from playtesting: full control during the dodge. It starts forward, and one tick of
        // "right" turns it fully to the right - even a full turn around works.
        var player = new Player(OpenField());
        var right = PlayerInput.None with { Move = new Vector2(1f, 0f) };
        var back = PlayerInput.None with { Move = new Vector2(0f, -1f) };

        player.Tick(Shift);
        player.Tick(right);
        Assert.Equal(1f, Vector3.Dot(FlatVelocityDirection(player), player.View.FlatRight), Tolerance);
        Assert.Equal(Tuning.Player.DodgeSpeed, HorizontalSpeed(player), Tolerance);

        player.Tick(back);
        Assert.Equal(-1f, Vector3.Dot(FlatVelocityDirection(player), player.View.FlatForward), Tolerance);
        Assert.True(player.IsDodging);
    }

    [Fact]
    public void Letting_go_of_the_keys_during_a_dodge_keeps_its_last_direction()
    {
        var player = new Player(OpenField());
        var right = PlayerInput.None with { Move = new Vector2(1f, 0f) };

        player.Tick(Shift);
        player.Tick(right);
        player.Tick(PlayerInput.None);

        Assert.Equal(1f, Vector3.Dot(FlatVelocityDirection(player), player.View.FlatRight), Tolerance);
        Assert.Equal(Tuning.Player.DodgeSpeed, HorizontalSpeed(player), Tolerance);
    }

    [Fact]
    public void With_less_than_two_segments_there_is_no_dodge()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        ChainTimedJumps(player, Tuning.Player.StaminaJumps - 2);   // one segment left
        RunUntilLanded(player);
        Assert.Equal(SegmentFraction, player.Stamina.Fraction, Tolerance);

        player.Tick(ForwardDash);

        Assert.False(player.IsDodging);
        Assert.Equal(SegmentFraction, player.Stamina.Fraction, Tolerance);
    }

    [Fact]
    public void Jumping_is_not_possible_during_a_dodge()
    {
        var player = StandingPlayer();
        player.Tick(Shift);

        player.Tick(PlayerInput.None with { Jump = true });

        Assert.True(player.IsGrounded);
        Assert.True(player.IsDodging);
    }

    [Fact]
    public void The_bar_does_not_refill_during_a_dodge()
    {
        var player = StandingPlayer();
        RunTicks(player, PlayerInput.None, 30);              // standing long enough to refill normally
        player.Tick(Shift);
        var afterPaying = player.Stamina.Fraction;

        RunTicks(player, PlayerInput.None, DodgeTicks - 2);

        Assert.True(player.IsDodging);
        Assert.Equal(afterPaying, player.Stamina.Fraction);
    }

    [Fact]
    public void The_hitbox_is_smaller_during_a_dodge_and_back_afterwards()
    {
        var player = StandingPlayer();
        Assert.Equal(Tuning.Player.Height, player.CollisionHeight);

        player.Tick(Shift);
        Assert.Equal(Tuning.Player.DodgeHeight, player.CollisionHeight);

        RunTicks(player, PlayerInput.None, DodgeTicks);
        Assert.Equal(Tuning.Player.Height, player.CollisionHeight);
    }

    [Fact]
    public void The_eyes_go_down_smoothly_and_come_back_up()
    {
        var player = StandingPlayer();
        var standingEyes = player.EyePosition.Y - player.Position.Y;

        player.Tick(Shift);
        var afterOneTick = player.EyePosition.Y - player.Position.Y;
        RunTicks(player, PlayerInput.None, DodgeTicks / 2);
        var ducked = player.EyePosition.Y - player.Position.Y;
        RunTicks(player, PlayerInput.None, DodgeTicks);
        var standingAgain = player.EyePosition.Y - player.Position.Y;

        Assert.Equal(Tuning.Player.EyeHeight, standingEyes, Tolerance);
        Assert.InRange(afterOneTick, Tuning.Player.DodgeEyeHeight + 0.1f, Tuning.Player.EyeHeight - 0.01f);   // no jump
        Assert.Equal(Tuning.Player.DodgeEyeHeight, ducked, Tolerance);
        Assert.Equal(Tuning.Player.EyeHeight, standingAgain, Tolerance);
    }

    [Fact]
    public void After_the_dodge_the_speed_falls_back_to_the_top_speed()
    {
        var player = StandingPlayer();

        player.Tick(ForwardDash);
        RunTicks(player, Forward, DodgeTicks - 1);

        Assert.False(player.IsDodging);
        Assert.Equal(Tuning.Player.MaxSpeed, HorizontalSpeed(player), Tolerance);
    }

    [Fact]
    public void A_dodge_off_a_ledge_falls_and_uses_up_the_air_dash()
    {
        // Found by the code review: dodge off a ledge, then a dash in the air = about 8 m of evading in 0.55 s.
        // Same rule as for the dash: the stay in the air that starts during the dodge already had its move.
        var player = new Player(LevelParser.Parse("P\n_\n_\n_\n_\n."));
        var startHeight = player.Position.Y;
        player.Tick(Shift);
        RunTicks(player, PlayerInput.None, DodgeTicks - 1);
        Assert.False(player.IsGrounded);
        Assert.True(player.Position.Y < startHeight, "Gravity works during a dodge.");

        player.Tick(Shift);

        Assert.False(player.IsDashing);
    }

    [Fact]
    public void Shift_and_jump_in_the_same_tick_is_a_dodge_without_a_jump()
    {
        var player = StandingPlayer();

        player.Tick(Shift with { Jump = true });
        RunTicks(player, PlayerInput.None with { Jump = true }, DodgeTicks + 5);   // jump still held

        Assert.True(player.IsGrounded, "Holding jump through the dodge must not jump afterwards.");
    }

    [Fact]
    public void A_dodge_cancels_a_buffered_jump()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        PressShortlyBeforeLanding(player);                  // landed, jump queued for the next tick

        player.Tick(ForwardDash);
        RunTicks(player, Forward, DodgeTicks + 5);

        Assert.True(player.IsGrounded);
        Assert.False(player.HasPerfectJumpBonus);
    }

    [Fact]
    public void Holding_shift_dodges_only_once()
    {
        var player = StandingPlayer();
        var starts = 0;

        for (var i = 0; i < 3 * DodgeTicks; i++)
        {
            var wasDodging = player.IsDodging;
            player.Tick(Shift);
            if (!wasDodging && player.IsDodging)
            {
                starts++;
            }
        }

        Assert.Equal(1, starts);
    }

    /// <summary>A player standing at the start of a long corridor.</summary>
    private static Player StandingPlayer() =>
        new(LevelParser.Parse(string.Join('\n', Enumerable.Repeat(".", 40).Prepend("P"))));

    /// <summary>A wide open room, so the dodge can turn sideways.</summary>
    private static Level OpenField()
    {
        var row = new string('.', 11);
        var rows = Enumerable.Repeat(row, 11).ToArray();
        rows[5] = "....." + "P" + ".....";
        return LevelParser.Parse(string.Join('\n', rows));
    }

    private static Vector3 FlatVelocityDirection(Player player) => Vector3.Normalize(player.Velocity with { Y = 0f });
}
