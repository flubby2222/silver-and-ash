using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>The "Reload" hint under the crosshair (issue #259).</summary>
public class AmmoHintTests
{
    private static readonly RangedBalance Pistol = Balance.Default.Pistol;
    private static readonly int ReloadTicks = SimulationClock.SecondsToTicks(Pistol.ReloadTime);
    private static readonly int CooldownTicks = SimulationClock.SecondsToTicks(Pistol.Cooldown);

    private static Player NewPlayer() => new(LevelParser.Parse("P"));

    private static void Tick(Player player, PlayerInput input, int ticks = 1)
    {
        for (var tick = 0; tick < ticks; tick++)
        {
            player.Tick(input);
        }
    }

    /// <summary>Fires the weapon in the hand until its magazine is empty.</summary>
    private static void EmptyMagazine(Player player)
    {
        while (player.Ranged.Magazine.Rounds > 0)
        {
            Tick(player, PlayerInput.None with { Shoot = true });
            Tick(player, PlayerInput.None, CooldownTicks + 1);
        }
    }

    [Fact]
    public void A_full_magazine_brings_no_hint()
    {
        Assert.Equal(AmmoHint.None, NewPlayer().AmmoHint);
    }

    [Fact]
    public void Rounds_left_bring_no_hint()
    {
        var player = NewPlayer();
        Tick(player, PlayerInput.None with { Shoot = true });

        Assert.True(player.Ranged.Magazine.Rounds > 0);
        Assert.Equal(AmmoHint.None, player.AmmoHint);
    }

    [Fact]
    public void An_empty_magazine_asks_for_a_reload()
    {
        var player = NewPlayer();
        EmptyMagazine(player);

        Assert.Equal(AmmoHint.Reload, player.AmmoHint);
    }

    [Fact]
    public void The_hint_is_gone_while_reloading()
    {
        var player = NewPlayer();
        EmptyMagazine(player);

        Tick(player, PlayerInput.None with { Reload = true });

        Assert.True(player.Ranged.Magazine.IsReloading);
        Assert.Equal(AmmoHint.None, player.AmmoHint);
    }

    [Fact]
    public void The_hint_stays_gone_after_the_reload()
    {
        var player = NewPlayer();
        EmptyMagazine(player);

        Tick(player, PlayerInput.None with { Reload = true });
        Tick(player, PlayerInput.None, ReloadTicks + 1);

        Assert.Equal(Pistol.MagazineSize, player.Ranged.Magazine.Rounds);
        Assert.Equal(AmmoHint.None, player.AmmoHint);
    }

    [Fact]
    public void Switching_to_a_loaded_weapon_takes_the_hint_away()
    {
        var player = NewPlayer();
        player.Equip(1, WeaponKind.Pistol);
        EmptyMagazine(player);

        Tick(player, PlayerInput.None with { SelectSlot = 1 });

        Assert.Equal(1, player.SelectedSlot);
        Assert.Equal(AmmoHint.None, player.AmmoHint);
    }

    [Fact]
    public void Switching_back_to_the_empty_weapon_brings_the_hint_again()
    {
        var player = NewPlayer();
        player.Equip(1, WeaponKind.Pistol);
        EmptyMagazine(player);
        Tick(player, PlayerInput.None with { SelectSlot = 1 });

        Tick(player, PlayerInput.None with { SelectSlot = 0 });

        Assert.Equal(AmmoHint.Reload, player.AmmoHint);
    }

    [Fact]
    public void The_blade_never_brings_a_hint()
    {
        var player = NewPlayer();

        // Charge and swing the blade a few times: it has no magazine, the pistol's stays full.
        for (var swing = 0; swing < 3; swing++)
        {
            Tick(player, PlayerInput.None with { Melee = true }, 10);
            Tick(player, PlayerInput.None);
            Assert.Equal(AmmoHint.None, player.AmmoHint);
            Tick(player, PlayerInput.None, SimulationClock.SecondsToTicks(Balance.Default.Melee(WeaponKind.SilverBlade).Cooldown) + 1);
        }

        Assert.Equal(AmmoHint.None, player.AmmoHint);
    }
}
