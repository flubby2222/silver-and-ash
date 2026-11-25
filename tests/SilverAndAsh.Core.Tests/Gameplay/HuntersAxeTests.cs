using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>The hunter's axe (issue #201): slow, a wide swing that hits several enemies, heavy damage, chargeable.</summary>
public class HuntersAxeTests
{
    private static readonly MeleeBalance Axe = Balance.Default.HuntersAxe;
    private static readonly MeleeBalance Blade = Balance.Default.Blade;
    private static readonly int FullCharge = SimulationClock.SecondsToTicks(Axe.ChargeTime);
    private static readonly PlayerInput Melee = PlayerInput.None with { Melee = true };

    /// <summary>Ghouls that survive even a charged axe, so every hit can be read off their health.</summary>
    private static readonly Balance ToughGhouls = Balance.Default with { Ghoul = Balance.Default.Ghoul with { MaxHealth = 10_000 } };

    /// <summary>Holds the melee button <paramref name="ticks"/> ticks and lets go (the swing happens on letting go).</summary>
    private static List<GameEvent> Swing(GameWorld world, int ticks = 1)
    {
        var events = Run(world, Melee, ticks);
        events.AddRange(Run(world, PlayerInput.None, 1));
        return events;
    }

    /// <summary>A world with the player looking along +Z and <paramref name="weapon"/> in the hand.</summary>
    private static GameWorld With(WeaponKind weapon, string map, Balance? balance = null)
    {
        var world = new GameWorld(LevelParser.Parse(map), 1, balance ?? Balance.Default);
        world.Player.EquipMelee(weapon);
        return world;
    }

    // ---- The weapon ------------------------------------------------------------------------------------------------

    [Fact]
    public void The_axe_is_a_royal_melee_weapon_with_its_own_values()
    {
        Assert.Equal(new WeaponInfo("Hunting Axe", WeaponHand.Melee, ItemTier.Royal), Weapons.Info(WeaponKind.HuntersAxe));
        Assert.Same(Balance.Default.HuntersAxe, Balance.Default.Melee(WeaponKind.HuntersAxe));
        Assert.Equal(Weapon.HuntersAxe, Weapons.ForDamage(WeaponKind.HuntersAxe));
    }

    [Fact]
    public void Every_weapon_kind_has_its_own_damage_weapon()
    {
        var all = Enum.GetValues<WeaponKind>().Select(Weapons.ForDamage).ToList();

        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Fact]
    public void An_unknown_weapon_kind_has_no_damage_weapon()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Weapons.ForDamage((WeaponKind)999));
    }

    [Fact]
    public void Taking_the_axe_changes_the_hand_and_its_cooldown()
    {
        var player = new Player(LevelParser.Parse("P"));

        player.EquipMelee(WeaponKind.HuntersAxe);

        Assert.Equal(WeaponKind.HuntersAxe, player.Melee);
        Assert.Equal(SimulationClock.SecondsToTicks(Axe.Cooldown), player.MeleeCooldown.DurationTicks);
    }

    [Fact]
    public void The_map_page_shows_the_axe_and_its_damage()
    {
        var world = With(WeaponKind.HuntersAxe, "P\n.");

        var melee = CharacterSheet.Stats(world, world.Player).Single(line => line.Label == "Melee");

        Assert.Equal($"{Axe.Damage}  Hunting Axe", melee.Value);
    }

    // ---- The wide cone ---------------------------------------------------------------------------------------------

    // An open room; the player stands in its middle looking along +Z.
    private const string Room = ".......\n.......\n.......\n...P...\nG.....G\nG.....G\n.......";

    /// <summary>
    /// Puts the four ghouls of <see cref="Room"/> 1.5 m from the player: straight ahead, 70 degrees to the left and to
    /// the right (seen from the tip of the cone behind the eyes that is about 52 degrees: outside the blade's 45, inside
    /// the axe's 75), and one 110 degrees to the right (about 87 degrees from the tip: outside the axe's cone too).
    /// </summary>
    private static (Ghoul Ahead, Ghoul Left, Ghoul Right, Ghoul Outside) PlaceGhouls(GameWorld world)
    {
        var feet = world.Player.Position;
        Ghoul Put(int index, float degrees)
        {
            var radians = degrees * MathF.PI / 180f;
            var ghoul = world.Ghouls[index];
            ghoul.PlaceAt(new Vector3(feet.X + 1.5f * MathF.Sin(radians), feet.Y, feet.Z + 1.5f * MathF.Cos(radians)));
            return ghoul;
        }

        return (Put(0, 0f), Put(1, -70f), Put(2, 70f), Put(3, 110f));
    }

    [Fact]
    public void One_swing_of_the_axe_hits_every_ghoul_in_its_wide_cone_but_not_the_one_beside_it()
    {
        var world = With(WeaponKind.HuntersAxe, Room, ToughGhouls);
        var (ahead, left, right, outside) = PlaceGhouls(world);
        var full = ToughGhouls.Ghoul.MaxHealth;

        var events = Swing(world);

        Assert.Single(events.OfType<MeleeSwung>());
        Assert.Equal(full - Axe.Damage, ahead.Health);
        Assert.Equal(full - Axe.Damage, left.Health);
        Assert.Equal(full - Axe.Damage, right.Health);
        Assert.Equal(full, outside.Health);
    }

    [Fact]
    public void The_blade_hits_only_the_ghoul_ahead_of_the_same_four()
    {
        var world = With(WeaponKind.SilverBlade, Room, ToughGhouls);
        var (ahead, left, right, outside) = PlaceGhouls(world);
        var full = ToughGhouls.Ghoul.MaxHealth;

        Swing(world);

        Assert.Equal(full - Blade.Damage, ahead.Health);
        Assert.Equal(full, left.Health);
        Assert.Equal(full, right.Health);
        Assert.Equal(full, outside.Health);
    }

    // ---- Damage, pace and charging ---------------------------------------------------------------------------------

    [Fact]
    public void The_axe_does_its_own_damage()
    {
        var world = With(WeaponKind.HuntersAxe, "P\nG", ToughGhouls);
        var ghoul = world.Ghouls[0];

        Swing(world);

        Assert.Equal(ToughGhouls.Ghoul.MaxHealth - Axe.Damage, ghoul.Health);
    }

    /// <summary>Taps the melee button every other tick for <paramref name="ticks"/> ticks; returns the ticks it swung on.</summary>
    private static List<int> TapSwings(GameWorld world, int ticks)
    {
        var swungAt = new List<int>();
        for (var tick = 0; tick < ticks; tick++)
        {
            if (Run(world, tick % 2 == 0 ? Melee : PlayerInput.None, 1).OfType<MeleeSwung>().Any())
            {
                swungAt.Add(tick);
            }
        }

        return swungAt;
    }

    [Fact]
    public void The_axe_swings_no_faster_than_its_cooldown_allows()
    {
        var world = With(WeaponKind.HuntersAxe, "P\n.\n.");
        var cooldown = SimulationClock.SecondsToTicks(Axe.Cooldown);

        var swungAt = TapSwings(world, 4 * cooldown);

        Assert.True(swungAt.Count >= 3, $"Only {swungAt.Count} swings.");
        foreach (var (before, after) in swungAt.Zip(swungAt.Skip(1)))
        {
            Assert.InRange(after - before, cooldown, cooldown + 3);
        }
    }

    [Fact]
    public void Tapping_for_the_same_time_the_axe_swings_less_often_than_the_blade()
    {
        var ticks = 3 * SimulationClock.TicksPerSecond;

        var axe = TapSwings(With(WeaponKind.HuntersAxe, "P\n.\n."), ticks).Count;
        var blade = TapSwings(With(WeaponKind.SilverBlade, "P\n.\n."), ticks).Count;

        Assert.True(axe < blade, $"Axe {axe} swings, blade {blade}.");
    }

    [Fact]
    public void The_axe_charges_over_its_own_charge_time()
    {
        var early = With(WeaponKind.HuntersAxe, "P\n.\n.");
        var full = With(WeaponKind.HuntersAxe, "P\n.\n.");

        var half = Assert.Single(Swing(early, FullCharge / 2).OfType<MeleeSwung>());
        var whole = Assert.Single(Swing(full, FullCharge + 2).OfType<MeleeSwung>());

        Assert.Equal(WeaponKind.HuntersAxe, whole.Weapon);
        Assert.InRange(half.Charge, 0.3f, 0.7f);
        Assert.Equal(1f, whole.Charge);
    }

    [Fact]
    public void A_fully_charged_axe_hits_harder()
    {
        var world = With(WeaponKind.HuntersAxe, "P\nG", ToughGhouls);
        var ghoul = world.Ghouls[0];

        Swing(world, FullCharge + 2);

        Assert.Equal(ToughGhouls.Ghoul.MaxHealth - (int)MathF.Round(Axe.Damage * Axe.ChargeMaxMultiplier), ghoul.Health);
    }

    // ---- Invalid values in balance.json ----------------------------------------------------------------------------

    [Fact]
    public void A_cone_wider_than_180_degrees_falls_back_to_the_standard()
    {
        var result = BalanceFile.Parse("""{ "huntersAxe": { "angle": 200 } }""");

        Assert.Equal(Balance.Default.HuntersAxe, result.Balance.HuntersAxe);
        Assert.Contains(result.Warnings, w => w.Contains("huntersAxe.angle"));
    }

    [Fact]
    public void A_cooldown_of_zero_falls_back_to_the_standard()
    {
        var result = BalanceFile.Parse("""{ "huntersAxe": { "cooldown": 0 } }""");

        Assert.Equal(Balance.Default.HuntersAxe, result.Balance.HuntersAxe);
        Assert.Contains(result.Warnings, w => w.Contains("huntersAxe.cooldown"));
    }

    [Fact]
    public void A_negative_damage_falls_back_to_the_standard()
    {
        var result = BalanceFile.Parse("""{ "huntersAxe": { "damage": -5 } }""");

        Assert.Equal(Balance.Default.HuntersAxe, result.Balance.HuntersAxe);
        Assert.Contains(result.Warnings, w => w.Contains("huntersAxe.damage"));
    }

    [Fact]
    public void A_charged_knockback_shorter_than_the_plain_one_falls_back_to_both_standards()
    {
        var result = BalanceFile.Parse("""{ "huntersAxe": { "knockback": 8, "chargedKnockback": 2 } }""");

        Assert.Equal(Balance.Default.HuntersAxe, result.Balance.HuntersAxe);
        Assert.Contains(result.Warnings, w => w.Contains("huntersAxe.knockback") && w.Contains("huntersAxe.chargedKnockback"));
    }

    [Fact]
    public void Valid_axe_values_are_taken_over()
    {
        var result = BalanceFile.Parse("""{ "huntersAxe": { "damage": 90, "angle": 120 } }""");

        Assert.Empty(result.Warnings);
        Assert.Equal(90, result.Balance.HuntersAxe.Damage);
        Assert.Equal(120f, result.Balance.HuntersAxe.Angle);
    }
}
