using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>What the HUD reads from the world (issue #369) and the text of the pickup toast.</summary>
public class HudReadoutTests
{
    private static GameWorld PlainWorld() => new(LevelParser.Parse("P"));

    [Fact]
    public void A_fresh_player_has_full_bars_level_zero_and_no_boss_or_skill_point()
    {
        var world = PlainWorld();

        var readout = HudReadout.Of(world, world.Player);

        Assert.False(readout.IsDead);
        Assert.Equal(1f, readout.Life);
        Assert.Equal(1f, readout.Stamina);
        Assert.Equal(0, readout.Level);
        Assert.Equal(0f, readout.Experience);
        Assert.Equal(0, readout.PendingBoons);
        Assert.Null(readout.Boss);
        Assert.Equal(0f, readout.ConsecratedSecondsLeft);
        Assert.Equal(world.Player.Silver, readout.Silver);
        Assert.Equal(world.Player.Keys, readout.Keys);
    }

    [Fact]
    public void The_weapons_are_the_melee_hand_first_then_every_ranged_place_with_its_rounds()
    {
        var world = PlainWorld();
        var player = world.Player;

        var readout = HudReadout.Of(world, player);

        Assert.Equal(1 + Player.RangedSlotCount, readout.Weapons.Count);
        Assert.Equal(new HudWeapon(player.Melee, null, false, false), readout.Weapons[0]);
        for (var slot = 0; slot < Player.RangedSlotCount; slot++)
        {
            var weapon = readout.Weapons[1 + slot];
            if (player.RangedSlots[slot] is { } ranged)
            {
                // Issue #388: with the size of its magazine for the large number.
                Assert.Equal(new HudWeapon(ranged.Kind, ranged.Magazine.Rounds, ranged.Magazine.IsReloading, slot == player.SelectedSlot,
                    ranged.Magazine.ReloadProgress, ranged.Magazine.Size), weapon);
                Assert.Equal(world.Balance.Pistol.MagazineSize, weapon.MagazineSize);
            }
            else
            {
                Assert.Equal(new HudWeapon(null, null, false, false), weapon);
            }
        }

        Assert.Single(readout.Weapons, weapon => weapon.IsSelected);
    }

    [Fact]
    public void A_weapon_being_reloaded_shows_how_far_the_reload_has_come()
    {
        var world = PlainWorld();
        var player = world.Player;
        var slot = player.SelectedSlot;
        world.Tick(PlayerInput.None with { Shoot = true });
        world.Tick(PlayerInput.None);
        world.Tick(PlayerInput.None with { Reload = true });
        var magazine = player.RangedSlots[slot]!.Magazine;
        var reloadTicks = SimulationClock.SecondsToTicks(world.Balance.Pistol.ReloadTime);
        for (var tick = 0; tick < reloadTicks / 2; tick++)
        {
            world.Tick(PlayerInput.None);
        }

        var weapon = HudReadout.Of(world, player).Weapons[1 + slot];

        Assert.True(weapon.IsReloading);
        Assert.Equal(magazine.ReloadProgress, weapon.ReloadProgress);
        Assert.InRange(weapon.ReloadProgress, 0.3f, 0.7f);
    }

    [Fact]
    public void The_selected_ranged_weapon_is_the_one_the_player_switched_to()
    {
        var world = PlainWorld();
        var player = world.Player;
        player.Equip(1, WeaponKind.Rifle);

        world.Tick(PlayerInput.None with { SelectSlot = 1 });
        var weapons = HudReadout.Of(world, player).Weapons;

        Assert.Equal(1, player.SelectedSlot);
        Assert.False(weapons[1].IsSelected);
        Assert.True(weapons[2].IsSelected);
        Assert.Equal(WeaponKind.Rifle, weapons[2].Kind);
    }

    [Fact]
    public void A_reloading_weapon_shows_as_reloading()
    {
        var world = PlainWorld();
        var player = world.Player;
        Assert.False(HudReadout.Of(world, player).Weapons[1].IsReloading);

        world.Tick(PlayerInput.None with { Shoot = true });
        world.Tick(PlayerInput.None with { Reload = true });

        Assert.True(player.Ranged.Magazine.IsReloading, "The reload never started - the test would prove nothing.");
        Assert.True(HudReadout.Of(world, player).Weapons[1].IsReloading);
    }

    [Fact]
    public void The_melee_weapon_is_in_use_while_charged_and_until_it_is_ready_again()
    {
        // Issue #388: meanwhile the large rounds number gives way.
        var world = PlainWorld();
        var player = world.Player;
        Assert.False(HudReadout.Of(world, player).IsMeleeInUse);

        world.Tick(PlayerInput.None with { Melee = true });
        Assert.True(player.MeleeCharge.IsCharging, "The charge never started - the test would prove nothing.");
        Assert.True(HudReadout.Of(world, player).IsMeleeInUse);

        world.Tick(PlayerInput.None);
        Assert.False(player.MeleeCharge.IsCharging, "The swing never came - the test would prove nothing.");
        Assert.True(HudReadout.Of(world, player).IsMeleeInUse);

        var ticks = 0;
        while (!player.MeleeCooldown.IsReady && ticks++ < 10 * SimulationClock.TicksPerSecond)
        {
            world.Tick(PlayerInput.None);
        }

        Assert.True(player.MeleeCooldown.IsReady);
        Assert.False(HudReadout.Of(world, player).IsMeleeInUse);
    }

    [Fact]
    public void Spent_stamina_shows_as_the_share_left()
    {
        var world = PlainWorld();
        var player = world.Player;

        world.Tick(PlayerInput.None with { Jump = true });
        world.Tick(PlayerInput.None with { Dash = true });

        Assert.True(player.Stamina.Fraction < 1f, "No stamina was spent - the test would prove nothing.");
        Assert.Equal(player.Stamina.Fraction, HudReadout.Of(world, player).Stamina, 4);
    }

    [Fact]
    public void A_dead_player_shows_as_dead_with_no_life_left()
    {
        var world = PlainWorld();
        var player = world.Player;

        player.TakeDamage(player.MaxHealth, new List<GameEvent>());
        var readout = HudReadout.Of(world, player);

        Assert.True(readout.IsDead);
        Assert.Equal(0f, readout.Life);
    }

    [Fact]
    public void Damage_shows_as_the_share_of_life_left()
    {
        var world = PlainWorld();
        var player = world.Player;
        var damage = player.MaxHealth / 4;

        player.TakeDamage(damage, new List<GameEvent>());

        Assert.Equal((float)(player.MaxHealth - damage) / player.MaxHealth, HudReadout.Of(world, player).Life, 4);
    }

    [Fact]
    public void A_level_up_shows_the_waiting_boon_and_the_way_to_the_next_level()
    {
        var world = PlainWorld();
        var player = world.Player;

        player.AddExperience(player.ExperienceToNextLevel + 1, new List<GameEvent>());
        var readout = HudReadout.Of(world, player);

        Assert.Equal(1, readout.Level);
        Assert.Equal(1, readout.PendingBoons);
        Assert.Equal(1f / player.ExperienceToNextLevel, readout.Experience, 4);
    }

    [Fact]
    public void Consecrated_ammunition_shows_its_seconds_left()
    {
        var world = PlainWorld();

        world.Player.Consecrate(5f);

        Assert.Equal(5f, HudReadout.Of(world, world.Player).ConsecratedSecondsLeft, 3);
    }

    [Fact]
    public void In_the_boss_fight_the_boss_shows_with_the_share_of_his_health_left()
    {
        var world = BossWorld();
        Assert.Null(HudReadout.Of(world, world.Player).Boss);

        Enter(world);
        world.Gorm!.TakeDamage(world.Gorm.MaxHealth / 2, new List<GameEvent>());
        var boss = HudReadout.Of(world, world.Player).Boss;

        Assert.NotNull(boss);
        Assert.Equal(Gorm.Name, boss.Value.Name);
        Assert.Equal((float)world.Gorm.Health / world.Gorm.MaxHealth, boss.Value.Health, 4);
        Assert.InRange(boss.Value.Health, 0.4f, 0.6f);
    }

    [Fact]
    public void The_toast_names_what_a_player_took_and_only_for_that_player()
    {
        var eye = Equipment.All.First(piece => piece.Name == "Hunter's Eye");

        Assert.Equal("Picked up Hunter's Eye", PickupToast.TextFor(new EquipmentTaken(1, 0, eye, Vector3.Zero), 0));
        Assert.Equal("Picked up Blood Vial", PickupToast.TextFor(new PickupTaken(1, 0, ConsumableKind.BloodVial), 0));
        Assert.Equal("Picked up a Key", PickupToast.TextFor(new KeyTaken(1, 0), 0));
        Assert.Null(PickupToast.TextFor(new KeyTaken(1, 1), 0));
        Assert.Null(PickupToast.TextFor(new EquipmentTaken(1, 1, eye, Vector3.Zero), 0));
        Assert.Null(PickupToast.TextFor(new PickupTaken(1, 1, ConsumableKind.BloodVial), 0));
        Assert.Null(PickupToast.TextFor(new SilverTaken(1, 0, 5), 0));
    }

    [Fact]
    public void Every_consumable_has_a_toast_name()
    {
        Assert.All(Enum.GetValues<ConsumableKind>(), kind => Assert.False(string.IsNullOrWhiteSpace(PickupToast.NameOf(kind))));
        Assert.Equal(Equipment.All.Count + Enum.GetValues<ConsumableKind>().Length + 1, PickupToast.AllTexts().Distinct().Count());
    }

    // As in GormTests: room 0 (start), a door, the boss room with Gorm ('T').
    private const string Map = "##.P.##\n###.###\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#.....#\n#..T..#\n#.....#";

    private static GameWorld BossWorld()
    {
        var hall = Enumerable.Range(2, 10).SelectMany(z => Enumerable.Range(1, 5).Select(x => (x, z))).ToList();
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 7, 1, 0, [(2, 0), (3, 0), (4, 0)], [], []),
            new(1, RoomKind.Boss, "hall", 0, 2, 7, 10, 1, hall, [], []),
        ];
        LevelDoor[] doors = [new(3, 1, 0, 1, RoomSide.South, DoorKind.Boss)];
        return new GameWorld(LevelParser.Parse(Map), 1, Balance.Default, doors, rooms);
    }

    private static void Enter(GameWorld world)
    {
        world.Tick(PlayerInput.None with { Interact = true });
        var forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
        for (var tick = 0; tick < 3 * SimulationClock.TicksPerSecond && !world.IsBossFightOn; tick++)
        {
            world.Tick(forward);
        }

        Assert.True(world.IsBossFightOn, "The boss fight never started - the test would prove nothing.");
    }
}
