using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Issue #822: a thrown knife glances off stone - a wall, a column, the floor or the ceiling - up to
/// <see cref="ThrownBalance.Ricochets"/> times (two), mirrored at the surface and without losing speed or damage; the
/// next stone keeps it. A target ends the flight at once, however often the knife has glanced off. Every test drives the
/// world tick by tick with the game's own values; where and when the knife should be is worked out here from the stone
/// of the level (<see cref="Trace"/>), not taken from the knife.
/// </summary>
public class ThrowingKnivesRicochetTests
{
    private static readonly RangedBalance Knives = Balance.Default.ThrowingKnives;
    private static readonly ThrownBalance Flight = Balance.Default.KnifeFlight;

    /// <summary>Metres a knife flies in one tick.</summary>
    private static readonly float Step = Flight.Speed * SimulationClock.SecondsPerTick;

    /// <summary>Dummies that never fall over: so every hit can be counted in their health.</summary>
    private static readonly Balance Sturdy = Balance.Default with { Dummy = Balance.Default.Dummy with { MaxHealth = 100_000 } };

    /// <summary>A hall of 10 x 10 m, the player in its middle (5 m to every wall), looking along +Z.</summary>
    private const string Hall = ".....\n.....\n..P..\n.....\n.....";

    /// <summary>The hall's far edge on X and on Z, in metres.</summary>
    private const float HallEdge = 5 * Level.CellSize;

    /// <summary>How near a point or a direction must be to the expected one.</summary>
    private const float Near = 0.001f;

    private static GameWorld World(string map, Balance? balance = null) => World(LevelParser.Parse(map), balance);

    private static GameWorld World(Level level, Balance? balance = null)
    {
        var world = new GameWorld(level, 1, balance ?? Sturdy);
        world.Player.Equip(0, WeaponKind.ThrowingKnives);
        return world;
    }

    /// <summary>A one-cell corridor along +Z: the player, <paramref name="free"/> empty cells, then <paramref name="end"/>.</summary>
    private static string Corridor(int free, string end) => "P\n" + string.Concat(Enumerable.Repeat(".\n", free)) + end;

    /// <summary>Turns the view (like the mouse would) so it looks at <paramref name="point"/>.</summary>
    private static void AimAt(GameWorld world, Vector3 point)
    {
        var player = world.Player;
        var direction = Vector3.Normalize(point - player.EyePosition);
        var pixels = new Vector2(
            -(MathF.Atan2(direction.X, direction.Z) - player.View.YawRadians) / ControlSettings.MouseSensitivity,
            -(MathF.Asin(direction.Y) - player.View.PitchRadians) / ControlSettings.MouseSensitivity);
        player.Look(PlayerInput.None with { Look = pixels });
    }

    /// <summary>One straight stretch of a knife's way: from a point along a direction to the stone that ends it.</summary>
    private sealed record Leg(Vector3 From, Vector3 Direction, float Length, Vector3 End, Vector3 Normal);

    /// <summary>The mirror image of <paramref name="direction"/> at a surface that points along <paramref name="normal"/>.</summary>
    private static Vector3 Mirror(Vector3 direction, Vector3 normal) => direction - 2f * Vector3.Dot(direction, normal) * normal;

    /// <summary>
    /// The way a knife thrown now would take if it glanced off every stone: the first <paramref name="legs"/> stretches,
    /// worked out from the level alone.
    /// </summary>
    private static List<Leg> Trace(GameWorld world, int legs)
    {
        var from = world.Player.EyePosition;
        var direction = world.Player.View.Forward;
        var way = new List<Leg>();
        for (var leg = 0; leg < legs; leg++)
        {
            var stone = Raycast.AgainstLevel(world.Level, from, direction, 200f)!.Value;
            way.Add(new Leg(from, direction, stone.Distance, stone.Point, stone.Normal));
            from = stone.Point;
            direction = Mirror(direction, stone.Normal);
        }

        return way;
    }

    /// <summary>In which tick a knife has flown <paramref name="distance"/> metres, the tick of the throw counted as 1.</summary>
    private static int TicksFor(float distance) => (int)MathF.Ceiling(distance / Step);

    /// <summary>The tick (the throw is 1) in which the knife reaches the end of leg <paramref name="index"/> of <paramref name="way"/>.</summary>
    private static int TickAtEndOf(List<Leg> way, int index) => TicksFor(way.Take(index + 1).Sum(leg => leg.Length));

    /// <summary>
    /// Throws and lets the world run for <paramref name="ticks"/> ticks in all (the throw is tick 1). Returns the events
    /// of every tick, [0] being the throw's.
    /// </summary>
    private static List<List<GameEvent>> ThrowAndRun(GameWorld world, int ticks)
    {
        var all = new List<List<GameEvent>>();
        for (var tick = 1; tick <= ticks; tick++)
        {
            world.Tick(PlayerInput.None with { Shoot = tick == 1 });
            all.Add(world.Events.ToList());
        }

        return all;
    }

    /// <summary>The tick (the throw is 1) of the one event that <paramref name="wanted"/> picks, and the event.</summary>
    private static (int Tick, T Event) Only<T>(List<List<GameEvent>> ticks, Func<T, bool>? wanted = null)
        where T : GameEvent
    {
        var found = ticks.SelectMany((events, index) => events.OfType<T>().Where(e => wanted?.Invoke(e) ?? true).Select(e => (Tick: index + 1, Event: e))).ToList();
        return Assert.Single(found);
    }

    private static void AssertNear(Vector3 expected, Vector3 actual, string what) =>
        Assert.True(Vector3.Distance(expected, actual) < Near, $"{what}: expected {expected}, was {actual}.");

    private static int Lost(TrainingDummy dummy) => Sturdy.Dummy.MaxHealth - dummy.Health;

    // ---- Off a wall ------------------------------------------------------------------------------------------------

    [Fact]
    public void The_game_s_knives_glance_off_twice()
    {
        Assert.Equal(2, Flight.Ricochets);
    }

    [Fact]
    public void A_knife_glances_off_a_wall_mirrored_and_flies_on_at_its_speed()
    {
        var world = World(Hall);
        AimAt(world, world.Player.EyePosition + new Vector3(2f, 0.5f, 5f));   // at the wall ahead, to the right and up
        var thrown = world.Player.View.Forward;
        var way = Trace(world, 2);
        var tick = TickAtEndOf(way, 0);
        Assert.Equal(HallEdge, way[0].End.Z, 2);

        var ticks = ThrowAndRun(world, tick);

        // Nothing before the tick in which the knife gets to the wall; in it, one ricochet and nothing sticks.
        Assert.DoesNotContain(ticks.Take(tick - 1).SelectMany(events => events), e => e is KnifeRicocheted or KnifeStuck or BulletImpact);
        Assert.DoesNotContain(ticks[tick - 1], e => e is KnifeStuck or BulletImpact);
        var ricochet = Assert.Single(ticks[tick - 1].OfType<KnifeRicocheted>());
        Assert.Equal(1, ricochet.Count);
        Assert.Equal(WeaponKind.ThrowingKnives, ricochet.Weapon);
        Assert.Equal(world.Player.Id, ricochet.PlayerId);
        AssertNear(way[0].End, ricochet.Point, "the place of the ricochet");
        Assert.Equal(ricochet.Point, ricochet.Place);
        Assert.Equal(-Vector3.UnitZ, ricochet.Normal);

        // Mirrored at a wall across Z: only Z turns round.
        AssertNear(thrown with { Z = -thrown.Z }, ricochet.Direction, "the direction after the wall");
        Assert.Equal(1f, ricochet.Direction.Length(), 4);

        // The knife is still in the air, and has flown the rest of this tick's stretch along the new line already.
        var knife = Assert.Single(world.FlyingKnives);
        Assert.Equal(ricochet.KnifeId, knife.Id);
        Assert.Equal(1, knife.Ricochets);
        Assert.Equal(ricochet.Direction, knife.Direction);
        Assert.Empty(world.StuckKnives);
        var beyond = tick * Step - way[0].Length;
        AssertNear(way[0].End + ricochet.Direction * beyond, knife.Position, "the knife after the tick of the ricochet");
        Assert.Equal(Knives.Range - tick * Step, knife.RangeLeft, 2);

        // One tick on it is one full stretch further along the mirrored line: it lost no speed.
        var before = knife.Position;
        world.Tick(PlayerInput.None);
        AssertNear(before + ricochet.Direction * Step, Assert.Single(world.FlyingKnives).Position, "the knife one tick later");
    }

    [Fact]
    public void After_two_ricochets_the_third_stone_keeps_the_knife()
    {
        var world = World(Hall);
        AimAt(world, world.Player.EyePosition + new Vector3(2f, 0.5f, 5f));
        var way = Trace(world, 3);
        Assert.True(way.Sum(leg => leg.Length) < Knives.Range, "The three stretches must fit into the knife's range.");

        // The wall ahead, the wall on the right, the wall behind.
        Assert.Equal([-Vector3.UnitZ, -Vector3.UnitX, Vector3.UnitZ], way.Select(leg => leg.Normal).ToArray());

        var ticks = ThrowAndRun(world, TickAtEndOf(way, 2) + SimulationClock.TicksPerSecond);

        var ricochets = ticks.SelectMany((events, index) => events.OfType<KnifeRicocheted>().Select(e => (Tick: index + 1, Event: e))).ToList();
        Assert.Equal(2, ricochets.Count);
        for (var index = 0; index < 2; index++)
        {
            Assert.Equal(TickAtEndOf(way, index), ricochets[index].Tick);
            Assert.Equal(index + 1, ricochets[index].Event.Count);
            AssertNear(way[index].End, ricochets[index].Event.Point, $"ricochet {index + 1}");
            Assert.Equal(way[index].Normal, ricochets[index].Event.Normal);
            AssertNear(way[index + 1].Direction, ricochets[index].Event.Direction, $"the direction after ricochet {index + 1}");
        }

        // The third stone keeps it: there, in the tick its way says, flown in along the last stretch.
        var (stuckInTick, stuck) = Only<KnifeStuck>(ticks);
        Assert.Equal(TickAtEndOf(way, 2), stuckInTick);
        Assert.Null(stuck.TargetId);
        Assert.Equal(ricochets[0].Event.KnifeId, stuck.KnifeId);
        Assert.Equal(ricochets[1].Event.KnifeId, stuck.KnifeId);
        AssertNear(way[2].End, stuck.Point, "where the knife sticks");
        AssertNear(way[2].Direction, stuck.Direction, "the way it flew in");
        var (markInTick, mark) = Only<BulletImpact>(ticks);
        Assert.Equal(stuckInTick, markInTick);
        Assert.Equal(way[2].Normal, mark.Normal);

        // It stays: a second later it is still there, nothing flies, and nothing more was reported.
        Assert.Empty(world.FlyingKnives);
        var held = Assert.Single(world.StuckKnives);
        Assert.Equal(stuck.KnifeId, held.Id);
        Assert.Equal(stuck.Point, held.Point);
    }

    [Fact]
    public void A_knife_that_comes_straight_back_flies_through_its_thrower()
    {
        // Thrown level at the wall ahead, it comes back along its own line - through the eyes it was thrown from.
        var world = World(Corridor(2, "#"));
        var health = world.Player.Health;
        var eye = world.Player.EyePosition;
        var way = Trace(world, 3);
        Assert.Equal([-Vector3.UnitZ, Vector3.UnitZ, -Vector3.UnitZ], way.Select(leg => leg.Normal).ToArray());

        var ticks = ThrowAndRun(world, TickAtEndOf(way, 2));

        Assert.DoesNotContain(ticks.SelectMany(events => events), e => e is PlayerHit or TargetHitBy);
        Assert.Equal(health, world.Player.Health);
        var (_, second) = Only<KnifeRicocheted>(ticks, e => e.Count == 2);
        Assert.True(second.Point.Z < eye.Z, $"The second ricochet at {second.Point} is not behind the thrower at {eye}.");
        var (_, stuck) = Only<KnifeStuck>(ticks);
        Assert.Null(stuck.TargetId);
        AssertNear(way[0].End, stuck.Point, "back in the wall it first glanced off");
    }

    // ---- Off the floor and the ceiling -----------------------------------------------------------------------------

    [Theory]
    [InlineData(-2f)] // down: the floor, the ceiling, and the floor keeps it
    [InlineData(2f)]  // up: the ceiling, the floor, and the ceiling keeps it
    public void A_knife_glances_off_the_floor_and_the_ceiling(float rise)
    {
        var world = World(Hall);
        AimAt(world, world.Player.EyePosition + new Vector3(0f, rise, 1f));
        var thrown = world.Player.View.Forward;
        var way = Trace(world, 3);
        var first = rise < 0f ? Vector3.UnitY : -Vector3.UnitY;   // the floor points up, the ceiling down
        Assert.Equal([first, -first, first], way.Select(leg => leg.Normal).ToArray());

        var ticks = ThrowAndRun(world, TickAtEndOf(way, 2));

        var (firstTick, one) = Only<KnifeRicocheted>(ticks, e => e.Count == 1);
        Assert.Equal(TickAtEndOf(way, 0), firstTick);
        Assert.Equal(first, one.Normal);
        AssertNear(way[0].End, one.Point, "the first ricochet");
        Assert.Equal(rise < 0f ? 0f : Cell.DefaultCeilingHeight, one.Point.Y, 2);

        // Mirrored at a level surface: only the height turns round.
        AssertNear(thrown with { Y = -thrown.Y }, one.Direction, "the direction after the first ricochet");

        var (secondTick, two) = Only<KnifeRicocheted>(ticks, e => e.Count == 2);
        Assert.Equal(TickAtEndOf(way, 1), secondTick);
        Assert.Equal(-first, two.Normal);
        Assert.Equal(rise < 0f ? Cell.DefaultCeilingHeight : 0f, two.Point.Y, 2);
        AssertNear(thrown, two.Direction, "the direction after the second ricochet");

        var (stuckInTick, stuck) = Only<KnifeStuck>(ticks);
        Assert.Equal(TickAtEndOf(way, 2), stuckInTick);
        AssertNear(way[2].End, stuck.Point, "where the knife sticks");
        Assert.Equal(first, Only<BulletImpact>(ticks).Event.Normal);
    }

    // ---- In a corner -----------------------------------------------------------------------------------------------

    [Fact]
    public void In_a_corner_the_knife_glances_off_two_walls_in_one_tick()
    {
        var world = World(Hall);
        var eye = world.Player.EyePosition;
        AimAt(world, new Vector3(HallEdge - 0.05f, eye.Y, HallEdge));   // level, at the wall ahead 5 cm from the corner
        var thrown = world.Player.View.Forward;
        var way = Trace(world, 3);
        var tick = TickAtEndOf(way, 0);
        Assert.True(way[1].Length < 0.2f && TickAtEndOf(way, 1) == tick, "Both walls of the corner must lie on the stretch of one tick.");

        var ticks = ThrowAndRun(world, tick);

        Assert.DoesNotContain(ticks.Take(tick - 1).SelectMany(events => events), e => e is KnifeRicocheted);
        var both = ticks[tick - 1].OfType<KnifeRicocheted>().ToList();
        Assert.Equal([1, 2], both.Select(e => e.Count).ToArray());
        Assert.Equal([-Vector3.UnitZ, -Vector3.UnitX], both.Select(e => e.Normal).ToArray());
        AssertNear(way[0].End, both[0].Point, "the first wall");
        AssertNear(way[1].End, both[1].Point, "the second wall");

        // Mirrored at both walls it flies back the way it came, and nothing stuck: both ricochets are used up.
        AssertNear(-thrown, both[1].Direction, "the direction out of the corner");
        Assert.DoesNotContain(ticks[tick - 1], e => e is KnifeStuck or BulletImpact);
        var knife = Assert.Single(world.FlyingKnives);
        Assert.Equal(2, knife.Ricochets);
        var beyond = tick * Step - way[0].Length - way[1].Length;
        AssertNear(way[1].End - thrown * beyond, knife.Position, "the knife after the tick in the corner");

        // The next stone - the opposite corner's wall - keeps it.
        var rest = ThrowAndRun(world, TickAtEndOf(way, 2) - tick + 1);
        var (_, stuck) = Only<KnifeStuck>(rest);
        AssertNear(way[2].End, stuck.Point, "where the knife sticks");
        Assert.DoesNotContain(rest.SelectMany(events => events), e => e is KnifeRicocheted);
    }

    // ---- A target ends the flight ----------------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void A_target_keeps_the_knife_with_full_damage_after_any_number_of_ricochets(int ricochets)
    {
        // A corridor one cell wide, the dummy 8 m ahead. Aimed at the dummy's picture in the side walls - one corridor
        // width to the side for every ricochet - the knife zigzags between the walls and meets the dummy's middle.
        var world = World(Corridor(3, "D\n."));
        var dummy = world.Dummies[0];
        var middle = dummy.Body.Position + new Vector3(0f, dummy.Body.Height / 2f, 0f);
        AimAt(world, middle + new Vector3(ricochets * Level.CellSize, 0f, 0f));
        var way = Trace(world, ricochets + 1);
        var last = way[ricochets];
        Assert.All(way.Take(ricochets), leg => Assert.False(
            Raycast.AgainstCylinder(leg.From, leg.Direction, dummy.Body) <= leg.Length, "The dummy must not stand on a stretch before the last."));
        var onLast = Raycast.AgainstCylinder(last.From, last.Direction, dummy.Body)!.Value;
        Assert.True(onLast < last.Length, "The dummy must stand on the last stretch, before its stone.");
        var flown = way.Take(ricochets).Sum(leg => leg.Length) + onLast;

        var ticks = ThrowAndRun(world, TicksFor(flown) + SimulationClock.TicksPerSecond);
        var all = ticks.SelectMany(events => events).ToList();

        Assert.Equal(Enumerable.Range(1, ricochets).ToArray(), all.OfType<KnifeRicocheted>().Select(e => e.Count).ToArray());
        Assert.All(all.OfType<KnifeRicocheted>(), e => Assert.Equal(1f, MathF.Abs(e.Normal.X)));
        var (stuckInTick, stuck) = Only<KnifeStuck>(ticks);
        Assert.Equal(TicksFor(flown), stuckInTick);
        Assert.Equal(dummy.Id, stuck.TargetId);
        AssertNear(last.From + last.Direction * onLast, stuck.Point, "where the knife sticks in the dummy");
        AssertNear(last.Direction, stuck.Direction, "the way it flew in");

        // Full damage, once, in the body - and no mark on stone.
        Assert.Equal(Knives.Damage, Lost(dummy));
        Assert.Equal(new DummyHit(dummy.Id, Knives.Damage), Assert.Single(all.OfType<DummyHit>()));
        Assert.DoesNotContain(all, e => e is BulletImpact);
        Assert.Empty(world.FlyingKnives);
        Assert.Equal(dummy.Id, Assert.Single(world.StuckKnives).TargetId);
    }

    [Fact]
    public void Stone_just_before_a_target_turns_the_knife_away_the_target_is_not_hurt()
    {
        // A column stands so close before the dummy that the knife would reach both within the stretch of one tick: it
        // glances off the column in that tick, and what is left of the stretch takes it away from the dummy.
        var open = LevelParser.Parse(Corridor(2, "D\n."));
        var probe = World(open);
        var eye = probe.Player.EyePosition;
        var forward = probe.Player.View.Forward;
        var toDummy = Raycast.AgainstCylinder(eye, forward, probe.Dummies[0].Body)!.Value;
        var tick = TicksFor(toDummy);
        var start = (tick - 1) * Step;
        var intoStretch = toDummy - start;
        Assert.True(intoStretch > 0.02f, "The dummy's front lies on the edge of a stretch - no room for stone before it.");
        var column = new Column(eye.X, eye.Z + start + intoStretch / 2f + Column.Radius);
        var world = World(open.WithColumns([column]));
        var way = Trace(world, 3);
        Assert.True(start < way[0].Length && way[0].Length < toDummy, "The column's front must lie on the same stretch as the dummy's, before it.");

        var ticks = ThrowAndRun(world, TickAtEndOf(way, 2) + SimulationClock.TicksPerSecond);

        var (firstTick, first) = Only<KnifeRicocheted>(ticks, e => e.Count == 1);
        Assert.Equal(tick, firstTick);
        AssertNear(way[0].End, first.Point, "the column's front");
        AssertNear(-forward, first.Direction, "the direction off the column");
        Assert.DoesNotContain(ticks.SelectMany(events => events), e => e is DummyHit or TargetHitBy);
        Assert.Equal(0, Lost(world.Dummies[0]));

        // Off the wall behind the thrower it comes back - and now the column keeps it.
        var (_, stuck) = Only<KnifeStuck>(ticks);
        Assert.Null(stuck.TargetId);
        AssertNear(way[0].End, stuck.Point, "back in the column");
    }

    // ---- The range -------------------------------------------------------------------------------------------------

    [Fact]
    public void A_ricochet_gives_the_knife_no_more_range()
    {
        // The wall is 27 m ahead, the knife flies 40 m: it glances off, and 13 m on the way back it is gone - without a
        // trace, like every knife at the end of its range.
        var world = World(Corridor(13, "#"));
        var way = Trace(world, 2);
        Assert.True(way[0].Length < Knives.Range && way[0].Length + way[1].Length > Knives.Range, "The knife's range must end between the first wall and the second.");
        var lastTick = TicksFor(Knives.Range);
        var turnedAt = way[0].End.Z;
        var back = Knives.Range - way[0].Length;

        var ticks = ThrowAndRun(world, lastTick - 1);
        var seenLast = Assert.Single(world.FlyingKnives);
        Assert.Equal(1, seenLast.Ricochets);

        // Seen last one tick before the end of its range: at most `back` metres from the wall, and nearly that far.
        var fromWall = turnedAt - seenLast.Position.Z;
        Assert.True(fromWall <= back + Near && fromWall > back - Step - Near, $"The knife is {fromWall} m back from the wall, its range ends {back} m from it.");
        Assert.True(seenLast.RangeLeft > 0f && seenLast.RangeLeft <= Step + Near, $"{seenLast.RangeLeft} m of range left one tick before the end.");

        // The tick in which its range ends, and a second more.
        for (var tick = 0; tick <= SimulationClock.TicksPerSecond; tick++)
        {
            world.Tick(PlayerInput.None);
            ticks.Add(world.Events.ToList());
        }

        Assert.Empty(world.FlyingKnives);
        Assert.Empty(world.StuckKnives);
        var (ricochetTick, _) = Only<KnifeRicocheted>(ticks);
        Assert.Equal(TickAtEndOf(way, 0), ricochetTick);
        Assert.DoesNotContain(ticks.SelectMany(events => events), e => e is KnifeStuck or BulletImpact);
    }

    // ---- The number comes from the balance -------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void The_balance_says_how_often_a_knife_glances_off(int allowed)
    {
        var world = World(Hall, Sturdy with { KnifeFlight = Flight with { Ricochets = allowed } });
        AimAt(world, world.Player.EyePosition + new Vector3(2f, 0.5f, 5f));
        var way = Trace(world, allowed + 1);
        Assert.True(way.Sum(leg => leg.Length) < Knives.Range, "The stretches must fit into the knife's range.");

        var ticks = ThrowAndRun(world, TickAtEndOf(way, allowed) + SimulationClock.TicksPerSecond);
        var all = ticks.SelectMany(events => events).ToList();

        Assert.Equal(Enumerable.Range(1, allowed).ToArray(), all.OfType<KnifeRicocheted>().Select(e => e.Count).ToArray());
        var (stuckInTick, stuck) = Only<KnifeStuck>(ticks);
        Assert.Equal(TickAtEndOf(way, allowed), stuckInTick);
        AssertNear(way[allowed].End, stuck.Point, "where the knife sticks");
        Assert.Empty(world.FlyingKnives);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("5", 5)]
    public void A_number_of_ricochets_from_zero_up_is_taken_from_the_file(string value, int expected)
    {
        var result = BalanceFile.Parse($$"""{ "knifeFlight": { "ricochets": {{value}} } }""");

        Assert.Empty(result.Warnings);
        Assert.Equal(expected, result.Balance.KnifeFlight.Ricochets);
        Assert.Equal(Flight.Speed, result.Balance.KnifeFlight.Speed);
    }

    [Fact]
    public void A_negative_number_of_ricochets_falls_back_to_the_standard()
    {
        var result = BalanceFile.Parse("""{ "knifeFlight": { "ricochets": -1 } }""");

        Assert.Equal(Flight, result.Balance.KnifeFlight);
        Assert.Contains(result.Warnings, w => w.Contains("knifeFlight.ricochets"));
    }

    [Fact]
    public void The_file_in_the_repository_has_the_standard_number_of_ricochets()
    {
        var result = BalanceFile.Load(RepositoryFiles.Find("assets/balance.json"));

        Assert.Empty(result.Warnings);
        Assert.Equal(Flight.Ricochets, result.Balance.KnifeFlight.Ricochets);
    }

    // ---- What must not happen --------------------------------------------------------------------------------------

    [Fact]
    public void A_knife_that_starts_inside_the_stone_sticks_there_and_does_not_glance_off()
    {
        // No player can throw from inside a wall; should a knife ever start there, it has no surface to be mirrored at.
        // This drives the knives of a world directly - the same Move the world calls once a tick.
        var level = LevelParser.Parse(Corridor(1, "#"));
        var thrower = new Player(level);
        var inStone = level.CellCenter(0, 2) + new Vector3(0f, BodySizes.Player.EyeHeight, 0f);
        Assert.True(LineOfSight.IsInsideGeometry(level, inStone));
        var knives = new ThrownKnives();
        var events = new List<GameEvent>();
        knives.Throw(thrower, WeaponKind.ThrowingKnives, inStone, Vector3.UnitZ, Flight, Knives.Range);

        knives.Move(level, [], (_, _, _) => true, events);

        Assert.DoesNotContain(events, e => e is KnifeRicocheted);
        var stuck = Assert.Single(events.OfType<KnifeStuck>());
        Assert.Equal(inStone, stuck.Point);
        Assert.Equal(Vector3.UnitZ, stuck.Direction);
        Assert.Empty(knives.Flying);
        Assert.Equal(inStone, Assert.Single(knives.Stuck).Point);
    }

    [Fact]
    public void A_knife_never_flies_inside_the_stone_while_it_glances_about()
    {
        // Thrown into a corner, across the room and at floor and ceiling: after every tick every knife in flight is in
        // the open, has range left and has not glanced off more often than the balance allows.
        var world = World(Hall);
        var eye = world.Player.EyePosition;
        var cooldown = SimulationClock.SecondsToTicks(Knives.Cooldown);
        Vector3[] aims = [new(HallEdge, eye.Y, HallEdge), eye + new Vector3(3f, -1f, 1f), eye + new Vector3(-1f, 2f, -0.5f)];
        var ricochets = 0;

        foreach (var aim in aims)
        {
            AimAt(world, aim);
            for (var tick = 0; tick <= cooldown; tick++)
            {
                world.Tick(PlayerInput.None with { Shoot = tick == 0 });
                ricochets += world.Events.OfType<KnifeRicocheted>().Count();
                Assert.All(world.FlyingKnives, knife =>
                {
                    Assert.False(LineOfSight.IsInsideGeometry(world.Level, knife.Position), $"Knife {knife.Id} is inside the stone at {knife.Position}.");
                    Assert.InRange(knife.Ricochets, 0, Flight.Ricochets);
                    Assert.True(knife.RangeLeft > 0f);
                    Assert.Equal(1f, knife.Direction.Length(), 4);
                });
            }
        }

        Assert.Equal(aims.Length * Flight.Ricochets, ricochets);
        Assert.Equal(aims.Length, world.StuckKnives.Count);
        Assert.Empty(world.FlyingKnives);
    }

    // ---- The same seed, the same flight (decision record 0003) -----------------------------------------------------

    [Fact]
    public void The_same_seed_and_the_same_inputs_give_the_same_ricochets()
    {
        // A room with a bat that circles the thrower: three knives thrown about the room, in two worlds of one seed.
        const string batRoom = "B......\n.......\n.......\n...P...\n.......\n.......\n.......";
        var bat = Balance.Default with { Bat = Balance.Default.Bat with { MaxHealth = 100_000 } };
        var first = World(batRoom, bat);
        var second = World(batRoom, bat);
        var cooldown = SimulationClock.SecondsToTicks(Knives.Cooldown);
        Vector3[] aims = [new(3f, 0.5f, 5f), new(-4f, -1f, 2f), new(1f, 2f, -3f)];
        var ricochets = 0;

        foreach (var aim in aims)
        {
            foreach (var world in new[] { first, second })
            {
                AimAt(world, world.Player.EyePosition + aim);
            }

            for (var tick = 0; tick <= cooldown; tick++)
            {
                var input = PlayerInput.None with { Shoot = tick == 0 };
                first.Tick(input);
                second.Tick(input);

                Assert.Equal(first.Events.Select(e => e.ToString()).ToArray(), second.Events.Select(e => e.ToString()).ToArray());
                Assert.Equal(first.Events.Select(e => e.Place).ToArray(), second.Events.Select(e => e.Place).ToArray());
                Assert.Equal(
                    first.FlyingKnives.Select(knife => (knife.Id, knife.Position, knife.Direction, knife.Ricochets, knife.RangeLeft)).ToArray(),
                    second.FlyingKnives.Select(knife => (knife.Id, knife.Position, knife.Direction, knife.Ricochets, knife.RangeLeft)).ToArray());
                Assert.Equal(
                    first.StuckKnives.Select(knife => (knife.Id, knife.Point, knife.Direction, knife.TargetId)).ToArray(),
                    second.StuckKnives.Select(knife => (knife.Id, knife.Point, knife.Direction, knife.TargetId)).ToArray());
                ricochets += first.Events.OfType<KnifeRicocheted>().Count();
            }
        }

        Assert.True(ricochets >= 4, $"Only {ricochets} ricochets - the comparison says little.");
    }
}
