using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.Gameplay.JumpTestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>The air dash in the looking direction (decision record 0007): W + Shift dashes where the player looks.</summary>
public class PlayerLookDashTests
{
    private const float Tolerance = 0.02f;

    private static readonly int DashTicks = SimulationClock.SecondsToTicks(Balance.Default.Player.DashDuration);
    private static readonly float DashDistance = Balance.Default.Player.DashDistance;

    private static readonly PlayerInput Right = PlayerInput.None with { Move = new Vector2(1f, 0f) };
    private static readonly PlayerInput ForwardRight = PlayerInput.None with { Move = Vector2.Normalize(new Vector2(1f, 1f)) };

    /// <summary>An open hall 11 x 11 cells with a 20 m high ceiling, the player in the middle.</summary>
    private static Level HighHall()
    {
        var cells = new Cell[11, 11];
        for (var x = 0; x < 11; x++)
        {
            for (var z = 0; z < 11; z++)
            {
                var edge = x is 0 or 10 || z is 0 or 10;
                cells[x, z] = edge ? Cell.Wall : new Cell(false, 0f, 20f);
            }
        }

        return new Level(cells, 5, 5);
    }

    /// <summary>A player who jumped, waited a few ticks and now looks <paramref name="degreesUp"/> up (negative: down).</summary>
    private static Player InTheAirLooking(float degreesUp, Level? level = null)
    {
        var player = new Player(level ?? HighHall());
        player.Tick(PlayerInput.None with { Jump = true });
        RunTicks(player, PlayerInput.None, 5);
        Assert.False(player.IsGrounded);

        // Moving the mouse up (negative Y) looks up.
        player.Look(PlayerInput.None with { Look = new Vector2(0f, -degreesUp * MathF.PI / 180f / ControlSettings.MouseSensitivity) });
        return player;
    }

    /// <summary>Dashes with <paramref name="input"/> (plus Shift) and holds the input for the rest of the dash.</summary>
    private static Vector3 Dash(Player player, PlayerInput input)
    {
        var start = player.Position;
        player.Tick(input with { Dash = true });
        RunTicks(player, input, DashTicks - 1);
        return player.Position - start;
    }

    [Theory]
    [InlineData(30f)]
    [InlineData(60f)]
    [InlineData(10f)]
    public void W_and_shift_in_the_air_dash_where_the_player_looks(float degreesUp)
    {
        var player = InTheAirLooking(degreesUp);
        var looking = player.View.Forward;

        var moved = Dash(player, Forward);

        Assert.False(player.IsDashing);
        Assert.Equal(DashDistance, moved.Length(), Tolerance);
        Assert.Equal(1f, Vector3.Dot(Vector3.Normalize(moved), looking), Tolerance);
    }

    [Theory]
    [InlineData("right")]
    [InlineData("forward-right")]
    [InlineData("none")]
    public void Other_directions_still_dash_flat_even_when_looking_up(string keys)
    {
        var player = InTheAirLooking(45f);
        var input = keys switch
        {
            "right" => Right,
            "forward-right" => ForwardRight,
            _ => PlayerInput.None,
        };

        var moved = Dash(player, input);

        Assert.Equal(0f, moved.Y, Tolerance);
        Assert.Equal(DashDistance, new Vector2(moved.X, moved.Z).Length(), Tolerance);
    }

    [Fact]
    public void After_a_dash_upwards_the_player_does_not_shoot_on_but_falls()
    {
        var player = InTheAirLooking(60f);

        Dash(player, Forward);
        player.Tick(PlayerInput.None);

        Assert.True(player.Velocity.Y <= 0f, $"Still rising with {player.Velocity.Y} m/s after the dash.");
    }

    [Fact]
    public void A_dash_upwards_ends_at_the_ceiling()
    {
        // The standard 3.5 m ceiling: a steep dash upwards hits it long before its 4 m are done.
        var level = LevelParser.Parse(".....\n.....\n..P..\n.....\n.....");
        var player = InTheAirLooking(70f, level);

        player.Tick(Forward with { Dash = true });
        var ticks = 1;
        while (player.IsDashing && ticks < DashTicks)
        {
            player.Tick(Forward);
            ticks++;
        }

        Assert.False(player.IsDashing);
        Assert.True(ticks < DashTicks, "The dash went on although the head was at the ceiling.");
        Assert.Equal(Cell.DefaultCeilingHeight, player.Position.Y + BodySizes.Player.Height, Tolerance);
        player.Tick(PlayerInput.None);
        Assert.True(player.Velocity.Y <= 0f);
    }

    [Fact]
    public void A_dash_downwards_ends_on_the_floor()
    {
        var player = InTheAirLooking(-70f);

        player.Tick(Forward with { Dash = true });
        var ticks = 1;
        while (player.IsDashing && ticks < DashTicks)
        {
            player.Tick(Forward);
            ticks++;
        }

        Assert.False(player.IsDashing);
        Assert.True(player.IsGrounded);
        Assert.True(ticks < DashTicks, "The dash went on through the floor time.");
    }

    [Fact]
    public void Only_one_air_dash_per_jump_also_upwards()
    {
        var player = InTheAirLooking(40f);
        Dash(player, Forward);
        var before = player.Position;

        player.Tick(Forward with { Dash = true });

        Assert.False(player.IsDashing);
        Assert.True(player.Position.Y < before.Y + 0.5f);
    }

    [Fact]
    public void A_dash_upwards_costs_one_segment_like_any_dash()
    {
        var player = InTheAirLooking(40f);
        var before = player.Stamina.Fraction;

        player.Tick(Forward with { Dash = true });

        Assert.True(player.IsDashing);
        Assert.Equal(before - (1f / Balance.Default.Player.StaminaJumps), player.Stamina.Fraction, Tolerance);
    }
}
