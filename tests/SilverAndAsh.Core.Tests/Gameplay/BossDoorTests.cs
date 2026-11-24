using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// The boss door over two cells with its own motion (issue #566): it swings shut over
/// <see cref="DoorsBalance.BossCloseTicks"/> ticks when the boss room seals and blocks - both cells together - from the hard
/// stop at <see cref="DoorsBalance.BossShutTick"/> on. Whoever stands in its doorway then is pushed into the boss room.
/// Once the boss room is cleared it opens again, evenly over <see cref="DoorsBalance.BossOpenTime"/>. Every start of a
/// motion is one <see cref="DoorSwingStarted"/>.
/// </summary>
public class BossDoorTests
{
    // Room 0 (start) in row 0, the boss door in cells (2, 1) and (3, 1), room 1 (boss room, one ghoul far in) in rows
    // 2-9, a second boss door of one cell at (3, 10) that nobody opens, room 2 (corridor) in row 11.
    private const string Map =
        "#.P..#\n##..##\n#....#\n#....#\n#....#\n#....#\n#....#\n#....#\n#....#\n#...G#\n###.##\n#....#";

    internal const int BossDoor = 0;
    private const int BackDoor = 1;

    private static readonly DoorsBalance Doors = Balance.Default.Doors;
    private static readonly int OpenTicks = SimulationClock.SecondsToTicks(Doors.BossOpenTime);
    private static readonly PlayerInput Back = new(new Vector2(0f, -1f), Vector2.Zero, false);
    internal static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };

    private static List<(int X, int Z)> Rows(int from, int to) =>
        Enumerable.Range(from, to - from + 1).SelectMany(z => new[] { (1, z), (2, z), (3, z), (4, z) }).ToList();

    internal static GameWorld World(string map = Map, IReadOnlyList<(int X, int Z)>? moreCells = null)
    {
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 6, 1, 0, Rows(0, 0), [], []),
            new(1, RoomKind.Boss, "boss", 0, 2, 6, 8, 1, Rows(2, 9), [(4, 9)], []),
            new(2, RoomKind.Corridor, "corridor", 0, 11, 6, 1, 2, Rows(11, 11), [], []),
        ];
        LevelDoor[] doors =
        [
            new(2, 1, 0, 1, RoomSide.South, DoorKind.Boss) { MoreCells = moreCells ?? [(3, 1)] },
            new(3, 10, 1, 2, RoomSide.South, DoorKind.Boss),
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

    // Issue #598: the stone jambs of the portal leave a passage only as wide as its opening (2 m at the door plane); the
    // player starts at a cell middle a metre beside its middle. Forward (+Z) and towards the middle of the opening, as a
    // player aims for a door - in the player's own view (Move.X goes to the view's right).
    internal static PlayerInput IntoTheOpening(GameWorld world)
    {
        var door = world.Doors[BossDoor];
        var aside = Math.Clamp(((door.BoxMin.X + door.BoxMax.X) / 2f - world.Player.Position.X) * 2f, -1f, 1f);
        var direction = new Vector3(aside, 0f, 1f);
        var view = world.Player.View;
        return new PlayerInput(new Vector2(Vector3.Dot(direction, view.FlatRight), Vector3.Dot(direction, view.FlatForward)), Vector2.Zero, false);
    }

    // As Run, with the input of each tick worked out from the world as it stands.
    private static List<GameEvent> Run(GameWorld world, Func<GameWorld, PlayerInput> input, int ticks, Func<bool>? until = null)
    {
        var events = new List<GameEvent>();
        for (var i = 0; i < ticks && until?.Invoke() != true; i++)
        {
            world.Tick(input(world));
            events.AddRange(world.Events);
        }

        return events;
    }

    // Opens the boss door and walks in through the opening until the boss room seals (at most 3 s). The sealing tick is
    // tick 1 of the slam: the doors move after the players.
    private static List<GameEvent> Enter(GameWorld world)
    {
        var events = Run(world, PressF, 1);
        events.AddRange(Run(world, IntoTheOpening, 180, () => world.Rooms[1].IsSealed));
        Assert.True(world.Rooms[1].IsSealed);
        return events;
    }

    internal static void KillEverything(GameWorld world)
    {
        foreach (var target in world.Targets.ToList())
        {
            target.TakeDamage(10_000, []);
        }
    }

    private static bool BothCellsSolid(GameWorld world, out bool second)
    {
        var door = world.Doors[BossDoor];
        second = world.Level[door.Cells[1].X, door.Cells[1].Z].IsSolid;
        return world.Level[door.Cells[0].X, door.Cells[0].Z].IsSolid;
    }

    private static bool InCells(RoomState room, Vector3 at) => room.Cells.Contains(Level.ToGrid(at.X, at.Z));

    [Fact]
    public void Both_cells_of_the_boss_door_block_from_the_hard_stop_and_free_together_when_the_boss_room_is_cleared()
    {
        var world = World();
        var door = world.Doors[BossDoor];
        Assert.Equal([(2, 1), (3, 1)], door.Cells);
        Enter(world);

        // Tick by tick through the whole slam: both cells free before the hard stop, both wall from it on.
        for (var tick = 1; tick <= Doors.BossCloseTicks + 10; tick++)
        {
            var shut = tick >= Doors.BossShutTick;
            var first = BothCellsSolid(world, out var second);
            Assert.True(shut == first, $"tick {tick}: first cell solid = {first}");
            Assert.True(shut == second, $"tick {tick}: second cell solid = {second}");
            Assert.Equal(shut, door.Blocks);
            Assert.Equal(shut, door.IsBarred);
            world.Tick(PlayerInput.None);
        }

        // The boss is dead: in that very tick both cells are free again, together.
        KillEverything(world);
        var events = Run(world, PlayerInput.None, 1);
        Assert.Contains(new RoomCleared(1), events);
        Assert.False(BothCellsSolid(world, out var secondAfter));
        Assert.False(secondAfter);
        Assert.False(door.Blocks);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_slammed_boss_door_never_shows_placeholder_bars(bool gratesModelled)
    {
        // Issue #568: no portcullis at the boss room and no coded bars in front of the portal - the leaves bar it. Tick
        // by tick through the slam and past its hard stop, and on the back door nobody opened (barred at once).
        var world = World();
        var door = world.Doors[BossDoor];
        var back = world.Doors[BackDoor];
        Enter(world);
        var barredTicks = 0;
        for (var tick = 1; tick <= Doors.BossCloseTicks + 10; tick++)
        {
            foreach (var shown in new[] { door, back })
            {
                Assert.False(
                    SilverAndAsh.Core.Presentation.GratePose.DrawsPlaceholderBars(shown.Kind, shown.IsBarred, shown.HasGrate, gratesModelled),
                    $"tick {tick}: door {shown.Id} shows bars");
            }

            barredTicks += door.IsBarred ? 1 : 0;
            world.Tick(PlayerInput.None);
        }

        // The doors really were barred all that while: the slammed one from its hard stop on, the other one throughout.
        Assert.Equal(Doors.BossCloseTicks + 10 - Doors.BossShutTick + 1, barredTicks);
        Assert.True(back.IsBarred);
    }

    [Fact]
    public void A_boss_door_nobody_opened_is_barred_at_once_and_only_unbarred_when_the_room_is_cleared()
    {
        // Error branch of the slam: a closed boss door has no leaves to swing - it shuts in the sealing tick, as before.
        var world = World();
        var back = world.Doors[BackDoor];
        Assert.Equal([(3, 10)], back.Cells);

        var events = Enter(world);

        Assert.True(back.IsBarred);
        Assert.True(world.Level[3, 10].IsSolid);
        Assert.Equal(0f, back.OpenFraction);
        Assert.DoesNotContain(events, e => e is DoorSwingStarted { DoorId: BackDoor } or GrateStarted);

        KillEverything(world);
        var cleared = Run(world, PlayerInput.None, 1);
        Assert.False(back.IsBarred);
        Assert.False(back.IsOpen);
        Assert.True(back.CanInteract);
        Assert.DoesNotContain(cleared, e => e is DoorSwingStarted { DoorId: BackDoor });
    }

    [Fact]
    public void The_boss_door_swings_shut_to_the_hard_stop_and_opens_again_evenly_over_its_opening_time()
    {
        var world = World();
        var door = world.Doors[BossDoor];

        // Walk in, remembering how far the leaves stood open at the end of the tick before the room sealed.
        Run(world, PressF, 1);
        var from = door.OpenFraction;
        for (var i = 0; i < 180 && !world.Rooms[1].IsSealed; i++)
        {
            from = door.OpenFraction;
            world.Tick(IntoTheOpening(world));
        }

        Assert.True(world.Rooms[1].IsSealed);
        Assert.InRange(from, 0.01f, 1f);

        // From there evenly down to closed at the hard stop, and closed for the rest of the slam.
        for (var tick = 1; tick <= Doors.BossCloseTicks; tick++)
        {
            var expected = from * MathF.Max(0f, 1f - ((float)tick / Doors.BossShutTick));
            Assert.True(MathF.Abs(expected - door.OpenFraction) < 1e-4f, $"tick {tick}: {door.OpenFraction}, expected {expected}");
            world.Tick(PlayerInput.None);
        }

        Assert.Equal(0f, door.OpenFraction);

        // Cleared: from closed evenly to fully open over the opening time - one step a tick.
        KillEverything(world);
        world.Tick(PlayerInput.None);
        for (var tick = 0; tick <= OpenTicks + 10; tick++)
        {
            var expected = Math.Min(tick, OpenTicks) / (float)OpenTicks;
            Assert.True(MathF.Abs(expected - door.OpenFraction) < 1e-4f, $"tick {tick}: {door.OpenFraction}, expected {expected}");
            world.Tick(PlayerInput.None);
        }
    }

    [Fact]
    public void Every_start_of_a_boss_door_motion_is_one_door_event_and_no_grate_event()
    {
        var world = World();
        var place = (world.Doors[BossDoor].BoxMin + world.Doors[BossDoor].BoxMax) / 2f;

        var opened = Run(world, PressF, 1);
        var opening = Assert.Single(opened, e => e is DoorSwingStarted);
        Assert.Equal(new DoorSwingStarted(BossDoor, false) { Place = place }, opening);
        Assert.Contains(opened, e => e is DoorOpened { DoorId: BossDoor });

        var walked = Run(world, IntoTheOpening, 180, () => world.Rooms[1].IsSealed);
        walked.AddRange(Run(world, PlayerInput.None, Doors.BossCloseTicks + 10));
        var closing = Assert.Single(walked, e => e is DoorSwingStarted);
        Assert.Equal(new DoorSwingStarted(BossDoor, true) { Place = place }, closing);

        KillEverything(world);
        var cleared = Run(world, PlayerInput.None, OpenTicks + 10);
        var reopening = Assert.Single(cleared, e => e is DoorSwingStarted);
        Assert.Equal(new DoorSwingStarted(BossDoor, false) { Place = place }, reopening);

        Assert.DoesNotContain(opened.Concat(walked).Concat(cleared), e => e is GrateStarted);
    }

    [Fact]
    public void A_player_who_steps_back_into_the_doorway_is_pushed_into_the_boss_room_at_the_hard_stop()
    {
        var world = World();
        var door = world.Doors[BossDoor];
        var radius = BodySizes.Player.Radius;
        Enter(world);

        // Since issue #567 a single player no longer gets into the doorway: they are held on the threshold, so this
        // start does not occur any more and the checks below hold anyway. Issue #584 aims this test at co-op, where a
        // second player can still stand in the doorway at the hard stop.
        // Back into the doorway while the leaves still swing - the way is not shut yet.
        Run(world, Back, 60, () => world.Player.Position.Z < door.BoxMax.Z - 0.2f);
        Assert.False(door.Blocks);
        var x = world.Player.Position.X;
        var health = world.Player.Health;

        Run(world, PlayerInput.None, Doors.BossCloseTicks, () => door.Blocks);

        // At the stop: just out of the door cells on the boss room's side, not caught and not hurt.
        Assert.True(door.Blocks);
        Assert.InRange(world.Player.Position.Z, door.BoxMax.Z + radius, door.BoxMax.Z + radius + 0.02f);
        Assert.Equal(x, world.Player.Position.X);
        Assert.True(InCells(world.Rooms[1], world.Player.Position), $"the player is at {world.Player.Position}");
        Assert.Equal(health, world.Player.Health);
    }

    // A bigger start room (rows 0-2) with an awake enemy two rows behind the player; the boss door in cells (2, 3) and
    // (3, 3), the boss room in rows 4-13 with its ghoul far in.
    private static GameWorld WithAFollower(char enemy)
    {
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 6, 3, 0, Rows(0, 2), [], []),
            new(1, RoomKind.Boss, "boss", 0, 4, 6, 10, 1, Rows(4, 13), [(4, 13)], []),
        ];
        LevelDoor[] doors = [new(2, 3, 0, 1, RoomSide.South, DoorKind.Boss) { MoreCells = [(3, 3)] }];
        var middle = string.Concat(Enumerable.Repeat("#....#\n", 9));
        var map = $"#.{enemy}..#\n#....#\n#.P..#\n##..##\n{middle}#...G#";
        return new GameWorld(LevelParser.Parse(map), 1, Balance.Default, doors, rooms);
    }

    [Theory]
    [InlineData('G')]
    [InlineData('B')]
    public void An_enemy_that_follows_a_player_into_the_doorway_is_pushed_into_the_boss_room_unhurt_at_the_hard_stop(char symbol)
    {
        var world = WithAFollower(symbol);
        var follower = world.Targets.Single(target => target is Ghoul or Bat && target.Body.Position.Z < Level.CellSize * 3);
        var radius = follower.Body.Radius;
        var door = world.Doors[0];
        bool InTheDoorway(Vector3 at) => at.Z + radius > door.BoxMin.Z && at.Z - radius < door.BoxMax.Z;

        // The player waits in the doorway until the follower is close behind, then steps in: the boss room seals.
        Run(world, PressF, 1);
        Run(world, IntoTheOpening, 120, () => world.Player.Position.Z > door.BoxMax.Z - 0.3f);
        Assert.False(world.Rooms[1].IsEntered);
        Run(world, PlayerInput.None, 600, () => follower.Body.Position.Z > door.BoxMin.Z - 1.5f);
        Run(world, IntoTheOpening, 60, () => world.Rooms[1].IsSealed);
        Assert.True(world.Rooms[1].IsSealed);
        var health = follower is Ghoul ghoul ? ghoul.Health : ((Bat)follower).Health;

        // It comes on through the swinging leaves; at the hard stop it is in the doorway - and pushed through.
        var before = follower.Body.Position;
        for (var tick = 0; tick < Doors.BossCloseTicks && !door.Blocks; tick++)
        {
            before = follower.Body.Position;
            world.Tick(PlayerInput.None);
        }

        var after = follower.Body.Position;
        Assert.True(door.Blocks);
        Assert.True(InTheDoorway(before), $"{symbol}: z = {before.Z} at the end of the tick before the stop");
        Assert.InRange(after.Z, door.BoxMax.Z + radius, door.BoxMax.Z + radius + 0.02f);
        Assert.InRange(after.X, door.BoxMin.X + radius, door.BoxMax.X - radius);
        // Not in the portal's stone, checked against the stone itself over the follower's whole body (feet to top).
        Assert.False(BossPortalStoneTests.InStone(world.Level, after, radius, follower.Body.Height), $"{symbol}: {after} is in the portal's stone");
        Assert.True(InCells(world.Rooms[1], after), $"{symbol}: {after}");
        Assert.Equal(health, follower is Ghoul g ? g.Health : ((Bat)follower).Health);
    }

    [Fact]
    public void A_door_cell_on_a_wall_is_refused()
    {
        // Error branch: the second cell of the boss door would lie in the wall beside the doorway.
        var error = Assert.Throws<ArgumentException>(() => World(moreCells: [(4, 1)]));

        Assert.Contains("4/1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_door_cell_outside_the_level_is_refused()
    {
        var error = Assert.Throws<ArgumentException>(() => World(moreCells: [(2, -1)]));

        Assert.Contains("2/-1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_door_with_a_gap_between_its_cells_is_refused()
    {
        // Error branch (issue #583): (2, 1) and (4, 1) are both walkable, but the wall at (3, 1) lies between them.
        var gap = Map.Replace("##..##", "##.#.#", StringComparison.Ordinal);

        var error = Assert.Throws<ArgumentException>(() => World(gap, [(4, 1)]));

        Assert.Contains("2/1, 4/1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_door_cell_along_the_passage_is_refused()
    {
        // Error branch (issue #583): (2, 2) is walkable floor of the boss room, but behind (2, 1), not beside it.
        var error = Assert.Throws<ArgumentException>(() => World(moreCells: [(2, 2)]));

        Assert.Contains("2/1, 2/2", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_door_cell_beside_but_out_of_the_wall_is_refused()
    {
        // Error branch (review of PR #701): (3, 2) lies one step along the wall from (2, 1), so there is no gap - but
        // one row deeper, in the boss room, not in the south wall. Only "in one wall" refuses it.
        var error = Assert.Throws<ArgumentException>(() => World(moreCells: [(3, 2)]));

        Assert.Contains("2/1, 3/2", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_cell_of_an_east_door_out_of_its_column_is_refused()
    {
        // The same for the other direction: an east door lies in one column. (2, 2) is one step along it from (1, 1),
        // but in the next column. Built as a door alone, since the world of this class has its doors in south walls.
        var level = LevelParser.Parse("P..\n...\n...");
        var plan = new LevelDoor(1, 1, 0, 1, RoomSide.East, DoorKind.Boss) { MoreCells = [(2, 2)] };

        var error = Assert.Throws<ArgumentException>(() => new Door(0, plan, level));

        Assert.Contains("1/1, 2/2", error.Message, StringComparison.Ordinal);
        Assert.Contains("East wall", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_door_with_a_cell_twice_is_refused()
    {
        var error = Assert.Throws<ArgumentException>(() => World(moreCells: [(2, 1)]));

        Assert.Contains("2/1, 2/1", error.Message, StringComparison.Ordinal);
    }
}
