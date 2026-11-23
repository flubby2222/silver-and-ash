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
/// yet. After the victory it opens again. Death in the boss fight and a restart of the level put door and teleporter
/// back to how they were at the start. Every test drives the world tick by tick with player input.
/// </summary>
public class BossRoomNoWayBackTests
{
    // Room 0 (antechamber, its teleporter under the player's start) in row 0, the boss door in cells (2, 1) and (3, 1),
    // room 1 (boss room, one ghoul far in) in rows 2-9.
    private const string Map = "#.P..#\n##..##\n#....#\n#....#\n#....#\n#....#\n#....#\n#....#\n#....#\n#...G#";

    private const int Antechamber = 0;
    private const int BossRoom = 1;
    private const int BossDoor = 0;

    private static readonly DoorsBalance Doors = Balance.Default.Doors;
    private static readonly float Radius = BodySizes.Player.Radius;
    private static readonly PlayerInput Forward = new(new Vector2(0f, 1f), Vector2.Zero, false);
    private static readonly PlayerInput Back = new(new Vector2(0f, -1f), Vector2.Zero, false);
    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };

    private static List<(int X, int Z)> Rows(int from, int to) =>
        Enumerable.Range(from, to - from + 1).SelectMany(z => new[] { (1, z), (2, z), (3, z), (4, z) }).ToList();

    private static GameWorld World()
    {
        PlacedRoom[] rooms =
        [
            new(Antechamber, RoomKind.Antechamber, "antechamber", 0, 0, 6, 1, 0, Rows(0, 0), [], []),
            new(BossRoom, RoomKind.Boss, "boss", 0, 2, 6, 8, 1, Rows(2, 9), [(4, 9)], []),
        ];
        LevelDoor[] doors = [new(2, 1, Antechamber, BossRoom, RoomSide.South, DoorKind.Boss) { MoreCells = [(3, 1)] }];
        TeleporterPlan[] teleporters = [new(2, 0, Antechamber, TeleporterSite.BossGate)];
        return new GameWorld(LevelParser.Parse(Map), 1, Balance.Default, doors, rooms, teleporters: teleporters);
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

    private static bool InCells(GameWorld world, int room, Vector3 at) => world.Rooms[room].Cells.Contains(Level.ToGrid(at.X, at.Z));

    // Opens the boss door and walks in, tick by tick, until the boss room seals (at most 3 s). Returns the events of
    // the sealing tick.
    private static List<GameEvent> WalkIn(GameWorld world)
    {
        Run(world, PressF, 1);
        var events = new List<GameEvent>();
        for (var i = 0; i < 180 && !world.Rooms[BossRoom].IsSealed; i++)
        {
            world.Tick(Forward);
            events = [.. world.Events];
        }

        Assert.True(world.Rooms[BossRoom].IsSealed);
        return events;
    }

    // The way back the player tries in this tick: walking, jumping, dodging (dash key on the ground) or dashing in the air.
    private static PlayerInput WayBack(string way, int tick) => way switch
    {
        "walk" => Back,
        "jump" => Back with { Jump = tick % 30 == 0 },
        "dodge" => Back with { Dash = tick % 30 == 0 },
        "dash" => Back with { Jump = tick % 40 == 0, Dash = tick % 40 == 8 },
        _ => throw new ArgumentOutOfRangeException(nameof(way), way, null),
    };

    // What the player did while trying to get back, and how it went.
    private sealed record Attempt(int TicksBeforeTheStop, int TicksHeld, bool Jumped, bool Dodged, bool Dashed);

    // Tries the way back for the whole slam and a second after it. In every tick the player's body stays out of the
    // doorway, in the boss room. Counts the ticks the door did not block yet, and those in which the player stood on
    // the threshold, held back.
    private static Attempt TryToGetBack(GameWorld world, string way)
    {
        var door = world.Doors[BossDoor];
        var held = door.BoxMax.Z + Radius + 0.01f;
        var attempt = new Attempt(0, 0, false, false, false);
        for (var tick = 0; tick < Doors.BossCloseTicks + 60; tick++)
        {
            var blocked = door.Blocks;
            world.Tick(WayBack(way, tick));
            var at = world.Player.Position;
            // Touching the shut door's face (from the hard stop on, as against a wall) is not standing in the doorway.
            Assert.True(at.Z - Radius >= door.BoxMax.Z - 1e-4f, $"{way}, tick {tick}: the body reaches into the doorway at z = {at.Z}");
            Assert.True(InCells(world, BossRoom, at), $"{way}, tick {tick}: the player is at {at}");
            var isHeld = !blocked && MathF.Abs(at.Z - held) < 1e-3f;

            // Held as by a wall: no speed left that points into the doorway.
            Assert.True(!isHeld || world.Player.Velocity.Z >= -1e-3f, $"{way}, tick {tick}: speed {world.Player.Velocity} on the threshold");
            attempt = attempt with
            {
                TicksBeforeTheStop = attempt.TicksBeforeTheStop + (blocked ? 0 : 1),
                TicksHeld = attempt.TicksHeld + (isHeld ? 1 : 0),
                Jumped = attempt.Jumped || !world.Player.IsGrounded,
                Dodged = attempt.Dodged || world.Player.IsDodging,
                Dashed = attempt.Dashed || world.Player.IsDashing,
            };
        }

        Assert.True(door.Blocks);
        return attempt;
    }

    [Theory]
    [InlineData("walk")]
    [InlineData("jump")]
    [InlineData("dodge")]
    [InlineData("dash")]
    public void A_player_who_walks_into_the_boss_room_shuts_the_door_behind_them_and_gets_no_way_back(string way)
    {
        var world = World();
        var door = world.Doors[BossDoor];

        // The door starts to shut in the very tick the player stands in the boss room with the whole body.
        var sealing = WalkIn(world);
        Assert.Contains(new DoorSwingStarted(BossDoor, true) { Place = (door.BoxMin + door.BoxMax) / 2f }, sealing);
        Assert.True(world.Player.Position.Z - Radius >= door.BoxMax.Z, $"z = {world.Player.Position.Z}");
        Assert.False(door.Blocks);

        var attempt = TryToGetBack(world, way);

        // The slam ran from the sealing tick; before its hard stop the player was held on the threshold - not by a wall.
        Assert.Equal(Doors.BossShutTick - 1, attempt.TicksBeforeTheStop);
        Assert.True(attempt.TicksHeld > Doors.BossShutTick / 2, $"{way}: held in {attempt.TicksHeld} ticks before the stop");
        Assert.Equal(way is "jump" or "dash", attempt.Jumped);
        Assert.Equal(way == "dodge", attempt.Dodged);
        Assert.Equal(way == "dash", attempt.Dashed);
        Assert.True(world.Rooms[BossRoom].IsSealed);
    }

    [Fact]
    public void After_the_victory_the_door_opens_again_and_the_way_back_is_free()
    {
        var world = World();
        var door = world.Doors[BossDoor];
        WalkIn(world);
        TryToGetBack(world, "walk");

        foreach (var target in world.Targets.ToList())
        {
            target.TakeDamage(10_000, []);
        }

        var cleared = Run(world, PlayerInput.None, 1);
        Assert.Contains(new RoomCleared(BossRoom), cleared);
        Assert.Contains(new DoorSwingStarted(BossDoor, false) { Place = (door.BoxMin + door.BoxMax) / 2f }, cleared);

        // Back through the doorway into the antechamber, with the whole body.
        Run(world, Back, 180, () => world.Player.Position.Z + Radius < door.BoxMin.Z);
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
        Assert.Equal(Snapshot(World()), Snapshot(fresh));

        Run(fresh, PlayerInput.None, 1);
        Assert.True(fresh.Teleporters[0].IsUnlocked);
        var sealing = WalkIn(fresh);
        Assert.Single(sealing, e => e is DoorSwingStarted { DoorId: BossDoor, Closing: true });
        var attempt = TryToGetBack(fresh, "walk");
        Assert.True(attempt.TicksHeld > Doors.BossShutTick / 2, $"held in {attempt.TicksHeld} ticks before the stop");
    }

    [Fact]
    public void A_player_who_dies_while_the_door_still_swings_shut_restarts_with_door_and_teleporter_as_at_the_start()
    {
        // Error branch: death in the boss fight, before the hard stop. The slam goes on to its end, the room stays sealed.
        var world = World();
        Run(world, PlayerInput.None, 1);
        Assert.True(world.Teleporters[0].IsUnlocked);
        WalkIn(world);
        Run(world, Back, Doors.BossShutTick / 2);
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
        var world = World();
        WalkIn(world);
        TryToGetBack(world, "walk");
        Assert.True(world.Teleporters[0].IsUnlocked);
        Assert.False(world.Player.IsDead);

        StandsAsAtTheStartAndShutsAgain(world.Restarted());
    }
}
