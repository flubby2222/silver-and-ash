using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class CrackedWallTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly PlayerInput Melee = PlayerInput.None with { Melee = true };
    private static readonly PlayerInput Shoot = PlayerInput.None with { Shoot = true };
    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };
    private static readonly int FullCharge = SimulationClock.SecondsToTicks(Balance.Default.Blade.ChargeTime);

    // Room 0 (start) in row 0, the cracked wall in cell (2, 1) - 1 m in front of the player's eyes - and room 1
    // (the secret room) in rows 2-3. With 'far' the wall sits in row 2 instead, 3 m away.
    private static GameWorld World(bool far = false)
    {
        var map = far ? "#.P.#\n#...#\n##.##\n#...#\n#...#" : "#.P.#\n##.##\n#...#\n#...#";
        var wallRow = far ? 2 : 1;
        static List<(int X, int Z)> Row(int z) => [(1, z), (2, z), (3, z)];
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 5, wallRow, 0, Enumerable.Range(0, wallRow).SelectMany(Row).ToList(), [], []),
            new(1, RoomKind.Secret, "nook", 0, wallRow + 1, 5, 2, 1, Row(wallRow + 1).Concat(Row(wallRow + 2)).ToList(), [], []),
        ];
        LevelDoor[] doors = [new(2, wallRow, 0, 1, RoomSide.South, DoorKind.Cracked)];
        return new GameWorld(LevelParser.Parse(map), 1, Balance.Default, doors, rooms);
    }

    /// <summary>Holds the blade for <paramref name="ticks"/> ticks and lets go; returns everything that happened.</summary>
    private static List<GameEvent> Swing(GameWorld world, int ticks)
    {
        var events = Run(world, Melee, ticks);
        events.AddRange(Run(world, PlayerInput.None, Second));
        return events;
    }

    [Fact]
    public void An_unbroken_cracked_wall_is_wall_in_every_way()
    {
        var world = World();
        var wall = world.Doors[0];

        Run(world, Forward, Second);

        Assert.True(world.Level[2, 1].IsSolid);
        Assert.True(world.LevelAsBuilt[2, 1].IsSolid);
        Assert.True(world.Player.Position.Z < 2f, $"The player walked into the wall to z = {world.Player.Position.Z}.");
        Assert.False(wall.CanInteract);
        Assert.Null(world.InteractionTarget(world.Player));
    }

    [Fact]
    public void F_does_not_open_it()
    {
        var world = World();

        Run(world, PressF, 5);

        Assert.False(world.Doors[0].IsOpen);
    }

    [Fact]
    public void Ordinary_and_half_charged_swings_and_shots_leave_it_whole()
    {
        var world = World();

        var events = Swing(world, 1);
        events.AddRange(Swing(world, FullCharge / 2));
        events.AddRange(Run(world, Shoot, Second));

        Assert.Contains(events, e => e is BladeSwung);
        Assert.Contains(events, e => e is ShotFired);
        Assert.False(world.Doors[0].IsOpen);
        Assert.DoesNotContain(events, e => e is WallBroken);
    }

    [Fact]
    public void A_fully_charged_blade_strike_breaks_it_open()
    {
        var world = World();
        var builtBefore = world.LevelAsBuilt;

        var events = Swing(world, FullCharge + 5);
        Run(world, Forward, 2 * Second);

        var wall = world.Doors[0];
        Assert.True(wall.IsOpen);
        Assert.Contains(new WallBroken(wall.Id, world.Player.Id), events);
        Assert.False(world.Level[2, 1].IsSolid);
        Assert.NotSame(builtBefore, world.LevelAsBuilt);
        Assert.False(world.LevelAsBuilt[2, 1].IsSolid);
        Assert.True(world.Player.Position.Z > 4f, $"The player only got to z = {world.Player.Position.Z}.");
    }

    [Fact]
    public void A_charged_strike_out_of_the_blades_reach_does_nothing()
    {
        var world = World(far: true);

        var events = Swing(world, FullCharge + 5);

        Assert.False(world.Doors[0].IsOpen);
        Assert.DoesNotContain(events, e => e is WallBroken);
    }

    [Fact]
    public void A_charged_strike_in_another_direction_does_nothing()
    {
        var world = World();
        world.Player.Look(PlayerInput.None with { Look = new Vector2(MathF.PI / ControlSettings.MouseSensitivity, 0f) });

        Swing(world, FullCharge + 5);

        Assert.False(world.Doors[0].IsOpen);
    }

    [Fact]
    public void The_map_shows_the_wall_only_once_it_is_broken()
    {
        var world = World();
        Run(world, PlayerInput.None, 1);
        Assert.True(world.Rooms[0].IsDiscovered);

        Assert.False(world.IsKnown(world.Doors[0]));

        Swing(world, FullCharge + 5);

        Assert.True(world.IsKnown(world.Doors[0]));
    }

    [Fact]
    public void A_new_try_brings_the_wall_back()
    {
        var world = World();
        Swing(world, FullCharge + 5);

        var restarted = world.Restarted();

        Assert.False(restarted.Doors[0].IsOpen);
        Assert.True(restarted.Level[2, 1].IsSolid);
    }

    [Fact]
    public void An_arena_never_puts_a_grate_on_its_cracked_wall()
    {
        // Room 0 (start) row 0, door A (2,1), room 1 (combat, one ghoul) rows 2-5, cracked wall (2,6), room 2 (secret) rows 7-8.
        const string map = "#.P.#\n##.##\n#...#\n#...#\n#.G.#\n#...#\n##.##\n#...#\n#...#";
        static List<(int X, int Z)> Rows(int from, int to) =>
            Enumerable.Range(from, to - from + 1).SelectMany(z => new[] { (1, z), (2, z), (3, z) }).ToList();
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 5, 1, 0, Rows(0, 0), [], []),
            new(1, RoomKind.Combat, "arena", 0, 2, 5, 4, 1, Rows(2, 5), [(2, 4)], []),
            new(2, RoomKind.Secret, "nook", 0, 7, 5, 2, 2, Rows(7, 8), [], []),
        ];
        LevelDoor[] doors = [new(2, 1, 0, 1, RoomSide.South), new(2, 6, 1, 2, RoomSide.South, DoorKind.Cracked)];
        var world = new GameWorld(LevelParser.Parse(map), 1, Balance.Default, doors, rooms);

        Run(world, PressF, 1);
        Run(world, Forward, 3 * Second, () => world.Rooms[1].IsSealed);

        Assert.True(world.Rooms[1].IsSealed, "The arena never sealed - the test would prove nothing.");
        Assert.True(world.Doors[0].IsBarred);
        Assert.False(world.Doors[1].IsBarred);
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
