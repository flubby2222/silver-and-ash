using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class PlayerDeathTests
{
    private static readonly int RestartTicks = SimulationClock.SecondsToTicks(Tuning.Combat.RestartDelay);

    private static readonly PlayerInput Everything = new(
        new Vector2(0f, 1f), new Vector2(50f, 20f), Jump: true, Dash: true, Melee: true, Shoot: true);

    [Fact]
    public void At_zero_health_the_player_dies_once()
    {
        var player = new Player(LevelParser.Parse("P"));
        var events = new List<GameEvent>();

        player.TakeDamage(60, events);
        player.TakeDamage(60, events);
        player.TakeDamage(60, events);                      // already dead: ignored

        Assert.True(player.IsDead);
        Assert.Equal([new PlayerHit(player.Id, 60), new PlayerHit(player.Id, 40), new PlayerDied(player.Id)], events);
    }

    [Fact]
    public void A_dead_player_no_longer_moves_looks_or_attacks()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n."));
        world.Player.TakeDamage(Tuning.Combat.PlayerMaxHealth, []);
        var position = world.Player.Position;
        var yaw = world.Player.View.YawRadians;

        world.Player.Look(Everything);
        for (var i = 0; i < 30; i++)
        {
            world.Tick(Everything with { Jump = i % 2 == 0, Melee = i % 2 == 0, Shoot = i % 2 == 0 });
            Assert.DoesNotContain(world.Events, e => e is BladeSwung or ShotFired);
        }

        Assert.Equal(position, world.Player.Position);
        Assert.Equal(yaw, world.Player.View.YawRadians);
    }

    [Fact]
    public void Dying_while_charging_the_blade_does_not_swing_it()
    {
        // Dead, the input counts as "nothing held" - that must not be taken as letting go of a charged blade.
        var world = new GameWorld(LevelParser.Parse("P\n.\n."));
        var melee = PlayerInput.None with { Melee = true };
        for (var i = 0; i < 30; i++)
        {
            world.Tick(melee);
        }

        world.Player.TakeDamage(Tuning.Combat.PlayerMaxHealth, []);
        for (var i = 0; i < 10; i++)
        {
            world.Tick(melee);
            Assert.DoesNotContain(world.Events, e => e is BladeSwung);
        }

        Assert.False(world.Player.BladeCharge.IsCharging);
    }

    [Fact]
    public void Ghouls_leave_a_dead_player_alone()
    {
        var world = new GameWorld(LevelParser.Parse("P..G"));
        world.Player.TakeDamage(Tuning.Combat.PlayerMaxHealth, []);

        for (var i = 0; i < 3 * SimulationClock.TicksPerSecond; i++)
        {
            world.Tick(PlayerInput.None);
            Assert.DoesNotContain(world.Events, e => e is GhoulWindUp or PlayerHit);
        }
    }

    [Fact]
    public void A_ghoul_in_the_middle_of_a_blow_lets_go_when_the_player_dies()
    {
        // Found by the mutation check: the test above kills the player before the ghoul ever chose them.
        var world = new GameWorld(LevelParser.Parse("P..G"));
        for (var i = 0; i < 3 * SimulationClock.TicksPerSecond && !world.Events.Any(e => e is GhoulWindUp); i++)
        {
            world.Tick(PlayerInput.None);
        }

        Assert.Equal(GhoulState.WindingUp, world.Ghouls[0].State);
        world.Player.TakeDamage(Tuning.Combat.PlayerMaxHealth, []);

        world.Tick(PlayerInput.None);

        Assert.Equal(GhoulState.Lurking, world.Ghouls[0].State);
    }

    [Fact]
    public void The_level_restarts_after_the_restart_delay()
    {
        var world = new GameWorld(LevelParser.Parse("P..G"));
        world.Player.TakeDamage(Tuning.Combat.PlayerMaxHealth, []);

        for (var tick = 1; tick < RestartTicks; tick++)
        {
            world.Tick(PlayerInput.None);
            Assert.False(world.ShouldRestart, $"Restart after only {tick} ticks.");
        }

        world.Tick(PlayerInput.None);
        Assert.True(world.ShouldRestart);
    }

    [Fact]
    public void A_living_player_never_restarts_the_level()
    {
        var world = new GameWorld(LevelParser.Parse("P......"));

        for (var i = 0; i < 2 * RestartTicks; i++)
        {
            world.Tick(PlayerInput.None);
        }

        Assert.False(world.ShouldRestart);
    }

    [Fact]
    public void A_restarted_world_starts_from_scratch()
    {
        var world = new GameWorld(LevelParser.Parse("P.D\n..G"));
        world.Player.TakeDamage(Tuning.Combat.PlayerMaxHealth, []);
        world.Ghouls[0].TakeDamage(Tuning.Ghoul.MaxHealth, []);
        world.Dummies[0].TakeDamage(30, []);

        var fresh = world.Restarted();

        Assert.NotSame(world, fresh);
        Assert.Same(world.Level, fresh.Level);
        Assert.Equal(Tuning.Combat.PlayerMaxHealth, fresh.Player.Health);
        Assert.False(fresh.Player.IsDead);
        Assert.False(fresh.Ghouls[0].IsDead);
        Assert.Equal(Tuning.Combat.DummyMaxHealth, fresh.Dummies[0].Health);
        Assert.Equal(GhoulState.Lurking, fresh.Ghouls[0].State);
    }
}
