using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// The portcullis in the doors of an arena (issue #558): it falls and rises on the door's own clock. It shuts the door
/// in the tick the arena seals (decision of the human: nobody escapes), frees it once it has risen above
/// <see cref="GratesBalance.BlockHeight"/>, pushes an enemy in the doorway into the arena and reports each start of a
/// motion. Every test runs the world tick by tick with the shipped values.
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
    public void A_grate_shuts_its_door_in_the_tick_the_room_seals_while_it_still_falls()
    {
        var world = World();
        Enter(world);
        var door = world.Doors[DoorA];
        var outside = Middle(door) with { Z = door.BoxMin.Z - 1f };

        // The doors move after the players: the tick the room sealed is already tick 1 of the fall. The grate still hangs
        // high up - but the door is shut for walking, sight and shots alike (its cell is wall in the level).
        Assert.Equal(GratePose.Height(1, GrateMotion.Fall, Grates), door.GrateHeight);
        Assert.False(Below(door.GrateHeight));
        Assert.True(door.IsBarred);
        Assert.True(world.Level[door.X, door.Z].IsSolid);
        Assert.False(LineOfSight.IsClear(world.Level, world.Player.Position + new Vector3(0f, 1.6f, 0f), outside));

        // All the way down it follows the shape of the fall - and blocks in every tick of it.
        for (var tick = 2; tick <= Grates.FallTicks; tick++)
        {
            world.Tick(PlayerInput.None);
            Assert.Equal(GratePose.Height(tick, GrateMotion.Fall, Grates), door.GrateHeight);
            Assert.True(door.IsBarred, $"tick {tick}");
            Assert.True(world.Level[door.X, door.Z].IsSolid, $"tick {tick}");
        }

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

    // Turns the player round by 180 degrees - to start in the corridor facing door B.
    private static void TurnRound(GameWorld world) =>
        world.Player.Look(PlayerInput.None with { Look = new Vector2(MathF.PI / ControlSettings.MouseSensitivity, 0f) });

    private static bool Touches(Door door, Vector3 at) =>
        Math.Clamp(at.X, door.BoxMin.X, door.BoxMax.X) is var x && Math.Clamp(at.Z, door.BoxMin.Z, door.BoxMax.Z) is var z
        && ((at.X - x) * (at.X - x)) + ((at.Z - z) * (at.Z - z)) < BodySizes.Player.Radius * BodySizes.Player.Radius;

    private static bool InCells(RoomState room, Vector3 at) => room.Cells.Contains(Level.ToGrid(at.X, at.Z));

    private static PlayerInput Attempt(string escape) => escape switch
    {
        "dash" => Back with { Dash = true },
        "jump" => Back with { Jump = true },
        _ => Back with { Dash = true, Jump = true },
    };

    // Decision of the human on issue #558: nobody escapes an arena. The review of PR #560 found the old way round
    // (a quick player got out while the grate fell, the arena opened again, its wave was already out and the room could
    // never be cleared). Here the player turns straight back with a dash, a jump or both, again and again - through
    // the door they came in by, from either side of the arena.
    [Theory]
    [InlineData(false, "dash")]
    [InlineData(false, "jump")]
    [InlineData(false, "dash and jump")]
    [InlineData(true, "dash")]
    [InlineData(true, "jump")]
    [InlineData(true, "dash and jump")]
    public void A_player_who_tries_to_flee_stays_in_the_sealed_arena_until_it_is_cleared(bool fromTheCorridor, string escape)
    {
        var world = World(map: fromTheCorridor ? FromTheCorridor : Map);
        if (fromTheCorridor)
        {
            TurnRound(world);
        }

        var room = world.Rooms[1];
        var door = world.Doors[fromTheCorridor ? DoorB : DoorA];
        var events = Enter(world);
        Assert.True(door.IsOpen);

        // Sealed only with the whole body out of every doorway - nobody is caught in a door that shuts.
        Assert.All(room.DoorIds, id => Assert.False(Touches(world.Doors[id], world.Player.Position)));

        for (var tick = 0; tick < 3 * Grates.FallTicks; tick++)
        {
            world.Tick(tick % 20 == 0 ? Attempt(escape) : Back);
            events.AddRange(world.Events);
            Assert.True(InCells(room, world.Player.Position), $"tick {tick}: the player is out at {world.Player.Position}");
            Assert.True(room.IsSealed, $"tick {tick}");
            Assert.True(door.IsBarred, $"tick {tick}");
        }

        // The wave is out - and stays in with the player.
        Assert.True(room.WavesReleased >= 1);
        Assert.Single(events, e => e is RoomSealed);
        Assert.DoesNotContain(events, e => e == new GrateStarted(door.Id, false));

        // Cleared, the grates rise and the way out is free again.
        KillEverything(world);
        Run(world, PlayerInput.None, Grates.LiftTicks + 1);
        Assert.True(room.IsCleared);
        Assert.All(room.DoorIds, id => Assert.False(world.Doors[id].IsBarred));
        Run(world, Back, 180, () => !InCells(room, world.Player.Position) && !Touches(door, world.Player.Position));
        Assert.False(InCells(room, world.Player.Position));
    }

    // Two combat rooms of one row each that share a door (tester finding on PR #560): room 0 (start) in row 0, door 0 in
    // row 1, room 1 (arena, one ghoul) in row 2, the shared door 1 in row 3, room 2 (arena, one ghoul) in row 4, door 2
    // in row 5, room 3 (corridor) in rows 6-7. The player starts in row 0 or, mirrored, in row 6.
    private const int SharedDoor = 1;

    private static GameWorld TwoArenas(bool fromTheCorridor)
    {
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 5, 1, 0, Rows(0, 0), [], []),
            new(1, RoomKind.Combat, "arena", 0, 2, 5, 1, 1, Rows(2, 2), [(1, 2)], []),
            new(2, RoomKind.Combat, "arena", 0, 4, 5, 1, 2, Rows(4, 4), [(3, 4)], []),
            new(3, RoomKind.Corridor, "corridor", 0, 6, 5, 2, 3, Rows(6, 7), [], []),
        ];
        LevelDoor[] doors =
        [
            new(2, 1, 0, 1, RoomSide.South) { HasGrate = true },
            new(2, 3, 1, 2, RoomSide.South) { HasGrate = true },
            new(2, 5, 2, 3, RoomSide.South) { HasGrate = true },
        ];
        var map = fromTheCorridor
            ? "#...#\n##.##\n#G..#\n##.##\n#..G#\n##.##\n#.P.#\n#...#"
            : "#.P.#\n##.##\n#G..#\n##.##\n#..G#\n##.##\n#...#\n#...#";
        var world = new GameWorld(LevelParser.Parse(map), 1, Balance.Default, doors, rooms);
        if (fromTheCorridor)
        {
            TurnRound(world);
        }

        return world;
    }

    [Theory]
    [InlineData(false, 1, 2)]
    [InlineData(true, 2, 1)]
    public void A_door_two_arenas_share_shuts_with_the_first_so_the_second_waits_until_the_first_is_cleared(
        bool fromTheCorridor, int first, int second)
    {
        var world = TwoArenas(fromTheCorridor);
        var shared = world.Doors[SharedDoor];

        // Opens the first door, steps onto the first arena's floor while still in its doorway (not entered yet) and
        // opens the shared door from there - then dashes for it.
        var sign = first < second ? 1f : -1f;
        var near = sign > 0 ? shared.BoxMin.Z : shared.BoxMax.Z;
        Run(world, PressF, 1);
        var creep = new PlayerInput(new Vector2(0f, 0.3f), Vector2.Zero, false);
        Run(world, creep, 300, () => sign * (near - world.Player.Position.Z) < ControlSettings.InteractionReach - 0.1f);
        Run(world, PlayerInput.None, 30);
        Assert.False(world.Rooms[first].IsEntered);
        Run(world, PressF, 1);
        Assert.True(shared.IsOpen);
        var events = Run(world, Forward with { Dash = true }, 1);
        events.AddRange(Run(world, Forward, 2 * Grates.FallTicks));

        // The first arena sealed - and the shared door, one of its doors, shut with it: the player is still in the first.
        Assert.True(world.Rooms[first].IsSealed);
        Assert.False(world.Rooms[second].IsEntered);
        Assert.True(shared.IsBarred);
        Assert.True(InCells(world.Rooms[first], world.Player.Position), $"the player is at {world.Player.Position}");
        Assert.Single(events, e => e == new GrateStarted(SharedDoor, true));

        // Cleared (its own ghoul only), all its grates rise - the shared one, too.
        foreach (var ghoul in world.Targets.OfType<Ghoul>().Where(ghoul => InCells(world.Rooms[first], ghoul.Position)).ToList())
        {
            ghoul.TakeDamage(10_000, []);
        }

        var rising = Run(world, PlayerInput.None, Grates.LiftTicks + 1);
        Assert.True(world.Rooms[first].IsCleared);
        Assert.False(shared.IsBarred);
        Assert.Single(rising, e => e == new GrateStarted(SharedDoor, false));

        // On into the second: it seals as any arena does, the shared grate falls again.
        var again = Run(world, Forward, 300, () => world.Rooms[second].IsSealed);
        Assert.True(world.Rooms[second].IsSealed);
        Assert.True(shared.IsBarred);
        Assert.Single(again, e => e == new GrateStarted(SharedDoor, true));
    }

    // A bigger start room (rows 0-2) with an awake enemy two rows behind the player; door A in row 3, the arena in rows
    // 4-7, door B in row 8, the corridor in rows 9-10.
    private static GameWorld WithAFollower(char enemy)
    {
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
        var map = $"#.{enemy}.#\n#...#\n#.P.#\n##.##\n#...#\n#...#\n#.G.#\n#...#\n##.##\n#...#\n#...#";
        return new GameWorld(LevelParser.Parse(map), 1, Balance.Default, doors, rooms);
    }

    // The player waits in the doorway (not in the arena yet) until the follower is in it, too, then steps in. Returns
    // where the follower was at the end of the tick before the room sealed.
    private static Vector3 StepInWith(GameWorld world, Func<Vector3> follower, float radius)
    {
        var door = world.Doors[DoorA];
        bool InTheDoorway() => Math.Abs(follower().Z - Middle(door).Z) < (Level.CellSize / 2f) + radius;

        Run(world, PressF, 1);
        Run(world, Forward, 120, () => world.Player.Position.Z > door.BoxMax.Z - 0.3f);
        Assert.False(world.Rooms[1].IsEntered);
        Run(world, PlayerInput.None, 600, InTheDoorway);
        Assert.True(InTheDoorway(), $"the follower never came into the doorway: {follower()}");

        var before = follower();
        for (var tick = 0; tick < 60 && !world.Rooms[1].IsSealed; tick++)
        {
            before = follower();
            world.Tick(Forward);
        }

        Assert.True(world.Rooms[1].IsSealed);
        return before;
    }

    [Fact]
    public void A_ghoul_that_follows_a_player_into_the_doorway_is_pushed_into_the_arena_unhurt_as_the_room_seals()
    {
        var world = WithAFollower('G');
        var follower = world.Targets.OfType<Ghoul>().Single(g => g.Position.Z < Level.CellSize * 3);
        var door = world.Doors[DoorA];

        var before = StepInWith(world, () => follower.Position, follower.Body.Radius);

        // Reaching into the door cell at the end of the tick before, right behind the door on the arena's side in the sealing
        // tick - whichever side it came from, so no enemy is ever shut out of its arena.
        Assert.True(before.Z + follower.Body.Radius > door.BoxMin.Z && before.Z - follower.Body.Radius < door.BoxMax.Z, $"z = {before.Z}");
        Assert.InRange(follower.Position.Z, door.BoxMax.Z + follower.Body.Radius, door.BoxMax.Z + follower.Body.Radius + 0.02f);
        Assert.True(InCells(world.Rooms[1], follower.Position));
        Assert.Equal(Balance.Default.Ghoul.MaxHealth, follower.Health);
        Assert.True(door.IsBarred);
    }

    [Fact]
    public void A_bat_that_follows_a_player_into_the_doorway_is_pushed_into_the_arena_unhurt_as_the_room_seals()
    {
        var world = WithAFollower('B');
        var follower = world.Targets.OfType<Bat>().Single();
        var door = world.Doors[DoorA];

        var before = StepInWith(world, () => follower.Position, follower.Body.Radius);

        Assert.True(before.Z + follower.Body.Radius > door.BoxMin.Z && before.Z - follower.Body.Radius < door.BoxMax.Z, $"z = {before.Z}");
        // It only reached into the doorway from the start room - and is pushed through into the arena all the same,
        // out of the door cell on the arena's side (it flies on a little in the same tick).
        Assert.InRange(follower.Position.Z, door.BoxMax.Z + follower.Body.Radius, door.BoxMax.Z + follower.Body.Radius + 0.1f);
        Assert.True(InCells(world.Rooms[1], follower.Position));
        Assert.Equal(Balance.Default.Bat.MaxHealth, follower.Health);
        Assert.True(door.IsBarred);
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
    public void A_closed_door_is_barred_and_not_offered_from_the_tick_its_room_seals()
    {
        // Door B stays closed: the player in the arena looks at it while the grate is still high.
        var world = World();
        Enter(world);
        var door = world.Doors[DoorB];

        Assert.False(Below(door.GrateHeight));
        Assert.True(door.IsBarred);
        Assert.False(door.CanInteract);
    }
}