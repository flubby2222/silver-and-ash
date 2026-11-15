using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class MapDiscoveryTests
{
    // Room 0 (start), door A, room 1 (combat, no enemies), door B, room 2 (corridor) - as in ArenaTests.
    private const string Map = "#.P.#\n##.##\n#...#\n#...#\n#...#\n#...#\n##.##\n#...#\n#...#";

    private static readonly PlayerInput Forward = new(new Vector2(0f, 1f), Vector2.Zero, false);
    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };

    private static List<(int X, int Z)> Rows(int from, int to) =>
        Enumerable.Range(from, to - from + 1).SelectMany(z => new[] { (1, z), (2, z), (3, z) }).ToList();

    private static GameWorld World()
    {
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 5, 1, 0, Rows(0, 0), [], []),
            new(1, RoomKind.Combat, "arena", 0, 2, 5, 4, 1, Rows(2, 5), [], []),
            new(2, RoomKind.Corridor, "corridor", 0, 7, 5, 2, 2, Rows(7, 8), [], []),
        ];
        LevelDoor[] doors = [new(2, 1, 0, 1, RoomSide.South), new(2, 6, 1, 2, RoomSide.South)];
        return new GameWorld(LevelParser.Parse(Map), 1, Balance.Default, doors, rooms);
    }

    private static void Run(GameWorld world, PlayerInput input, int ticks, Func<bool>? until = null)
    {
        for (var i = 0; i < ticks && until?.Invoke() != true; i++)
        {
            world.Tick(input);
        }
    }

    // ---- Blood vials lying around ----------------------------------------------------------------------------------

    /// <summary>The same rooms with a ghoul in room 1 that always drops a blood vial (or, with <c>ammo</c>, ammunition).</summary>
    private static GameWorld WorldWithDrop(bool ammo = false)
    {
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 5, 1, 0, Rows(0, 0), [], []),
            new(1, RoomKind.Combat, "arena", 0, 2, 5, 4, 1, Rows(2, 5), [(2, 4)], []),
            new(2, RoomKind.Corridor, "corridor", 0, 7, 5, 2, 2, Rows(7, 8), [], []),
        ];
        LevelDoor[] doors = [new(2, 1, 0, 1, RoomSide.South), new(2, 6, 1, 2, RoomSide.South)];
        var balance = Balance.Default with
        {
            Drops = new DropsBalance { GhoulChance = 1f, BatChance = 1f, ConsecratedAmmoShare = ammo ? 1f : 0f },
        };
        var map = "#.P.#\n##.##\n#...#\n#...#\n#.G.#\n#...#\n##.##\n#...#\n#...#";
        return new GameWorld(LevelParser.Parse(map), 1, balance, doors, rooms);
    }

    [Fact]
    public void A_blood_vial_in_a_room_not_yet_discovered_is_not_on_the_map()
    {
        var world = WorldWithDrop();
        world.Ghouls[0].TakeDamage(10_000, []);

        Run(world, PlayerInput.None, 5);

        Assert.Single(world.Pickups);
        Assert.Empty(world.KnownBloodVials);
    }

    [Fact]
    public void A_blood_vial_in_a_discovered_room_is_on_the_map()
    {
        var world = WorldWithDrop();
        world.Ghouls[0].TakeDamage(10_000, []);
        Run(world, PressF, 1);

        Run(world, Forward, 120, () => world.Rooms[1].IsDiscovered);

        Assert.True(world.Rooms[1].IsDiscovered);
        Assert.Same(world.Pickups[0], Assert.Single(world.KnownBloodVials));
    }

    [Fact]
    public void A_blood_vial_taken_is_gone_from_the_map()
    {
        var world = WorldWithDrop();
        world.Player.TakeDamage(50, []);
        world.Ghouls[0].TakeDamage(10_000, []);
        Run(world, PressF, 1);

        Run(world, Forward, 3 * SimulationClock.TicksPerSecond, () => world.Pickups.Count == 0);

        Assert.Empty(world.Pickups);
        Assert.Empty(world.KnownBloodVials);
    }

    [Fact]
    public void Consecrated_ammunition_is_not_on_the_map()
    {
        var world = WorldWithDrop(ammo: true);
        world.Ghouls[0].TakeDamage(10_000, []);
        Run(world, PressF, 1);

        Run(world, Forward, 120, () => world.Rooms[1].IsDiscovered);

        Assert.Equal(ConsumableKind.ConsecratedAmmo, Assert.Single(world.Pickups).Kind);
        Assert.Empty(world.KnownBloodVials);
    }

    [Fact]
    public void Hand_made_levels_show_no_vials()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nG"), balance: Balance.Default with { Drops = new DropsBalance { GhoulChance = 1f, ConsecratedAmmoShare = 0f } });
        world.Ghouls[0].TakeDamage(10_000, []);

        Run(world, PlayerInput.None, 2);

        Assert.Single(world.Pickups);
        Assert.Empty(world.KnownBloodVials);
    }

    [Fact]
    public void Before_the_first_tick_nothing_is_discovered()
    {
        var world = World();

        Assert.All(world.Rooms, room => Assert.False(room.IsDiscovered));
    }

    [Fact]
    public void The_room_the_player_stands_in_is_discovered()
    {
        var world = World();

        world.Tick(PlayerInput.None);

        Assert.True(world.Rooms[0].IsDiscovered);
        Assert.False(world.Rooms[1].IsDiscovered);
        Assert.False(world.Rooms[2].IsDiscovered);
    }

    [Fact]
    public void A_room_is_discovered_with_the_first_step_onto_its_floor()
    {
        var world = World();
        Run(world, PressF, 1);

        // Through door A (z 2..4 m) until the feet are on the first floor row of room 1 (z > 4 m).
        Run(world, Forward, 180, () => world.Player.Position.Z > 4.05f);

        Assert.True(world.Rooms[1].IsDiscovered);
    }

    [Fact]
    public void Standing_in_a_doorway_discovers_nothing_behind_it()
    {
        var world = World();
        Run(world, PressF, 1);

        Run(world, Forward, 180, () => world.Player.Position.Z > 3f);

        Assert.False(world.Rooms[1].IsDiscovered);
    }

    [Fact]
    public void Corridors_are_discovered_too()
    {
        var world = World();
        Run(world, PressF, 1);
        Run(world, Forward, 240, () => world.Player.Position.Z > 11f);
        Run(world, PressF, 1);

        Run(world, Forward, 240, () => world.Rooms[2].IsDiscovered);

        Assert.True(world.Rooms[2].IsDiscovered);
    }

    [Fact]
    public void Doors_of_discovered_rooms_are_known()
    {
        var world = World();
        world.Tick(PlayerInput.None);

        Assert.True(world.IsKnown(world.Doors[0]));
        Assert.False(world.IsKnown(world.Doors[1]));

        Run(world, PressF, 1);
        Run(world, Forward, 180, () => world.Rooms[1].IsDiscovered);

        Assert.True(world.IsKnown(world.Doors[1]));
    }

    [Fact]
    public void Discovered_stays_discovered()
    {
        var world = World();
        world.Tick(PlayerInput.None);
        Run(world, PressF, 1);
        Run(world, Forward, 180, () => world.Rooms[1].IsDiscovered);

        Run(world, new PlayerInput(new Vector2(0f, -1f), Vector2.Zero, false), 240, () => world.Player.Position.Z < 1.5f);

        Assert.True(world.Rooms[0].IsDiscovered);
        Assert.True(world.Rooms[1].IsDiscovered);
    }

    [Fact]
    public void A_new_run_starts_without_discoveries()
    {
        var world = World();
        world.Tick(PlayerInput.None);

        var restarted = world.Restarted();

        Assert.All(restarted.Rooms, room => Assert.False(room.IsDiscovered));
    }

    [Fact]
    public void Nothing_is_discovered_during_the_pause()
    {
        var world = World();

        world.Tick(PlayerInput.None with { Paused = true });

        Assert.False(world.Rooms[0].IsDiscovered);
    }

    [Fact]
    public void A_door_is_known_from_either_side()
    {
        // The same door A, but described from the arena's side: room A = arena (unknown), room B = start (known).
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 5, 1, 0, Rows(0, 0), [], []),
            new(1, RoomKind.Combat, "arena", 0, 2, 5, 4, 1, Rows(2, 5), [], []),
        ];
        var world = new GameWorld(
            LevelParser.Parse("#.P.#\n##.##\n#...#\n#...#\n#...#\n#...#"), 1, Balance.Default,
            [new LevelDoor(2, 1, 1, 0, RoomSide.North)], rooms);

        world.Tick(PlayerInput.None);

        Assert.True(world.IsKnown(world.Doors[0]));
    }

    [Fact]
    public void Doors_of_a_level_without_rooms_are_never_known()
    {
        var world = new GameWorld(LevelParser.Parse("#P#\n#.#\n#.#"), 1, Balance.Default, [new LevelDoor(1, 1, 0, 1, RoomSide.South)]);
        world.Tick(PlayerInput.None);

        Assert.False(world.IsKnown(world.Doors[0]));
    }

    [Fact]
    public void Doors_know_the_rooms_they_join()
    {
        var world = World();

        Assert.Equal((0, 1), (world.Doors[0].RoomA, world.Doors[0].RoomB));
        Assert.Equal((1, 2), (world.Doors[1].RoomA, world.Doors[1].RoomB));
    }
}
