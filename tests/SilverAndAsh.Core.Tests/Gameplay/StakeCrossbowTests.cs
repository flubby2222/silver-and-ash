using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// The stake crossbow (Vampiric, issue #203): one stake per load that runs through every enemy in a row - each of them
/// once and with the full damage - until a wall or a column stops it, and a slow reload.
/// </summary>
public class StakeCrossbowTests
{
    private static readonly RangedBalance Crossbow = Balance.Default.StakeCrossbow;
    private static readonly int ReloadTicks = SimulationClock.SecondsToTicks(Crossbow.ReloadTime);

    // How many targets one stake can hit: the first and every one it goes through.
    private static readonly int MostTargets = 1 + Crossbow.Pierce;

    /// <summary>Dummies that never fall over: so every hit can be counted in their health.</summary>
    private static readonly Balance Sturdy = Balance.Default with { Dummy = Balance.Default.Dummy with { MaxHealth = 100_000 } };

    /// <summary>Ghouls that survive the stake and neither see nor walk, so they stand in their row until the shot.</summary>
    private static readonly Balance StillGhouls = Balance.Default with
    {
        Ghoul = Balance.Default.Ghoul with { MaxHealth = 100_000, SightRange = 0f },
    };

    /// <summary>A one-cell corridor along +Z from the given rows; the player looks along it, the crossbow in hand.</summary>
    private static GameWorld World(string map, Balance? balance = null) => World(LevelParser.Parse(map), balance);

    private static GameWorld World(Level level, Balance? balance = null)
    {
        var world = new GameWorld(level, 1, balance ?? Sturdy);
        world.Player.Equip(0, WeaponKind.StakeCrossbow);
        return world;
    }

    /// <summary>One press of the trigger: the events of that tick, then a tick with nothing pressed.</summary>
    private static List<GameEvent> Shoot(GameWorld world)
    {
        world.Tick(PlayerInput.None with { Shoot = true });
        var events = world.Events.ToList();
        world.Tick(PlayerInput.None);
        return events;
    }

    private static List<GameEvent> Run(GameWorld world, PlayerInput input, int ticks = 1)
    {
        var events = new List<GameEvent>();
        for (var tick = 0; tick < ticks; tick++)
        {
            world.Tick(input);
            events.AddRange(world.Events);
        }

        return events;
    }

    private static int Lost(TrainingDummy dummy) => Sturdy.Dummy.MaxHealth - dummy.Health;

    /// <summary>The corridor with <paramref name="dummies"/> dummies in a row in front of the player and room behind them.</summary>
    private static string Row(int dummies) => "P\n" + string.Concat(Enumerable.Repeat("D\n", dummies)) + ".\n.";

    [Fact]
    public void The_stake_crossbow_is_a_vampiric_ranged_weapon_with_its_own_values()
    {
        Assert.Equal(new WeaponInfo("Crossbow", WeaponHand.Ranged, ItemTier.Vampiric), Weapons.Info(WeaponKind.StakeCrossbow));
        Assert.Same(Balance.Default.StakeCrossbow, Balance.Default.Ranged(WeaponKind.StakeCrossbow));
        Assert.Equal(Weapon.StakeCrossbow, Weapons.ForDamage(WeaponKind.StakeCrossbow));
    }

    [Fact]
    public void The_stake_crossbow_can_be_found_as_loot()
    {
        Assert.Contains(Equipment.Of(WeaponKind.StakeCrossbow), Equipment.All);
    }

    [Fact]
    public void Give_stakecrossbow_on_the_command_line_puts_the_crossbow_into_a_free_place()
    {
        var give = LaunchOptions.Parse(["--give", "stakecrossbow"]).Give!;
        var world = new GameWorld(LevelParser.Parse("P\n."));
        var free = world.Player.RangedSlots.ToList().FindIndex(weapon => weapon is null);

        GiveOptions.GiveTo(world, give);

        Assert.Equal([Equipment.Of(WeaponKind.StakeCrossbow)], give);
        var crossbow = world.Player.RangedSlots[free]!;
        Assert.Equal(WeaponKind.StakeCrossbow, crossbow.Kind);
        Assert.Equal(Crossbow.MagazineSize, crossbow.Magazine.Size);
    }

    [Fact]
    public void A_crossbow_in_a_place_has_its_magazine_and_cooldown()
    {
        var player = new Player(LevelParser.Parse("P"));

        player.Equip(1, WeaponKind.StakeCrossbow);

        var crossbow = player.RangedSlots[1]!;
        Assert.Equal(Crossbow.MagazineSize, crossbow.Magazine.Size);
        Assert.Equal(SimulationClock.SecondsToTicks(Crossbow.Cooldown), crossbow.Cooldown.DurationTicks);
    }

    [Fact]
    public void A_shot_says_it_came_from_the_crossbow()
    {
        var world = World("P\n.");

        var events = Shoot(world);

        Assert.Equal(WeaponKind.StakeCrossbow, Assert.Single(events.OfType<ShotFired>()).Weapon);
    }

    // ---- Through a row ---------------------------------------------------------------------------------------------

    [Fact]
    public void The_stake_runs_through_a_row_of_targets_up_to_its_limit_and_no_further()
    {
        var world = World(Row(MostTargets + 1));
        var dummies = world.Dummies;

        Shoot(world);

        for (var index = 0; index < MostTargets; index++)
        {
            Assert.True(Lost(dummies[index]) > 0, $"Target {index + 1} in the row was not hit.");
        }

        Assert.Equal(0, Lost(dummies[MostTargets]));
    }

    [Fact]
    public void Each_target_in_the_row_takes_the_full_damage()
    {
        // All of them are hit at the same height, so each one loses the same: the damage does not fall off along the row.
        var world = World(Row(MostTargets));

        Shoot(world);

        Assert.All(world.Dummies, dummy => Assert.Equal(Lost(world.Dummies[0]), Lost(dummy)));
        Assert.True(Lost(world.Dummies[0]) >= Crossbow.Damage);
    }

    [Fact]
    public void Each_target_is_hit_once_by_one_stake_and_says_it_was_the_crossbow()
    {
        // The flesh-hit sound comes from TargetHitBy: one per target the stake goes through, none twice.
        var world = World(Row(MostTargets + 1));

        var events = Shoot(world);

        var hits = events.OfType<TargetHitBy>().ToList();
        Assert.Equal(MostTargets, hits.Count);
        Assert.Equal(MostTargets, hits.Select(hit => hit.TargetId).Distinct().Count());
        Assert.All(hits, hit => Assert.Equal(Weapon.StakeCrossbow, hit.Weapon));
        Assert.Equal(MostTargets, events.OfType<DummyHit>().Count());
    }

    [Fact]
    public void The_stake_runs_through_a_row_of_ghouls_and_hits_each_body_once()
    {
        // A ghoul has a torso and a head; the stake goes through the row at a slant and still takes each ghoul only once.
        // The row stands seven cells off, so a line from the eyes down into the last ghoul's torso passes through every
        // torso before it, below the heads: each hit is a body hit.
        var map = "P\n" + string.Concat(Enumerable.Repeat(".\n", 7)) +string.Concat(Enumerable.Repeat("G\n", MostTargets)) + ".\n.";
        var world = World(map, StillGhouls);
        var last = world.Ghouls[^1];
        AimAt(world, last.Position + last.Facing * BodySizes.Ghoul.TorsoForward + new Vector3(0f, BodySizes.Ghoul.TorsoHeight * 0.2f, 0f));

        var events = Shoot(world);

        var hits = events.OfType<GhoulHit>().ToList();
        Assert.Equal(world.Ghouls.Select(ghoul => ghoul.Id).Order(), hits.Select(hit => hit.GhoulId).Order());
        Assert.All(hits, hit => Assert.Equal(new GhoulHit(hit.GhoulId, Crossbow.Damage), hit));
        Assert.Equal(MostTargets, events.OfType<TargetHitBy>().Count(hit => hit.Weapon == Weapon.StakeCrossbow));
    }

    // ---- What stops it ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_wall_stops_the_stake()
    {
        var world = World("P\nD\n#\nD");

        Shoot(world);

        Assert.True(Lost(world.Dummies[0]) > 0);
        Assert.Equal(0, Lost(world.Dummies[1]));
    }

    [Fact]
    public void A_column_stops_the_stake()
    {
        var level = LevelParser.Parse("P\nD\n.\nD\n.").WithColumns([Column.AtCell(0, 2)]);
        var world = World(level);

        var events = Shoot(world);

        Assert.True(Lost(world.Dummies[0]) > 0);
        Assert.Equal(0, Lost(world.Dummies[1]));
        var impact = Assert.Single(events.OfType<BulletImpact>());
        Assert.Equal(WeaponKind.StakeCrossbow, impact.Weapon);
        Assert.True(impact.Point.Z < world.Dummies[1].Position.Z, "The stake did not end in the column.");
    }

    [Fact]
    public void Through_the_row_the_stake_ends_in_the_wall_with_a_hard_hit()
    {
        var world = World("P\nD\nD\n.\n#");

        var events = Shoot(world);

        var impact = Assert.Single(events.OfType<BulletImpact>());
        Assert.Equal(WeaponKind.StakeCrossbow, impact.Weapon);
    }

    [Fact]
    public void A_target_that_ignores_the_hit_does_not_use_up_a_place_of_the_stake()
    {
        // Issue #793: in the showroom the exhibits take no damage. Their hits do not count, so they do not stop the stake:
        // as many exhibits as it has places would otherwise swallow it before the wall.
        var level = LevelParser.Parse(Row(MostTargets) + "\n#");
        var world = new GameWorld(level, 1, Sturdy) { Showroom = new ShowroomExhibits(null, [], [], [], []) };
        world.Player.Equip(0, WeaponKind.StakeCrossbow);

        var events = Shoot(world);

        Assert.Empty(events.OfType<TargetHitBy>());
        Assert.Equal(WeaponKind.StakeCrossbow, Assert.Single(events.OfType<BulletImpact>()).Weapon);
    }

    [Fact]
    public void The_pistol_s_bullet_in_the_wall_still_says_it_was_the_pistol()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n#"), 1, Sturdy);

        var events = Shoot(world);

        Assert.Equal(WeaponKind.Pistol, Assert.Single(events.OfType<BulletImpact>()).Weapon);
    }

    // ---- Reloading -------------------------------------------------------------------------------------------------

    [Fact]
    public void After_its_shot_the_crossbow_must_reload_before_it_fires_again()
    {
        var world = World("P\n.");
        var events = Shoot(world);
        events.AddRange(Run(world, PlayerInput.None, SimulationClock.SecondsToTicks(Crossbow.Cooldown)));
        Assert.Single(events.OfType<ShotFired>());

        // The next press fires nothing: it starts the reload.
        var press = Run(world, PlayerInput.None with { Shoot = true });

        Assert.DoesNotContain(press, e => e is ShotFired);
        Assert.Equal(WeaponKind.StakeCrossbow, Assert.Single(press.OfType<ReloadStarted>()).Weapon);
        Assert.True(world.Player.Ranged.Magazine.IsReloading);
    }

    [Fact]
    public void Reloading_takes_the_crossbow_s_time_and_says_it_was_the_crossbow()
    {
        var world = World("P\n.");
        Shoot(world);
        var started = Run(world, PlayerInput.None with { Reload = true });
        Assert.Equal(WeaponKind.StakeCrossbow, Assert.Single(started.OfType<ReloadStarted>()).Weapon);

        var waiting = Run(world, PlayerInput.None, ReloadTicks - 1);
        Assert.DoesNotContain(waiting, e => e is ReloadFinished);
        Assert.True(world.Player.Ranged.Magazine.IsReloading);

        var done = Run(world, PlayerInput.None);
        Assert.Equal(WeaponKind.StakeCrossbow, Assert.Single(done.OfType<ReloadFinished>()).Weapon);
        Assert.Equal(Crossbow.MagazineSize, world.Player.Ranged.Magazine.Rounds);
    }

    [Fact]
    public void The_crossbow_reloads_slower_than_every_other_ranged_weapon()
    {
        var others = Enum.GetValues<WeaponKind>()
            .Where(kind => kind != WeaponKind.StakeCrossbow && Weapons.Info(kind).Hand == WeaponHand.Ranged);

        Assert.All(others, kind => Assert.True(Balance.Default.Ranged(kind).ReloadTime < Crossbow.ReloadTime, $"{kind} reloads as slowly."));
    }

    // ---- Head ------------------------------------------------------------------------------------------------------

    [Fact]
    public void Headshots_and_Gorm_s_head_count_for_the_crossbow_too()
    {
        var rules = DamageRules.Standard();

        Assert.Equal(new DamageResult((int)MathF.Round(Crossbow.Damage * Crossbow.HeadshotMultiplier), true), rules.Calculate(new Hit(Weapon.StakeCrossbow, HitZone.Head, Crossbow.Damage)));
        Assert.Equal(Crossbow.Damage, rules.Calculate(new Hit(Weapon.StakeCrossbow, HitZone.Body, Crossbow.Damage)).Amount);
        Assert.True(rules.Calculate(new Hit(Weapon.StakeCrossbow, HitZone.BossHead, Crossbow.Damage)).Amount > Crossbow.Damage);
    }

    [Fact]
    public void A_crossbow_headshot_in_the_world_uses_the_crossbow_s_multiplier()
    {
        // The pistol gets no head bonus here, so a stake counted as a pistol shot would show.
        var balance = Sturdy with { Pistol = Sturdy.Pistol with { HeadshotMultiplier = 1f } };
        var world = World("P\n.\n.\nD\n.", balance);
        var dummy = world.Dummies[0];
        AimAt(world, dummy.Position with { Y = dummy.Position.Y + BodySizes.Dummy.Height - BodySizes.Dummy.HeadHeight / 2f });

        world.Tick(PlayerInput.None with { Shoot = true });

        var damage = (int)MathF.Round(Crossbow.Damage * Crossbow.HeadshotMultiplier, MidpointRounding.AwayFromZero);
        Assert.Contains(new DummyHit(0, damage, Critical: true), world.Events);
    }

    /// <summary>Turns the view (like the mouse would) so it looks at <paramref name="point"/>.</summary>
    private static void AimAt(GameWorld world, Vector3 point)
    {
        var player = world.Player;
        var direction = Vector3.Normalize(point - player.EyePosition);
        var pixels = new Vector2(
            -(MathF.Atan2(direction.X, direction.Z) - player.View.YawRadians) / ControlSettings.MouseSensitivity,
            -(MathF.Asin(direction.Y) - player.View.PitchRadians) / ControlSettings.MouseSensitivity);
        player.Look(PlayerInput.None with { Look = pixels });
    }

    // ---- Invalid values in balance.json ----------------------------------------------------------------------------

    [Fact]
    public void A_negative_damage_falls_back_to_the_standard()
    {
        var result = BalanceFile.Parse("""{ "stakeCrossbow": { "damage": -5 } }""");

        Assert.Equal(Crossbow, result.Balance.StakeCrossbow);
        Assert.Contains(result.Warnings, w => w.Contains("stakeCrossbow.damage"));
    }

    [Fact]
    public void A_magazine_of_zero_falls_back_to_the_standard()
    {
        var result = BalanceFile.Parse("""{ "stakeCrossbow": { "magazineSize": 0 } }""");

        Assert.Equal(Crossbow, result.Balance.StakeCrossbow);
        Assert.Contains(result.Warnings, w => w.Contains("stakeCrossbow.magazineSize"));
    }

    [Fact]
    public void A_reload_time_of_zero_falls_back_to_the_standard()
    {
        var result = BalanceFile.Parse("""{ "stakeCrossbow": { "reloadTime": 0 } }""");

        Assert.Equal(Crossbow, result.Balance.StakeCrossbow);
        Assert.Contains(result.Warnings, w => w.Contains("stakeCrossbow.reloadTime"));
    }

    [Fact]
    public void A_negative_pierce_falls_back_to_the_standard()
    {
        var result = BalanceFile.Parse("""{ "stakeCrossbow": { "pierce": -1 } }""");

        Assert.Equal(Crossbow, result.Balance.StakeCrossbow);
        Assert.Contains(result.Warnings, w => w.Contains("stakeCrossbow.pierce"));
    }

    [Fact]
    public void A_spread_over_90_degrees_falls_back_to_the_standard()
    {
        var result = BalanceFile.Parse("""{ "stakeCrossbow": { "spread": 100 } }""");

        Assert.Equal(Crossbow, result.Balance.StakeCrossbow);
        Assert.Contains(result.Warnings, w => w.Contains("stakeCrossbow.spread"));
    }

    [Fact]
    public void A_range_of_zero_falls_back_to_the_standard()
    {
        var result = BalanceFile.Parse("""{ "stakeCrossbow": { "range": 0 } }""");

        Assert.Equal(Crossbow, result.Balance.StakeCrossbow);
        Assert.Contains(result.Warnings, w => w.Contains("stakeCrossbow.range"));
    }

    [Fact]
    public void Valid_crossbow_values_are_taken_over_zero_damage_pierce_and_spread_among_them()
    {
        var result = BalanceFile.Parse("""{ "stakeCrossbow": { "damage": 0, "pierce": 0, "spread": 0, "reloadTime": 4 } }""");

        Assert.Empty(result.Warnings);
        Assert.Equal(0, result.Balance.StakeCrossbow.Damage);
        Assert.Equal(0, result.Balance.StakeCrossbow.Pierce);
        Assert.Equal(0f, result.Balance.StakeCrossbow.Spread);
        Assert.Equal(4f, result.Balance.StakeCrossbow.ReloadTime);
    }
}
