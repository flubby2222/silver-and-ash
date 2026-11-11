using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class GameWorldTests
{
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };

    // Player in row 0, a dummy two rows ahead (forward = +Z = the next rows), free space behind it.
    private static readonly Level Corridor = LevelParser.Parse("P\n.\nD\n.\n.\n.");

    [Fact]
    public void The_world_puts_a_dummy_on_every_D_cell()
    {
        var world = new GameWorld(LevelParser.Parse("P.D\n.D."));

        Assert.Equal(2, world.Dummies.Count);
        Assert.Equal([0, 1], world.Dummies.Select(d => d.Id));
        Assert.Equal(world.Level.CellCenter(2, 0), world.Dummies[0].Position);
        Assert.Equal(world.Level.CellCenter(1, 1), world.Dummies[1].Position);
    }

    [Fact]
    public void The_player_cannot_walk_through_a_dummy()
    {
        var world = new GameWorld(Corridor);
        var dummy = world.Dummies[0];

        RunTicks(world, Forward, 3 * SimulationClock.TicksPerSecond);

        var closest = dummy.Position.Z - Tuning.Combat.DummyRadius - Tuning.Player.Radius;
        Assert.InRange(world.Player.Position.Z, closest - 0.05f, closest + 0.01f);

        // Standing still at the dummy - like at a wall, no speed "into" it is left over.
        Assert.True(world.Player.HorizontalSpeed < 0.1f, $"Still {world.Player.HorizontalSpeed:0.00} m/s into the dummy.");
    }

    [Fact]
    public void A_dummy_on_the_ground_does_not_block_the_way()
    {
        var world = new GameWorld(Corridor);
        world.Dummies[0].TakeDamage(Tuning.Combat.DummyMaxHealth, []);

        RunTicks(world, Forward, SimulationClock.TicksPerSecond);

        Assert.True(world.Player.Position.Z > world.Dummies[0].Position.Z, "Stopped at a dummy lying on the ground.");
    }

    [Fact]
    public void Walking_into_a_dummy_at_an_angle_slides_past_it()
    {
        // Like a wall: only the "into the dummy" part of the movement is stopped.
        var world = new GameWorld(LevelParser.Parse("...\n.P.\n...\n.D.\n...\n..."));
        var start = world.Player.Position;
        var forwardRight = PlayerInput.None with { Move = new Vector2(0.15f, 1f) };

        RunTicks(world, forwardRight, 2 * SimulationClock.TicksPerSecond);

        Assert.True(world.Player.Position.Z > world.Dummies[0].Position.Z, $"Got stuck at the dummy instead of sliding past: {world.Player.Position}.");
        var sideways = Vector3.Dot(world.Player.Position - start, world.Player.View.FlatRight);
        Assert.True(sideways > 0.5f, $"Did not move to the right: {world.Player.Position}, start {start}.");
    }

    [Fact]
    public void The_world_reports_the_events_of_player_and_dummies()
    {
        var world = new GameWorld(Corridor);
        world.Dummies[0].TakeDamage(Tuning.Combat.DummyMaxHealth, []);

        var seen = new List<GameEvent>();
        world.Tick(PlayerInput.None with { Melee = true });
        seen.AddRange(world.Events);
        for (var i = 0; i < 4 * SimulationClock.TicksPerSecond; i++)
        {
            world.Tick(PlayerInput.None);
            seen.AddRange(world.Events);
        }

        Assert.Contains(seen, e => e is BladeSwung);
        Assert.Contains(new DummyStoodUp(0), seen);
    }

    [Fact]
    public void Events_of_a_tick_are_gone_in_the_next_tick()
    {
        var world = new GameWorld(Corridor);
        world.Tick(PlayerInput.None with { Melee = true });

        world.Tick(PlayerInput.None);

        Assert.Empty(world.Events);
    }

    private static void RunTicks(GameWorld world, PlayerInput input, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            world.Tick(input);
        }
    }
}
