using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.BotRun;

/// <summary>
/// Mode "arena": every door of every combat room (issues #558, #560). For each door the bot walks in from outside until
/// the arena seals, then once clears it and walks out the same way, and four times tries to escape at once (waiting 0,
/// 6 or 14 ticks, with and without dash). Two arenas side by side are cleared one after the other. The boss door must
/// block at once and have no grate.
/// </summary>
internal static class ArenaDoors
{
    public static void Run(LevelZero zero, int seed, Report report)
    {
        var probe = zero.NewRun(seed, Play.Balance);
        foreach (var door in probe.Doors)
        {
            var boss = probe.Rooms[door.RoomA].Kind == RoomKind.Boss || probe.Rooms[door.RoomB].Kind == RoomKind.Boss;
            report.Check(door.Kind == DoorKind.Cracked && door.HasGrate, $"seed {seed}: cracked wall {door.Id} has a grate");
            report.Check(boss && door.HasGrate, $"seed {seed}: boss door {door.Id} has a grate");
            if (boss && door.Kind != DoorKind.Cracked)
            {
                report.Try($"seed {seed} boss door {door.Id}", () => BossDoor(zero, seed, door.Id, report));
                continue;
            }

            if (Arena.Find(probe, door) is null)
            {
                continue;
            }

            report.Count($"arena doors {door.Kind}{(door.HasGrate ? "+grate" : "")}");
            report.Check(!door.HasGrate && door.Kind is DoorKind.Normal or DoorKind.Locked, $"seed {seed}: arena door {door.Id} has no grate");
            if (door.Kind == DoorKind.Cracked)
            {
                continue;
            }

            var where = $"seed {seed} door {door.Id}";
            report.Try($"{where} clear", () => Clear(zero, seed, door.Id, report));
            foreach (var (delay, dash) in new[] { (0, false), (0, true), (6, true), (14, false) })
            {
                report.Try($"{where} escape", () => Escape(zero, seed, door.Id, delay, dash, report));
            }

            if (probe.Rooms[door.RoomA].Kind == RoomKind.Combat && probe.Rooms[door.RoomB].Kind == RoomKind.Combat)
            {
                report.Try($"{where} chain", () => Chain(zero, seed, door.Id, report));
            }
        }
    }

    /// <summary>Walks into the arena behind the door until it seals; null if it never does.</summary>
    private static (Play Play, Arena Arena)? WalkIn(LevelZero zero, int seed, int doorId, string what, Report report)
    {
        var world = zero.NewRun(seed, Play.Balance);
        var arena = Arena.Find(world, world.Doors[doorId])!;
        var play = new Play(world, report, $"seed {seed} door {doorId} {what}");
        arena.Enter(play);
        if (!arena.Room.IsSealed)
        {
            report.Count($"not sealed on walking in ({what})");
            return null;
        }

        report.Count("sealed on walking in");
        return (play, arena);
    }

    private static void Clear(LevelZero zero, int seed, int doorId, Report report)
    {
        if (WalkIn(zero, seed, doorId, "clear", report) is not var (play, arena))
        {
            return;
        }

        var grates = play.World.Balance.Grates;
        var doors = arena.Room.DoorIds.Select(id => play.World.Doors[id]).ToList();
        var withGrate = doors.Where(door => door.HasGrate).ToList();
        play.Ticks(grates.FallTicks + 20, PlayerInput.None);
        foreach (var door in withGrate)
        {
            report.Check(!door.Blocks || door.GrateHeight != 0f, $"{play.Context}: grate of door {door.Id} not down (height {door.GrateHeight}, blocks {door.Blocks})");
            var falls = play.Events.OfType<GrateStarted>().Count(e => e.DoorId == door.Id && e.Falling);
            report.Check(falls != 1, $"{play.Context}: door {door.Id} fell {falls} times");
        }

        foreach (var door in doors.Where(door => !door.HasGrate && door.Kind != DoorKind.Cracked))
        {
            report.Check(!door.Blocks, $"{play.Context}: door {door.Id} without grate does not block");
        }

        var ticks = 0;
        var cleared = play.TicksUntil(1500, PlayerInput.None, () =>
        {
            // let the enemies come first, as in a real fight, then kill whatever is left in the room
            if (++ticks > 240)
            {
                Play.KillEnemies(play.World, arena.Room);
            }

            return arena.Room.IsCleared;
        });
        if (report.Check(!cleared, $"{play.Context}: room did not clear"))
        {
            return;
        }

        var heights = withGrate.ToDictionary(door => door.Id, door => door.GrateHeight);
        for (var i = 0; i < grates.LiftTicks + 10; i++)
        {
            play.Tick(PlayerInput.None);
            foreach (var door in withGrate)
            {
                report.Check(door.GrateHeight + 1e-5f < heights[door.Id], $"{play.Context}: grate {door.Id} went down while lifting");
                heights[door.Id] = door.GrateHeight;
            }
        }

        foreach (var door in doors.Where(door => door.Kind != DoorKind.Cracked))
        {
            report.Check(door.IsBarred, $"{play.Context}: door {door.Id} still barred after lifting");
            report.Check(door.HasGrate && door.GrateHeight < GratePose.OpenHeight, $"{play.Context}: grate {door.Id} not fully up (height {door.GrateHeight})");
        }

        foreach (var door in withGrate)
        {
            var lifts = play.Events.OfType<GrateStarted>().Count(e => e.DoorId == door.Id && !e.Falling);
            report.Check(lifts != 1, $"{play.Context}: door {door.Id} lifted {lifts} times");
        }

        play.Face(arena.Outside);
        var gone = play.TicksUntil(600, Play.Forward, () => Play.Cell(play.World.Player.Position) == arena.Outside);
        report.Count(gone ? "cleared: walked out" : "cleared: could NOT walk out");
        report.Check(!gone, $"{play.Context}: cannot walk out after clearing");
    }

    /// <summary>Waits <paramref name="delay"/> ticks after sealing, then turns round and runs for the door.</summary>
    private static void Escape(LevelZero zero, int seed, int doorId, int delay, bool dash, Report report)
    {
        var tag = $"escape (delay {delay}{(dash ? ", dash" : "")})";
        if (WalkIn(zero, seed, doorId, tag, report) is not var (play, arena))
        {
            return;
        }

        var room = arena.Room;
        var grates = play.World.Balance.Grates;
        play.Ticks(delay, PlayerInput.None);
        play.Face(arena.Outside);
        var outsideWhileSealed = false;
        for (var i = 0; i < 80; i++)
        {
            play.Tick(Play.Forward with { Dash = dash && i % 20 == 0, Jump = dash && i % 7 == 0 });
            var cell = Play.Cell(play.World.Player.Position);
            if (!outsideWhileSealed && room.IsSealed && !room.Cells.Contains(cell) && !play.World.Doors.Any(d => d.X == cell.X && d.Z == cell.Z))
            {
                outsideWhileSealed = true;
                report.Finding($"{play.Context}: room sealed but player outside in cell {cell} at tick {i}");
            }
        }

        play.Face(arena.Outside);
        play.Ticks(40, Play.Forward);
        play.Ticks(grates.LiftTicks + grates.FallTicks + 30, PlayerInput.None);
        var doors = room.DoorIds.Select(id => play.World.Doors[id]).ToList();
        if (room.Cells.Contains(Play.Cell(play.World.Player.Position)))
        {
            report.Count($"{tag}: caught inside");
            report.Check(!room.IsSealed, $"{play.Context}: player inside but the room is open again");
            foreach (var door in doors.Where(door => door.HasGrate && !door.Blocks))
            {
                report.Finding($"{play.Context}: player caught inside but grate of door {door.Id} does not block");
            }

            return;
        }

        report.Count($"{tag}: got out");
        if (report.Check(room.IsSealed, $"{play.Context}: player outside, yet the room is still sealed"))
        {
            return;
        }

        report.Check(doors.Any(door => door.IsBarred || (door.HasGrate && door.GrateHeight < GratePose.OpenHeight)), $"{play.Context}: room open again but a door is barred or a grate down");
        var falls = play.Events.OfType<GrateStarted>().Count(e => e.Falling && room.DoorIds.Contains(e.DoorId));
        var lifts = play.Events.OfType<GrateStarted>().Count(e => !e.Falling && room.DoorIds.Contains(e.DoorId));
        report.Check(falls != lifts, $"{play.Context}: {falls} grate falls but {lifts} lifts");

        // walk in again: the arena must seal a second time
        play.PlaceAt(arena.Outside);
        play.Face(arena.Inside);
        play.Tick(Play.Forward with { Interact = true });
        var again = play.TicksUntil(600, Play.Forward, () => room.IsSealed);
        report.Count(again ? "re-enter after escape: sealed again" : "re-enter after escape: NOT sealed");
        report.Check(!again, $"{play.Context}: walking in again does not seal the room");
    }

    /// <summary>Two arenas share the door: seal and clear A, then walk on through the lifted door into B.</summary>
    private static void Chain(LevelZero zero, int seed, int doorId, Report report)
    {
        var world = zero.NewRun(seed, Play.Balance);
        var door = world.Doors[doorId];
        var a = world.Rooms[door.RoomA];
        var b = world.Rooms[door.RoomB];
        if (Play.NeighbourIn(door, a) is not { } inA || Play.NeighbourIn(door, b) is not { } inB)
        {
            return;
        }

        var play = new Play(world, report, $"seed {seed} door {doorId} chain");
        var grates = world.Balance.Grates;
        report.Count("chain: two arenas side by side");
        play.GiveKeys();
        play.PlaceAt(inA);
        play.Face(inB);
        if (!play.TicksUntil(200, PlayerInput.None, () => a.IsSealed))
        {
            report.Count("chain: A never sealed");
            return;
        }

        report.Check(!door.Blocks, $"{play.Context}: A sealed but the shared door does not block");
        for (var i = 0; i < 80; i++)
        {
            play.Tick(Play.Forward with { Dash = i % 20 == 0, Jump = i % 7 == 0 });
        }

        report.Check(!a.Cells.Contains(Play.Cell(world.Player.Position)), $"{play.Context}: player left sealed A through the shared door");
        report.Check(b.IsSealed, $"{play.Context}: B sealed while A is still sealed");
        if (report.Check(!ClearRoom(play, a), $"{play.Context}: A did not clear"))
        {
            return;
        }

        play.Ticks(grates.LiftTicks + 20, PlayerInput.None);
        report.Check(door.IsBarred, $"{play.Context}: shared door still barred after A cleared");
        play.Face(inB);
        play.Tick(Play.Interact);
        if (report.Check(!play.TicksUntil(400, Play.Forward, () => b.IsSealed), $"{play.Context}: could not enter B after clearing A"))
        {
            return;
        }

        play.Ticks(grates.FallTicks + 10, PlayerInput.None);
        foreach (var id in b.DoorIds.Where(id => world.Doors[id].Kind != DoorKind.Cracked && !world.Doors[id].IsBarred))
        {
            report.Finding($"{play.Context}: door {id} of sealed B not barred");
        }

        if (report.Check(!ClearRoom(play, b), $"{play.Context}: B did not clear"))
        {
            return;
        }

        play.Ticks(grates.LiftTicks + 20, PlayerInput.None);
        foreach (var id in b.DoorIds.Where(id => world.Doors[id].IsBarred))
        {
            report.Finding($"{play.Context}: door {id} still barred after B cleared");
        }

        report.Count("chain: A then B cleared, all open");
    }

    private static bool ClearRoom(Play play, RoomState room) =>
        play.TicksUntil(900, PlayerInput.None, () =>
        {
            Play.KillEnemies(play.World, room);
            return room.IsCleared;
        });

    /// <summary>The boss room seals without a grate: its door blocks at once and no grate starts.</summary>
    private static void BossDoor(LevelZero zero, int seed, int doorId, Report report)
    {
        var world = zero.NewRun(seed, Play.Balance);
        var door = world.Doors[doorId];
        var boss = world.Rooms[door.RoomA].Kind == RoomKind.Boss ? world.Rooms[door.RoomA] : world.Rooms[door.RoomB];
        if (Play.NeighbourIn(door, boss) is not { } inside || Play.NeighbourIn(door, Play.OtherRoom(world, door, boss)) is not { } outside)
        {
            return;
        }

        var play = new Play(world, report, $"seed {seed} boss door {doorId}");
        play.GiveKeys();
        play.PlaceAt(outside);
        play.Face(inside);
        play.Tick(Play.Interact);
        if (!play.TicksUntil(600, Play.Forward, () => boss.IsSealed))
        {
            report.Count("boss door: could not enter");
            return;
        }

        foreach (var id in boss.DoorIds.Where(id => world.Doors[id].Kind != DoorKind.Cracked && !world.Doors[id].Blocks))
        {
            report.Finding($"{play.Context}: door {id} does not block at once after sealing");
        }

        report.Check(play.Events.OfType<GrateStarted>().Any(), $"{play.Context}: a grate started at the boss room");
        report.Count("boss door: sealed at once");
    }
}

/// <summary>A combat room behind a door, with the cell just outside and just inside that door.</summary>
internal sealed record Arena(RoomState Room, Door Door, (int X, int Z) Outside, (int X, int Z) Inside)
{
    public static Arena? Find(GameWorld world, Door door)
    {
        foreach (var index in new[] { door.RoomA, door.RoomB })
        {
            var room = world.Rooms[index];
            if (room.Kind == RoomKind.Combat
                && Play.NeighbourIn(door, room) is { } inside
                && Play.NeighbourIn(door, Play.OtherRoom(world, door, room)) is { } outside)
            {
                return new Arena(room, door, outside, inside);
            }
        }

        return null;
    }

    /// <summary>
    /// Clears the room outside first (as in a real run: if it is an arena itself it would seal and bar the shared door,
    /// issue #578), then stands outside the door, opens it and walks in until the arena seals.
    /// </summary>
    public void Enter(Play play)
    {
        Play.KillEnemies(play.World, Play.OtherRoom(play.World, Door, Room));
        play.GiveKeys();
        play.PlaceAt(Outside);
        play.Face(Inside);
        play.Tick(Play.Interact);
        play.TicksUntil(500, Play.Forward, () => Room.IsSealed);
    }
}
