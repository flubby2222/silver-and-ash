using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Issue #567, decisions of the human: the boss door falls shut as soon as a player stands in the boss room, and there
/// is no way back into the antechamber until the boss is beaten - not walking, jumping, dodging or dashing, and not in
/// the ticks before the hard stop of the slam (<see cref="DoorsBalance.BossShutTick"/>), while the door does not block
/// yet. The threshold holds a player back as a wall would: sideways along it they slide on, a jump goes on. After the
/// victory the door opens again. Death in the boss fight and a restart of the level put door and teleporter back to how
/// they were at the start. Every test drives the world tick by tick with player input.
/// </summary>
public class BossRoomNoWayBackTests
{
    private const int Antechamber = 0;
    private const int BossRoom = 1;
    private const int BossDoor = 0;

    private static readonly DoorsBalance Doors = Balance.Default.Doors;
    private static readonly float Radius = BodySizes.Player.Radius;
    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };

    // One test level, in two directions. The antechamber (its teleporter under the player's start) lies on the low
    // side of the two-cell boss door, the boss room (one ghoul far in) on the high side. The player looks through the
    // door (yaw 0 along +Z, yaw π/2 along +X), turned as the mouse turns them.
    private sealed record Layout(string Map, bool PassesAlongZ)
    {
        public static readonly Vector2 In = new(0f, 1f);

        public static readonly Vector2 Sideways = new(1f, 0f);

        public PlayerInput Back => new(-In, Vector2.Zero, false);

        // Issue #598: the stone jambs of the portal leave a passage only as wide as its opening (2 m at the door plane);
        // the player starts at a cell middle a metre beside its middle. Forward and towards the middle of the opening,
        // as a player aims for a door - in the player's own view (Move.X goes to the view's right).
        public PlayerInput IntoTheOpening(GameWorld world)
        {
            var door = world.Doors[BossDoor];
            var aside = Math.Clamp((Across((door.BoxMin + door.BoxMax) / 2f) - Across(world.Player.Position)) * 2f, -1f, 1f);
            var direction = PassesAlongZ ? new Vector3(aside, 0f, 1f) : new Vector3(1f, 0f, aside);
            var view = world.Player.View;
            return new PlayerInput(new Vector2(Vector3.Dot(direction, view.FlatRight), Vector3.Dot(direction, view.FlatForward)), Vector2.Zero, false);
        }

        // Through the doorway, along the passage (grows into the boss room).
        public float Along(Vector3 at) => PassesAlongZ ? at.Z : at.X;

        // Across the passage, along the threshold.
        public float Across(Vector3 at) => PassesAlongZ ? at.X : at.Z;

        public (int X, int Z) Cell(int along, int across) => PassesAlongZ ? (across, along) : (along, across);

        public GameWorld World()
        {
            List<(int X, int Z)> Rows(int from, int to) =>
                Enumerable.Range(from, to - from + 1).SelectMany(along => Enumerable.Range(1, 4).Select(across => Cell(along, across))).ToList();

            var (x0, z0) = Cell(0, 0);
            var (x2, z2) = Cell(2, 0);
            var (w1, d1) = PassesAlongZ ? (6, 1) : (1, 6);
            var (w8, d8) = PassesAlongZ ? (6, 8) : (8, 6);
            PlacedRoom[] rooms =
            [
                new(Antechamber, RoomKind.Antechamber, "antechamber", x0, z0, w1, d1, 0, Rows(0, 0), [], []),
                new(BossRoom, RoomKind.Boss, "boss", x2, z2, w8, d8, 1, Rows(2, 9), [Cell(9, 4)], []),
            ];
            var (doorX, doorZ) = Cell(1, 2);
            var side = PassesAlongZ ? RoomSide.South : RoomSide.East;
            LevelDoor[] doors = [new(doorX, doorZ, Antechamber, BossRoom, side, DoorKind.Boss) { MoreCells = [Cell(1, 3)] }];
            var (tx, tz) = Cell(0, 2);
            TeleporterPlan[] teleporters = [new(tx, tz, Antechamber, TeleporterSite.BossGate)];
            var world = new GameWorld(LevelParser.Parse(Map), 1, Balance.Default, doors, rooms, teleporters: teleporters);

            // A mouse to the left by a quarter turn (sensitivity 1 rad per pixel): from +Z to +X.
            world.Player.View.ApplyMouse(new Vector2(PassesAlongZ ? 0f : -MathF.PI / 2f, 0f), 1f);
            return world;
        }
    }

    // Boss door in the south wall of the antechamber: the passage runs along Z.
    private static readonly Layout NorthSouth = new(
        "#.P..#\n##..##\n#....#\n#....#\n#....#\n#....#\n#....#\n#....#\n#....#\n#...G#", true);

    // The same level turned: the boss door in the east wall, the passage runs along X (issue #583).
    private static readonly Layout EastWest = new(
        "##########\n.#........\nP.........\n..........\n.#.......G\n##########", false);

    private static Layout LayoutNamed(string name) => name == "north-south" ? NorthSouth : EastWest;

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

    private static bool InCells(GameWorld world, int room, Vector3 at) => world.Rooms[room].Cells.Contains(Level.ToGrid(at.X, at.Z));

    private static float FaceToBossRoom(Layout layout, Door door) => layout.PassesAlongZ ? door.BoxMax.Z : door.BoxMax.X;

    // Opens the boss door and walks in through the opening, tick by tick, until the boss room seals (at most 3 s). Returns the events of
    // the sealing tick.
    private static List<GameEvent> WalkIn(Layout layout, GameWorld world)
    {
        Run(world, PressF, 1);
        var events = new List<GameEvent>();
        for (var i = 0; i < 180 && !world.Rooms[BossRoom].IsSealed; i++)
        {
            world.Tick(layout.IntoTheOpening(world));
            events = [.. world.Events];
        }

        Assert.True(world.Rooms[BossRoom].IsSealed);
        return events;
    }

    // The way back the player tries in this tick: walking, jumping, dodging (dash key on the ground), dashing in the
    // air, or walking back and to one side at once (as the leaves swing, along the threshold).
    private static PlayerInput WayBack(Layout layout, string way, int tick) => way switch
    {
        "walk" => layout.Back,
        "jump" => layout.Back with { Jump = tick % 30 == 0 },
        "dodge" => layout.Back with { Dash = tick % 30 == 0 },
        "dash" => layout.Back with { Jump = tick % 40 == 0, Dash = tick % 40 == 8 },
        "slant+" => layout.Back with { Move = -Layout.In + Layout.Sideways },
        "slant-" => layout.Back with { Move = -Layout.In - Layout.Sideways },
        _ => throw new ArgumentOutOfRangeException(nameof(way), way, null),
    };

    // One tick on the threshold, held back: where the player was and how fast, before and after.
    private sealed record Held(Vector3 Before, Vector3 SpeedBefore, Vector3 At, Vector3 Speed, bool InTheAir);

    // What the player did while trying to get back, and how it went.
    private sealed record Attempt(int TicksBeforeTheStop, List<Held> Holds, bool Jumped, bool Dodged, bool Dashed);

    // Tries the way back for the whole slam and a second after it. In every tick the player's body stays out of the
    // doorway, in the boss room. Counts the ticks the door did not block yet, and keeps those in which the player stood
    // on the threshold, held back.
    private static Attempt TryToGetBack(Layout layout, GameWorld world, string way)
    {
        var door = world.Doors[BossDoor];
        var face = FaceToBossRoom(layout, door);
        var threshold = face + Radius + 0.01f;
        var attempt = new Attempt(0, [], false, false, false);
        for (var tick = 0; tick < Doors.BossCloseTicks + 60; tick++)
        {
            var blocked = door.Blocks;
            var (before, speedBefore) = (world.Player.Position, world.Player.Velocity);
            world.Tick(WayBack(layout, way, tick));
            var (at, speed) = (world.Player.Position, world.Player.Velocity);

            // Touching the shut door's face (from the hard stop on, as against a wall) is not standing in the doorway.
            Assert.True(layout.Along(at) - Radius >= face - 1e-4f, $"{way}, tick {tick}: the body reaches into the doorway at {at}");
            Assert.True(InCells(world, BossRoom, at), $"{way}, tick {tick}: the player is at {at}");

            // Held as by a wall: no speed left that points into the doorway. Held is only who stands on the threshold in
            // front of the doorway: past its edge a player walks free (since issue #598 around the plinth and the buttress
            // of the portal, and on along the wall at about the threshold's depth).
            var (low, high) = layout.PassesAlongZ ? (door.BoxMin.X, door.BoxMax.X) : (door.BoxMin.Z, door.BoxMax.Z);
            var inFront = layout.Across(at) + Radius > low && layout.Across(at) - Radius < high;
            var isHeld = !blocked && inFront && MathF.Abs(layout.Along(at) - threshold) < 1e-3f;
            Assert.True(!isHeld || layout.Along(speed) >= -1e-3f, $"{way}, tick {tick}: speed {speed} on the threshold");
            if (isHeld)
            {
                attempt.Holds.Add(new Held(before, speedBefore, at, speed, !world.Player.IsGrounded));
            }

            attempt = attempt with
            {
                TicksBeforeTheStop = attempt.TicksBeforeTheStop + (blocked ? 0 : 1),
                Jumped = attempt.Jumped || !world.Player.IsGrounded,
                Dodged = attempt.Dodged || world.Player.IsDodging,
                Dashed = attempt.Dashed || world.Player.IsDashing,
            };
        }

        Assert.True(door.Blocks);
        return attempt;
    }

    [Theory]
    [InlineData("north-south", "walk")]
    [InlineData("north-south", "jump")]
    [InlineData("north-south", "dodge")]
    [InlineData("north-south", "dash")]
    [InlineData("east-west", "walk")]
    [InlineData("east-west", "jump")]
    [InlineData("east-west", "dodge")]
    [InlineData("east-west", "dash")]
    public void A_player_who_walks_into_the_boss_room_shuts_the_door_behind_them_and_gets_no_way_back(string direction, string way)
    {
        var layout = LayoutNamed(direction);
        var world = layout.World();
        var door = world.Doors[BossDoor];

        // The door starts to shut in the very tick the player stands in the boss room with the whole body.
        var sealing = WalkIn(layout, world);
        Assert.Contains(new DoorSwingStarted(BossDoor, true) { Place = (door.BoxMin + door.BoxMax) / 2f }, sealing);
        Assert.True(layout.Along(world.Player.Position) - Radius >= FaceToBossRoom(layout, door), $"at {world.Player.Position}");
        Assert.False(door.Blocks);

        var attempt = TryToGetBack(layout, world, way);

        // The slam ran from the sealing tick; before its hard stop the player was held on the threshold - not by a wall.
        Assert.Equal(Doors.BossShutTick - 1, attempt.TicksBeforeTheStop);
        Assert.True(attempt.Holds.Count > Doors.BossShutTick / 2, $"{way}: held in {attempt.Holds.Count} ticks before the stop");
        Assert.Equal(way is "jump" or "dash", attempt.Jumped);
        Assert.Equal(way == "dodge", attempt.Dodged);
        Assert.Equal(way == "dash", attempt.Dashed);
        Assert.True(world.Rooms[BossRoom].IsSealed);
    }

    [Theory]
    [InlineData("north-south")]
    [InlineData("east-west")]
    public void A_jump_on_the_threshold_goes_on_while_the_player_is_held_back(string direction)
    {
        var layout = LayoutNamed(direction);
        var world = layout.World();
        WalkIn(layout, world);

        var inTheAir = TryToGetBack(layout, world, "jump").Holds.Where(held => held.InTheAir).ToList();

        // Held in the air: the feet stay off the floor, the jump climbs well above it, and the speed up and down runs on
        // under gravity alone from one tick to the next - the hold takes nothing of it.
        Assert.NotEmpty(inTheAir);
        Assert.All(inTheAir, held => Assert.True(held.At.Y > 0f, $"feet at {held.At} in the air"));
        Assert.True(inTheAir.Max(held => held.At.Y) > 0.5f, $"highest {inTheAir.Max(held => held.At.Y)} m");
        Assert.Contains(inTheAir, held => held.Speed.Y < 0f);
        Assert.All(
            inTheAir.Where(held => held.SpeedBefore.Y != 0f),
            held => Assert.True(held.Speed.Y < held.SpeedBefore.Y && held.Speed.Y > held.SpeedBefore.Y - 1f, $"{held.SpeedBefore.Y} -> {held.Speed.Y}"));
    }

    [Theory]
    [InlineData("north-south", "slant+")]
    [InlineData("north-south", "slant-")]
    [InlineData("east-west", "slant+")]
    [InlineData("east-west", "slant-")]
    public void A_player_pressing_back_and_aside_slides_along_the_threshold_past_the_doorway(string direction, string way)
    {
        var layout = LayoutNamed(direction);
        var world = layout.World();
        var door = world.Doors[BossDoor];
        WalkIn(layout, world);

        var attempt = TryToGetBack(layout, world, way);

        // On the threshold the speed along it stays: the player slides on, as along a wall ...
        Assert.NotEmpty(attempt.Holds);
        Assert.All(attempt.Holds, held => Assert.True(MathF.Abs(layout.Across(held.Speed)) > 1f, $"speed {held.Speed} at {held.At}"));
        Assert.All(attempt.Holds, held => Assert.True(MathF.Abs(layout.Across(held.At) - layout.Across(held.Before)) > 0.01f, $"stuck at {held.At}"));

        // ... past the edge of the doorway, into the corner of the boss room - and never into the doorway (TryToGetBack).
        var across = layout.Across(world.Player.Position);
        var (low, high) = layout.PassesAlongZ ? (door.BoxMin.X, door.BoxMax.X) : (door.BoxMin.Z, door.BoxMax.Z);
        Assert.True(across + Radius < low || across - Radius > high, $"{way}: the player stands at {world.Player.Position}, the doorway is {low}-{high}");
    }

    [Fact]
    public void After_the_victory_the_door_opens_again_and_the_way_back_is_free()
    {
        var layout = NorthSouth;
        var world = layout.World();
        var door = world.Doors[BossDoor];
        WalkIn(layout, world);
        TryToGetBack(layout, world, "walk");

        foreach (var target in world.Targets.ToList())
        {
            target.TakeDamage(10_000, []);
        }

        var cleared = Run(world, PlayerInput.None, 1);
        Assert.Contains(new RoomCleared(BossRoom), cleared);
        Assert.Contains(new DoorSwingStarted(BossDoor, false) { Place = (door.BoxMin + door.BoxMax) / 2f }, cleared);

        // Back through the doorway into the antechamber, with the whole body.
        Run(world, layout.Back, 180, () => world.Player.Position.Z + Radius < door.BoxMin.Z);
        Assert.True(world.Player.Position.Z + Radius < door.BoxMin.Z, $"z = {world.Player.Position.Z}");
        Assert.True(InCells(world, Antechamber, world.Player.Position), $"the player is at {world.Player.Position}");
    }

    // What door, rooms, teleporter and player look like right now - to compare one world with another.
    private static string Snapshot(GameWorld world) => string.Join(
        " | ",
        world.Rooms.Select(room => $"room {room.Index}: entered={room.IsEntered} sealed={room.IsSealed} cleared={room.IsCleared}")
            .Concat(world.Doors.Select(door => $"door {door.Id}: open={door.IsOpen} barred={door.IsBarred} blocks={door.Blocks} swing={door.OpenFraction}"))
            .Concat(world.Doors.SelectMany(door => door.Cells).Select(cell => $"cell {cell}: solid={world.Level[cell.X, cell.Z].IsSolid}"))
            .Concat(world.Teleporters.Select(teleporter => $"teleporter {teleporter.Id}: unlocked={teleporter.IsUnlocked}"))
            .Append($"player at {world.Player.Position}, dead={world.Player.IsDead}"));

    // Both runs: the fresh world stands as a new one does, and the boss room shuts the same way again - no way back.
    private static void StandsAsAtTheStartAndShutsAgain(GameWorld fresh)
    {
        Assert.Equal(Snapshot(NorthSouth.World()), Snapshot(fresh));

        Run(fresh, PlayerInput.None, 1);
        Assert.True(fresh.Teleporters[0].IsUnlocked);
        var sealing = WalkIn(NorthSouth, fresh);
        Assert.Single(sealing, e => e is DoorSwingStarted { DoorId: BossDoor, Closing: true });
        var attempt = TryToGetBack(NorthSouth, fresh, "walk");
        Assert.True(attempt.Holds.Count > Doors.BossShutTick / 2, $"held in {attempt.Holds.Count} ticks before the stop");
    }

    [Fact]
    public void A_player_who_dies_while_the_door_still_swings_shut_restarts_with_door_and_teleporter_as_at_the_start()
    {
        // Error branch: death in the boss fight, before the hard stop. The slam goes on to its end, the room stays sealed.
        var world = NorthSouth.World();
        Run(world, PlayerInput.None, 1);
        Assert.True(world.Teleporters[0].IsUnlocked);
        WalkIn(NorthSouth, world);
        Run(world, NorthSouth.Back, Doors.BossShutTick / 2);
        Assert.False(world.Doors[BossDoor].Blocks);

        world.Player.TakeDamage(world.Player.MaxHealth, []);
        Run(world, PlayerInput.None, SimulationClock.SecondsToTicks(Balance.Default.RestartDelay) + 1);
        Assert.True(world.ShouldRestart);
        Assert.True(world.Rooms[BossRoom].IsSealed);
        Assert.True(world.Doors[BossDoor].Blocks);
        Assert.True(InCells(world, BossRoom, world.Player.Position), $"the dead player is at {world.Player.Position}");

        StandsAsAtTheStartAndShutsAgain(world.Restarted());
    }

    [Fact]
    public void A_restart_of_the_level_during_the_boss_fight_puts_door_and_teleporter_back_to_the_start()
    {
        // Error branch: the level starts again while the player lives, the door shut behind them.
        var world = NorthSouth.World();
        WalkIn(NorthSouth, world);
        TryToGetBack(NorthSouth, world, "walk");
        Assert.True(world.Teleporters[0].IsUnlocked);
        Assert.False(world.Player.IsDead);

        StandsAsAtTheStartAndShutsAgain(world.Restarted());
    }
}
