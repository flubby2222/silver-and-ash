using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class DoorTests
{
    // The player starts in cell (1,0) looking along +Z (down the map). A door in cell (1,1) is 1 m in front
    // of the eyes - within reach. A door in cell (1,2) is 3 m away - out of reach.
    private const string Hall = "#P#\n#.#\n#.#\n#.#\n#.#";

    private static readonly PlayerInput Forward = new(new Vector2(0f, 1f), Vector2.Zero, false);

    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };

    private static GameWorld World(string map, params (int X, int Z)[] doors) =>
        new(LevelParser.Parse(map), 1, Balance.Default,
            doors.Select(door => new LevelDoor(door.X, door.Z, 0, 1, RoomSide.South)).ToList());

    private static List<GameEvent> Run(GameWorld world, PlayerInput input, int ticks)
    {
        var events = new List<GameEvent>();
        for (var i = 0; i < ticks; i++)
        {
            world.Tick(input);
            events.AddRange(world.Events);
        }

        return events;
    }

    [Fact]
    public void Doors_start_closed()
    {
        var world = World(Hall, (1, 1));

        var door = Assert.Single(world.Doors);
        Assert.False(door.IsOpen);
        Assert.Equal(DoorKind.Normal, door.Kind);
        Assert.True(world.Level[1, 1].IsSolid);
    }

    [Fact]
    public void A_closed_door_blocks_walking()
    {
        var world = World(Hall, (1, 1));

        Run(world, Forward, 120);

        Assert.True(world.Player.Position.Z < 2f, $"The player walked through the door to z = {world.Player.Position.Z}.");
    }

    [Fact]
    public void F_opens_the_door_in_front_and_it_can_be_walked_through()
    {
        var world = World(Hall, (1, 1));

        var events = Run(world, PressF, 1);
        Run(world, Forward, 120);

        Assert.True(world.Doors[0].IsOpen);
        Assert.Contains(new DoorOpened(world.Doors[0].Id), events);
        Assert.False(world.Level[1, 1].IsSolid);
        Assert.True(world.Player.Position.Z > 4f, $"The player only got to z = {world.Player.Position.Z}.");
    }

    [Fact]
    public void An_open_door_stays_open()
    {
        var world = World(Hall, (1, 1));

        Run(world, PressF, 1);
        Run(world, PressF, 5);
        Run(world, Forward, 600);

        Assert.True(world.Doors[0].IsOpen);
        Assert.False(world.Level[1, 1].IsSolid);
    }

    [Fact]
    public void A_door_out_of_reach_does_not_open()
    {
        var world = World(Hall, (1, 2));

        Run(world, PressF, 1);

        Assert.False(world.Doors[0].IsOpen);
        Assert.Null(world.InteractionTarget(world.Player));
    }

    [Fact]
    public void A_door_behind_the_player_does_not_open()
    {
        var world = World(Hall, (1, 1));
        world.Player.Look(PlayerInput.None with { Look = new Vector2(MathF.PI / ControlSettings.MouseSensitivity, 0f) });

        Run(world, PressF, 1);

        Assert.False(world.Doors[0].IsOpen);
    }

    [Fact]
    public void Without_F_nothing_opens()
    {
        var world = World(Hall, (1, 1));

        Run(world, PlayerInput.None, 60);

        Assert.False(world.Doors[0].IsOpen);
    }

    [Fact]
    public void The_door_in_front_is_offered_with_its_prompt()
    {
        var world = World(Hall, (1, 1));

        var target = world.InteractionTarget(world.Player);

        Assert.Same(world.Doors[0], target);
        Assert.Equal("Open", target!.Prompt);
    }

    [Fact]
    public void An_open_door_is_not_offered_any_more()
    {
        var world = World(Hall, (1, 1));

        Run(world, PressF, 1);

        Assert.Null(world.InteractionTarget(world.Player));
    }

    [Fact]
    public void Of_two_doors_in_a_row_only_the_near_one_opens()
    {
        // Looking down the hall, the ray passes the near door first - the one behind it is out of reach anyway,
        // but the near one must also hide it.
        var world = World("#P#\n#.#\n#.#\n#.#", (1, 1), (1, 2));

        Run(world, PressF, 1);

        Assert.True(world.Doors[0].IsOpen);
        Assert.False(world.Doors[1].IsOpen);
    }

    // The arena grate is no kind of door any more but a state of every door (IsBarred) - see ArenaTests.
    [Theory]
    [InlineData(DoorKind.Locked)]
    public void Locked_doors_do_not_open_with_F(DoorKind kind)
    {
        var world = new GameWorld(LevelParser.Parse(Hall), 1, Balance.Default, [new LevelDoor(1, 1, 0, 1, RoomSide.South, kind)]);

        Run(world, PressF, 1);

        Assert.Null(world.InteractionTarget(world.Player));
        Assert.False(world.Doors[0].IsOpen);
    }

    [Fact]
    public void A_door_behind_a_corner_of_wall_is_not_offered()
    {
        // The player looks diagonally past the wall at cell (2,0): the ray hits that wall after 1.25 m and would
        // only reach the door at (2,1) after about 1.7 m - within reach, but hidden.
        var world = World("#P##\n#..#\n####", (2, 1));
        var yaw = MathF.Atan2(0.8f, 0.6f);
        world.Player.Look(PlayerInput.None with { Look = new Vector2(-yaw / ControlSettings.MouseSensitivity, 0f) });

        Assert.Null(world.InteractionTarget(world.Player));
    }

    [Fact]
    public void A_dead_player_cannot_open_doors()
    {
        var world = World(Hall, (1, 1));
        world.Player.TakeDamage(world.Player.MaxHealth, []);

        Run(world, PressF, 1);

        Assert.False(world.Doors[0].IsOpen);
    }

    [Fact]
    public void A_closed_door_stops_bullets()
    {
        var world = World("#P#\n#.#\n#D#", (1, 1));

        var events = Run(world, PlayerInput.None with { Shoot = true }, 1);

        Assert.Contains(events, e => e is ShotFired);
        Assert.DoesNotContain(events, e => e is DummyHit);
    }

    [Fact]
    public void Through_an_open_door_bullets_hit()
    {
        var world = World("#P#\n#.#\n#D#", (1, 1));
        Run(world, PressF, 1);

        var events = Run(world, PlayerInput.None with { Shoot = true }, 1);

        Assert.Contains(events, e => e is DummyHit);
    }

    [Fact]
    public void Enemies_find_no_way_through_a_closed_door()
    {
        var world = World(Hall, (1, 2));

        Assert.Null(Pathfinder.FindPath(world.Level, (1, 0), (1, 4)));
    }

    [Fact]
    public void A_ghoul_behind_a_closed_door_never_gets_the_player()
    {
        var world = World(Hall.Replace("#.#\n#.#\n#.#\n#.#", "#.#\n#.#\n#.#\n#G#"), (1, 1));

        var events = Run(world, PlayerInput.None, 600);

        Assert.DoesNotContain(events, e => e is PlayerHit);
        Assert.Equal(world.Player.MaxHealth, world.Player.Health);
    }

    [Fact]
    public void Through_an_open_door_the_ghoul_comes()
    {
        var world = World(Hall.Replace("#.#\n#.#\n#.#\n#.#", "#.#\n#.#\n#.#\n#G#"), (1, 1));
        Run(world, PressF, 1);

        var events = Run(world, PlayerInput.None, 600);

        Assert.Contains(events, e => e is PlayerHit);
    }

    [Fact]
    public void A_new_run_starts_with_all_doors_closed()
    {
        var world = World(Hall, (1, 1));
        Run(world, PressF, 1);

        var restarted = world.Restarted();

        Assert.False(Assert.Single(restarted.Doors).IsOpen);
        Assert.True(restarted.Level[1, 1].IsSolid);
    }

    [Fact]
    public void A_run_with_closed_doors_can_be_started_again()
    {
        var world = World(Hall, (1, 1), (1, 3));
        Run(world, PressF, 1);

        var restarted = world.Restarted().Restarted();

        Assert.Equal(2, restarted.Doors.Count);
        Assert.All(restarted.Doors, door => Assert.False(door.IsOpen));
    }

    [Fact]
    public void A_door_must_lie_on_a_walkable_cell()
    {
        Assert.Throws<ArgumentException>(() => World(Hall, (0, 1)));
        Assert.Throws<ArgumentException>(() => World(Hall, (5, 1)));
    }

    [Fact]
    public void A_level_without_doors_has_none()
    {
        var world = new GameWorld(LevelParser.Parse(Hall));

        Assert.Empty(world.Doors);
        Assert.Null(world.InteractionTarget(world.Player));
    }
}
