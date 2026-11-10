using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class PlayerMovementTests
{
    private const float Tolerance = 0.001f;

    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly PlayerInput ForwardRight = PlayerInput.None with { Move = new Vector2(1f, 1f) };

    // A large open hall (60 x 60 m), so walls never get in the way of these movement tests.
    private static readonly Level OpenHall = CreateOpenHall(size: 31);

    private static Player NewPlayer() => new(OpenHall);

    private static Level CreateOpenHall(int size)
    {
        var rows = new string[size];
        for (var z = 0; z < size; z++)
        {
            var row = new string('.', size).ToCharArray();
            if (z == size / 2)
            {
                row[size / 2] = 'P';
            }

            rows[z] = new string(row);
        }

        return LevelParser.Parse(string.Join('\n', rows));
    }

    [Fact]
    public void Without_input_the_player_stands_still()
    {
        var player = NewPlayer();
        var start = player.Position;

        RunTicks(player, PlayerInput.None, 60);

        Assert.Equal(start, player.Position);
        Assert.Equal(Vector3.Zero, player.Velocity);
    }

    [Fact]
    public void Reaches_top_speed_after_the_acceleration_time()
    {
        var player = NewPlayer();

        RunTicks(player, Forward, SimulationClock.SecondsToTicks(Tuning.Player.AccelerationTime));

        Assert.Equal(Tuning.Player.MaxSpeed, player.Velocity.Length(), Tolerance);
    }

    [Fact]
    public void Is_still_accelerating_halfway_through_the_acceleration_time()
    {
        var player = NewPlayer();

        RunTicks(player, Forward, SimulationClock.SecondsToTicks(Tuning.Player.AccelerationTime) / 2);

        Assert.InRange(player.Velocity.Length(), 0.1f, Tuning.Player.MaxSpeed - 0.1f);
    }

    [Fact]
    public void Walks_about_six_metres_in_one_second()
    {
        var player = NewPlayer();
        var start = player.Position;

        RunTicks(player, Forward, SimulationClock.TicksPerSecond);

        // A little less than 6 m, because the first 0.1 s are spent accelerating.
        Assert.InRange(Vector3.Distance(start, player.Position), 5.6f, 6f);
    }

    [Fact]
    public void Walking_diagonally_is_not_faster()
    {
        var player = NewPlayer();

        RunTicks(player, ForwardRight, 60);

        Assert.Equal(Tuning.Player.MaxSpeed, player.Velocity.Length(), Tolerance);
    }

    [Fact]
    public void Stops_within_the_acceleration_time_after_letting_go()
    {
        var player = NewPlayer();
        RunTicks(player, Forward, 60);

        RunTicks(player, PlayerInput.None, SimulationClock.SecondsToTicks(Tuning.Player.AccelerationTime));

        Assert.Equal(Vector3.Zero, player.Velocity);
    }

    [Fact]
    public void Forward_follows_the_view_direction()
    {
        var player = NewPlayer();

        // Turn 90 degrees to the right: from looking along +Z to looking along -X.
        player.View.ApplyMouse(new Vector2(MathF.PI / 2f, 0f), sensitivity: 1f);
        var start = player.Position;
        RunTicks(player, Forward, 30);

        var moved = player.Position - start;
        Assert.True(moved.X < -1f);
        Assert.Equal(0f, moved.Z, Tolerance);
    }

    [Fact]
    public void Strafing_right_moves_to_the_right_of_the_view()
    {
        var player = NewPlayer();
        var start = player.Position;

        RunTicks(player, PlayerInput.None with { Move = new Vector2(1f, 0f) }, 30);

        // Looking along +Z, right is -X.
        Assert.True((player.Position - start).X < -1f);
    }

    [Fact]
    public void Looking_up_does_not_slow_down_or_lift_the_player()
    {
        var player = NewPlayer();
        player.View.ApplyMouse(new Vector2(0f, -10000f));
        var start = player.Position;

        RunTicks(player, Forward, 60);

        Assert.Equal(Tuning.Player.MaxSpeed, player.Velocity.Length(), Tolerance);
        Assert.Equal(start.Y, player.Position.Y);
    }

    [Fact]
    public void The_player_stops_at_walls_and_starts_again_after_turning_away()
    {
        // Looking along +Z, the wall row at z = 6 m is two cells ahead.
        var player = new Player(LevelParser.Parse(
            """
            ...
            .P.
            ...
            ###
            """));

        RunTicks(player, Forward, 120);

        Assert.Equal(6f - Tuning.Player.Radius, player.Position.Z, Tolerance);
        Assert.Equal(0f, player.Velocity.Z, Tolerance);

        // Walk back: the wall must not hold the player.
        var back = PlayerInput.None with { Move = new Vector2(0f, -1f) };
        RunTicks(player, back, 30);
        Assert.True(player.Position.Z < 5f);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void Interpolated_eye_position_blends_between_the_last_two_ticks(float alpha)
    {
        var player = NewPlayer();
        RunTicks(player, Forward, 30);

        var expected = Vector3.Lerp(player.PreviousPosition, player.Position, alpha)
            + new Vector3(0f, Tuning.Player.EyeHeight, 0f);

        Assert.Equal(expected, player.InterpolatedEyePosition(alpha));
    }

    [Fact]
    public void Interpolation_at_one_is_the_current_eye_position()
    {
        var player = NewPlayer();
        RunTicks(player, Forward, 10);

        Assert.Equal(player.EyePosition, player.InterpolatedEyePosition(1f));
        Assert.NotEqual(player.EyePosition, player.InterpolatedEyePosition(0f));
    }

    private static void RunTicks(Player player, PlayerInput input, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            player.Tick(input);
        }
    }
}
