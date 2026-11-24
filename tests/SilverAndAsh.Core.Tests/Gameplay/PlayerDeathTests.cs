using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class PlayerDeathTests
{
    private static readonly int RestartTicks = SimulationClock.SecondsToTicks(Balance.Default.RestartDelay);

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

    /// <summary>Issue #718: on the test course health stops at 1 - every hit is still reported, none kills.</summary>
    [Fact]
    public void A_player_who_cannot_die_keeps_one_health_and_still_feels_each_hit()
    {
        var player = new Player(LevelParser.Parse("P")) { CannotDie = true };
        var events = new List<GameEvent>();
        var health = player.Health;

        player.TakeDamage(health - 10, events);
        player.TakeDamage(health, events);
        player.TakeDamage(health, events);

        Assert.False(player.IsDead);
        Assert.Equal(1, player.Health);
        Assert.Equal([new PlayerHit(player.Id, health - 10), new PlayerHit(player.Id, 9), new PlayerHit(player.Id, 0)], events);
    }

    /// <summary>
    /// Issue #722: "cannot die" is a rule of the world, asked at every hit - not a mark set on the players there at the
    /// start - so a player the world makes later follows it, too.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Whether_a_player_can_die_is_a_rule_of_the_world(bool cannotDie)
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.")) { PlayersCannotDie = cannotDie };
        var player = world.Player;

        player.TakeDamage(player.MaxHealth, []);
        player.TakeDamage(player.MaxHealth, []);
        world.Tick(PlayerInput.None);

        Assert.Equal(cannotDie, player.CannotDie);
        Assert.Equal(!cannotDie, player.IsDead);
        Assert.Equal(cannotDie ? 1 : 0, player.Health);
        Assert.Equal(!cannotDie, world.IsLost);
    }

    [Fact]
    public void A_dead_player_no_longer_moves_looks_or_attacks()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n."));
        world.Player.TakeDamage(Balance.Default.Player.MaxHealth, []);
        var position = world.Player.Position;
        var yaw = world.Player.View.YawRadians;

        world.Player.Look(Everything);
        for (var i = 0; i < 30; i++)
        {
            world.Tick(Everything with { Jump = i % 2 == 0, Melee = i % 2 == 0, Shoot = i % 2 == 0 });
            Assert.DoesNotContain(world.Events, e => e is MeleeSwung or ShotFired);
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

        world.Player.TakeDamage(Balance.Default.Player.MaxHealth, []);
        for (var i = 0; i < 10; i++)
        {
            world.Tick(melee);
            Assert.DoesNotContain(world.Events, e => e is MeleeSwung);
        }

        Assert.False(world.Player.MeleeCharge.IsCharging);
    }

    [Fact]
    public void Ghouls_leave_a_dead_player_alone()
    {
        var world = new GameWorld(LevelParser.Parse("P..G"));
        world.Player.TakeDamage(Balance.Default.Player.MaxHealth, []);

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
        world.Player.TakeDamage(Balance.Default.Player.MaxHealth, []);

        world.Tick(PlayerInput.None);

        Assert.Equal(GhoulState.Lurking, world.Ghouls[0].State);
    }

    [Fact]
    public void The_level_restarts_after_the_restart_delay()
    {
        var world = new GameWorld(LevelParser.Parse("P..G"));
        world.Player.TakeDamage(Balance.Default.Player.MaxHealth, []);

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
        world.Player.TakeDamage(Balance.Default.Player.MaxHealth, []);
        world.Ghouls[0].TakeDamage(Balance.Default.Ghoul.MaxHealth, []);
        world.Dummies[0].TakeDamage(30, []);

        var fresh = world.Restarted();

        Assert.NotSame(world, fresh);
        Assert.Same(world.Level, fresh.Level);
        Assert.Equal(Balance.Default.Player.MaxHealth, fresh.Player.Health);
        Assert.False(fresh.Player.IsDead);
        Assert.False(fresh.Ghouls[0].IsDead);
        Assert.Equal(Balance.Default.Dummy.MaxHealth, fresh.Dummies[0].Health);
        Assert.Equal(GhoulState.Lurking, fresh.Ghouls[0].State);
    }
}
