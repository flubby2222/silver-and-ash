using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class ArenaTests
{
    // Room 0 (start) in row 0, door A in row 1 (1 m in front of the player's eyes), room 1 (combat, one ghoul)
    // in rows 2-5, door B in row 6, room 2 (corridor) in rows 7-8.
    private const string Map = "#.P.#\n##.##\n#...#\n#...#\n#.G.#\n#...#\n##.##\n#...#\n#...#";

    private const int DoorA = 0;
    private const int DoorB = 1;

    private static readonly PlayerInput Forward = new(new Vector2(0f, 1f), Vector2.Zero, false);
    private static readonly PlayerInput Back = new(new Vector2(0f, -1f), Vector2.Zero, false);
    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };

    private static List<(int X, int Z)> Rows(int from, int to) =>
        Enumerable.Range(from, to - from + 1).SelectMany(z => new[] { (1, z), (2, z), (3, z) }).ToList();

    // The same map with a bat instead of the ghoul, and with two ghouls.
    private const string BatMap = "#.P.#\n##.##\n#...#\n#...#\n#.B.#\n#...#\n##.##\n#...#\n#...#";
    private const string TwoGhoulsMap = "#.P.#\n##.##\n#...#\n#G..#\n#.G.#\n#...#\n##.##\n#...#\n#...#";

    private static GameWorld World(bool withGhoul = true, string? mapText = null)
    {
        var map = mapText ?? (withGhoul ? Map : Map.Replace('G', '.'));
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 5, 1, 0, Rows(0, 0), [], []),
            new(1, RoomKind.Combat, "arena", 0, 2, 5, 4, 1, Rows(2, 5), [(2, 4)], []),
            new(2, RoomKind.Corridor, "corridor", 0, 7, 5, 2, 2, Rows(7, 8), [], []),
        ];
        LevelDoor[] doors = [new(2, 1, 0, 1, RoomSide.South), new(2, 6, 1, 2, RoomSide.South)];
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
        events.AddRange(Run(world, Forward, 180, () => world.Rooms[1].IsEntered));
        return events;
    }

    private static void KillEverything(GameWorld world)
    {
        foreach (var target in world.Targets.ToList())
        {
            target.TakeDamage(10_000, []);
        }
    }

    [Fact]
    public void Enemies_in_rooms_sleep_until_the_room_is_entered()
    {
        var world = World();
        var ghoul = world.Ghouls[0];
        var start = ghoul.Position;

        // Door open, the ghoul in plain view - but asleep.
        var events = Run(world, PressF, 1);
        events.AddRange(Run(world, PlayerInput.None, 300));

        Assert.True(world.IsAsleep(ghoul));
        Assert.Equal(start, ghoul.Position);
        Assert.DoesNotContain(events, e => e is PlayerHit);
    }

    [Fact]
    public void Entering_a_combat_room_seals_all_its_doors_and_wakes_its_enemies()
    {
        var world = World();

        var events = Enter(world);

        Assert.Contains(new RoomSealed(1), events);
        Assert.True(world.Rooms[1].IsSealed);
        Assert.True(world.Doors[DoorA].IsBarred);
        Assert.True(world.Doors[DoorB].IsBarred);
        Assert.True(world.Level[2, 1].IsSolid);
        Assert.True(world.Level[2, 6].IsSolid);
        Assert.False(world.IsAsleep(world.Ghouls[0]));
        Assert.Null(world.InteractionTarget(world.Player));
    }

    [Fact]
    public void The_room_seals_only_once_the_player_is_fully_inside()
    {
        var world = World();

        Enter(world);

        // The door cell ends at z = 4 m: the whole body must be past it, or the grate would close on the player.
        Assert.True(world.Rooms[1].IsSealed);
        Assert.True(world.Player.Position.Z - BodySizes.Player.Radius >= 2f * Level.CellSize);
    }

    [Fact]
    public void Standing_in_the_doorway_does_not_seal_the_room()
    {
        var world = World();
        Run(world, PressF, 1);

        // Walk only into the door cell (it reaches from z = 2 m to 4 m).
        Run(world, Forward, 180, () => world.Player.Position.Z > 3f);

        Assert.False(world.Rooms[1].IsEntered);
        Assert.False(world.Doors[DoorA].IsBarred);
    }

    [Fact]
    public void Once_cleared_the_door_one_came_through_opens_and_the_others_can_be_opened()
    {
        var world = World();
        Enter(world);

        KillEverything(world);
        var events = Run(world, PlayerInput.None, 1);

        Assert.Contains(new RoomCleared(1), events);
        Assert.True(world.Rooms[1].IsCleared);
        Assert.False(world.Rooms[1].IsSealed);
        Assert.True(world.Doors[DoorA].IsOpen);
        Assert.False(world.Doors[DoorA].IsBarred);
        Assert.False(world.Doors[DoorB].IsOpen);
        Assert.False(world.Doors[DoorB].IsBarred);
        Assert.True(world.Doors[DoorB].CanInteract);
        Assert.False(world.Level[2, 1].IsSolid);
        Assert.True(world.Level[2, 6].IsSolid);
    }

    [Fact]
    public void A_sealed_room_stays_sealed_while_an_enemy_lives()
    {
        var world = World();
        Enter(world);

        var events = Run(world, PlayerInput.None, 30);

        Assert.DoesNotContain(events, e => e is RoomCleared);
        Assert.True(world.Doors[DoorA].IsBarred);
    }

    [Fact]
    public void A_sleeping_bat_hangs_still_until_the_room_is_entered()
    {
        var world = World(mapText: BatMap);
        var bat = world.Bats[0];
        var start = bat.Position;

        var events = Run(world, PressF, 1);
        events.AddRange(Run(world, PlayerInput.None, 300));

        Assert.True(world.IsAsleep(bat));
        Assert.Equal(start, bat.Position);
        Assert.DoesNotContain(events, e => e is BatSpat);
    }

    [Fact]
    public void While_one_of_two_enemies_lives_the_room_stays_sealed()
    {
        var world = World(mapText: TwoGhoulsMap);
        Enter(world);

        world.Ghouls[0].TakeDamage(10_000, []);
        var events = Run(world, PlayerInput.None, 5);

        Assert.DoesNotContain(events, e => e is RoomCleared);
        Assert.True(world.Rooms[1].IsSealed);
        Assert.True(world.Doors[DoorA].IsBarred);
    }

    [Fact]
    public void A_barred_door_cannot_be_opened_with_F()
    {
        // A bat, not a ghoul, so the way to door B stays free.
        var world = World(mapText: BatMap);
        Enter(world);

        // Walk up to door B (its cell starts at z = 12 m) inside the sealed room.
        Run(world, Forward, 180, () => world.Player.Position.Z > 11f);
        Run(world, PressF, 1);

        Assert.True(world.Rooms[1].IsSealed);
        Assert.Null(world.InteractionTarget(world.Player));
        Assert.False(world.Doors[DoorB].IsOpen);
    }

    [Fact]
    public void A_cleared_room_does_not_seal_again()
    {
        var world = World();
        Enter(world);
        KillEverything(world);
        Run(world, PlayerInput.None, 1);

        // Out into the start room and back in.
        Run(world, Back, 240, () => world.Player.Position.Z < 1.5f);
        var events = Run(world, Forward, 240, () => world.Player.Position.Z > 7f);

        Assert.DoesNotContain(events, e => e is RoomSealed);
        Assert.DoesNotContain(events, e => e is RoomCleared);
        Assert.False(world.Doors[DoorA].IsBarred);
    }

    [Fact]
    public void A_combat_room_without_enemies_is_cleared_at_once_and_never_sealed()
    {
        var world = World(withGhoul: false);

        var events = Enter(world);

        Assert.DoesNotContain(events, e => e is RoomSealed);
        Assert.True(world.Rooms[1].IsCleared);
        Assert.False(world.Doors[DoorB].IsBarred);
    }

    [Fact]
    public void Hitting_a_sleeping_enemy_wakes_its_room_but_does_not_seal_it()
    {
        var world = World();
        Run(world, PressF, 1);

        // From the start room through the open door, aimed at the middle of the (low, crawling) ghoul.
        var body = world.Ghouls[0].Body;
        var direction = Vector3.Normalize(body.Position + new Vector3(0f, body.Height / 2f, 0f) - world.Player.EyePosition);
        world.Player.Look(PlayerInput.None with
        {
            Look = new Vector2(
                -(MathF.Atan2(direction.X, direction.Z) - world.Player.View.YawRadians) / ControlSettings.MouseSensitivity,
                -(MathF.Asin(direction.Y) - world.Player.View.PitchRadians) / ControlSettings.MouseSensitivity),
        });
        var events = Run(world, PlayerInput.None with { Shoot = true }, 1);

        Assert.Contains(events, e => e is GhoulHit);
        Assert.False(world.IsAsleep(world.Ghouls[0]));
        Assert.False(world.Rooms[1].IsSealed);
    }

    [Fact]
    public void Start_rooms_and_corridors_never_seal()
    {
        var world = World(withGhoul: false);
        Enter(world);
        Run(world, PlayerInput.None, 1);

        // Through room 1 and door B into the corridor.
        var events = Run(world, Forward, 60, () => world.Player.Position.Z > 11f);
        events.AddRange(Run(world, PressF, 1));
        events.AddRange(Run(world, Forward, 240, () => world.Rooms[2].IsEntered));

        Assert.True(world.Rooms[2].IsEntered);
        Assert.True(world.Rooms[0].IsEntered);
        Assert.DoesNotContain(events, e => e is RoomSealed);

        // Only arenas can be cleared - start and corridor are just "entered".
        Assert.False(world.Rooms[0].IsCleared);
        Assert.False(world.Rooms[2].IsCleared);
        Assert.DoesNotContain(events, e => e is RoomCleared { RoomIndex: 0 or 2 });
    }

    [Fact]
    public void A_new_run_forgets_all_rooms()
    {
        var world = World();
        Enter(world);

        var restarted = world.Restarted();

        Assert.False(restarted.Rooms[1].IsEntered);
        Assert.True(restarted.IsAsleep(restarted.Ghouls[0]));
        Assert.All(restarted.Doors, door => Assert.False(door.IsBarred));
    }

    [Fact]
    public void Without_rooms_every_enemy_is_awake()
    {
        var world = new GameWorld(LevelParser.Parse(Map));

        Assert.Empty(world.Rooms);
        Assert.False(world.IsAsleep(world.Ghouls[0]));
    }
}
