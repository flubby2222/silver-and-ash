using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class PlayerJumpTests
{
    private static readonly PlayerInput Jump = PlayerInput.None with { Jump = true };
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };

    private static readonly Level Room = LevelParser.Parse(
        """
        .....
        .....
        ..P..
        .....
        .....
        """);

    [Fact]
    public void Standing_player_stays_on_the_floor()
    {
        var player = new Player(Room);

        RunTicks(player, PlayerInput.None, 120);

        Assert.True(player.IsGrounded);
        Assert.Equal(Cell.DefaultFloorHeight, player.Position.Y);
        Assert.Equal(0f, player.Velocity.Y);
    }

    [Fact]
    public void A_jump_is_about_one_metre_high()
    {
        var player = new Player(Room);

        var (maxHeight, _) = JumpAndMeasure(player);

        Assert.InRange(maxHeight, 0.95f * Tuning.Player.JumpHeight, 1.05f * Tuning.Player.JumpHeight);
    }

    [Fact]
    public void A_jump_lasts_about_the_jump_duration()
    {
        var player = new Player(Room);

        var (_, airTicks) = JumpAndMeasure(player);

        var expected = SimulationClock.SecondsToTicks(Tuning.Player.JumpDuration);
        Assert.InRange(airTicks, expected - 2, expected + 2);
    }

    [Fact]
    public void The_player_lands_on_the_floor_again()
    {
        var player = new Player(Room);

        JumpAndMeasure(player);

        Assert.True(player.IsGrounded);
        Assert.Equal(Cell.DefaultFloorHeight, player.Position.Y);
        Assert.Equal(0f, player.Velocity.Y);
    }

    [Fact]
    public void There_is_no_double_jump()
    {
        var player = new Player(Room);
        player.Tick(Jump);
        RunTicks(player, PlayerInput.None, 5);
        var velocityBefore = player.Velocity.Y;

        // Fresh press in mid-air.
        player.Tick(Jump);

        Assert.False(player.IsGrounded);
        Assert.True(player.Velocity.Y < velocityBefore, "A second press in the air must not push the player up again.");
    }

    [Fact]
    public void Holding_the_jump_button_jumps_only_once()
    {
        var player = new Player(Room);

        // Hold the button for 3 seconds: one jump, then standing.
        var jumps = 0;
        var wasGrounded = true;
        for (var tick = 0; tick < 3 * SimulationClock.TicksPerSecond; tick++)
        {
            player.Tick(Jump);
            if (wasGrounded && !player.IsGrounded)
            {
                jumps++;
            }

            wasGrounded = player.IsGrounded;
        }

        Assert.Equal(1, jumps);
        Assert.True(player.IsGrounded);
    }

    [Fact]
    public void The_ceiling_stops_a_jump()
    {
        // Low ceiling: 2.2 m, player is 1.8 m tall -> the feet can rise only 0.4 m.
        var cells = new Cell[1, 1];
        cells[0, 0] = new Cell(false, 0f, 2.2f);
        var player = new Player(new Level(cells, 0, 0));

        var (maxHeight, _) = JumpAndMeasure(player);

        Assert.Equal(2.2f - Tuning.Player.Height, maxHeight, 0.001f);
        Assert.True(player.IsGrounded);
    }

    [Fact]
    public void The_floor_height_of_the_cell_is_used()
    {
        // A raised floor at 0.5 m (prepared for stairs and platforms after 1.0).
        var cells = new Cell[1, 1];
        cells[0, 0] = new Cell(false, 0.5f, 3.5f);
        var player = new Player(new Level(cells, 0, 0));

        RunTicks(player, PlayerInput.None, 30);

        Assert.True(player.IsGrounded);
        Assert.Equal(0.5f, player.Position.Y);
    }

    [Fact]
    public void Steering_in_the_air_is_weaker_than_on_the_ground()
    {
        var onGround = new Player(Room);
        RunTicks(onGround, Forward, 3);

        var inAir = new Player(Room);
        inAir.Tick(Jump);
        RunTicks(inAir, Forward, 3);

        var groundSpeed = new Vector2(onGround.Velocity.X, onGround.Velocity.Z).Length();
        var airSpeed = new Vector2(inAir.Velocity.X, inAir.Velocity.Z).Length();
        Assert.True(airSpeed > 0f, "Steering in the air must be possible.");
        Assert.True(airSpeed < groundSpeed, "Steering in the air must be weaker than on the ground.");
    }

    [Fact]
    public void Running_jumps_get_a_small_speed_boost()
    {
        // A 40 m long corridor along +Z, so no wall gets in the way while running and jumping.
        var player = new Player(LevelParser.Parse(string.Join('\n', Enumerable.Repeat(".", 19).Prepend("P"))));
        RunTicks(player, Forward, 30);   // full running speed on the ground
        var groundSpeed = new Vector2(player.Velocity.X, player.Velocity.Z).Length();

        player.Tick(Forward with { Jump = true });
        RunTicks(player, Forward, 20);   // still in the air
        var airSpeed = new Vector2(player.Velocity.X, player.Velocity.Z).Length();

        Assert.False(player.IsGrounded);
        Assert.Equal(Tuning.Player.MaxSpeed, groundSpeed, 0.001f);
        Assert.Equal(Tuning.Player.MaxSpeed * Tuning.Player.AirSpeedMultiplier, airSpeed, 0.001f);
    }

    [Fact]
    public void A_full_jump_fits_under_the_standard_ceiling()
    {
        // Otherwise every jump in a normal room would bump the head.
        Assert.True(Tuning.Player.Height + Tuning.Player.JumpHeight < Cell.DefaultCeilingHeight);
    }

    [Fact]
    public void A_jump_in_a_normal_room_is_not_stopped_by_the_ceiling()
    {
        var player = new Player(Room);

        var (maxHeight, _) = JumpAndMeasure(player);

        Assert.Equal(Tuning.Player.JumpHeight, maxHeight, 0.05f);
    }

    /// <summary>Presses jump once, then waits until the player has landed.</summary>
    private static (float MaxHeight, int AirTicks) JumpAndMeasure(Player player)
    {
        var startY = player.Position.Y;
        var maxHeight = 0f;
        var airTicks = 0;

        player.Tick(Jump);
        while (!player.IsGrounded && airTicks < 10 * SimulationClock.TicksPerSecond)
        {
            airTicks++;
            maxHeight = MathF.Max(maxHeight, player.Position.Y - startY);
            player.Tick(PlayerInput.None);
        }

        return (maxHeight, airTicks);
    }

    private static void RunTicks(Player player, PlayerInput input, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            player.Tick(input);
        }
    }
}
