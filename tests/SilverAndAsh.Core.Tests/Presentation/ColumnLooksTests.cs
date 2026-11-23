using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

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

    /// <summary>A player who survives a lot - to let Gorm strike the column until it breaks.</summary>
    private static readonly Balance Tough = Balance.Default with { Player = Balance.Default.Player with { MaxHealth = 100_000 } };

    // As in GormCoverTests: room 0 (start) in row 0, door in row 1, the boss room (5 wide) in rows 2-11, Gorm ('T') in row 10.
    private const string GormMap = "##.P.##\n###.###\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#..T..#\n#.....#";

    [Fact]
    public void In_the_world_every_blow_shows_its_stage_the_break_starts_the_fall_and_81_ticks_later_the_column_lies()
    {
        var column = Column.AtCell(3, 6);
        var world = GormWorld(LevelParser.Parse(GormMap).WithColumns([column]));
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

    private static GameWorld GormWorld(Level level)
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
