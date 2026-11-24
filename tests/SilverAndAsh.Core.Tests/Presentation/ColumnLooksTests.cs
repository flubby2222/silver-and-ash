using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Issue #601: the stage of every column learnt from Core's events, tick by tick, as the game does it
/// (Program.ReadEvents): a blow picks Z1 or Z2, the break starts the fall, 81 ticks later the column lies broken, and a
/// restart shows every column intact again.
/// </summary>
public class ColumnLooksTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly int HitsToBreak = Balance.Default.Columns.HitsToBreak;

    [Fact]
    public void In_the_world_every_blow_shows_its_stage_the_break_starts_the_fall_and_81_ticks_later_the_column_lies()
    {
        var column = Column.AtCell(3, 6);
        var world = GormHallWorld(LevelParser.Parse(GormHallMap).WithColumns([column]), balance: Tough);
        var looks = new ColumnLooks();
        Enter(world, looks);
        Assert.Equal(ColumnLook.Intact, looks.Of(column, world.Ticks));

        var hits = 0;
        long? brokeAt = null;
        Vector3? firstStriker = null;
        for (var tick = 0; tick < 180 * Second && brokeAt is null; tick++)
        {
            world.Tick(PlayerInput.None);
            Read(world, looks);
            foreach (var hit in world.Events.OfType<ColumnHit>())
            {
                hits = hit.Hits;
                firstStriker ??= world.Gorm!.Position;
            }

            if (world.Events.Any(e => e is ColumnBroke))
            {
                brokeAt = world.Ticks;
            }
            else
            {
                // Between the blows the stage is that of the blows taken so far.
                var expected = hits == 0 ? ColumnStage.Intact : hits >= HitsToBreak - 1 ?ColumnStage.Hit2 : ColumnStage.Hit1;
                Assert.Equal(expected, looks.Of(column, world.Ticks).Stage);
                Assert.Null(looks.Of(column, world.Ticks).TicksSinceBreak);
            }
        }

        Assert.True(brokeAt is not null, $"The column never broke ({hits} blows).");
        Assert.Equal(HitsToBreak, hits);
        Assert.Equal([column], looks.Broken);
        Assert.Empty(world.LevelAsBuilt.Columns);

        // The side of the blows looks at Gorm where he struck the first one.
        var yaw = looks.Of(column, world.Ticks).Yaw;
        Assert.Equal(ColumnBreakLook.YawToward(column.Centre, firstStriker!.Value), yaw);

        // The fall, tick by tick in the running world, until the broken model lies.
        while (world.Ticks - brokeAt < ColumnBreakLook.SwapTick + 5)
        {
            var since = (int)(world.Ticks - brokeAt.Value);
            var look = looks.Of(column, world.Ticks);
            Assert.Equal(since < ColumnBreakLook.SwapTick ? ColumnStage.Falling : ColumnStage.Broken, look.Stage);
            Assert.Equal(since, look.TicksSinceBreak);
            Assert.Equal(yaw, look.Yaw);
            world.Tick(PlayerInput.None);
            Read(world, looks);
        }

        // A restart builds the level anew, and with it a new ColumnLooks (as Program does): the column stands intact.
        var restarted = world.Restarted();
        var fresh = new ColumnLooks();
        Assert.Equal([column], restarted.LevelAsBuilt.Columns);
        Assert.Equal(ColumnLook.Intact, fresh.Of(column, restarted.Ticks));
        Assert.Empty(fresh.Broken);

        // The old one would still show it broken - Program must make a new one on every restart (RestartViewsTests).
        Assert.NotEqual(ColumnStage.Intact, looks.Of(column, restarted.Ticks).Stage);
    }

    [Fact]
    public void Two_columns_struck_by_the_same_attacks_break_shortly_one_after_the_other_and_each_shows_its_own_stage_and_fall()
    {
        // Issue #611: two columns of the hall between Gorm and the player, at different distances from Gorm - his shock
        // wave and his chain reach the nearer one first, so its last blow and its break come a few ticks earlier.
        Column[] columns = [Column.AtCell(2, 7), Column.AtCell(4, 4)];
        var world = GormHallWorld(LevelParser.Parse(GormHallMap).WithColumns(columns), balance: Tough);
        var looks = new ColumnLooks();
        Enter(world, looks);

        var attack = 0;
        var hits = columns.ToDictionary(column => column, _ => 0);
        var lastBlowIn = new Dictionary<Column, int>();
        var firstStriker = new Dictionary<Column, Vector3>();
        var brokeAt = new Dictionary<Column, long>();
        for (var tick = 0; tick < 180 * Second && brokeAt.Count < columns.Length; tick++)
        {
            world.Tick(PlayerInput.None);
            Read(world, looks);
            foreach (var gameEvent in world.Events)
            {
                switch (gameEvent)
                {
                    case GormSlammed or GormSwingStarted:
                        attack++;
                        break;
                    case ColumnHit hit:
                        hits[hit.Column] = hit.Hits;
                        lastBlowIn[hit.Column] = attack;
                        firstStriker.TryAdd(hit.Column, world.Gorm!.Position);
                        break;
                    case ColumnBroke broke:
                        brokeAt[broke.Column] = world.Ticks;
                        break;
                }
            }

            AssertOwnLooks(columns, looks, world.Ticks, hits, brokeAt);
        }

        Assert.True(brokeAt.Count == columns.Length, $"Not both columns broke: blows {string.Join(", ", hits.Values)}.");
        Assert.Equal(lastBlowIn[columns[0]], lastBlowIn[columns[1]]);
        Assert.True(brokeAt[columns[0]] < brokeAt[columns[1]], $"The nearer column broke at {brokeAt[columns[0]]}, the farther at {brokeAt[columns[1]]}.");
        Assert.Equal(columns, looks.Broken);

        // Each turned towards where Gorm stood at its own first blow - two columns, two sides.
        foreach (var column in columns)
        {
            Assert.Equal(ColumnBreakLook.YawToward(column.Centre, firstStriker[column]), looks.Of(column, world.Ticks).Yaw);
        }

        Assert.NotEqual(looks.Of(columns[0], world.Ticks).Yaw, looks.Of(columns[1], world.Ticks).Yaw);

        // Both falls, tick by tick, until the later one lies too: each counts from its own break.
        while (world.Ticks - brokeAt[columns[1]] < ColumnBreakLook.SwapTick + 5)
        {
            world.Tick(PlayerInput.None);
            Read(world, looks);
            AssertOwnLooks(columns, looks, world.Ticks, hits, brokeAt);
        }
    }

    // Every column shows what it has taken itself: its blows before its break, its own fall after it.
    private static void AssertOwnLooks(Column[] columns, ColumnLooks looks, long now, Dictionary<Column, int> hits, Dictionary<Column, long> brokeAt)
    {
        foreach (var column in columns)
        {
            var look = looks.Of(column, now);
            if (brokeAt.TryGetValue(column, out var broke))
            {
                var since = (int)(now - broke);
                Assert.Equal(since < ColumnBreakLook.SwapTick ? ColumnStage.Falling : ColumnStage.Broken, look.Stage);
                Assert.Equal(since, look.TicksSinceBreak);
            }
            else
            {
                Assert.Equal(ColumnBreakLook.StageOf(hits[column], HitsToBreak), look.Stage);
                Assert.Null(look.TicksSinceBreak);
            }
        }
    }

    [Fact]
    public void The_side_of_the_blows_stays_where_the_first_blow_came_from()
    {
        var column = Column.AtCell(3, 6);
        var looks = new ColumnLooks();
        var first = new Vector3(column.X + 4f, 0f, column.Z);
        var second = new Vector3(column.X, 0f, column.Z - 4f);

        looks.OnEvent(new ColumnHit(column, 1, 3), 10, first);
        looks.OnEvent(new ColumnHit(column, 2, 3), 20, second);
        looks.OnEvent(new ColumnBroke(column), 30, second);

        Assert.Equal(ColumnBreakLook.YawToward(column.Centre, first), looks.Of(column, 31).Yaw);
    }

    [Fact]
    public void Without_a_striker_the_column_is_not_turned_and_other_events_change_nothing()
    {
        var column = Column.AtCell(2, 2);
        var other = Column.AtCell(5, 5);
        var looks = new ColumnLooks();

        looks.OnEvent(new ColumnHit(column, 1, 3), 5, null);

        Assert.Equal(new ColumnLook(ColumnStage.Hit1, 0f), looks.Of(column, 6));
        Assert.Equal(ColumnLook.Intact, looks.Of(other, 6));
        Assert.Empty(looks.Broken);
    }

    [Fact]
    public void A_column_broken_without_a_blow_seen_falls_too_and_the_broken_ones_come_in_the_order_they_broke()
    {
        var early = Column.AtCell(1, 1);
        var late = Column.AtCell(4, 4);
        var looks = new ColumnLooks();

        looks.OnEvent(new ColumnBroke(late), 50, null);
        looks.OnEvent(new ColumnBroke(early), 20, null);
        looks.OnEvent(new ColumnBroke(early), 40, null);

        Assert.Equal([early, late], looks.Broken);
        Assert.Equal(new ColumnLook(ColumnStage.Falling, 0f, 0), looks.Of(early, 20));
        Assert.Equal(new ColumnLook(ColumnStage.Broken, 0f, ColumnBreakLook.SwapTick), looks.Of(early, 20 + ColumnBreakLook.SwapTick));

        // Asked for a moment before the break (the world never goes back, but nothing may count below zero).
        Assert.Equal(0, looks.Of(late, 10).TicksSinceBreak);
    }

    private static void Read(GameWorld world, ColumnLooks looks)
    {
        foreach (var gameEvent in world.Events)
        {
            looks.OnEvent(gameEvent, world.Ticks, world.Gorm?.Position);
        }
    }

    private static void Enter(GameWorld world, ColumnLooks looks)
    {
        world.Tick(PlayerInput.None with { Interact = true });
        Read(world, looks);
        for (var tick = 0; tick < 3 * Second && !world.Rooms[1].IsSealed; tick++)
        {
            world.Tick(PlayerInput.None with { Move = new Vector2(0f, 1f) });
            Read(world, looks);
        }

        Assert.True(world.Rooms[1].IsSealed, "The boss room never sealed - the test would prove nothing.");
    }
}
