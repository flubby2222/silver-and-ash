using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.Gameplay.JumpTestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class PlayerDashTests
{
    private const float Tolerance = 0.01f;

    private static readonly int DashTicks = SimulationClock.SecondsToTicks(Tuning.Player.DashDuration);

    private static readonly PlayerInput DashOnly = PlayerInput.None with { Dash = true };

    private static float SegmentFraction => 1f / Tuning.Player.StaminaJumps;

    [Fact]
    public void A_dash_covers_the_dash_distance_in_the_dash_duration()
    {
        var player = StandingPlayer();
        var start = player.Position;

        player.Tick(DashOnly);
        RunTicks(player, PlayerInput.None, DashTicks - 1);

        Assert.False(player.IsDashing);
        Assert.Equal(Tuning.Player.DashDistance, Vector3.Distance(start, player.Position), Tolerance);
    }

    [Fact]
    public void Without_a_walking_direction_the_dash_goes_forward()
    {
        var player = StandingPlayer();
        var start = player.Position;

        player.Tick(DashOnly);
        RunTicks(player, PlayerInput.None, DashTicks - 1);

        var moved = Vector3.Normalize(player.Position - start);
        Assert.Equal(1f, Vector3.Dot(moved, player.View.FlatForward), Tolerance);
    }

    [Fact]
    public void The_dash_follows_the_walking_direction()
    {
        var player = new Player(OpenField());
        var right = PlayerInput.None with { Move = new Vector2(1f, 0f) };
        var start = player.Position;

        player.Tick(right with { Dash = true });
        RunTicks(player, right, DashTicks - 1);

        var moved = Vector3.Normalize(player.Position - start);
        Assert.Equal(1f, Vector3.Dot(moved, player.View.FlatRight), Tolerance);
    }

    [Fact]
    public void A_dash_costs_one_segment()
    {
        var player = StandingPlayer();

        player.Tick(DashOnly);

        Assert.True(player.IsDashing);
        Assert.Equal(1f - SegmentFraction, player.Stamina.Fraction, Tolerance);
    }

    [Fact]
    public void Holding_the_dash_button_dashes_only_once()
    {
        // Counts dash starts directly - the stamina is no good measure here, it refills while standing
        // (code review: two dashes would still have left more than 1 - 2 segments).
        var player = StandingPlayer();
        var starts = 0;

        for (var i = 0; i < 3 * DashTicks; i++)
        {
            var wasDashing = player.IsDashing;
            player.Tick(DashOnly);
            if (!wasDashing && player.IsDashing)
            {
                starts++;
            }
        }

        Assert.Equal(1, starts);
    }

    [Fact]
    public void A_dash_off_a_ledge_uses_up_the_air_dash()
    {
        // Found by the code review: ground dash over a pit, then a second dash in the air = 8 m flat glide
        // without a jump. The stay in the air starts during the first dash, so that one counts as the air dash.
        var player = new Player(LevelParser.Parse("P\n_\n_\n_\n_\n."));
        player.Tick(DashOnly);
        RunTicks(player, PlayerInput.None, DashTicks - 1);
        Assert.False(player.IsGrounded);

        player.Tick(DashOnly);

        Assert.False(player.IsDashing);
    }

    [Fact]
    public void A_jump_pressed_during_a_dash_is_not_buffered()
    {
        // Found by the code review: an air dash that lands on a low step (0.25 m) while jump is being pressed.
        // The press was buffered, and the jump fired after the dash - late, but still as a perfect jump.
        // A dash ignores the jump button: on the ground it already did, now in the air, too.
        var scenarios = 0;
        for (var flat = 3; flat <= 8; flat++)
        {
            var rows = Enumerable.Repeat(".", flat).Prepend("P").Concat(Enumerable.Repeat("1", 30));
            var player = new Player(LevelParser.Parse(string.Join('\n', rows)));
            RunTicks(player, Forward, 30);
            player.Tick(ForwardJump);
            while (!player.IsGrounded && !(player.Velocity.Y < 0f && player.Position.Y < 0.2f))
            {
                player.Tick(Forward);
            }

            player.Tick(ForwardDash);
            var landedDuringDash = false;
            for (var i = 0; player.IsDashing; i++)
            {
                player.Tick(i % 2 == 0 ? ForwardJump : Forward);
                landedDuringDash |= player.IsGrounded;
            }

            if (!landedDuringDash)
            {
                continue;
            }

            scenarios++;
            RunTicks(player, Forward, 20);
            Assert.True(player.IsGrounded, $"{flat} flat cells: a buffered jump fired after the dash.");
            Assert.False(player.HasPerfectJumpBonus, $"{flat} flat cells: perfect bonus from a press during the dash.");
        }

        Assert.True(scenarios > 0, "No setup landed during the dash - the test proves nothing.");
    }

    [Fact]
    public void With_an_empty_bar_there_is_no_dash()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        ChainTimedJumps(player, Tuning.Player.StaminaJumps - 1);   // in the air, bar empty
        Assert.Equal(0f, player.Stamina.Fraction);

        player.Tick(ForwardDash);

        Assert.False(player.IsDashing);
    }

    [Fact]
    public void In_the_air_there_is_only_one_dash_per_jump()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        RunTicks(player, Forward, 5);

        player.Tick(ForwardDash);
        Assert.True(player.IsDashing);
        RunTicks(player, Forward, DashTicks);
        Assert.False(player.IsGrounded);
        var afterFirst = player.Stamina.Fraction;

        player.Tick(ForwardDash);                           // second dash in the same jump

        Assert.False(player.IsDashing);
        Assert.Equal(afterFirst, player.Stamina.Fraction);
    }

    [Fact]
    public void After_landing_the_air_dash_is_available_again()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        RunTicks(player, Forward, 5);
        player.Tick(ForwardDash);
        RunUntilLanded(player);
        player.Tick(Forward);

        player.Tick(ForwardJump);
        RunTicks(player, Forward, 5);
        player.Tick(ForwardDash);

        Assert.True(player.IsDashing);
    }

    [Fact]
    public void There_is_no_gravity_during_a_dash()
    {
        var player = RunningPlayer();
        player.Tick(ForwardJump);
        RunTicks(player, Forward, 10);
        player.Tick(ForwardDash);
        var height = player.Position.Y;

        RunTicks(player, Forward, DashTicks - 1);

        Assert.Equal(height, player.Position.Y, Tolerance);
    }

    [Fact]
    public void A_wall_stops_the_dash()
    {
        // Wall 2 cells ahead: the start is in the middle of cell 0, the wall begins at 3 m.
        var player = new Player(LevelParser.Parse("P\n.\n#"));
        var start = player.Position;

        player.Tick(DashOnly);
        RunTicks(player, PlayerInput.None, DashTicks + 5);

        var travelled = Vector3.Distance(start, player.Position);
        Assert.True(travelled < 3f - Tuning.Player.Radius + Tolerance, $"Travelled {travelled:0.00} m into a wall 3 m away.");
    }

    [Fact]
    public void After_the_dash_the_speed_falls_back_to_the_top_speed()
    {
        var player = StandingPlayer();

        player.Tick(ForwardDash);
        RunTicks(player, Forward, DashTicks - 1);

        Assert.False(player.IsDashing);
        Assert.Equal(Tuning.Player.MaxSpeed, HorizontalSpeed(player), Tolerance);
    }

    [Fact]
    public void Jumping_is_not_possible_during_a_dash()
    {
        var player = StandingPlayer();
        player.Tick(DashOnly);

        player.Tick(PlayerInput.None with { Jump = true });

        Assert.True(player.IsGrounded);
        Assert.True(player.IsDashing);
    }

    /// <summary>A player standing at the start of a long corridor.</summary>
    private static Player StandingPlayer() =>
        new(LevelParser.Parse(string.Join('\n', Enumerable.Repeat(".", 40).Prepend("P"))));

    /// <summary>A wide open room, so the dash can go sideways, too.</summary>
    private static Level OpenField()
    {
        var row = new string('.', 11);
        var rows = Enumerable.Repeat(row, 11).ToArray();
        rows[5] = "....." + "P" + ".....";
        return LevelParser.Parse(string.Join('\n', rows));
    }
}
