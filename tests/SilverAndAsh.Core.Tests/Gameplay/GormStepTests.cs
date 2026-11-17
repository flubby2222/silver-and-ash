using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>Gorm's heavy steps, reported for the sound (issue #298) - and nothing else about him changes.</summary>
public class GormStepTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;

    /// <summary>A Gorm who never attacks (no range for any attack) - to look at his walking alone.</summary>
    private static readonly Balance NoAttacks = Balance.Default with { Gorm = Balance.Default.Gorm with { NearRange = 0f, FarRange = 0f } };

    // A long corridor without rooms: Gorm is awake from the start and walks towards the player at its other end.
    private static GameWorld Corridor(Balance balance, int length = 40) =>
        new(LevelParser.Parse("P" + new string('.', length) + "T"), 1, balance);

    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    // Each tick: the distance he has walked so far (flat) and the number of steps in that tick.
    private static List<(float Walked, int Steps)> Walk(GameWorld world, int ticks)
    {
        var gorm = world.Gorm!;
        var walked = 0f;
        var result = new List<(float, int)>();
        for (var tick = 0; tick < ticks; tick++)
        {
            var before = gorm.Position;
            world.Tick(PlayerInput.None);
            walked += FlatDistance(before, gorm.Position);
            result.Add((walked, world.Events.OfType<GormStepped>().Count(step => step.GormId == gorm.Id)));
        }

        return result;
    }

    // The ticks between his steps can only be the two whole numbers round step length / speed.
    private static void AssertStepsEvery(IReadOnlyList<int> stepTicks, float speed)
    {
        var ticksPerStep = Gorm.StepLength / (speed * SimulationClock.SecondsPerTick);
        Assert.True(stepTicks.Count >= 3, $"only {stepTicks.Count} steps - the test would prove nothing");
        Assert.All(stepTicks.Zip(stepTicks.Skip(1)), pair =>
            Assert.InRange(pair.Second - pair.First, (int)MathF.Floor(ticksPerStep), (int)MathF.Ceiling(ticksPerStep)));
    }

    private static List<int> StepTicks(List<(float Walked, int Steps)> walk) =>
        walk.Select((entry, tick) => (entry.Steps, tick)).Where(entry => entry.Steps > 0).Select(entry => entry.tick).ToList();

    [Fact]
    public void His_steps_are_slow_and_heavy()
    {
        // Slower than a player's steps, and somewhere between a heavy stride and a plod.
        var secondsPerStep = Gorm.StepLength / Balance.Default.Gorm.Speed;
        Assert.InRange(secondsPerStep, 0.6f, 1.2f);
        Assert.True(secondsPerStep > 1f / Player.StepsPerSecond);
    }

    [Fact]
    public void Gorm_steps_once_every_step_length_he_walks()
    {
        var world = Corridor(NoAttacks);

        var walk = Walk(world, 4 * Second);

        Assert.All(walk, entry => Assert.InRange(entry.Steps, 0, 1));
        var walked = walk[^1].Walked;
        Assert.True(walked > 3 * Gorm.StepLength, $"he walked only {walked} m - the test would prove nothing");
        Assert.Equal((int)MathF.Floor(walked / Gorm.StepLength), walk.Sum(entry => entry.Steps));

        // The k-th step comes in the tick in which he has walked k step lengths.
        var k = 0;
        for (var tick = 0; tick < walk.Count; tick++)
        {
            if (walk[tick].Steps == 0)
            {
                continue;
            }

            k++;
            var before = tick == 0 ? 0f : walk[tick - 1].Walked;
            Assert.True(before < (k * Gorm.StepLength) + 0.001f && walk[tick].Walked >= (k * Gorm.StepLength) - 0.001f);
        }

        AssertStepsEvery(StepTicks(walk), Balance.Default.Gorm.Speed);
    }

    [Fact]
    public void Gorm_standing_in_front_of_a_player_takes_no_step()
    {
        var world = Corridor(NoAttacks, length: 12);
        // The corridor is 26 m long (cells of 2 m): at his pace he needs a good ten seconds to come up to the player.
        var approach = Walk(world, 12 * Second);
        Assert.True(approach.Sum(entry => entry.Steps) > 0, "he never walked - the test would prove nothing");
        Assert.InRange(FlatDistance(world.Gorm!.Position, world.Player.Position), 0f, Balance.Default.Gorm.StopDistance + 0.5f);

        var standing = Walk(world, 3 * Second);

        Assert.Equal(0, standing.Sum(entry => entry.Steps));
        Assert.Equal(0f, standing[^1].Walked, 0.001f);
    }

    [Fact]
    public void A_dead_gorm_takes_no_step()
    {
        var world = Corridor(NoAttacks);
        Assert.True(Walk(world, 2 * Second).Sum(entry => entry.Steps) > 0);

        world.Gorm!.TakeDamage(world.Gorm.Health, []);

        Assert.Equal(0, Walk(world, 3 * Second).Sum(entry => entry.Steps));
    }

    [Fact]
    public void In_phase_2_his_steps_come_faster()
    {
        var world = Corridor(NoAttacks, length: 60);
        var gorm = world.Gorm!;
        var events = new List<GameEvent>();
        gorm.TakeDamage(gorm.Health - (int)(gorm.MaxHealth * Balance.Default.Gorm.Phase2Health), events);
        Assert.Contains(events, e => e is GormRoared);

        // While he roars he stands: no step. Afterwards he walks faster - and steps more often.
        var roaring = Walk(world, SimulationClock.SecondsToTicks(Balance.Default.Gorm.RoarTime) - 1);
        Assert.Equal(0, roaring.Sum(entry => entry.Steps));

        var walk = Walk(world, 4 * Second);
        AssertStepsEvery(StepTicks(walk), Balance.Default.Gorm.Speed * Balance.Default.Gorm.Phase2SpeedUp);
    }

    [Fact]
    public void No_step_falls_in_the_tick_of_a_warning_or_an_attack()
    {
        // With his attacks: he stands still to wind up and to strike, so a step never covers a warning.
        var world = Corridor(Balance.Default, length: 20);
        var steps = 0;
        var warnings = 0;
        for (var tick = 0; tick < 20 * Second; tick++)
        {
            world.Tick(PlayerInput.None);
            var stepped = world.Events.Any(e => e is GormStepped);
            steps += stepped ? 1 : 0;
            warnings += world.Events.Count(e => e is GormWindUp);
            Assert.False(stepped && world.Events.Any(e => e is GormWindUp or GormSlammed or GormSwingStarted or GormSwung or GormThrew));
        }

        Assert.True(steps > 0 && warnings > 0, $"{steps} steps, {warnings} warnings - the test would prove nothing");
    }

    [Fact]
    public void After_standing_his_first_step_comes_after_a_whole_step_length()
    {
        // Gorm alone in a corridor (cells of 2 m), driven directly: a player far off to walk to, one right in front of
        // him to stop for. He walks part of a step length, stops - and then needs a whole step length for his next step.
        var level = LevelParser.Parse("P" + new string('.', 39));
        var far = new List<Player> { new(LevelParser.Parse("P" + new string('.', 39)), balance: NoAttacks) };
        var gorm = new Gorm(9, level.CellCenter(30, 0), -Vector3.UnitX, NoAttacks, new Random(1));
        var near = new List<Player> { new(LevelParser.Parse(new string('.', 28) + "P" + new string('.', 11)), balance: NoAttacks) };

        (float Walked, int Steps) Act(List<Player> players, int ticks)
        {
            var walked = 0f;
            var steps = 0;
            for (var tick = 0; tick < ticks && steps == 0; tick++)
            {
                var before = gorm.Position;
                var events = new List<GameEvent>();
                gorm.Act(level, players, players.Select(player => player.Body).ToList(), events);
                walked += FlatDistance(before, gorm.Position);
                steps += events.OfType<GormStepped>().Count();
            }

            return (walked, steps);
        }

        var part = Act(far, (int)(0.6f * Gorm.StepLength / (Balance.Default.Gorm.Speed * SimulationClock.SecondsPerTick)));
        Assert.Equal(0, part.Steps);
        Assert.InRange(part.Walked, 0.5f * Gorm.StepLength, 0.7f * Gorm.StepLength);

        var standing = Act(near, Second);
        Assert.Equal((0f, 0), standing);

        var again = Act(far, 4 * Second);
        Assert.Equal(1, again.Steps);
        Assert.InRange(again.Walked, Gorm.StepLength - 0.001f, Gorm.StepLength + (Balance.Default.Gorm.Speed * SimulationClock.SecondsPerTick));
    }

    [Fact]
    public void The_same_level_gives_the_same_steps()
    {
        // The steps only count the distance he walks and draw no random number; that they change nothing else in a
        // run is what the line-identical balance report shows.
        var a = Corridor(Balance.Default, length: 20);
        var b = Corridor(Balance.Default, length: 20);

        for (var tick = 0; tick < 15 * Second; tick++)
        {
            a.Tick(PlayerInput.None);
            b.Tick(PlayerInput.None);
            Assert.Equal(a.Events, b.Events);
        }

        Assert.Equal(a.Gorm!.Position, b.Gorm!.Position);
    }
}
