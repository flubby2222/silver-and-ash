using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// The portcullis in the doors of an arena (issue #558): it falls and rises on the door's own clock, blocks only while
/// it hangs below <see cref="GratesBalance.BlockHeight"/>, pushes whoever stands in the doorway into the arena and
/// reports each start of a motion. Every test runs the world tick by tick with the shipped values.
/// </summary>
public class GrateTests
{
    // The map of the arena tests: room 0 (start) in row 0, door A in row 1, room 1 (combat, one ghoul) in rows 2-5,
    // door B in row 6, room 2 (corridor) in rows 7-8. Both doors have a grate here.
    private const string Map = "#.P.#\n##.##\n#...#\n#...#\n#.G.#\n#...#\n##.##\n#...#\n#...#";

    // The same, but the player starts in the corridor (row 7), right behind door B.
    private const string FromTheCorridor = "#...#\n##.##\n#...#\n#...#\n#.G.#\n#...#\n##.##\n#.P.#\n#...#";

    private const int DoorA = 0;
    private const int DoorB = 1;

    private static readonly GratesBalance Grates = Balance.Default.Grates;
    private static readonly PlayerInput Forward = new(new Vector2(0f, 1f), Vector2.Zero, false);
    private static readonly PlayerInput Back = new(new Vector2(0f, -1f), Vector2.Zero, false);
    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };

    private static List<(int X, int Z)> Rows(int from, int to) =>
        Enumerable.Range(from, to - from + 1).SelectMany(z => new[] { (1, z), (2, z), (3, z) }).ToList();

    private static GameWorld World(bool grates = true, string map = Map, RoomKind arena = RoomKind.Combat)
    {
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 5, 1, 0, Rows(0, 0), [], []),
            new(1, arena, "arena", 0, 2, 5, 4, 1, Rows(2, 5), [(2, 4)], []),
            new(2, RoomKind.Corridor, "corridor", 0, 7, 5, 2, 2, Rows(7, 8), [], []),
        ];
        LevelDoor[] doors =
        [
            new(2, 1, 0, 1, RoomSide.South) { HasGrate = grates },
            new(2, 6, 1, 2, RoomSide.South) { HasGrate = grates },
        ];
        return new GameWorld(LevelParser.Parse(map), 1, Balance.Default, doors, rooms);
    }

    private static List<GameEvent> Run(GameWorld world, PlayerInput input, int ticks, Func<bool>? until = null)
    {
        var events = new List<GameEvent>();
        for (var i = 0; i < ticks && until?.Invoke() != true; i++)
        {
            world.Tick(input);
            events.AddRange(world.Events);
        }

        return events;
    }

    // Opens door A and walks in until the room seals (at most 3 s).
    private static List<GameEvent> Enter(GameWorld world)
    {
        var events = Run(world, PressF, 1);
        events.AddRange(Run(world, Forward, 180, () => world.Rooms[1].IsSealed));
        Assert.True(world.Rooms[1].IsSealed);
        return events;
    }

    private static bool Below(float height) => height < Grates.BlockHeight;

    private static void KillEverything(GameWorld world)
    {
        foreach (var target in world.Targets.ToList())
        {
            target.TakeDamage(10_000, []);
        }
    }

    private static Vector3 Middle(Door door) => (door.BoxMin + door.BoxMax) / 2f;

    [Fact]
    public void A_falling_grate_blocks_from_the_tick_it_hangs_below_the_block_height_on()
    {
        var world = World();
        Enter(world);
        var door = world.Doors[DoorA];

        // The doors move after the players: the tick the room sealed is already tick 1 of the fall. High up, the open
        // door still lets through.
        Assert.Equal(GratePose.Height(1, GrateMotion.Fall, Grates), door.GrateHeight);
        Assert.False(door.IsBarred);
        Assert.False(world.Level[door.X, door.Z].IsSolid);

        var firstBlocking = -1;
        for (var tick = 2; tick <= Grates.FallTicks; tick++)
        {
            world.Tick(PlayerInput.None);
            var height = GratePose.Height(tick, GrateMotion.Fall, Grates);
            Assert.Equal(height, door.GrateHeight);
            Assert.Equal(Below(height), door.IsBarred);
            Assert.Equal(Below(height), world.Level[door.X, door.Z].IsSolid);
            if (firstBlocking < 0 && Below(height))
            {
                firstBlocking = tick;
            }
        }

        // It blocks part way down - after the start, before the grate stands on the floor - and stays down.
        Assert.InRange(firstBlocking, 2, GratePose.ShutTick(Grates));
        Assert.Equal(0f, door.GrateHeight);
        Run(world, PlayerInput.None, 2 * Grates.LiftTicks);
        Assert.True(door.IsBarred);
        Assert.True(world.Level[door.X, door.Z].IsSolid);
    }

    [Fact]
    public void A_rising_grate_blocks_until_it_hangs_above_the_block_height_again()
    {
        var world = World();
        Enter(world);
        Run(world, PlayerInput.None, Grates.FallTicks);
        var door = world.Doors[DoorA];

        KillEverything(world);
        world.Tick(PlayerInput.None);
        Assert.True(world.Rooms[1].IsCleared);

        // The room is cleared after the doors moved: that tick is tick 0 of the lift, on the floor.
        Assert.Equal(0f, door.GrateHeight);
        var lastBlocking = 0;
        for (var tick = 1; tick <= Grates.LiftTicks; tick++)
        {
            world.Tick(PlayerInput.None);
            var height = GratePose.Height(tick, GrateMotion.Lift, Grates);
            Assert.Equal(height, door.GrateHeight);
            Assert.Equal(Below(height), door.IsBarred);
            Assert.Equal(Below(height), world.Level[door.X, door.Z].IsSolid);
            if (Below(height))
            {
                lastBlocking = tick;
            }
        }

        Assert.InRange(lastBlocking, 1, Grates.LiftTicks - 1);
        Assert.Equal(GratePose.OpenHeight, door.GrateHeight);
        Assert.False(door.IsBarred);
    }

    [Fact]
    public void A_door_without_a_grate_is_barred_at_once_as_before()
    {
        var world = World(grates: false);

        var events = Enter(world);

        Assert.True(world.Doors[DoorA].IsBarred);
        Assert.True(world.Level[2, 1].IsSolid);
        Assert.DoesNotContain(events, e => e is GrateStarted);
    }

    [Fact]
    public void The_grate_counts_its_own_ticks_not_the_clock_of_the_world()
    {
        // The same arena entered after 5 s of waiting: the fall starts the same - at the tick of sealing.
        var early = World();
        var late = World();
        Run(late, PlayerInput.None, 300);
        Enter(early);
        Enter(late);

        for (var tick = 0; tick < Grates.FallTicks; tick++)
        {
            Assert.Equal(early.Doors[DoorA].GrateHeight, late.Doors[DoorA].GrateHeight);
            early.Tick(PlayerInput.None);
            late.Tick(PlayerInput.None);
        }

        Assert.NotEqual(early.Ticks, late.Ticks);
    }

    [Fact]
    public void Each_grate_reports_the_start_of_its_fall_and_of_its_rise_once()
    {
        var world = World();

        var sealing = Enter(world);
        var falling = Run(world, PlayerInput.None, Grates.FallTicks);
        KillEverything(world);
        var rising = Run(world, PlayerInput.None, Grates.LiftTicks + 1);

        Assert.Equal([new GrateStarted(DoorA, true), new GrateStarted(DoorB, true)], sealing.OfType<GrateStarted>());
        Assert.DoesNotContain(falling, e => e is GrateStarted);
        Assert.Equal([new GrateStarted(DoorA, false), new GrateStarted(DoorB, false)], rising.OfType<GrateStarted>());

        // Each where its door is - and in the tick the room sealed or was cleared.
        Assert.All(sealing.Concat(rising).OfType<GrateStarted>(), e => Assert.Equal(Middle(world.Doors[e.DoorId]), e.Place));
        Assert.Equal(sealing.FindIndex(e => e is RoomSealed) - 2, sealing.FindIndex(e => e is GrateStarted));
        Assert.Contains(rising, e => e is RoomCleared);
    }

    [Fact]
    public void A_player_in_the_doorway_is_pushed_into_the_arena_unharmed()
    {
        var world = World();
        Enter(world);
        var door = world.Doors[DoorA];
        var health = world.Player.Health;

        // Back into the door cell while the grate is still high (the cell reaches from z = 2 m to 4 m).
        Run(world, Back, 60, () => world.Player.Position.Z < door.BoxMax.Z - 0.5f);
        Assert.False(door.IsBarred);
        Run(world, PlayerInput.None, Grates.FallTicks, () => door.IsBarred);
        Assert.True(door.IsBarred);

        // Out of the door cell on the arena's side, by its body's radius - in the tick the grate started to block.
        var position = world.Player.Position;
        Assert.True(position.Z - BodySizes.Player.Radius > door.BoxMax.Z, $"z = {position.Z}");
        Assert.InRange(position.Z, door.BoxMax.Z + BodySizes.Player.Radius, door.BoxMax.Z + BodySizes.Player.Radius + 0.1f);
        Assert.Contains(Level.ToGrid(position.X, position.Z), world.Rooms[1].Cells);
        Assert.Equal(health, world.Player.Health);
        Assert.True(world.Rooms[1].IsSealed);

        // Walking back against the grate gets nowhere.
        Run(world, Back, 60);
        Assert.True(world.Player.Position.Z - BodySizes.Player.Radius >= door.BoxMax.Z - 0.01f);
    }

    [Fact]
    public void A_player_in_the_far_doorway_is_pushed_back_into_the_arena_too()
    {
        // Door B: the arena is room A of the door, on its low side (smaller z). The player starts in the corridor, turns
        // round and comes in through it.
        var world = World(map: FromTheCorridor);
        world.Player.Look(PlayerInput.None with { Look = new Vector2(MathF.PI / ControlSettings.MouseSensitivity, 0f) });
        Run(world, PressF, 1);
        Assert.True(world.Doors[DoorB].IsOpen);
        Run(world, Forward, 300, () => world.Rooms[1].IsSealed);
        Assert.True(world.Rooms[1].IsSealed);
        var door = world.Doors[DoorB];

        Run(world, Back, 120, () => world.Player.Position.Z > door.BoxMin.Z + 0.5f);
        Assert.False(door.IsBarred);
        Run(world, PlayerInput.None, Grates.FallTicks, () => door.IsBarred);

        Assert.True(world.Player.Position.Z + BodySizes.Player.Radius < door.BoxMin.Z, $"z = {world.Player.Position.Z}");
        Assert.Contains(Level.ToGrid(world.Player.Position.X, world.Player.Position.Z), world.Rooms[1].Cells);
    }

    [Fact]
    public void A_ghoul_that_follows_a_player_into_the_doorway_is_pushed_into_the_arena_unhurt()
    {
        // A bigger start room (rows 0-2) with an awake ghoul right behind the player; door A in row 3, the arena in rows
        // 4-7, door B in row 8, the corridor in rows 9-10.
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 5, 3, 0, Rows(0, 2), [], []),
            new(1, RoomKind.Combat, "arena", 0, 4, 5, 4, 1, Rows(4, 7), [(2, 6)], []),
            new(2, RoomKind.Corridor, "corridor", 0, 9, 5, 2, 2, Rows(9, 10), [], []),
        ];
        LevelDoor[] doors =
        [
            new(2, 3, 0, 1, RoomSide.South) { HasGrate = true },
            new(2, 8, 1, 2, RoomSide.South) { HasGrate = true },
        ];
        var map = "#.G.#\n#...#\n#.P.#\n##.##\n#...#\n#...#\n#.G.#\n#...#\n##.##\n#...#\n#...#";
        var world = new GameWorld(LevelParser.Parse(map), 1, Balance.Default, doors, rooms);
        var follower = world.Targets.OfType<Ghoul>().Single(g => g.Position.Z < 2f * 3);
        var door = world.Doors[DoorA];

        // The player stops just inside; the ghoul crawls after it into the doorway until the grate blocks.
        Enter(world);
        var wasInTheDoorway = false;
        for (var tick = 0; tick < Grates.FallTicks && !door.IsBarred; tick++)
        {
            wasInTheDoorway = follower.Position.Z > door.BoxMin.Z && follower.Position.Z < door.BoxMax.Z;
            world.Tick(PlayerInput.None);
        }

        Assert.True(door.IsBarred);
        Assert.True(wasInTheDoorway);
        Assert.True(follower.Position.Z >= door.BoxMax.Z + follower.Body.Radius);
        Assert.Equal(Balance.Default.Ghoul.MaxHealth, follower.Health);
    }
    [Fact]
    public void A_player_who_got_out_before_the_grate_shut_finds_the_arena_open_again()
    {
        var world = World();
        Enter(world);
        var door = world.Doors[DoorA];

        // Straight back out with a dodge, fast enough to be past the grate before it blocks.
        var events = Run(world, Back with { Dash = true }, 1);
        events.AddRange(Run(world, Back, Grates.FallTicks, () => !world.Rooms[1].IsSealed));

        Assert.False(world.Rooms[1].IsSealed);
        Assert.False(world.Rooms[1].IsEntered);
        Assert.False(world.Rooms[1].IsCleared);
        Assert.Contains(new GrateStarted(DoorA, false), events);
        Assert.Contains(new GrateStarted(DoorB, false), events);

        // The grates rise again; the room counts as not entered, so stepping back in counts again. With its ghoul dead
        // (it came out and would follow the player) that clears it, with no new fall.
        Run(world, PlayerInput.None, Grates.LiftTicks);
        Assert.False(door.IsBarred);
        Assert.False(world.Level[door.X, door.Z].IsSolid);
        KillEverything(world);
        var again = Run(world, Forward, 300, () => world.Rooms[1].IsEntered);
        Assert.True(world.Rooms[1].IsCleared);
        Assert.Contains(new RoomCleared(1), again);
        Assert.DoesNotContain(again, e => e is GrateStarted);
    }
    [Fact]
    public void A_player_who_dies_in_the_sealed_arena_leaves_it_sealed_and_the_restart_brings_the_grates_up()
    {
        var world = World();
        Enter(world);
        Run(world, PlayerInput.None, Grates.FallTicks);

        world.Player.TakeDamage(world.Player.MaxHealth, []);
        var events = Run(world, PlayerInput.None, SimulationClock.SecondsToTicks(Balance.Default.RestartDelay) + 1);

        Assert.True(world.ShouldRestart);
        Assert.True(world.Rooms[1].IsSealed);
        Assert.True(world.Doors[DoorA].IsBarred);
        Assert.DoesNotContain(events, e => e is GrateStarted);

        var fresh = world.Restarted();
        Assert.All(fresh.Doors, door =>
        {
            Assert.True(door.HasGrate);
            Assert.False(door.IsBarred);
            Assert.Equal(GratePose.OpenHeight, door.GrateHeight);
        });
        Assert.False(fresh.Rooms[1].IsSealed);

        // ...and the arena of the new run falls shut the same way.
        Enter(fresh);
        Run(fresh, PlayerInput.None, Grates.FallTicks);
        Assert.True(fresh.Doors[DoorA].IsBarred);
    }

    [Fact]
    public void A_grate_turned_round_on_its_way_moves_on_from_where_it_hangs()
    {
        var world = World();
        Enter(world);
        var door = world.Doors[DoorA];
        Run(world, PlayerInput.None, GratePose.ShutTick(Grates) / 2);
        var before = door.GrateHeight;
        Assert.InRange(before, 0.01f, GratePose.OpenHeight - 0.01f);

        // Cleared while the grate is still falling: it rises from its height, it does not jump to the floor.
        KillEverything(world);
        world.Tick(PlayerInput.None);
        Assert.True(world.Rooms[1].IsCleared);
        Assert.InRange(door.GrateHeight, before - 0.2f, before + 0.2f);

        var last = door.GrateHeight;
        for (var tick = 0; tick < Grates.LiftTicks; tick++)
        {
            world.Tick(PlayerInput.None);
            Assert.True(door.GrateHeight >= last, $"tick {tick}: {door.GrateHeight} < {last}");
            last = door.GrateHeight;
        }

        Assert.Equal(GratePose.OpenHeight, door.GrateHeight);
    }

    [Fact]
    public void A_closed_door_is_not_offered_while_its_grate_falls()
    {
        // Door B stays closed: the player in the arena looks at it while the grate is still high.
        var world = World();
        Enter(world);
        var door = world.Doors[DoorB];

        Assert.False(door.IsBarred);
        Assert.False(door.CanInteract);
    }
}