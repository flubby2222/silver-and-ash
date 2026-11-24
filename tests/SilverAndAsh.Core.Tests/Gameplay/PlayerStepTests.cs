using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>Footsteps (issue #267): a <see cref="PlayerStepped"/> for every step length walked on the ground.</summary>
public class PlayerStepTests
{
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly float StepLength = Balance.Default.Player.MaxSpeed / Player.StepsPerSecond;

    // Time for ticks that normally take milliseconds - only a loop without end runs into it. Until it is up, such a loop
    // fills the memory with step events (about 0.3 GB a second), so it is no longer than needed.
    private static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(5);

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

    [Fact]
    public async Task A_player_without_top_speed_moved_on_the_ground_takes_no_steps_and_the_tick_ends()
    {
        // Issue #698: with MaxSpeed 0 the step length is 0, and counting steps never ended once the player moved on
        // the ground without walking - here by a dodge, whose speed does not depend on MaxSpeed. The ticks run on
        // their own thread, so a relapse turns this test red instead of hanging the whole test run.
        var balance = Balance.Default with { Player = Balance.Default.Player with { MaxSpeed = 0f } };
        var player = new Player(OpenHall, balance: balance);
        var start = player.Position;
        var dodgeTicks = SimulationClock.SecondsToTicks(balance.Player.DodgeDuration);
        var shift = PlayerInput.None with { Dash = true };

        var (ended, ticks) = await RunWithTimeLimit(player, () => Run(player, [shift, .. Enumerable.Repeat(PlayerInput.None, dodgeTicks)]));

        Assert.True(ended, "The ticks did not end - counting steps loops at a step length of 0.");
        Assert.True(player.IsGrounded);
        Assert.True(Vector3.Distance(start, player.Position) > 0f, "The dodge did not move the player - the test would check nothing.");
        Assert.Empty(Steps(ticks.SelectMany(tick => tick)));
    }

    [Theory]
    [InlineData(1e-8f)]
    [InlineData(1e-3f)]
    public async Task A_player_with_a_tiny_top_speed_takes_only_a_few_steps_a_tick_and_the_tick_ends(float maxSpeed)
    {
        // Issue #706: at a step length below the float resolution of the walked distance, taking a step no longer
        // shortens it and counting steps never ended (1e-8); just above, a tick reported hundreds of steps (1e-3).
        var balance = Balance.Default with { Player = Balance.Default.Player with { MaxSpeed = maxSpeed } };
        var player = new Player(OpenHall, balance: balance);
        var start = player.Position;
        var dodgeTicks = SimulationClock.SecondsToTicks(balance.Player.DodgeDuration);
        var shift = PlayerInput.None with { Dash = true };

        var (ended, ticks) = await RunWithTimeLimit(player, () => Run(player, [shift, .. Enumerable.Repeat(PlayerInput.None, dodgeTicks)]));

        Assert.True(ended, $"The ticks did not end - counting steps loops at a step length of {player.StepLength} m.");
        Assert.True(player.IsGrounded);
        Assert.True(Vector3.Distance(start, player.Position) > 0f, "The dodge did not move the player - the test would check nothing.");
        Assert.All(ticks, tick => Assert.InRange(Steps(tick).Count, 0, Player.MaxStepsPerTick));
    }

    [Fact]
    public void At_normal_values_no_tick_comes_near_the_step_limit()
    {
        // Issue #706: the limit per tick must never change a normal walk - not even when dodging, the fastest way over
        // the ground.
        var player = new Player(OpenHall);
        var dodgeTicks = SimulationClock.SecondsToTicks(Balance.Default.Player.DodgeDuration);
        var walk = Enumerable.Repeat(Forward, SimulationClock.TicksPerSecond);

        var ticks = Run(player, [.. walk, Forward with { Dash = true }, .. Enumerable.Repeat(Forward, dodgeTicks), .. walk]);

        Assert.NotEmpty(Steps(ticks.SelectMany(tick => tick)));
        Assert.All(ticks, tick => Assert.InRange(Steps(tick).Count, 0, 1));
    }

    private static List<PlayerStepped> Steps(IEnumerable<GameEvent> events) => [.. events.OfType<PlayerStepped>()];

    /// <summary>The events of every tick, one list per input.</summary>
    private static List<List<GameEvent>> Run(Player player, IEnumerable<PlayerInput> inputs)
    {
        var ticks = new List<List<GameEvent>>();
        foreach (var input in inputs)
        {
            player.Tick(input);
            ticks.Add([.. player.Events]);
        }

        return ticks;
    }

    /// <summary>
    /// Runs <paramref name="ticks"/> on their own thread, so a relapse into a loop without end turns a test red instead
    /// of hanging the test run (issue #698). If the ticks do not end in time, the loop is stopped from outside: the
    /// walked distance becomes NaN, which passes no comparison. Otherwise the thread would keep adding step events after
    /// the test - many gigabytes within minutes (issue #706).
    /// </summary>
    private static async Task<(bool Ended, List<List<GameEvent>> Ticks)> RunWithTimeLimit(Player player, Func<List<List<GameEvent>>> ticks)
    {
        var stride = typeof(Player).GetField("_strideDistance", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.True(stride is not null, "Player has no field _strideDistance any more - the stop for a loop without end needs a new handle.");

        var run = Task.Run(ticks);
        if (await Task.WhenAny(run, Task.Delay(TimeLimit)) == run)
        {
            return (true, await run);
        }

        // A stop can get lost while the loop writes the field back, and a later tick may start counting anew - so keep
        // stopping until the ticks are through.
        var stopping = Stopwatch.StartNew();
        while (!run.IsCompleted && stopping.Elapsed < TimeLimit)
        {
            stride.SetValue(player, float.NaN);
            await Task.WhenAny(run, Task.Delay(1));
        }

        Assert.True(run.IsCompleted, "The ticks did not end and could not be stopped - the test leaves a thread running.");
        return (false, []);
    }

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
