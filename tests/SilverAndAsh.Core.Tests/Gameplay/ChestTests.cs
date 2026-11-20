using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class ChestTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };

    /// <summary>
    /// A corridor along +Z: the player in row 0, a ghoul in row 1 (killed at once - with <c>keyFromGhoul</c> it drops a
    /// key the player walks over), the chest in row 3. Nothing else drops, so all loot comes from the chest.
    /// </summary>
    private static GameWorld World(ChestPlan chest, bool keyFromGhoul = false)
    {
        var balance = Balance.Default with
        {
            Drops = Balance.Default.Drops with { GhoulChance = 0f, BatChance = 0f },
            Silver = Balance.Default.Silver with { GhoulChance = 0f, BatChance = 0f },
            Keys = new KeysBalance { RoomChance = 0f, EnemyChance = keyFromGhoul ? 1f : 0f },
        };
        var world = new GameWorld(LevelParser.Parse("#P#\n#G#\n#.#\n#.#\n#.#"), 1, balance, chests: [chest]);
        world.Ghouls[0].TakeDamage(10_000, []);
        return world;
    }

    private static ChestPlan Plan(
        ChestTier tier = ChestTier.Guard, bool locked = false, int silver = 25, bool vial = true, int x = 1, int z = 3, int room = 0,
        bool rises = false) => new(x, z, room, tier, locked, silver, vial, rises);

    /// <summary>Walks up to the chest in row 3 (stopping about 1.4 m before it) and looks down at it.</summary>
    private static void StandBeforeChest(GameWorld world)
    {
        var player = world.Player;
        for (var tick = 0; player.Position.Z < 5f; tick++)
        {
            Assert.True(tick < 3 * Second, "The player never got close to the chest.");
            world.Tick(Forward);
        }

        Run(world, PlayerInput.None, Second / 2);
        LookDown(player, 35f);
    }

    private static void LookDown(Player player, float degrees) =>
        player.Look(PlayerInput.None with { Look = new Vector2(0f, degrees * MathF.PI / 180f / ControlSettings.MouseSensitivity) });

    [Fact]
    public void A_standing_chest_is_offered_with_its_name()
    {
        var world = World(Plan(ChestTier.King));
        StandBeforeChest(world);

        var target = world.InteractionTarget(world.Player);

        Assert.Same(world.Chests[0], target);
        Assert.Equal(new InteractionOffer("Open King's Chest", true), target!.OfferFor(world.Player));
    }

    [Fact]
    public void F_opens_the_chest_and_its_contents_jump_out_towards_the_player()
    {
        var world = World(Plan(silver: 27, vial: true));
        StandBeforeChest(world);
        var chest = world.Chests[0];

        var events = Run(world, PressF, 1);

        Assert.True(chest.IsOpen);
        Assert.Contains(new ChestOpened(chest.Id, world.Player.Id, ChestTier.Guard), events);
        Assert.Equal(chest.Position, events.OfType<ChestOpened>().Single().Place); // heard from the chest (#491)
        var silver = Assert.Single(world.Loot);
        Assert.Equal(LootKind.Silver, silver.Kind);
        Assert.Equal(27, silver.Amount);
        var vial = Assert.Single(world.Pickups);
        Assert.Equal(ConsumableKind.BloodVial, vial.Kind);

        // Both lie between the chest and the player, still in the chest's cell.
        foreach (var place in new[] { silver.Position, vial.Position })
        {
            Assert.InRange(place.Z, 3 * Level.CellSize, chest.Position.Z - 0.5f);
        }
    }

    [Fact]
    public void A_chest_without_a_vial_gives_only_silver()
    {
        var world = World(Plan(vial: false));
        StandBeforeChest(world);

        Run(world, PressF, 1);

        Assert.Single(world.Loot);
        Assert.Empty(world.Pickups);
    }

    [Fact]
    public void Walking_on_takes_the_silver_from_the_chest()
    {
        var world = World(Plan(silver: 31, vial: false));
        StandBeforeChest(world);
        Run(world, PressF, 1);

        Run(world, Forward, Second);

        Assert.Equal(31, world.Player.Silver);
    }

    [Fact]
    public void An_open_chest_stays_empty_and_is_not_offered_again()
    {
        var world = World(Plan(vial: false));
        StandBeforeChest(world);
        Run(world, PressF, 1);

        var events = Run(world, PressF, 5);

        Assert.True(world.Chests[0].IsOpen);
        Assert.Null(world.InteractionTarget(world.Player));
        Assert.DoesNotContain(events, e => e is ChestOpened or SilverDropped);
        Assert.Single(world.Loot);
    }

    [Fact]
    public void Without_a_key_a_locked_chest_says_so_and_stays_shut()
    {
        var world = World(Plan(locked: true));
        StandBeforeChest(world);
        var chest = world.Chests[0];

        Assert.Same(chest, world.InteractionTarget(world.Player));
        Assert.Equal(new InteractionOffer("Locked - needs a key", false), chest.OfferFor(world.Player));

        var events = Run(world, PressF, 5);

        Assert.False(chest.IsOpen);
        Assert.True(chest.IsLocked);
        Assert.Empty(world.Loot);
        Assert.DoesNotContain(events, e => e is ChestOpened or ChestUnlocked);
    }

    [Fact]
    public void With_a_key_F_unlocks_and_opens_the_chest_and_uses_up_the_key()
    {
        var world = World(Plan(locked: true), keyFromGhoul: true);
        StandBeforeChest(world);
        var chest = world.Chests[0];
        Assert.Equal(1, world.Player.Keys);
        Assert.Equal(new InteractionOffer("Unlock (1 key)", true), chest.OfferFor(world.Player));

        var events = Run(world, PressF, 1);

        Assert.True(chest.IsOpen);
        Assert.False(chest.IsLocked);
        Assert.Equal(0, world.Player.Keys);
        Assert.Contains(new ChestUnlocked(chest.Id, world.Player.Id), events);
        Assert.Contains(events, e => e is ChestOpened);
        Assert.Single(world.Loot);
    }

    [Fact]
    public void An_unlocked_chest_costs_no_key()
    {
        var world = World(Plan(locked: false), keyFromGhoul: true);
        StandBeforeChest(world);

        Run(world, PressF, 1);

        Assert.True(world.Chests[0].IsOpen);
        Assert.Equal(1, world.Player.Keys);
    }

    [Fact]
    public void A_chest_out_of_reach_is_not_offered()
    {
        var world = World(Plan());
        LookDown(world.Player, 35f);

        Run(world, PressF, 1);

        Assert.Null(world.InteractionTarget(world.Player));
        Assert.False(world.Chests[0].IsOpen);
    }

    [Fact]
    public void A_new_try_brings_the_chests_back_closed()
    {
        var world = World(Plan(locked: false));
        StandBeforeChest(world);
        Run(world, PressF, 1);

        var restarted = world.Restarted();

        var chest = Assert.Single(restarted.Chests);
        Assert.False(chest.IsOpen);
        Assert.Equal(world.Chests[0].Plan, chest.Plan);
    }

    // ---- The chest of a combat room rises when the room is cleared --------------------------------------------------

    // Room 0 (start) in row 0, door A in row 1, room 1 (combat, one ghoul) in rows 2-5, door B in row 6, room 2
    // (corridor) in rows 7-8. The chest of room 1 stands in cell (3, 3), a chest of room 2 in (2, 8).
    private const string ArenaMap = "#.P.#\n##.##\n#...#\n#...#\n#.G.#\n#...#\n##.##\n#...#\n#...#";

    private static List<(int X, int Z)> Rows(int from, int to) =>
        Enumerable.Range(from, to - from + 1).SelectMany(z => new[] { (1, z), (2, z), (3, z) }).ToList();

    private static GameWorld Arena()
    {
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 5, 1, 0, Rows(0, 0), [], []),
            new(1, RoomKind.Combat, "arena", 0, 2, 5, 4, 1, Rows(2, 5), [(2, 4)], [], [(3, 3)]),
            new(2, RoomKind.Combat, "other", 0, 7, 5, 2, 2, Rows(7, 8), [], [], [(2, 8)]),
        ];
        LevelDoor[] doors = [new(2, 1, 0, 1, RoomSide.South), new(2, 6, 1, 2, RoomSide.South)];
        ChestPlan[] chests = [Plan(x: 3, z: 3, room: 1, rises: true), Plan(x: 2, z: 8, room: 2, rises: true)];
        var balance = Balance.Default with { Keys = new KeysBalance { RoomChance = 0f, EnemyChance = 0f } };
        return new GameWorld(LevelParser.Parse(ArenaMap), 1, balance, doors, rooms, chests);
    }

    private static void Enter(GameWorld world)
    {
        Run(world, PressF, 1);
        Run(world, Forward, 3 * Second, () => world.Rooms[1].IsSealed);
        Assert.True(world.Rooms[1].IsSealed, "The arena never sealed - the test would prove nothing.");
    }

    [Fact]
    public void A_combat_room_chest_waits_in_the_floor_while_the_room_is_not_cleared()
    {
        var world = Arena();
        Enter(world);

        Run(world, PlayerInput.None, 2 * Second);

        var chest = world.Chests[0];
        Assert.False(chest.IsRising);
        Assert.Equal(0f, chest.RiseFraction);
        Assert.False(chest.CanInteract);
    }

    [Fact]
    public void Clearing_the_room_lets_its_chest_rise_within_a_second()
    {
        var world = Arena();
        Enter(world);
        var chest = world.Chests[0];

        world.Ghouls[0].TakeDamage(10_000, []);
        var events = Run(world, PlayerInput.None, 1);

        Assert.Contains(new ChestRose(chest.Id), events);
        Assert.True(chest.IsRising);
        Assert.InRange(chest.RiseFraction, 0f, 0.5f);
        Assert.False(chest.CanInteract);

        Run(world, PlayerInput.None, SimulationClock.SecondsToTicks(Chest.RiseSeconds));

        Assert.Equal(1f, chest.RiseFraction);
        Assert.True(chest.CanInteract);
    }

    [Fact]
    public void Only_the_chest_of_the_cleared_room_rises()
    {
        var world = Arena();
        Enter(world);

        world.Ghouls[0].TakeDamage(10_000, []);
        var events = Run(world, PlayerInput.None, 2 * Second);

        Assert.False(world.Chests[1].IsRising);
        Assert.Single(events.OfType<ChestRose>());
    }

    [Fact]
    public void A_room_cleared_before_it_was_entered_lets_its_chest_rise_too()
    {
        var world = Arena();
        world.Ghouls[0].TakeDamage(10_000, []);
        Run(world, PressF, 1);

        Run(world, Forward, 3 * Second, () => world.Rooms[1].IsCleared);

        Assert.True(world.Rooms[1].IsCleared);
        Assert.True(world.Chests[0].IsRising);
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
