using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>Footsteps (issue #267): a <see cref="PlayerStepped"/> for every step length walked on the ground.</summary>
public class PlayerStepTests
{
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly float StepLength = Balance.Default.Player.MaxSpeed / Player.StepsPerSecond;

    // A large open hall (60 x 60 m), so walls never get in the way.
    private static readonly Level OpenHall = CreateOpenHall(size: 31);

    [Fact]
    public void The_step_length_follows_from_the_top_speed()
    {
        Assert.Equal(StepLength, new Player(OpenHall).StepLength, 0.0001f);
    }

    [Fact]
    public void Walking_reports_one_step_per_step_length()
    {
        var player = new Player(OpenHall);
        var start = player.Position;

        var steps = Steps(Run(player, Forward, 3 * SimulationClock.TicksPerSecond));

        var walked = Vector2.Distance(new Vector2(start.X, start.Z), new Vector2(player.Position.X, player.Position.Z));
        Assert.Equal((int)(walked / StepLength), steps.Count);
    }

    [Fact]
    public void At_full_speed_the_player_takes_about_two_steps_a_second()
    {
        var player = new Player(OpenHall);
        Run(player, Forward, SimulationClock.TicksPerSecond);

        var steps = Steps(Run(player, Forward, 4 * SimulationClock.TicksPerSecond));

        Assert.InRange(steps.Count, (int)(4 * Player.StepsPerSecond) - 1, (int)(4 * Player.StepsPerSecond) + 1);
    }

    [Fact]
    public void The_feet_take_turns_starting_with_the_left()
    {
        var player = new Player(OpenHall);

        var steps = Steps(Run(player, Forward, 3 * SimulationClock.TicksPerSecond));

        Assert.True(steps.Count >= 4);
        Assert.All(steps.Select((step, i) => (step, i)), pair =>
            Assert.Equal(pair.i % 2 == 0 ? Foot.Left : Foot.Right, pair.step.Foot));
        Assert.All(steps, step => Assert.Equal(player.Id, step.PlayerId));
    }

    [Fact]
    public void Standing_still_makes_no_steps()
    {
        var player = new Player(OpenHall);

        Assert.Empty(Steps(Run(player, PlayerInput.None, 3 * SimulationClock.TicksPerSecond)));
    }

    [Fact]
    public void Turning_on_the_spot_makes_no_steps()
    {
        var player = new Player(OpenHall);

        Assert.Empty(Steps(Run(player, PlayerInput.None with { Look = new Vector2(5f, 0f) }, SimulationClock.TicksPerSecond)));
    }

    [Fact]
    public void In_the_air_there_are_no_steps()
    {
        var player = new Player(OpenHall);
        Run(player, Forward, SimulationClock.TicksPerSecond);

        // Jump at full speed and keep walking forward until the feet are on the ground again.
        var inTheAir = new List<GameEvent>();
        player.Tick(Forward with { Jump = true });
        Assert.False(player.IsGrounded);
        var airTicks = 0;
        while (!player.IsGrounded && airTicks < 5 * SimulationClock.TicksPerSecond)
        {
            inTheAir.AddRange(player.Events);
            player.Tick(Forward);
            airTicks++;
        }

        Assert.True(player.IsGrounded);
        // The jump carried the player further than one step length - and still not one step.
        Assert.True(airTicks * Balance.Default.Player.MaxSpeed / SimulationClock.TicksPerSecond > StepLength);
        Assert.Empty(Steps(inTheAir));
    }

    [Fact]
    public void After_landing_the_first_step_comes_after_a_whole_step_length()
    {
        var player = new Player(OpenHall);
        Run(player, Forward, SimulationClock.TicksPerSecond);
        player.Tick(Forward with { Jump = true });
        while (!player.IsGrounded)
        {
            player.Tick(Forward);
        }

        var landed = player.Position;
        var ticks = 0;
        while (!player.Events.OfType<PlayerStepped>().Any() && ticks < 3 * SimulationClock.TicksPerSecond)
        {
            player.Tick(Forward);
            ticks++;
        }

        var walked = Vector2.Distance(new Vector2(landed.X, landed.Z), new Vector2(player.Position.X, player.Position.Z));
        Assert.InRange(walked, StepLength, StepLength + Balance.Default.Player.MaxSpeed / SimulationClock.TicksPerSecond + 0.01f);
    }

    [Fact]
    public void Two_players_count_their_steps_each_on_their_own()
    {
        var walker = new Player(OpenHall);
        var stander = new Player(OpenHall);
        var alone = new Player(OpenHall);
        var walkerSteps = new List<GameEvent>();
        var standerSteps = new List<GameEvent>();
        var aloneSteps = new List<GameEvent>();

        for (var tick = 0; tick < 3 * SimulationClock.TicksPerSecond; tick++)
        {
            walker.Tick(Forward);
            stander.Tick(PlayerInput.None);
            walkerSteps.AddRange(walker.Events);
            standerSteps.AddRange(stander.Events);
            alone.Tick(Forward);
            aloneSteps.AddRange(alone.Events);
        }

        Assert.Empty(Steps(standerSteps));
        Assert.NotEmpty(Steps(walkerSteps));
        Assert.Equal(Steps(aloneSteps), Steps(walkerSteps));
    }

    private static List<PlayerStepped> Steps(IEnumerable<GameEvent> events) => [.. events.OfType<PlayerStepped>()];

    private static List<GameEvent> Run(Player player, PlayerInput input, int ticks)
    {
        var events = new List<GameEvent>();
        for (var i = 0; i < ticks; i++)
        {
            player.Tick(input);
            events.AddRange(player.Events);
        }

        return events;
    }

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
}
