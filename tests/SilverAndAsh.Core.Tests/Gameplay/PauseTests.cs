using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class PauseTests
{
    // A ghoul 6 m in front of the player, awake (hand-made level): without a pause it would come at once.
    private const string Hall = "#P#\n#.#\n#.#\n#G#";

    private static readonly PlayerInput Forward = new(new Vector2(0f, 1f), Vector2.Zero, false);

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
    public void A_paused_world_stands_still()
    {
        var world = new GameWorld(LevelParser.Parse(Hall));
        var player = world.Player.Position;
        var ghoul = world.Ghouls[0].Position;

        var events = Run(world, Forward with { Paused = true, Shoot = true }, 600);

        Assert.Equal(player, world.Player.Position);
        Assert.Equal(ghoul, world.Ghouls[0].Position);
        Assert.Empty(events);
        Assert.Equal(world.Player.MaxHealth, world.Player.Health);
    }

    [Fact]
    public void After_the_pause_the_world_goes_on()
    {
        var world = new GameWorld(LevelParser.Parse(Hall));
        var start = world.Player.Position;
        Run(world, Forward with { Paused = true }, 60);

        Run(world, Forward, 30);

        Assert.True(world.Player.Position.Z > start.Z);
    }

    [Fact]
    public void Doors_do_not_open_during_the_pause()
    {
        var world = new GameWorld(LevelParser.Parse("#P#\n#.#\n#.#"), 1, Balance.Default, [new LevelDoor(1, 1, 0, 1, RoomSide.South)]);

        Run(world, PlayerInput.None with { Paused = true, Interact = true }, 1);

        Assert.False(world.Doors[0].IsOpen);
    }

    [Fact]
    public void The_restart_delay_waits_during_the_pause()
    {
        var world = new GameWorld(LevelParser.Parse(Hall));
        world.Player.TakeDamage(world.Player.MaxHealth, []);

        Run(world, PlayerInput.None with { Paused = true }, 60 * 60);

        Assert.False(world.ShouldRestart);
    }

    [Fact]
    public void Events_of_the_last_tick_are_gone_after_a_paused_tick()
    {
        var world = new GameWorld(LevelParser.Parse(Hall));
        world.Tick(PlayerInput.None with { Shoot = true });
        Assert.NotEmpty(world.Events);

        world.Tick(PlayerInput.None with { Paused = true });

        Assert.Empty(world.Events);
    }

    [Fact]
    public void Turning_follows_the_mouse_sensitivity()
    {
        var normal = new GameWorld(LevelParser.Parse(Hall)).Player;
        var fast = new GameWorld(LevelParser.Parse(Hall)).Player;
        var mouse = PlayerInput.None with { Look = new Vector2(100f, 0f) };

        normal.Look(mouse);
        fast.Look(mouse, sensitivity: 2f);

        Assert.Equal(2f * normal.View.YawRadians, fast.View.YawRadians, 0.0001f);
    }
}
