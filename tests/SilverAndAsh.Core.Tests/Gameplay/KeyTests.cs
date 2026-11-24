using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class KeyTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };

    /// <summary>A corridor along +Z: the player in row 0, a ghoul 4 m ahead, room to walk on.</summary>
    private static readonly Level GhoulAhead = LevelParser.Parse("P\n.\nG\n.\n.");

    /// <summary>Keys from every kill and every cleared room, no silver (so the loot is only keys).</summary>
    private static readonly Balance AlwaysKeys = Balance.Default with
    {
        Keys = new KeysBalance { RoomChance = 1f, EnemyChance = 1f },
        Silver = Balance.Default.Silver with { GhoulChance = 0f, BatChance = 0f },
    };

    private static Balance KeyChances(float room, float enemy) => AlwaysKeys with { Keys = new KeysBalance { RoomChance = room, EnemyChance = enemy } };

    // ---- Keys from enemies ------------------------------------------------------------------------------------------

    [Fact]
    public void A_killed_enemy_can_drop_a_key_where_it_died()
    {
        var world = new GameWorld(GhoulAhead, balance: AlwaysKeys);
        var ghoul = world.Ghouls[0];

        Kill(ghoul);
        world.Tick(PlayerInput.None);

        var key = Assert.Single(world.Loot);
        Assert.Equal(LootKind.Key, key.Kind);
        Assert.Equal(1, key.Amount);
        Assert.Equal(ghoul.Position.X, key.Position.X, 0.01f);
        Assert.Equal(ghoul.Position.Z, key.Position.Z, 0.01f);
        Assert.Contains(world.Events, e => e == new KeyDropped(key.Id, ghoul.Position));
    }

    [Fact]
    public void A_killed_bat_can_drop_a_key_too()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nB"), balance: AlwaysKeys);
        Kill(world.Bats[0]);

        Run(world, PlayerInput.None, Second);

        Assert.Equal(LootKind.Key, Assert.Single(world.Loot).Kind);
    }

    [Fact]
    public void Without_a_chance_no_key_drops()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nG\n.\nB"), balance: KeyChances(1f, 0f));
        Kill(world.Ghouls[0]);
        Kill(world.Bats[0]);

        Run(world, PlayerInput.None, Second);

        Assert.Empty(world.Loot);
    }

    [Fact]
    public void Keys_drop_as_often_as_the_standard_chance_says()
    {
        // 1000 worlds, one ghoul and one bat each: 2000 kills, each with the standard chance.
        const int Kills = 2000;
        var keys = 0;
        for (var seed = 0; seed < Kills / 2; seed++)
        {
            var world = new GameWorld(LevelParser.Parse("P#G\n.#.\n.#B"), seed);
            Kill(world.Ghouls[0]);
            Kill(world.Bats[0]);
            world.Tick(PlayerInput.None);
            keys += world.Loot.Count(loot => loot.Kind == LootKind.Key);
        }

        var (low, high) = Band(Kills, Balance.Default.Keys.EnemyChance);
        Assert.InRange(keys, low, high);
    }

    [Fact]
    public void The_same_seed_drops_the_same_keys()
    {
        Assert.Equal(KeysAfterKillingEverything(3), KeysAfterKillingEverything(3));
    }

    // ---- Keys from cleared rooms ------------------------------------------------------------------------------------

    // Room 0 (start) in row 0, door A in row 1 (1 m in front of the player's eyes), room 1 (combat, one ghoul)
    // in rows 2-5, door B in row 6, room 2 (corridor) in rows 7-8. The same set-up as the arena tests.
    private const string ArenaMap = "#.P.#\n##.##\n#...#\n#...#\n#.G.#\n#...#\n##.##\n#...#\n#...#";

    private static List<(int X, int Z)> Rows(int from, int to) =>
        Enumerable.Range(from, to - from + 1).SelectMany(z => new[] { (1, z), (2, z), (3, z) }).ToList();

    private static GameWorld Arena(Balance balance, string map = ArenaMap)
    {
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 5, 1, 0, Rows(0, 0), [], []),
            new(1, RoomKind.Combat, "arena", 0, 2, 5, 4, 1, Rows(2, 5), [(2, 4)], []),
            new(2, RoomKind.Corridor, "corridor", 0, 7, 5, 2, 2, Rows(7, 8), [], []),
        ];
        LevelDoor[] doors = [new(2, 1, 0, 1, RoomSide.South), new(2, 6, 1, 2, RoomSide.South)];
        return new GameWorld(LevelParser.Parse(map), 1, balance, doors, rooms);
    }

    // Opens door A and walks in until the room seals (at most 3 s).
    private static void Enter(GameWorld world)
    {
        Run(world, PressF, 1);
        Run(world, Forward, 3 * Second, () => world.Rooms[1].IsSealed);
        Assert.True(world.Rooms[1].IsSealed, "The arena never sealed - the test would prove nothing.");
    }

    [Fact]
    public void A_cleared_room_can_leave_a_key_in_its_middle()
    {
        var world = Arena(KeyChances(1f, 0f));
        Enter(world);

        Kill(world.Ghouls[0]);
        var events = Run(world, PlayerInput.None, 1);

        var key = Assert.Single(world.Loot);
        Assert.Equal(LootKind.Key, key.Kind);

        // The middle of rows 2-5 lies between rows 3 and 4: the nearer cell in the earlier row is (2, 3).
        Assert.Equal(2.5f * Level.CellSize, key.Position.X, 0.01f);
        Assert.Equal(3.5f * Level.CellSize, key.Position.Z, 0.01f);
        Assert.Contains(events, e => e is KeyDropped);
    }

    [Fact]
    public void A_room_cleared_before_it_was_entered_can_leave_a_key_too()
    {
        var world = Arena(KeyChances(1f, 0f));
        Kill(world.Ghouls[0]);
        Run(world, PressF, 1);

        Run(world, Forward, 3 * Second, () => world.Rooms[1].IsCleared);

        Assert.True(world.Rooms[1].IsCleared);
        Assert.Contains(world.Loot, loot => loot.Kind == LootKind.Key);
    }

    [Fact]
    public void Without_a_room_chance_a_cleared_room_leaves_no_key()
    {
        var world = Arena(KeyChances(0f, 0f));
        Enter(world);

        Kill(world.Ghouls[0]);
        Run(world, PlayerInput.None, 1);

        Assert.True(world.Rooms[1].IsCleared);
        Assert.Empty(world.Loot);
    }

    [Fact]
    public void A_room_that_is_not_cleared_leaves_no_key()
    {
        var world = Arena(KeyChances(1f, 0f));
        Enter(world);

        Run(world, PlayerInput.None, Second);

        Assert.Empty(world.Loot);
    }

    // ---- Picking keys up --------------------------------------------------------------------------------------------

    [Fact]
    public void Walking_over_a_key_takes_it()
    {
        var world = new GameWorld(GhoulAhead, balance: AlwaysKeys);
        var player = world.Player;
        Kill(world.Ghouls[0]);
        world.Tick(PlayerInput.None);
        var key = world.Loot[0];

        var events = Run(world, Forward, Second);

        Assert.Equal(1, player.Keys);
        Assert.Empty(world.Loot);
        Assert.True(key.IsTaken);
        Assert.Contains(events, e => e == new KeyTaken(key.Id, player.Id));
    }

    [Fact]
    public void Keys_add_up()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nG\n.\nG\n.\n."), balance: AlwaysKeys);
        Kill(world.Ghouls[0]);
        Kill(world.Ghouls[1]);

        Run(world, Forward, 2 * Second);

        Assert.Equal(2, world.Player.Keys);
    }

    [Fact]
    public void A_key_lies_still_until_someone_comes()
    {
        var world = new GameWorld(GhoulAhead, balance: AlwaysKeys);
        Kill(world.Ghouls[0]);

        Run(world, PlayerInput.None, 3 * Second);

        Assert.Single(world.Loot);
        Assert.Equal(0, world.Player.Keys);
    }

    [Fact]
    public void The_dead_take_no_key()
    {
        // The player runs at the key and dies just before it (0.8 m). The body still slides on over it.
        var world = new GameWorld(GhoulAhead, balance: AlwaysKeys);
        var player = world.Player;
        Kill(world.Ghouls[0]);
        world.Tick(PlayerInput.None);
        var key = world.Loot[0];
        for (var tick = 0; FlatDistance(player.Position, key.Position) > 0.8f; tick++)
        {
            Assert.True(tick < 3 * Second, "The player never got close to the key.");
            world.Tick(Forward);
        }

        player.TakeDamage(player.Health, []);
        var events = Run(world, Forward, Second);

        Assert.True(FlatDistance(player.Position, key.Position) < BodySizes.Player.Radius + BodySizes.Pickup.Radius,
            "The body did not slide far enough to touch the key - the test would prove nothing.");
        Assert.DoesNotContain(events, e => e is KeyTaken);
        Assert.Equal(0, player.Keys);
    }

    [Fact]
    public void A_new_run_starts_without_keys()
    {
        var world = new GameWorld(GhoulAhead, balance: AlwaysKeys);
        Kill(world.Ghouls[0]);
        Run(world, Forward, Second);
        Assert.Equal(1, world.Player.Keys);

        Assert.Equal(0, world.Restarted().Player.Keys);
    }

    // ---- Locked doors -----------------------------------------------------------------------------------------------

    /// <summary>
    /// A hall along +Z: rows 0-2 behind the player hold one ghoul per wanted key, the player starts in row 3 looking
    /// along +Z, the door is in row 4 (1 m in front of the eyes), rows 5-6 lie behind it. The ghouls are killed at once
    /// (every kill drops a key); the player walks back over the keys and up to the door again.
    /// </summary>
    private static GameWorld LockedDoorWorld(int keys, DoorKind kind = DoorKind.Locked)
    {
        var rows = Enumerable.Range(0, 3).Select(row => row < keys ? "#G#" : "#.#").Concat(["#P#", "#.#", "#.#", "#.#"]);
        var world = new GameWorld(
            LevelParser.Parse(string.Join('\n', rows)), 1, AlwaysKeys, [new LevelDoor(1, 4, 0, 1, RoomSide.South, kind)]);
        foreach (var ghoul in world.Ghouls)
        {
            Kill(ghoul);
        }

        Run(world, PlayerInput.None with { Move = new Vector2(0f, -1f) }, 2 * Second);
        Run(world, Forward, 2 * Second);
        Assert.Equal(keys, world.Player.Keys);
        Assert.Empty(world.Loot);
        return world;
    }

    [Fact]
    public void Without_a_key_a_locked_door_says_so_and_stays_shut()
    {
        var world = LockedDoorWorld(keys: 0);
        var door = world.Doors[0];

        Assert.Same(door, world.InteractionTarget(world.Player));
        Assert.Equal(new InteractionOffer("Locked - needs a key", false), door.OfferFor(world.Player));

        var events = Run(world, PressF, 5);

        Assert.False(door.IsOpen);
        Assert.True(world.Level[1, 4].IsSolid);
        Assert.DoesNotContain(events, e => e is DoorOpened or DoorUnlocked);
    }

    [Fact]
    public void An_unlocked_door_swings_open_over_the_time_of_a_locked_door()
    {
        // Issue #245: the heavy leaf takes longer than a wooden door - but the way is free at once.
        var world = LockedDoorWorld(keys: 1);
        var door = world.Doors[0];
        var total = SimulationClock.SecondsToTicks(Balance.Default.Doors.LockedOpenTime);

        Run(world, PressF, 1);
        Assert.False(world.Level[1, 4].IsSolid);
        Assert.Equal(1f / total, door.OpenFraction, 0.0001f);
        Assert.True(door.OpenedFromLowSide);

        Run(world, PlayerInput.None, total - 2);
        Assert.True(door.OpenFraction < 1f);
        Run(world, PlayerInput.None, 1);
        Assert.Equal(1f, door.OpenFraction);
    }

    [Fact]
    public void With_a_key_F_unlocks_the_door_and_uses_up_the_key()
    {
        var world = LockedDoorWorld(keys: 1);
        var door = world.Doors[0];
        Assert.Equal(new InteractionOffer("Unlock (1 key)", true), door.OfferFor(world.Player));

        var events = Run(world, PressF, 1);

        Assert.True(door.IsOpen);
        Assert.False(world.Level[1, 4].IsSolid);
        Assert.Equal(0, world.Player.Keys);
        Assert.Contains(new DoorUnlocked(door.Id, world.Player.Id), events);
        Assert.Contains(new DoorOpened(door.Id), events);
    }

    [Fact]
    public void Unlocking_uses_exactly_one_key()
    {
        var world = LockedDoorWorld(keys: 3);

        Run(world, PressF, 1);

        Assert.True(world.Doors[0].IsOpen);
        Assert.Equal(2, world.Player.Keys);
    }

    [Fact]
    public void An_unlocked_door_stays_open_and_costs_nothing_more()
    {
        var world = LockedDoorWorld(keys: 2);

        Run(world, PressF, 1);
        Run(world, PressF, 5);
        Run(world, Forward, 2 * Second);

        Assert.True(world.Doors[0].IsOpen);
        Assert.Equal(1, world.Player.Keys);
        Assert.True(world.Player.Position.Z > 5 * Level.CellSize, $"The player only got to z = {world.Player.Position.Z}.");
    }

    [Fact]
    public void Wooden_doors_cost_no_key()
    {
        var world = LockedDoorWorld(keys: 1, DoorKind.Normal);

        Run(world, PressF, 1);

        Assert.True(world.Doors[0].IsOpen);
        Assert.Equal(1, world.Player.Keys);
    }

    /// <summary>Where a count of hits out of <paramref name="tries"/> with <paramref name="chance"/> lies: expected value +- 3.5 standard deviations.</summary>
    private static (double Low, double High) Band(int tries, float chance)
    {
        var expected = tries * (double)chance;
        var deviation = Math.Sqrt(tries * chance * (1.0 - chance));
        return (expected - (3.5 * deviation), expected + (3.5 * deviation));
    }

    private static void Kill(ITarget target) => target.TakeDamage(10_000, []);

    private static List<Vector3> KeysAfterKillingEverything(int seed)
    {
        var world = new GameWorld(LevelParser.Parse("PGGGG\nBBBBB\nGGGGG\nBBBBB"), seed);
        foreach (var target in world.Targets.ToList())
        {
            Kill(target);
        }

        world.Tick(PlayerInput.None);
        return world.Loot.Where(loot => loot.Kind == LootKind.Key).Select(loot => loot.Position).ToList();
    }

    private static List<GameEvent> Run(GameWorld world, PlayerInput input, int ticks, Func<bool>? until = null)
    {
        var events = new List<GameEvent>();
        for (var tick = 0; tick < ticks && until?.Invoke() != true; tick++)
        {
            world.Tick(input);
            events.AddRange(world.Events);
        }

        return events;
    }
}
