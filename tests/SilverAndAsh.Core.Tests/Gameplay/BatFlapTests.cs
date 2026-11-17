using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>A bat's wing beats, reported for the sound (issue #297) - and nothing else about the bat changes.</summary>
public class BatFlapTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly int FlapTicks = SimulationClock.SecondsToTicks(Bat.FlapInterval);

    [Fact]
    public void The_flap_interval_lies_where_the_audio_designer_asked_for_it()
    {
        Assert.InRange(Bat.FlapInterval, 0.3f, 0.5f);
    }

    [Fact]
    public void An_awake_bat_flaps_once_every_interval_where_it_is()
    {
        var world = new GameWorld(LevelParser.Parse("P...B"));
        var bat = world.Bats[0];
        var flaps = new List<(int Tick, BatFlapped Flap, Vector3 CentreAfter)>();

        for (var tick = 0; tick < 3 * Second; tick++)
        {
            world.Tick(PlayerInput.None);
            flaps.AddRange(world.Events.OfType<BatFlapped>().Select(flap => (tick, flap, bat.Centre)));
        }

        Assert.NotEqual(BatState.Lurking, bat.State);
        Assert.All(flaps, f => Assert.Equal(bat.Id, f.Flap.BatId));

        // Exactly once per beat, never two in one tick, and evenly spaced from the first beat on.
        Assert.Equal(flaps.Count, flaps.Select(f => f.Tick).Distinct().Count());
        Assert.InRange(flaps[0].Tick, 0, FlapTicks); // the first one within a beat of noticing a player (tick 0)
        Assert.All(flaps.Zip(flaps.Skip(1)), pair => Assert.Equal(FlapTicks, pair.Second.Tick - pair.First.Tick));
        Assert.Equal((3 * Second - 1 - flaps[0].Tick) / FlapTicks + 1, flaps.Count);

        // At the bat's body: where it was in that tick, before its move (a few centimetres from where it ends up).
        Assert.All(flaps, f => Assert.True(Vector3.Distance(f.Flap.At, f.CentreAfter) < 0.25f, $"{f.Flap.At} vs {f.CentreAfter}"));
    }

    [Fact]
    public void A_lurking_bat_hangs_still_without_a_sound()
    {
        // A wall hides the player: the bat lurks the whole time.
        var world = new GameWorld(LevelParser.Parse("P.#.B"));

        var events = Run(world, 3 * Second);

        Assert.Equal(BatState.Lurking, world.Bats[0].State);
        Assert.DoesNotContain(events, e => e is BatFlapped);
    }

    [Fact]
    public void A_bat_too_far_away_to_notice_a_player_does_not_flap()
    {
        var world = new GameWorld(LevelParser.Parse("P........B"));

        var events = Run(world, 2 * Second);

        Assert.Equal(BatState.Lurking, world.Bats[0].State);
        Assert.DoesNotContain(events, e => e is BatFlapped);
    }

    [Fact]
    public void A_dead_bat_flaps_no_more()
    {
        var world = new GameWorld(LevelParser.Parse("P...B"));
        var bat = world.Bats[0];
        Assert.Contains(Run(world, Second), e => e is BatFlapped);

        bat.TakeDamage(bat.Health, []);
        Assert.True(bat.IsDead);

        Assert.DoesNotContain(Run(world, 3 * Second), e => e is BatFlapped);
    }

    [Fact]
    public void Two_bats_flap_out_of_step()
    {
        var world = new GameWorld(LevelParser.Parse("B.....\n..P...\n.....B"));
        var ticks = new Dictionary<int, List<int>>();

        for (var tick = 0; tick < 2 * Second; tick++)
        {
            world.Tick(PlayerInput.None);
            foreach (var flap in world.Events.OfType<BatFlapped>())
            {
                (ticks.TryGetValue(flap.BatId, out var list) ? list : ticks[flap.BatId] = []).Add(tick);
            }
        }

        Assert.Equal(2, ticks.Count);
        var (first, second) = (ticks.Values.First(), ticks.Values.Last());
        Assert.Empty(first.Intersect(second));
    }

    [Fact]
    public void The_same_level_gives_the_same_wing_beats()
    {
        // The beat starts from the bat's number, not from the world's random numbers; that it changes nothing else in
        // a run is what the line-identical balance report shows.
        var level = LevelParser.Parse("B.....\n..P...\n.....B");
        var a = new GameWorld(level);
        var b = new GameWorld(level);

        for (var tick = 0; tick < 4 * Second; tick++)
        {
            a.Tick(PlayerInput.None);
            b.Tick(PlayerInput.None);
            Assert.Equal(a.Events, b.Events);
        }

        Assert.Equal(a.Bats.Select(bat => bat.Position), b.Bats.Select(bat => bat.Position));
    }

    private static List<GameEvent> Run(GameWorld world, int ticks)
    {
        var events = new List<GameEvent>();
        for (var tick = 0; tick < ticks; tick++)
        {
            world.Tick(PlayerInput.None);
            events.AddRange(world.Events);
        }

        return events;
    }
}
