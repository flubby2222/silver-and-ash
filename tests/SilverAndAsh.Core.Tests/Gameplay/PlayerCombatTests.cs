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
    public void A_click_swings_the_blade_from_the_eyes_in_the_looking_direction()
    {
        // Since #71 the blade swings when the button is let go (holding it charges the blade).
        var player = RunningPlayer();

        Click(player);

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
        Click(player);

        player.Tick(PlayerInput.None);

        Assert.Empty(player.Events);
    }

    [Fact]
    public void Holding_the_button_attacks_only_once()
    {
        // Same rule as jumping: every attack needs a fresh press, even after the cooldown is over.
        // (The blade swings once when the button is finally let go.)
        var player = RunningPlayer();
        var swings = 0;
        var shots = 0;

        for (var i = 0; i < 3 * PistolTicks; i++)
        {
            player.Tick(Melee with { Shoot = true });
            swings += player.Events.OfType<BladeSwung>().Count();
            shots += player.Events.OfType<ShotFired>().Count();
        }

        player.Tick(PlayerInput.None);
        swings += player.Events.OfType<BladeSwung>().Count();

        Assert.Equal(1, swings);
        Assert.Equal(1, shots);
    }

    [Fact]
    public void A_press_during_the_cooldown_does_nothing()
    {
        var player = RunningPlayer();
        Click(player);

        player.Tick(Melee);                                 // fresh press, but still waiting
        player.Tick(PlayerInput.None);

        Assert.Empty(player.Events);
    }

    [Fact]
    public void After_the_cooldown_the_next_press_swings_again()
    {
        var player = RunningPlayer();
        Click(player);
        RunTicks(player, PlayerInput.None, BladeTicks - 1);

        Click(player);

        Assert.IsType<BladeSwung>(Assert.Single(player.Events));
    }

    [Fact]
    public void Blade_and_pistol_have_their_own_cooldowns()
    {
        var player = RunningPlayer();
        Click(player);

        player.Tick(Shoot);

        Assert.IsType<ShotFired>(Assert.Single(player.Events));
        Assert.Equal(PistolTicks, player.PistolCooldown.DurationTicks);
        Assert.Equal(BladeTicks, player.BladeCooldown.DurationTicks);
    }

    [Fact]
    public void Both_buttons_together_use_both_weapons()
    {
        // The pistol fires on the press, the blade swings when its button is let go.
        var player = RunningPlayer();

        player.Tick(Melee with { Shoot = true });
        Assert.IsType<ShotFired>(Assert.Single(player.Events));

        player.Tick(PlayerInput.None);
        Assert.IsType<BladeSwung>(Assert.Single(player.Events));
    }

    [Fact]
    public void Attacks_cost_no_stamina()
    {
        // A quick click of the blade (not charged) and a shot cost nothing.
        var player = RunningPlayer();

        player.Tick(Melee with { Shoot = true });
        player.Tick(PlayerInput.None);

        Assert.Equal(1f, player.Stamina.Fraction);
    }

    [Fact]
    public void Attacking_works_in_the_air_during_a_dash_and_during_a_dodge()
    {
        var jumping = RunningPlayer();
        jumping.Tick(ForwardJump);
        jumping.Tick(Forward with { Melee = true });
        jumping.Tick(Forward);
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
        dodging.Tick(ForwardDash);
        Assert.True(dodging.IsDodging);
        Assert.Single(dodging.Events);
    }

    /// <summary>A quick click of the blade button: pressed in one tick, let go in the next (the swing happens then).</summary>
    private static void Click(Player player)
    {
        player.Tick(Melee);
        player.Tick(PlayerInput.None);
    }
}
