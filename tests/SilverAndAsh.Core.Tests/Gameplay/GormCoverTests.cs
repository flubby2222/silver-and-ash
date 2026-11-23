using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Issue #590: a free-standing column covers against Gorm's chain swing and shock wave - but it bears only
/// <see cref="ColumnsBalance.HitsToBreak"/> blows (one per attack that strikes it), then it breaks: the attacks go
/// through and the way is free.
/// </summary>
public class GormCoverTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly int HitsToBreak = Balance.Default.Columns.HitsToBreak;

    /// <summary>A player who survives a lot - to watch many attacks in a row.</summary>
    internal static readonly Balance Tough = Balance.Default with { Player = Balance.Default.Player with { MaxHealth = 100_000 } };

    /// <summary>In line between the player (cell 7/3) and Gorm (6 m further along +Z, close enough for hammer and swing).</summary>
    private static readonly Column Between = Column.AtCell(7, 5);

    /// <summary>Within reach of both attacks, but 4 m off the line between them.</summary>
    private static readonly Column Aside = Column.AtCell(9, 5);

    /// <summary>An open hall, 15 cells wide and 25 deep; the player in column 7, row 3, looking along +Z.</summary>
    private static Level Hall(params Column[] columns)
    {
        var rows = Enumerable.Range(0, 25).Select(z => new string(Enumerable.Range(0, 15).Select(x => (x, z) == (7, 3) ? 'P' : '.').ToArray()));
        return LevelParser.Parse(string.Join('\n', rows)).WithColumns(columns);
    }

    /// <summary>Gorm and one player facing each other across the hall, ticked together, the columns worn as in a world.</summary>
    private sealed class Duel
    {
        public Duel(params Column[] columns)
        {
            Level = Hall(columns);
            Player = new Player(Level, balance: Tough);
            Gorm = new Gorm(9, Player.Position + new Vector3(0f, 0f, 6f), -Vector3.UnitZ, Tough, new Random(1));
        }

        public Level Level { get; }

        public Player Player { get; }

        public Gorm Gorm { get; }

        public ColumnWear Wear { get; } = new(HitsToBreak);

        public List<GameEvent> Events { get; } = [];

        public void Run(int ticks, Func<bool> until, Func<PlayerInput>? input = null)
        {
            for (var tick = 0; tick < ticks && !until(); tick++)
            {
                Player.Tick(input?.Invoke() ?? PlayerInput.None);
                Events.AddRange(Player.Events);
                var events = new List<GameEvent>();
                Gorm.Act(Level, [Player], [Player.Body], events, columns: Wear);
                Events.AddRange(events);
            }
        }

        /// <summary>
        /// Runs until Gorm starts winding up <paramref name="attack"/> (an attack of the other kind may come first), then
        /// through it until he rests or is stuck. Gives the events of that attack.
        /// </summary>
        public List<GameEvent> Through(GormAttack attack)
        {
            Run(60 * Second, () => Gorm.State == GormState.WindingUp && Gorm.Attack == attack);
            Assert.True(Gorm.State == GormState.WindingUp && Gorm.Attack == attack, $"Gorm never wound up his {attack}.");
            var start = Events.Count;
            Run(5 * Second, () => Gorm.State is GormState.Recovering or GormState.Stuck);
            return Events.Skip(start).ToList();
        }
    }

    [Fact]
    public void A_column_between_covers_against_the_chain_swing_and_takes_the_blow()
    {
        var duel = new Duel(Between);
        var health = duel.Player.Health;

        var swing = duel.Through(GormAttack.Swing);

        Assert.Equal(health, duel.Player.Health);
        Assert.Contains(new GormSwung(9, false), swing);
        Assert.Equal([new ColumnHit(Between, duel.Wear.HitsOn(Between), HitsToBreak)], swing.OfType<ColumnHit>());
    }

    [Fact]
    public void A_column_between_covers_against_the_shock_wave_and_takes_the_blow()
    {
        var duel = new Duel(Between);
        var health = duel.Player.Health;

        var hammer = duel.Through(GormAttack.Hammer);

        Assert.Equal(health, duel.Player.Health);
        Assert.DoesNotContain(duel.Events, e => e is ShockwaveHit);
        Assert.Equal([new ColumnHit(Between, duel.Wear.HitsOn(Between), HitsToBreak)], hammer.OfType<ColumnHit>());
    }

    [Theory]
    [InlineData(GormAttack.Swing)]
    [InlineData(GormAttack.Hammer)]
    public void A_column_off_the_line_does_not_cover_though_the_blow_strikes_it(GormAttack attack)
    {
        var duel = new Duel(Aside);
        var events = duel.Through(attack);

        Assert.Contains(events, e => e is PlayerHit);
        Assert.Equal([new ColumnHit(Aside, duel.Wear.HitsOn(Aside), HitsToBreak)], events.OfType<ColumnHit>());
    }

    [Theory]
    [InlineData(GormAttack.Swing, 0.9f, true)]
    [InlineData(GormAttack.Swing, 1.1f, false)]
    [InlineData(GormAttack.Hammer, 0.9f, true)]
    [InlineData(GormAttack.Hammer, 1.1f, false)]
    public void A_column_covers_as_wide_as_it_is(GormAttack attack, float offsetInRadii, bool covers)
    {
        // The column moved sideways off the line between them: its edge still on the line, or just off it.
        var duel = new Duel(new Column(Between.X + (offsetInRadii * Column.Radius), Between.Z));
        var health = duel.Player.Health;

        var events = duel.Through(attack);

        Assert.Single(events.OfType<ColumnHit>());
        Assert.Equal(!covers, events.Any(e => e is PlayerHit));
        Assert.Equal(covers, duel.Player.Health == health);
    }

    [Fact]
    public void The_cover_ends_with_the_break_whoever_steps_behind_the_broken_column_is_hit()
    {
        // The player 6 m in front of Gorm, the column 2 m in front of him and 1 m to the side: its shadow falls 3 m beside
        // the player. He stands well off it until the column breaks, then steps into it.
        var start = Column.AtCell(7, 3).Centre;
        var column = new Column(start.X - 1f, start.Y + 4f);
        var duel = new Duel(column);
        var shadow = start.X - 3f;
        var health = duel.Player.Health;

        var origin = BreakWithTheHammer(duel, column);
        Assert.False(Behind(column, origin, duel.Player.Position), "The player stood in the shadow already at the break - the test would prove nothing.");

        // From the break on he walks sideways into the shadow (+X move goes to -X) and stops there.
        var hit = duel.Events.Count;
        duel.Run(
            5 * Second,
            () => duel.Gorm.State is GormState.Recovering or GormState.Stuck,
            () => PlayerInput.None with { Move = new Vector2(duel.Player.Position.X > shadow ? 1f : 0f, 0f) });

        Assert.True(Behind(column, origin, duel.Player.Position), "The player never got behind the broken column.");
        Assert.Contains(duel.Events.Skip(hit), e => e is ShockwaveHit);
        Assert.True(duel.Player.Health < health);
        Assert.Equal(GormState.Recovering, duel.Gorm.State);
    }

    [Fact]
    public void Who_was_behind_the_column_at_the_break_is_hit_once_he_leaves_its_shadow()
    {
        var duel = new Duel(Between);
        var start = duel.Player.Position.X;
        var health = duel.Player.Health;

        var origin = BreakWithTheHammer(duel, Between);
        Assert.True(Behind(Between, origin, duel.Player.Position), "The player was not behind the column at the break - the test would prove nothing.");

        // From the break on he walks sideways out of the shadow (-X move goes to +X) and stops there.
        var after = duel.Events.Count;
        duel.Run(
            5 * Second,
            () => duel.Gorm.State is GormState.Recovering or GormState.Stuck,
            () => PlayerInput.None with { Move = new Vector2(duel.Player.Position.X < start + 3f ? -1f : 0f, 0f) });

        Assert.False(Behind(Between, origin, duel.Player.Position), "The player never left the shadow.");
        Assert.Contains(duel.Events.Skip(after), e => e is ShockwaveHit);
        Assert.True(duel.Player.Health < health);
    }

    /// <summary>
    /// Waits for Gorm's hammer, wears <paramref name="column"/> down to its last blow and runs on until the wave breaks it.
    /// Nobody is hit up to then. Gives where the wave started.
    /// </summary>
    private static Vector3 BreakWithTheHammer(Duel duel, Column column)
    {
        BreakWith(duel, column, GormAttack.Hammer);
        return duel.Gorm.Shockwave!.Value.Origin;
    }

    /// <summary>
    /// Waits for Gorm's <paramref name="attack"/>, wears <paramref name="column"/> down to its last blow and runs on until
    /// that attack breaks it. Nobody is hit up to then.
    /// </summary>
    private static void BreakWith(Duel duel, Column column, GormAttack attack)
    {
        duel.Run(60 * Second, () => duel.Gorm.State == GormState.WindingUp && duel.Gorm.Attack == attack);
        Assert.True(duel.Gorm.Attack == attack, $"Gorm never wound up his {attack}.");
        while (duel.Wear.HitsOn(column) < HitsToBreak - 1)
        {
            duel.Wear.Strike(column, 0f, []);
        }

        var before = duel.Events.Count;
        duel.Run(5 * Second, () => duel.Events.Skip(before).Any(e => e is ColumnBroke));
        Assert.Contains(duel.Events.Skip(before), e => e is ColumnBroke);
        Assert.DoesNotContain(duel.Events.Skip(before), e => e is PlayerHit or ShockwaveHit);
    }

    /// <summary>The column stands on the flat line from <paramref name="from"/> to <paramref name="to"/>.</summary>
    private static bool Behind(Column column, Vector3 from, Vector3 to)
    {
        var start = new Vector2(from.X, from.Z);
        var line = new Vector2(to.X, to.Z) - start;
        var along = Math.Clamp(Vector2.Dot(column.Centre - start, line) / line.LengthSquared(), 0f, 1f);
        return Vector2.Distance(column.Centre, start + (line * along)) < Column.Radius;
    }

    [Fact]
    public void A_column_behind_another_is_covered_by_it_and_takes_no_blow()
    {
        // Gorm - Between - this one - the player, all in a line.
        var duel = new Duel(Between, Column.AtCell(7, 4));

        duel.Through(GormAttack.Hammer);

        Assert.Equal([new ColumnHit(Between, 1, HitsToBreak)], duel.Events.OfType<ColumnHit>());
    }

    [Fact]
    public void The_column_counts_one_blow_per_attack_covers_up_to_its_last_and_then_lets_the_attacks_through()
    {
        var duel = new Duel(Between);
        var health = duel.Player.Health;

        // Hammer and swing take turns; each strikes the column once.
        duel.Run(120 * Second, () => duel.Events.OfType<ColumnBroke>().Any());
        var attacksUntilBroken = duel.Events.Count(e => e is GormSlammed or GormSwingStarted);
        Assert.Equal(HitsToBreak, attacksUntilBroken);
        Assert.Equal(Enumerable.Range(1, HitsToBreak).Select(hits => new ColumnHit(Between, hits, HitsToBreak)), duel.Events.OfType<ColumnHit>());

        // The breaking blow is still caught: the player is unhurt to the end of that attack.
        duel.Run(5 * Second, () => duel.Gorm.State is GormState.Recovering or GormState.Stuck);
        Assert.Equal(health, duel.Player.Health);
        Assert.True(duel.Wear.IsBroken(Between));

        // The next attack goes through, and the broken column takes no more blows.
        var before = duel.Events.Count;
        duel.Run(30 * Second, () => duel.Events.Skip(before).Any(e => e is PlayerHit));
        Assert.True(duel.Player.Health < health, "After the column broke, the next attack still did not hit.");
        Assert.Single(duel.Events.OfType<ColumnBroke>());
        Assert.Equal(HitsToBreak, duel.Events.OfType<ColumnHit>().Count());
    }

    [Fact]
    public void The_blows_on_a_column_are_heard_where_it_stands()
    {
        var duel = new Duel(Between);

        duel.Run(120 * Second, () => duel.Events.OfType<ColumnBroke>().Any());

        Assert.All(duel.Events.OfType<ColumnHit>(), hit => Assert.Equal(Between.Centre, new Vector2(hit.Place!.Value.X, hit.Place.Value.Z)));
        var broke = Assert.Single(duel.Events.OfType<ColumnBroke>());
        Assert.Equal(new ColumnBroke(Between), broke);
        Assert.Equal(Between.Centre, new Vector2(broke.Place!.Value.X, broke.Place.Value.Z));
    }

    [Fact]
    public void A_column_takes_no_blow_while_the_attack_does_not_reach_it()
    {
        var duel = new Duel(Column.AtCell(1, 24));

        duel.Through(GormAttack.Swing);
        duel.Through(GormAttack.Hammer);

        Assert.DoesNotContain(duel.Events, e => e is ColumnHit);
    }

    // ---- The edges of the rule (issue #602) ----------------------------------------------------------------------------

    /// <summary>Where Gorm stands in a <see cref="Duel"/>: 6 m in front of the player; he looks along -Z, his right is +X.</summary>
    private static readonly Vector2 GormStart = Column.AtCell(7, 3).Centre + new Vector2(0f, 6f);

    /// <summary>The column whose middle lies <paramref name="distance"/> from Gorm at <paramref name="bearing"/> (radians, to his right).</summary>
    private static Column SeenFromGorm(float bearing, float distance) =>
        new(GormStart.X + (distance * MathF.Sin(bearing)), GormStart.Y - (distance * MathF.Cos(bearing)));

    /// <summary>Gorm stood where the geometry of a test assumes, looking along -Z - else the test would prove nothing.</summary>
    private static void AssertGormStoodStill(Duel duel)
    {
        Assert.True(Vector2.Distance(GormStart, new Vector2(duel.Gorm.Position.X, duel.Gorm.Position.Z)) < 0.001f, $"Gorm moved to {duel.Gorm.Position}.");
        Assert.True(Vector3.Distance(-Vector3.UnitZ, duel.Gorm.Facing) < 0.001f, $"Gorm turned to {duel.Gorm.Facing}.");
    }

    [Theory]
    [InlineData("arc", 0.9f, true)]
    [InlineData("arc", 1.1f, false)]
    [InlineData("reach", 0.9f, true)]
    [InlineData("reach", 1.1f, false)]
    public void The_chain_swing_strikes_a_column_at_the_edge_of_its_arc_and_reach_as_far_as_the_column_is_wide(string edge, float outside, bool struck)
    {
        var gorm = Balance.Default.Gorm;
        var halfArc = gorm.SwingArc * MathF.PI / 360f;

        // "arc": 3 m from him, just behind the start of the swing at his left - its middle outside the arc by `outside`
        // times the angle half the column fills. "reach": half way to his left, its near side `outside` radii beyond
        // the reach minus one radius (0.9: 0.1 radius inside the reach, 1.1: 0.1 radius beyond it).
        const float Near = 3f;
        var column = edge == "arc"
            ? SeenFromGorm(-halfArc - (outside * MathF.Asin(Column.Radius / Near)), Near)
            : SeenFromGorm(-halfArc / 2f, gorm.SwingReach + (outside * Column.Radius));
        var duel = new Duel(column);

        var swing = duel.Through(GormAttack.Swing);

        AssertGormStoodStill(duel);
        Assert.Equal(struck, swing.OfType<ColumnHit>().Any(hit => hit.Column == column));
    }

    [Theory]
    [InlineData(0.9f, true)]
    [InlineData(1.1f, false)]
    public void The_shock_wave_strikes_a_column_once_it_reaches_the_near_side_not_the_middle(float outside, bool struck)
    {
        // To his left (-X), the middle `outside` radii beyond where the wave stops: 0.9 - its near side 0.1 radius inside,
        // 1.1 - 0.1 radius outside.
        var column = SeenFromGorm(-MathF.PI / 2f, Balance.Default.Gorm.ShockwaveRadius + (outside * Column.Radius));
        var duel = new Duel(column);

        var hammer = duel.Through(GormAttack.Hammer);

        AssertGormStoodStill(duel);
        Assert.Contains(hammer, e => e is GormSlammed);
        Assert.Equal(struck, hammer.OfType<ColumnHit>().Any(hit => hit.Column == column));
    }

    [Theory]
    [InlineData(GormAttack.Hammer, GormAttack.Swing)]
    [InlineData(GormAttack.Swing, GormAttack.Hammer)]
    public void The_shelter_kept_at_the_break_ends_with_that_attack_the_next_one_hits_who_stays_put(GormAttack breaking, GormAttack next)
    {
        var duel = new Duel(Between);
        var health = duel.Player.Health;

        BreakWith(duel, Between, breaking);
        duel.Run(5 * Second, () => duel.Gorm.State is GormState.Recovering or GormState.Stuck);
        Assert.Equal(health, duel.Player.Health);

        // He stays where he stood - in the shadow of the broken column, on the same line to Gorm - for the next attack.
        var events = duel.Through(next);

        AssertGormStoodStill(duel);
        Assert.True(Behind(Between, duel.Gorm.Position, duel.Player.Position), "The player left the shadow - the test would prove nothing.");
        Assert.Contains(events, e => e is PlayerHit);
        Assert.True(duel.Player.Health < health, $"The {next} after the break did not hit.");
    }

    [Fact]
    public void Provisional_a_column_behind_one_the_shock_wave_just_broke_takes_a_blow_from_the_same_wave()
    {
        // PROVISIONAL (issue #602): the human has not judged this yet. The cover of a column ends with its break, so the
        // column behind it is no longer covered and the same wave strikes it. The player behind both stays covered.
        // Gorm - Between - Behind - the player, all in a line.
        var behind = Column.AtCell(7, 4);
        var duel = new Duel(Between, behind);
        var health = duel.Player.Health;

        BreakWithTheHammer(duel, Between);
        Assert.DoesNotContain(duel.Events, e => e is ColumnHit hit && hit.Column == behind);
        var afterBreak = duel.Events.Count;
        duel.Run(5 * Second, () => duel.Gorm.State is GormState.Recovering or GormState.Stuck);

        Assert.Equal([new ColumnHit(behind, 1, HitsToBreak)], duel.Events.Skip(afterBreak).OfType<ColumnHit>());
        Assert.Equal(health, duel.Player.Health);
    }

    // ---- In the world: the broken column leaves the level ------------------------------------------------------------

    // As in ColumnEnemyTests: room 0 (start) in row 0, door in row 1, the boss room (5 wide) in rows 2-11, Gorm ('T') in row 10.
    internal const string GormMap = "##.P.##\n###.###\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#..T..#\n#.....#";

    [Fact]
    public void In_the_world_a_broken_column_no_longer_blocks_the_way_nor_is_built_and_a_restart_brings_it_back()
    {
        var column = Column.AtCell(3, 6);
        var world = GormWorld(LevelParser.Parse(GormMap).WithColumns([column]));
        Enter(world);
        var across = (From: new Vector3(column.X, 1f, column.Z - 3f), To: new Vector3(column.X, 1f, column.Z + 3f));
        Assert.False(LineOfSight.IsClear(world.Level, across.From, across.To));
        var builtBefore = world.LevelAsBuilt;

        var broke = false;
        var hits = 0;
        for (var tick = 0; tick < 180 * Second && !broke; tick++)
        {
            world.Tick(PlayerInput.None);
            hits += world.Events.OfType<ColumnHit>().Count();
            broke = world.Events.Any(e => e is ColumnBroke);
        }

        Assert.True(broke, $"The column never broke ({hits} blows).");
        Assert.Equal(HitsToBreak, hits);
        Assert.Empty(world.Level.Columns);
        Assert.Empty(world.EnemyLevel.Columns);
        Assert.Empty(world.LevelAsBuilt.Columns);
        Assert.NotSame(builtBefore, world.LevelAsBuilt);
        Assert.Null(world.Level.ColumnAt(new Vector3(column.X, 1f, column.Z)));
        Assert.True(LineOfSight.IsClear(world.Level, across.From, across.To));
        Assert.Equal([column], world.Restarted().Level.Columns);
    }

    internal static GameWorld GormWorld(Level level)
    {
        var rows = Enumerable.Range(2, 10).SelectMany(z => Enumerable.Range(1, 5).Select(x => (x, z))).ToList();
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 7, 1, 0, [(2, 0), (3, 0), (4, 0)], [], []),
            new(1, RoomKind.Boss, "hall", 0, 2, 7, 10, 1, rows, [], []),
        ];
        LevelDoor[] doors = [new(3, 1, 0, 1, RoomSide.South, DoorKind.Boss)];
        return new GameWorld(level, 1, Tough, doors, rooms);
    }

    internal static void Enter(GameWorld world)
    {
        world.Tick(PlayerInput.None with { Interact = true });
        for (var tick = 0; tick < 3 * Second && !world.Rooms[1].IsSealed; tick++)
        {
            world.Tick(PlayerInput.None with { Move = new Vector2(0f, 1f) });
        }

        Assert.True(world.Rooms[1].IsSealed, "The boss room never sealed - the test would prove nothing.");
    }
}
