using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using static SilverAndAsh.Core.Tests.Gameplay.JumpTestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class PlayerCombatTests
{
    private static readonly PlayerInput Melee = PlayerInput.None with { Melee = true };
    private static readonly PlayerInput Shoot = PlayerInput.None with { Shoot = true };

    private static readonly int BladeTicks = SimulationClock.SecondsToTicks(Tuning.Combat.BladeCooldown);
    private static readonly int PistolTicks = SimulationClock.SecondsToTicks(Tuning.Combat.PistolCooldown);

    [Fact]
    public void The_player_starts_with_full_health()
    {
        Assert.Equal(Tuning.Combat.PlayerMaxHealth, RunningPlayer().Health);
    }

    [Fact]
    public void Pressing_melee_swings_the_blade_from_the_eyes_in_the_looking_direction()
    {
        var player = RunningPlayer();

        player.Tick(Melee);

        var swing = Assert.IsType<BladeSwung>(Assert.Single(player.Events));
        Assert.Equal(player.EyePosition, swing.Origin);
        Assert.Equal(player.View.Forward, swing.Direction);
        Assert.False(player.BladeCooldown.IsReady);
    }

    [Fact]
    public void Pressing_shoot_fires_the_pistol()
    {
        var player = RunningPlayer();

        player.Tick(Shoot);

        Assert.IsType<ShotFired>(Assert.Single(player.Events));
        Assert.False(player.PistolCooldown.IsReady);
    }

    [Fact]
    public void Events_are_reported_only_in_the_tick_they_happen()
    {
        var player = RunningPlayer();
        player.Tick(Melee);

        player.Tick(PlayerInput.None);

        Assert.Empty(player.Events);
    }

    [Fact]
    public void Holding_the_button_attacks_only_once()
    {
        // Same rule as jumping: every attack needs a fresh press, even after the cooldown is over.
        var player = RunningPlayer();
        var swings = 0;
        var shots = 0;

        for (var i = 0; i < 3 * PistolTicks; i++)
        {
            player.Tick(Melee with { Shoot = true });
            swings += player.Events.OfType<BladeSwung>().Count();
            shots += player.Events.OfType<ShotFired>().Count();
        }

        Assert.Equal(1, swings);
        Assert.Equal(1, shots);
    }

    [Fact]
    public void A_press_during_the_cooldown_does_nothing()
    {
        var player = RunningPlayer();
        player.Tick(Melee);
        player.Tick(PlayerInput.None);

        player.Tick(Melee);                                 // fresh press, but still waiting

        Assert.Empty(player.Events);
    }

    [Fact]
    public void After_the_cooldown_the_next_press_swings_again()
    {
        var player = RunningPlayer();
        player.Tick(Melee);
        RunTicks(player, PlayerInput.None, BladeTicks - 1);

        player.Tick(Melee);

        Assert.IsType<BladeSwung>(Assert.Single(player.Events));
    }

    [Fact]
    public void Blade_and_pistol_have_their_own_cooldowns()
    {
        var player = RunningPlayer();
        player.Tick(Melee);

        player.Tick(Shoot);

        Assert.IsType<ShotFired>(Assert.Single(player.Events));
        Assert.Equal(PistolTicks, player.PistolCooldown.DurationTicks);
        Assert.Equal(BladeTicks, player.BladeCooldown.DurationTicks);
    }

    [Fact]
    public void Both_weapons_in_the_same_tick_both_attack()
    {
        var player = RunningPlayer();

        player.Tick(Melee with { Shoot = true });

        Assert.Equal(2, player.Events.Count);
        Assert.Contains(player.Events, e => e is BladeSwung);
        Assert.Contains(player.Events, e => e is ShotFired);
    }

    [Fact]
    public void Attacks_cost_no_stamina()
    {
        var player = RunningPlayer();

        player.Tick(Melee with { Shoot = true });

        Assert.Equal(1f, player.Stamina.Fraction);
    }

    [Fact]
    public void Attacking_works_in_the_air_during_a_dash_and_during_a_dodge()
    {
        var jumping = RunningPlayer();
        jumping.Tick(ForwardJump);
        jumping.Tick(Forward with { Melee = true });
        Assert.False(jumping.IsGrounded);
        Assert.Single(jumping.Events);

        var dashing = RunningPlayer();
        dashing.Tick(ForwardJump);
        RunTicks(dashing, Forward, 5);
        dashing.Tick(ForwardDash);
        dashing.Tick(ForwardDash with { Shoot = true });
        Assert.True(dashing.IsDashing);
        Assert.Single(dashing.Events);

        var dodging = RunningPlayer();
        dodging.Tick(ForwardDash);
        dodging.Tick(ForwardDash with { Melee = true });
        Assert.True(dodging.IsDodging);
        Assert.Single(dodging.Events);
    }
}
