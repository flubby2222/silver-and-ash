using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>Weapons as data, one melee and three ranged places, switching (decision record 0008, issue #189).</summary>
public class WeaponSlotTests
{
    private static readonly int CooldownTicks = SimulationClock.SecondsToTicks(Balance.Default.Pistol.Cooldown);

    private static Player NewPlayer() => new(LevelParser.Parse("P"));

    private static List<GameEvent> Tick(Player player, PlayerInput input, int ticks = 1)
    {
        var events = new List<GameEvent>();
        for (var tick = 0; tick < ticks; tick++)
        {
            player.Tick(input);
            events.AddRange(player.Events);
        }

        return events;
    }

    private static List<GameEvent> Select(Player player, int slot) => Tick(player, PlayerInput.None with { SelectSlot = slot });

    private static List<GameEvent> Wheel(Player player, int step) => Tick(player, PlayerInput.None with { CycleSlot = step });

    /// <summary>Fires once and waits until the weapon is ready again.</summary>
    private static void Fire(Player player)
    {
        Tick(player, PlayerInput.None with { Shoot = true });
        Tick(player, PlayerInput.None, CooldownTicks + 1);
    }

    [Fact]
    public void A_run_starts_with_the_silver_blade_and_the_pistol_in_the_first_place()
    {
        var player = NewPlayer();

        Assert.Equal(WeaponKind.SilverBlade, player.Melee);
        Assert.Equal(Player.RangedSlotCount, player.RangedSlots.Count);
        Assert.Equal(WeaponKind.Pistol, player.RangedSlots[0]!.Kind);
        Assert.Null(player.RangedSlots[1]);
        Assert.Null(player.RangedSlots[2]);
        Assert.Equal(0, player.SelectedSlot);
        Assert.Same(player.RangedSlots[0], player.Ranged);
    }

    [Fact]
    public void Every_weapon_knows_its_name_hand_and_tier()
    {
        Assert.Equal(new WeaponInfo("Silver Blade", WeaponHand.Melee, ItemTier.Worn), Weapons.Info(WeaponKind.SilverBlade));
        Assert.Equal(new WeaponInfo("Pistol", WeaponHand.Ranged, ItemTier.Worn), Weapons.Info(WeaponKind.Pistol));
        Assert.All(Enum.GetValues<WeaponKind>(), kind => Assert.False(string.IsNullOrWhiteSpace(Weapons.Info(kind).Name)));
    }

    [Fact]
    public void A_melee_weapon_cannot_go_into_a_ranged_place()
    {
        var player = NewPlayer();

        Assert.Throws<ArgumentException>(() => player.Equip(1, WeaponKind.SilverBlade));
        Assert.Throws<ArgumentOutOfRangeException>(() => player.Equip(Player.RangedSlotCount, WeaponKind.Pistol));
    }

    [Fact]
    public void A_number_key_takes_that_weapon_into_the_hand_and_says_so()
    {
        var player = NewPlayer();
        player.Equip(2, WeaponKind.Pistol);

        var events = Select(player, 2);

        Assert.Equal(2, player.SelectedSlot);
        Assert.Same(player.RangedSlots[2], player.Ranged);
        Assert.Contains(new WeaponSwitched(player.Id, 2, WeaponKind.Pistol), events);
    }

    [Fact]
    public void An_empty_place_or_the_one_in_hand_changes_nothing()
    {
        var player = NewPlayer();

        var events = Select(player, 1);
        events.AddRange(Select(player, 0));

        Assert.Equal(0, player.SelectedSlot);
        Assert.DoesNotContain(events, e => e is WeaponSwitched);
    }

    [Fact]
    public void The_wheel_goes_round_the_places_skipping_empty_ones()
    {
        var player = NewPlayer();
        player.Equip(2, WeaponKind.Pistol);

        Wheel(player, 1);
        Assert.Equal(2, player.SelectedSlot);

        Wheel(player, 1);
        Assert.Equal(0, player.SelectedSlot);

        Wheel(player, -1);
        Assert.Equal(2, player.SelectedSlot);

        player.Equip(1, WeaponKind.Pistol);
        Wheel(player, -1);
        Assert.Equal(1, player.SelectedSlot);
    }

    [Fact]
    public void With_one_weapon_the_wheel_does_nothing()
    {
        var player = NewPlayer();

        var events = Wheel(player, 1);
        events.AddRange(Wheel(player, -1));

        Assert.Equal(0, player.SelectedSlot);
        Assert.DoesNotContain(events, e => e is WeaponSwitched);
    }

    [Fact]
    public void Each_weapon_keeps_its_own_rounds()
    {
        var player = NewPlayer();
        player.Equip(1, WeaponKind.Pistol);
        Fire(player);
        Fire(player);

        Select(player, 1);
        Fire(player);

        var size = Balance.Default.Pistol.MagazineSize;
        Assert.Equal(size - 2, player.RangedSlots[0]!.Magazine.Rounds);
        Assert.Equal(size - 1, player.RangedSlots[1]!.Magazine.Rounds);
    }

    [Fact]
    public void Switching_breaks_the_reload_off()
    {
        var player = NewPlayer();
        player.Equip(1, WeaponKind.Pistol);
        Fire(player);
        Tick(player, PlayerInput.None with { Reload = true });
        Tick(player, PlayerInput.None);
        var first = player.Ranged;
        Assert.True(first.Magazine.IsReloading);

        Select(player, 1);
        Tick(player, PlayerInput.None, SimulationClock.SecondsToTicks(Balance.Default.Pistol.ReloadTime) + 1);

        Assert.False(first.Magazine.IsReloading);
        Assert.Equal(Balance.Default.Pistol.MagazineSize - 1, first.Magazine.Rounds);
    }

    [Fact]
    public void Switching_and_shooting_in_one_tick_fires_the_new_weapon()
    {
        var player = NewPlayer();
        player.Equip(1, WeaponKind.Pistol);

        var events = Tick(player, PlayerInput.None with { SelectSlot = 1, Shoot = true });

        Assert.Contains(events, e => e is ShotFired);
        Assert.Equal(Balance.Default.Pistol.MagazineSize, player.RangedSlots[0]!.Magazine.Rounds);
        Assert.Equal(Balance.Default.Pistol.MagazineSize - 1, player.RangedSlots[1]!.Magazine.Rounds);
    }

    [Fact]
    public void Every_carried_weapon_cools_down_also_in_the_bag()
    {
        var player = NewPlayer();
        player.Equip(1, WeaponKind.Pistol);
        Tick(player, PlayerInput.None with { Shoot = true });
        Select(player, 1);

        Tick(player, PlayerInput.None, CooldownTicks);

        Assert.True(player.RangedSlots[0]!.Cooldown.IsReady);
    }
}
