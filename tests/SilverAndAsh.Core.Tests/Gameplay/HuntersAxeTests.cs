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

    // An open room (cells of 2 m); the player stands in its middle looking along +Z.
    private const string Room = ".......\n.......\n.......\n...P...\nG.....G\nG.....G\n.......";

    // How far inside and outside a limit of the axe a ghoul is put: degrees for the cone's edge, metres for its reach.
    private const float EdgeDegrees = 3f;
    private const float EdgeMetres = 0.1f;

    /// <summary>
    /// Ghouls that neither see nor walk (sight range 0), so they stand where they were put until the swing, and survive
    /// even a charged axe, so every hit can be read off their health.
    /// </summary>
    private static readonly Balance StillGhouls = ToughGhouls with { Ghoul = ToughGhouls.Ghoul with { SightRange = 0f } };

    /// <summary>
    /// Puts <paramref name="ghoul"/> on the floor so that the point of it a swing aims at (the top of its axis: a ghoul
    /// is lower than the eyes) lies <paramref name="fromTip"/> metres from the tip of the cone of <paramref name="weapon"/>
    /// - <see cref="MeleeBalance.ConeStartBehind"/> behind the eyes - at <paramref name="degrees"/> from the looking
    /// direction, to the right (positive) or the left (negative).
    /// </summary>
    private static void PutAtAngle(GameWorld world, Ghoul ghoul, MeleeBalance weapon, float degrees, float fromTip)
    {
        var player = world.Player;
        var forward = Vector3.Normalize(player.View.Forward with { Y = 0f });
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var drop = player.EyePosition.Y - (player.Position.Y + BodySizes.Ghoul.Height);
        var tip = player.EyePosition - forward * weapon.ConeStartBehind;
        var radians = MathF.Abs(degrees) * MathF.PI / 180f;
        var along = fromTip * MathF.Cos(radians);
        var aside = MathF.Sqrt(MathF.Pow(fromTip * MathF.Sin(radians), 2) - drop * drop) * MathF.Sign(degrees);
        var spot = tip + forward * along + right * aside;
        ghoul.PlaceAt(spot with { Y = player.Position.Y });
    }

    /// <summary>
    /// Puts <paramref name="ghoul"/> straight ahead so that its surface is <paramref name="surface"/> metres from the eyes
    /// (the reach of a swing counts from the eyes to the body's surface).
    /// </summary>
    private static void PutAhead(GameWorld world, Ghoul ghoul, float surface)
    {
        var player = world.Player;
        var forward = Vector3.Normalize(player.View.Forward with { Y = 0f });
        var drop = player.EyePosition.Y - (player.Position.Y + BodySizes.Ghoul.Height);
        var flat = MathF.Sqrt(MathF.Pow(surface + BodySizes.Ghoul.Radius, 2) - drop * drop);
        ghoul.PlaceAt((player.EyePosition + forward * flat) with { Y = player.Position.Y });
    }

    /// <summary>
    /// The four ghouls of <see cref="Room"/> around the edge of the axe's cone (<see cref="Balance.Default"/>): one
    /// straight ahead, one <see cref="EdgeDegrees"/> inside the edge on the left and on the right, and one
    /// <see cref="EdgeDegrees"/> outside it on the right - a bit farther out, so it does not stand on the one inside.
    /// </summary>
    private static (Ghoul Ahead, Ghoul Left, Ghoul Right, Ghoul Outside) PlaceAtTheAxeEdge(GameWorld world)
    {
        var half = Axe.Angle / 2f;
        var (ahead, left, right, outside) = (world.Ghouls[0], world.Ghouls[1], world.Ghouls[2], world.Ghouls[3]);
        PutAhead(world, ahead, Axe.Range / 2f);
        PutAtAngle(world, left, Axe, -(half - EdgeDegrees), 1.6f);
        PutAtAngle(world, right, Axe, half - EdgeDegrees, 1.6f);
        PutAtAngle(world, outside, Axe, half + EdgeDegrees, 2.5f);
        return (ahead, left, right, outside);
    }

    [Fact]
    public void One_swing_of_the_axe_hits_every_ghoul_in_its_wide_cone_but_not_the_one_beside_it()
    {
        var world = With(WeaponKind.HuntersAxe, Room, StillGhouls);
        var (ahead, left, right, outside) = PlaceAtTheAxeEdge(world);
        var full = StillGhouls.Ghoul.MaxHealth;

        var events = Swing(world);

        Assert.Single(events.OfType<MeleeSwung>());
        Assert.Equal(full - Axe.Damage, ahead.Health);
        Assert.Equal(full - Axe.Damage, left.Health);
        Assert.Equal(full - Axe.Damage, right.Health);
        Assert.Equal(full, outside.Health);
    }

    [Fact]
    public void The_ghouls_at_the_edge_of_the_cone_stand_where_they_were_put_until_the_swing()
    {
        // The edge tests only mean something if nobody walked in or out of the cone before the swing.
        var world = With(WeaponKind.HuntersAxe, Room, StillGhouls);
        var placed = PlaceAtTheAxeEdge(world);
        var before = new[] { placed.Ahead, placed.Left, placed.Right, placed.Outside }.Select(ghoul => ghoul.Position).ToList();

        Swing(world);

        Assert.Equal(before, new[] { placed.Ahead, placed.Left, placed.Right, placed.Outside }.Select(ghoul => ghoul.Position));
    }

    [Fact]
    public void The_blade_hits_only_the_ghoul_ahead_of_the_same_four()
    {
        var world = With(WeaponKind.SilverBlade, Room, StillGhouls);
        var (ahead, left, right, outside) = PlaceAtTheAxeEdge(world);
        var full = StillGhouls.Ghoul.MaxHealth;

        Swing(world);

        Assert.Equal(full - Blade.Damage, ahead.Health);
        Assert.Equal(full, left.Health);
        Assert.Equal(full, right.Health);
        Assert.Equal(full, outside.Health);
    }

    [Fact]
    public void The_axe_reaches_a_ghoul_just_inside_its_range_but_not_one_just_beyond()
    {
        var near = With(WeaponKind.HuntersAxe, Room, StillGhouls);
        var far = With(WeaponKind.HuntersAxe, Room, StillGhouls);
        PutAhead(near, near.Ghouls[0], Axe.Range - EdgeMetres);
        PutAhead(far, far.Ghouls[0], Axe.Range + EdgeMetres);
        var full = StillGhouls.Ghoul.MaxHealth;

        Swing(near);
        Swing(far);

        Assert.Equal(full - Axe.Damage, near.Ghouls[0].Health);
        Assert.Equal(full, far.Ghouls[0].Health);
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

        // The first tick of holding is the press that starts charging; every further tick charges one tick's worth.
        var charged = FullCharge / 2;
        var half = Assert.Single(Swing(early, charged + 1).OfType<MeleeSwung>());
        var whole = Assert.Single(Swing(full, FullCharge + 2).OfType<MeleeSwung>());

        Assert.Equal(WeaponKind.HuntersAxe, whole.Weapon);
        Assert.Equal((float)charged / FullCharge, half.Charge);
        Assert.Equal(1f, whole.Charge);
    }

    [Fact]
    public void Charging_the_axe_fully_costs_its_stamina_segments()
    {
        var world = With(WeaponKind.HuntersAxe, "P\n.\n.");
        var stamina = world.Player.Stamina;
        var cost = Axe.ChargeStaminaSegments * stamina.CostPerSegment;

        // Press, then hold until the charge is just full - still holding, so nothing has refilled the bar yet.
        Run(world, Melee, FullCharge + 1);

        Assert.True(world.Player.MeleeCharge.IsFull);
        Assert.True(cost > 0, "Charging the axe must cost stamina.");
        Assert.Equal(stamina.Capacity - cost, stamina.Amount);
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
