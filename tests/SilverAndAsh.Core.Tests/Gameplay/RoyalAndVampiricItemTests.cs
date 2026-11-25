using System.Globalization;
using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// The second four items (decision record 0008, issue #196): Blade Parry, Hunter's Eye, Leech and Ashen Heart - each with
/// and without the item, through the ticks as in the game. Every number comes from <see cref="Balance.Default"/> or from
/// what the world reported in its events.
/// </summary>
public class RoyalAndVampiricItemTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly ItemsBalance Values = Balance.Default.Items;
    private static readonly BloodBoltBalance Bolt = Balance.Default.BloodBolt;
    private static readonly decimal LeechShare = (decimal)Values.Leech.Share;
    private static readonly PlayerInput Shoot = PlayerInput.None with { Shoot = true };
    private static readonly PlayerInput Press = PlayerInput.None with { Melee = true };

    /// <summary>Ghouls that stand still and survive many hits: what a hit reports is its whole damage.</summary>
    private static readonly Balance Sturdy = Balance.Default with
    {
        Ghoul = Balance.Default.Ghoul with { Speed = 0f, MaxHealth = 100_000 },
        Dummy = Balance.Default.Dummy with { MaxHealth = 100_000 },
    };

    /// <summary>A corridor along +Z: the player in row 0, room ahead for bolts to fly in.</summary>
    private static readonly Level Corridor = LevelParser.Parse("P\n.\n.\n.\n.");

    // ---- Blade Parry -----------------------------------------------------------------------------------------------

    [Fact]
    public void Without_blade_parry_a_swing_passes_through_a_blood_bolt()
    {
        var world = new GameWorld(Corridor);
        var bolt = BoltAhead(world, Balance.Default.Blade.Range * 0.7f);

        var seen = Swing(world);
        seen.AddRange(Run(world, PlayerInput.None, Second, () => world.Events.Any(e => e is PlayerHit)));

        Assert.DoesNotContain(seen, e => e is ProjectileParried);
        Assert.Contains(seen, e => e is ProjectileHitPlayer hit && hit.ProjectileId == bolt.Id);
        Assert.Contains(seen, e => e == new PlayerHit(world.Player.Id, Bolt.Damage));
    }

    [Fact]
    public void With_blade_parry_a_swing_shatters_a_blood_bolt_in_reach()
    {
        var world = new GameWorld(Corridor);
        world.Player.Take(ItemKind.BladeParry);
        var bolt = BoltAhead(world, Balance.Default.Blade.Range * 0.7f);

        var seen = Swing(world);
        var where = bolt.Position;
        seen.AddRange(Run(world, PlayerInput.None, Second));

        Assert.Equal(new ProjectileParried(bolt.Id, world.Player.Id, where), Assert.Single(seen.OfType<ProjectileParried>()));
        Assert.True(bolt.IsGone);
        Assert.Empty(world.Projectiles);
        Assert.DoesNotContain(seen, e => e is PlayerHit or ProjectileHitPlayer or ProjectileImpact);
        Assert.Equal(world.Player.MaxHealth, world.Player.Health);
    }

    [Fact]
    public void Blade_parry_shatters_the_bolt_in_the_tick_of_the_swing()
    {
        var world = new GameWorld(Corridor);
        world.Player.Take(ItemKind.BladeParry);
        BoltAhead(world, Balance.Default.Blade.Range * 0.7f);

        world.Tick(Press);
        Assert.Single(world.Projectiles);
        world.Tick(PlayerInput.None);

        Assert.Contains(world.Events, e => e is MeleeSwung);
        Assert.Contains(world.Events, e => e is ProjectileParried);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void Blade_parry_does_not_reach_a_bolt_beyond_the_weapons_range()
    {
        var world = new GameWorld(Corridor);
        world.Player.Take(ItemKind.BladeParry);
        var bolt = BoltAhead(world, Balance.Default.Blade.Range + 1.5f);

        var seen = Swing(world);
        Assert.True(Vector3.Distance(bolt.Position, world.Player.EyePosition) > Balance.Default.Blade.Range, "The bolt flew into reach - the test would prove nothing.");
        seen.AddRange(Run(world, PlayerInput.None, Second, () => world.Events.Any(e => e is PlayerHit)));

        Assert.DoesNotContain(seen, e => e is ProjectileParried);
        Assert.Contains(seen, e => e == new PlayerHit(world.Player.Id, Bolt.Damage));
    }

    [Fact]
    public void Blade_parry_does_not_reach_a_bolt_behind_the_player()
    {
        // The player stands in the middle of the corridor; the bolt is close behind, flying away.
        var world = new GameWorld(LevelParser.Parse(".\n.\nP\n.\n."));
        world.Player.Take(ItemKind.BladeParry);
        var eye = world.Player.EyePosition;
        var bolt = world.Fire(eye - new Vector3(0f, 0f, 1.2f), -Vector3.UnitZ, Bolt.Speed, Bolt.Damage, Bolt.Radius, Bolt.LifeTime);

        var seen = Swing(world);

        Assert.DoesNotContain(seen, e => e is ProjectileParried);
        Assert.Contains(bolt, world.Projectiles);
    }

    [Fact]
    public void Blade_parry_works_with_every_melee_weapon_in_its_own_reach()
    {
        // The club reaches less far than the blade: a bolt within its reach is shattered.
        var world = new GameWorld(Corridor);
        world.Player.EquipMelee(WeaponKind.Club);
        world.Player.Take(ItemKind.BladeParry);
        BoltAhead(world, Balance.Default.Club.Range * 0.7f);

        var seen = Swing(world);

        Assert.Contains(seen, e => e is MeleeSwung { Weapon: WeaponKind.Club });
        Assert.Contains(seen, e => e is ProjectileParried);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void Blade_parry_names_the_player_who_swung()
    {
        var world = new GameWorld(Corridor);
        world.Player.Id = 2;
        world.Player.Take(ItemKind.BladeParry);
        var bolt = BoltAhead(world, Balance.Default.Blade.Range * 0.7f);

        var seen = Swing(world);

        Assert.Equal(2, Assert.Single(seen.OfType<ProjectileParried>()).PlayerId);
        Assert.Equal(bolt.Id, seen.OfType<ProjectileParried>().Single().ProjectileId);
    }

    [Fact]
    public void Blade_parry_shatters_every_bolt_in_reach_at_once()
    {
        var world = new GameWorld(Corridor);
        world.Player.Take(ItemKind.BladeParry);
        var near = BoltAhead(world, Balance.Default.Blade.Range * 0.5f);
        var far = BoltAhead(world, Balance.Default.Blade.Range * 0.8f);

        var seen = Swing(world);

        Assert.Equal([near.Id, far.Id], seen.OfType<ProjectileParried>().Select(p => p.ProjectileId).Order());
        Assert.Empty(world.Projectiles);
    }

    // ---- Hunter's Eye ----------------------------------------------------------------------------------------------

    [Fact]
    public void Without_hunters_eye_a_headshot_does_the_weapons_headshot_damage()
    {
        var hit = HeadshotOnAGhoul(item: null);

        Assert.Equal(new GhoulHit(hit.GhoulId, Expected(Balance.Default.Pistol.Damage, Balance.Default.Pistol.HeadshotMultiplier), true), hit);
    }

    [Fact]
    public void With_hunters_eye_a_headshot_does_more_damage()
    {
        var hit = HeadshotOnAGhoul(ItemKind.HuntersEye);

        var pistol = Balance.Default.Pistol;
        var expected = Expected(pistol.Damage, pistol.HeadshotMultiplier, 1f + Values.HuntersEyeHeadshot);
        Assert.True(expected > Expected(pistol.Damage, pistol.HeadshotMultiplier), "The eye adds nothing - the test would prove nothing.");
        Assert.Equal(new GhoulHit(hit.GhoulId, expected, true), hit);
    }

    [Fact]
    public void With_hunters_eye_a_body_shot_stays_the_same()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nG"), balance: Sturdy);
        world.Player.Take(ItemKind.HuntersEye);
        var ghoul = world.Ghouls[0];
        Face(world.Player, Torso(ghoul));

        world.Tick(Shoot);

        Assert.Contains(new GhoulHit(ghoul.Id, Balance.Default.Pistol.Damage, false), world.Events);
    }

    [Fact]
    public void With_hunters_eye_a_headshot_on_a_training_dummy_does_more_damage_too()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nD\n."), balance: Sturdy);
        world.Player.Take(ItemKind.HuntersEye);
        var dummy = world.Dummies[0];
        Face(world.Player, dummy.Position + new Vector3(0f, BodySizes.Dummy.Height - BodySizes.Dummy.HeadHeight / 2f, 0f));

        world.Tick(Shoot);

        var pistol = Balance.Default.Pistol;
        Assert.Contains(new DummyHit(dummy.Id, Expected(pistol.Damage, pistol.HeadshotMultiplier, 1f + Values.HuntersEyeHeadshot), true), world.Events);
    }

    [Fact]
    public void With_hunters_eye_a_melee_swing_stays_the_same()
    {
        // Melee strikes know no head: the eye never adds to them.
        var plain = SwingAtAGhoul(item: null);
        var eyed = SwingAtAGhoul(ItemKind.HuntersEye);

        Assert.Equal(plain, eyed);
        Assert.False(eyed.Critical);
    }

    [Fact]
    public void Hunters_eye_counts_on_a_bosss_head_and_for_every_ranged_weapon()
    {
        // Provisional (issue #196): Gorm's head is a headshot as well.
        var rules = DamageRules.Standard();
        var gorm = Balance.Default.Gorm.HeadMultiplier;
        var eye = 1f + Values.HuntersEyeHeadshot;
        ItemKind[] items = [ItemKind.HuntersEye];

        Assert.Equal(new DamageResult(Expected(40, gorm, eye), true), rules.Calculate(new Hit(Weapon.Pistol, HitZone.BossHead, 40, AttackerItems: items)));
        Assert.Equal(new DamageResult(Expected(40, Balance.Default.Rifle.HeadshotMultiplier, eye), true), rules.Calculate(new Hit(Weapon.Rifle, HitZone.Head, 40, AttackerItems: items)));
        Assert.Equal(new DamageResult(Expected(40, Balance.Default.Blunderbuss.HeadshotMultiplier, eye), true), rules.Calculate(new Hit(Weapon.Blunderbuss, HitZone.Head, 40, AttackerItems: items)));
        Assert.Equal(new DamageResult(40, false), rules.Calculate(new Hit(Weapon.Pistol, HitZone.Body, 40, AttackerItems: items)));
    }

    [Fact]
    public void Hunters_eye_counts_only_for_the_attacker_who_carries_it()
    {
        // The rule asks the hit for its attacker's items; a hit with none (or other ones) stays as it was.
        var rules = DamageRules.Standard();
        var plain = rules.Calculate(new Hit(Weapon.Pistol, HitZone.Head, 40));

        Assert.Equal(plain, rules.Calculate(new Hit(Weapon.Pistol, HitZone.Head, 40, AttackerItems: [ItemKind.BladeParry, ItemKind.Leech])));
        Assert.Equal(plain, rules.Calculate(new Hit(Weapon.Pistol, HitZone.Head, 40, AttackerItems: [])));
    }

    [Fact]
    public void The_headshot_factor_is_one_without_the_eye_or_off_the_head()
    {
        ItemKind[] eye = [ItemKind.HuntersEye];

        Assert.Equal(1f + Values.HuntersEyeHeadshot, ItemEffects.HeadshotFactor(HitZone.Head, eye, Values));
        Assert.Equal(1f + Values.HuntersEyeHeadshot, ItemEffects.HeadshotFactor(HitZone.BossHead, eye, Values));
        Assert.Equal(1f, ItemEffects.HeadshotFactor(HitZone.Body, eye, Values));
        Assert.Equal(1f, ItemEffects.HeadshotFactor(HitZone.Head, [], Values));
        Assert.Throws<ArgumentNullException>(() => ItemEffects.HeadshotFactor(HitZone.Head, null!, Values));
        Assert.Throws<ArgumentNullException>(() => ItemEffects.HeadshotFactor(HitZone.Head, eye, null!));
    }

    // ---- Leech (the item) ------------------------------------------------------------------------------------------

    [Fact]
    public void Without_the_leech_a_hit_heals_nothing()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nG"), balance: Sturdy);
        world.Player.TakeDamage(world.Player.MaxHealth / 2, []);
        var before = world.Player.Health;

        var events = ShootTorso(world);

        Assert.Contains(events, e => e is GhoulHit);
        Assert.Equal(before, world.Player.Health);
        Assert.Equal(0m, world.Player.LeechSaved);
    }

    [Fact]
    public void With_the_leech_hits_heal_by_a_share_of_the_damage_dealt()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nG"), balance: Sturdy);
        var player = world.Player;
        player.Take(ItemKind.Leech);
        player.TakeDamage(player.MaxHealth / 2, []);
        var before = player.Health;

        // Several shots, so that the saved fractions add up to more than each shot alone would give.
        var shots = new List<List<GameEvent>>();
        for (var shot = 0; shot < 3; shot++)
        {
            shots.Add(ShootTorso(world));
            RunTicks(world, PlayerInput.None, SimulationClock.SecondsToTicks(Balance.Default.Pistol.Cooldown) + 1);
        }

        var perShot = shots.Select(events => events.OfType<GhoulHit>().Sum(hit => hit.Damage)).ToList();
        var health = perShot.Sum() * LeechShare;
        var whole = (int)decimal.Floor(health);
        Assert.True(whole >= 1, "The shots give no whole point - the test would prove nothing.");
        Assert.True(perShot.Sum(damage => (int)decimal.Floor(damage * LeechShare)) < whole, "Each shot gives whole points alone - the fractions are not seen to add up.");
        Assert.Equal(before + whole, player.Health);
        Assert.Equal(whole, shots.SelectMany(events => events.OfType<PlayerHealed>()).Sum(healed => healed.Amount));
        Assert.Equal(health - whole, player.LeechSaved);
    }

    [Fact]
    public void With_the_leech_the_hit_that_kills_a_bat_heals_only_by_the_health_it_had_left()
    {
        // A bat has less health than a rifle shot deals: its hit reports only what it had left, and the item counts that
        // - like the weapon effect Leech (LeechTests: the reports of the enemy kinds are not aligned yet).
        var world = new GameWorld(LevelParser.Parse(BatRoom), balance: Balance.Default with { Player = Tough.Player });
        var player = world.Player;
        player.Equip(0, WeaponKind.Rifle);
        player.Take(ItemKind.Leech);
        player.TakeDamage(player.MaxHealth / 2, []);
        var before = player.Health;
        var bat = world.Bats[0];

        Face(player, bat.Centre);
        var events = Run(world, Shoot, 1);

        Assert.True(bat.IsDead, "The shot did not kill the bat - the test would prove nothing.");
        var dealt = events.OfType<BatHit>().Sum(hit => hit.Damage);
        Assert.True(dealt < Balance.Default.Rifle.Damage, "The bat's hit reports the whole shot - the test would prove nothing.");
        Assert.True(
            decimal.Floor(dealt * LeechShare) != decimal.Floor(Balance.Default.Rifle.Damage * LeechShare),
            "The whole shot heals as much as what the bat had left - the test would prove nothing.");
        var health = dealt * LeechShare;
        Assert.Equal(before + (int)decimal.Floor(health), player.Health);
        Assert.Equal(health - decimal.Floor(health), player.LeechSaved);
    }

    /// <summary>A 16 x 10 m room, the bat 14 m from the player.</summary>
    private static readonly string BatRoom = string.Join('\n',
        "........",
        "........",
        "P......B",
        "........",
        "........");

    [Fact]
    public void With_the_leech_a_training_dummy_heals_nothing()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nD\n."), balance: Sturdy);
        var player = world.Player;
        player.Take(ItemKind.Leech);
        player.TakeDamage(player.MaxHealth / 2, []);
        var before = player.Health;
        var dummy = world.Dummies[0];
        Face(player, dummy.Position + new Vector3(0f, BodySizes.Dummy.Height / 2f, 0f));

        world.Tick(Shoot);

        Assert.Contains(world.Events, e => e is DummyHit);
        Assert.Equal(before, player.Health);
        Assert.Equal(0m, player.LeechSaved);
    }

    [Fact]
    public void With_the_leech_full_health_stays_full()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nG"), balance: Sturdy);
        world.Player.Take(ItemKind.Leech);

        var events = ShootTorso(world);

        Assert.Contains(events, e => e is GhoulHit);
        Assert.Equal(world.Player.MaxHealth, world.Player.Health);
        Assert.DoesNotContain(events, e => e is PlayerHealed);
    }

    [Fact]
    public void The_leech_and_a_weapon_with_leech_heal_together_and_save_their_fractions_in_one_place()
    {
        // The item and the weapon effect share one path (Player.HealByLeech): both shares of the same hit add up.
        var always = Sturdy with
        {
            StatusEffects = Sturdy.StatusEffects with
            {
                WeaponShare = 1f,
                WeaponHitChance = new WeaponHitChanceBalance { Worn = 1f, Forged = 1f, Royal = 1f, Vampiric = 1f },
            },
        };
        var world = FoundWithLeech(WeaponKind.Rifle, seed => new GameWorld(LevelParser.Parse("P\n.\n.\nG"), seed, always));
        var player = world.Player;
        player.Equip(0, WeaponKind.Rifle);
        player.Take(ItemKind.Leech);
        player.TakeDamage(player.MaxHealth / 2, []);
        var before = player.Health;

        var events = ShootTorso(world);

        var dealt = events.OfType<GhoulHit>().Sum(hit => hit.Damage);
        var weaponShare = (decimal)Balance.Default.StatusEffects.Leech.Share;
        var health = dealt * (weaponShare + LeechShare);
        var whole = (int)decimal.Floor(health);
        Assert.True(whole > (int)decimal.Floor(dealt * weaponShare), "The item adds no point - the test would prove nothing.");
        Assert.Equal(before + whole, player.Health);
        Assert.Equal(health - whole, player.LeechSaved);
    }

    [Fact]
    public void With_the_leech_the_flame_heals_the_player_who_set_it_off()
    {
        // Provisional (issue #196): every counted hit on an enemy heals - the pulses of a Cleansing Flame too. The player
        // has number 3, so what the flame gives is seen to go to the one who set the flame off. The standard flame alone is
        // worth less than one point: it is seen in the saved fraction.
        var flame = Balance.Default.StatusEffects.CleansingFlame;
        var flameTicks = CleansingFlame.DurationTicks(flame);
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nG"), balance: Sturdy);
        var player = world.Player;
        player.Id = 3;
        player.Take(ItemKind.Leech);
        player.TakeDamage(player.MaxHealth / 2, []);
        var before = player.Health;
        world.ApplyStatus(world.Ghouls[0], StatusKind.CleansingFlame, player);

        var events = Run(world, PlayerInput.None, flameTicks);

        var health = events.OfType<GhoulHit>().Sum(hit => hit.Damage) * LeechShare;
        var whole = (int)decimal.Floor(health);
        Assert.True(health > 0m, "The flame dealt no damage - the test would prove nothing.");
        Assert.Contains(events, e => e is TargetHitBy { PlayerId: 3, Weapon: Weapon.CleansingFlame });
        Assert.Equal(before + whole, player.Health);
        Assert.Equal(health - whole, player.LeechSaved);
        Assert.All(events.OfType<PlayerHealed>(), healed => Assert.Equal(3, healed.PlayerId));
    }

    [Fact]
    public void The_leech_rule_gives_its_share_only_with_the_item()
    {
        Assert.Equal(Values.Leech, ItemEffects.LeechItem([ItemKind.Leech], Values));
        Assert.Null(ItemEffects.LeechItem([ItemKind.HuntersEye], Values));
        Assert.Null(ItemEffects.LeechItem([], Values));
        Assert.Throws<ArgumentNullException>(() => ItemEffects.LeechItem(null!, Values));
    }

    // ---- Ashen Heart -----------------------------------------------------------------------------------------------

    [Fact]
    public void Without_the_heart_a_lethal_bolt_kills()
    {
        var world = new GameWorld(Corridor);
        var player = world.Player;

        var events = HitByBolt(world, player.Health);

        Assert.True(player.IsDead);
        Assert.Contains(events, e => e == new PlayerDied(player.Id));
        Assert.DoesNotContain(events, e => e is AshenHeartBroke);
        Assert.True(world.IsLost);
    }

    [Fact]
    public void With_the_heart_a_lethal_bolt_leaves_the_player_with_its_health_and_breaks_it()
    {
        var world = WorldWithHeart();
        var player = world.Player;

        var events = HitByBolt(world, player.Health);

        Assert.False(player.IsDead);
        Assert.Equal(Math.Min(Values.AshenHeartHealth, player.MaxHealth), player.Health);
        Assert.Equal(new AshenHeartBroke(player.Id), Assert.Single(events.OfType<AshenHeartBroke>()));
        Assert.DoesNotContain(events, e => e is PlayerDied);
        Assert.DoesNotContain(ItemKind.AshenHeart, player.Items);
        Assert.False(world.IsLost);
    }

    [Fact]
    public void The_heart_saves_exactly_once()
    {
        var world = WorldWithHeart();
        var player = world.Player;
        HitByBolt(world, player.Health);
        Assert.False(player.IsDead, "The heart did not save - the test would prove nothing.");

        var events = HitByBolt(world, player.Health);

        Assert.True(player.IsDead);
        Assert.Contains(events, e => e == new PlayerDied(player.Id));
        Assert.DoesNotContain(events, e => e is AshenHeartBroke);
        Assert.True(world.IsLost);
    }

    [Fact]
    public void The_heart_saves_from_a_hit_far_above_the_players_health()
    {
        var world = WorldWithHeart();
        var player = world.Player;

        var events = HitByBolt(world, 100 * player.MaxHealth);

        Assert.False(player.IsDead);
        Assert.Equal(Math.Min(Values.AshenHeartHealth, player.MaxHealth), player.Health);
        Assert.Single(events.OfType<AshenHeartBroke>());
    }

    [Fact]
    public void The_heart_does_not_break_on_a_hit_the_player_survives()
    {
        var world = WorldWithHeart();
        var player = world.Player;

        var events = HitByBolt(world, player.Health - 1);

        Assert.Equal(1, player.Health);
        Assert.Contains(ItemKind.AshenHeart, player.Items);
        Assert.DoesNotContain(events, e => e is AshenHeartBroke);
        Assert.False(world.LootPool.Contains(Equipment.Of(ItemKind.AshenHeart)));
    }

    [Fact]
    public void The_heart_saves_from_a_ghouls_strike_too()
    {
        var world = new GameWorld(LevelParser.Parse("P\nG"));
        var player = world.Player;
        player.Take(ItemKind.AshenHeart);
        player.TakeDamage(player.Health - 1, []);

        var events = Run(world, PlayerInput.None, 10 * Second, () => world.Events.Any(e => e is PlayerHit));

        Assert.Contains(events, e => e is PlayerHit);
        Assert.False(player.IsDead);
        Assert.Equal(Math.Min(Values.AshenHeartHealth, player.MaxHealth), player.Health);
        Assert.Single(events.OfType<AshenHeartBroke>());
    }

    [Fact]
    public void Two_lethal_bolts_in_one_tick_break_the_heart_and_then_kill()
    {
        // The heart takes the first; the second hits the player with the heart's health and is lethal again.
        var world = WorldWithHeart();
        var player = world.Player;
        var eye = player.EyePosition;
        var lethal = 100 * player.MaxHealth;
        world.Fire(eye + new Vector3(0f, 0f, 1f), -Vector3.UnitZ, Bolt.Speed, lethal, Bolt.Radius, Bolt.LifeTime);
        world.Fire(eye + new Vector3(0f, 0f, 1f), -Vector3.UnitZ, Bolt.Speed, lethal, Bolt.Radius, Bolt.LifeTime);

        var events = Run(world, PlayerInput.None, Second, () => world.Events.Any(e => e is PlayerHit));

        Assert.Equal(2, events.OfType<ProjectileHitPlayer>().Count());
        Assert.Single(events.OfType<AshenHeartBroke>());
        Assert.Contains(events, e => e == new PlayerDied(player.Id));
        Assert.True(player.IsDead);
        Assert.Single(world.LootPool.Left, e => e == Equipment.Of(ItemKind.AshenHeart));
    }

    [Fact]
    public void The_heart_keeps_the_other_items()
    {
        var world = WorldWithHeart();
        var player = world.Player;
        player.Take(ItemKind.Leech);
        player.Take(ItemKind.HuntersEye);

        HitByBolt(world, player.Health);

        Assert.False(player.IsDead);
        Assert.Equal([ItemKind.HuntersEye, ItemKind.Leech], player.Items.Order());
    }

    [Fact]
    public void A_broken_heart_goes_back_into_the_loot_pool_and_can_be_found_again()
    {
        var world = WorldWithHeart();
        var heart = Equipment.Of(ItemKind.AshenHeart);
        var pool = world.LootPool;

        // Everything else is found already: before the heart breaks, nothing at all is left to draw.
        foreach (var other in pool.Left.ToList())
        {
            pool.Take(other);
        }

        Assert.Null(pool.Draw(ItemTier.Worn));

        HitByBolt(world, world.Player.Health);

        Assert.True(pool.Contains(heart));
        Assert.Equal(heart, pool.Draw(ItemTier.Worn));
        Assert.False(pool.Contains(heart));
    }

    [Fact]
    public void A_heart_that_was_never_taken_from_the_pool_is_not_put_back_twice()
    {
        // A heart handed out without a draw (the test course, a test) is still in the pool: breaking it adds no second one.
        var world = new GameWorld(Corridor);
        world.Player.Take(ItemKind.AshenHeart);

        HitByBolt(world, world.Player.Health);

        Assert.Single(world.LootPool.Left, e => e == Equipment.Of(ItemKind.AshenHeart));
    }

    [Fact]
    public void Where_players_cannot_die_the_heart_never_breaks()
    {
        var world = new GameWorld(Corridor) { PlayersCannotDie = true };
        var player = world.Player;
        player.Take(ItemKind.AshenHeart);

        var events = HitByBolt(world, 100 * player.MaxHealth);

        Assert.False(player.IsDead);
        Assert.Contains(ItemKind.AshenHeart, player.Items);
        Assert.DoesNotContain(events, e => e is AshenHeartBroke);
    }

    [Fact]
    public void The_heart_names_the_player_it_saved()
    {
        var world = WorldWithHeart();
        world.Player.Id = 2;

        var events = HitByBolt(world, world.Player.Health);

        Assert.Equal(new AshenHeartBroke(2), Assert.Single(events.OfType<AshenHeartBroke>()));
    }

    [Fact]
    public void The_hearts_health_never_exceeds_the_players_most_health()
    {
        ItemKind[] heart = [ItemKind.AshenHeart];

        Assert.Equal(Values.AshenHeartHealth, ItemEffects.AshenHeartHealth(heart, Values.AshenHeartHealth + 1, Values));
        Assert.Equal(Values.AshenHeartHealth - 1, ItemEffects.AshenHeartHealth(heart, Values.AshenHeartHealth - 1, Values));
        Assert.Null(ItemEffects.AshenHeartHealth([ItemKind.Leech], Balance.Default.Player.MaxHealth, Values));
        Assert.Throws<ArgumentOutOfRangeException>(() => ItemEffects.AshenHeartHealth(heart, 0, Values));
        Assert.Throws<ArgumentNullException>(() => ItemEffects.AshenHeartHealth(null!, 1, Values));
    }

    // ---- All four --------------------------------------------------------------------------------------------------

    [Fact]
    public void Without_their_item_the_new_rules_change_nothing()
    {
        ItemKind[] others = [ItemKind.HuntersBoots, ItemKind.IronFlask, ItemKind.SilverRosary, ItemKind.QuickdrawHolster];

        Assert.False(ItemEffects.ShattersBolts(others));
        Assert.True(ItemEffects.ShattersBolts([ItemKind.BladeParry]));
        Assert.Equal(1f, ItemEffects.HeadshotFactor(HitZone.Head, others, Values));
        Assert.Null(ItemEffects.LeechItem(others, Values));
        Assert.Null(ItemEffects.AshenHeartHealth(others, Balance.Default.Player.MaxHealth, Values));
    }

    [Fact]
    public void The_effect_lines_name_the_standard_values()
    {
        // The lines shown in the game are fixed text; if the balancing changes a number, they must change with it.
        Assert.Contains(Percent(Values.HuntersEyeHeadshot), Items.Info(ItemKind.HuntersEye).Effect, StringComparison.Ordinal);
        Assert.Contains(Percent(Values.Leech.Share), Items.Info(ItemKind.Leech).Effect, StringComparison.Ordinal);
        Assert.Contains($"{Values.AshenHeartHealth} health", Items.Info(ItemKind.AshenHeart).Effect, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "items": { "huntersEyeHeadshot": 0 } }""")]
    [InlineData("""{ "items": { "leech": { "share": 0 } } }""")]
    public void An_item_may_be_set_to_do_nothing(string json)
    {
        Assert.Empty(BalanceFile.Parse(json).Warnings);
    }

    [Theory]
    [InlineData("""{ "items": { "huntersEyeHeadshot": -0.5 } }""", "items.huntersEyeHeadshot")]
    [InlineData("""{ "items": { "leech": { "share": -0.1 } } }""", "items.leech.share")]
    [InlineData("""{ "items": { "leech": { "share": 1.5 } } }""", "items.leech.share")]
    [InlineData("""{ "items": { "ashenHeartHealth": 0 } }""", "items.ashenHeartHealth")]
    [InlineData("""{ "items": { "ashenHeartHealth": -30 } }""", "items.ashenHeartHealth")]
    public void Item_values_that_make_no_sense_are_refused(string json, string key)
    {
        var result = BalanceFile.Parse(json);

        Assert.Equal(Balance.Default, result.Balance);
        Assert.Contains(result.Warnings, w => w.Contains(key, StringComparison.Ordinal));
    }

    // ---- Helpers ---------------------------------------------------------------------------------------------------

    private static string Percent(float share) =>
        string.Create(CultureInfo.InvariantCulture, $"{Math.Round(share * 100):0}%");

    /// <summary>Damage as the rules work it out: base x all multipliers, half points rounded up.</summary>
    private static int Expected(int damage, params float[] factors) =>
        (int)MathF.Round(damage * factors.Aggregate(1f, (product, factor) => product * factor), MidpointRounding.AwayFromZero);

    /// <summary>
    /// Fires a blood bolt (standard values) straight at the player's eyes from <paramref name="distance"/> ahead, along
    /// the corridor towards the player.
    /// </summary>
    private static Projectile BoltAhead(GameWorld world, float distance) =>
        world.Fire(world.Player.EyePosition + new Vector3(0f, 0f, distance), -Vector3.UnitZ, Bolt.Speed, Bolt.Damage, Bolt.Radius, Bolt.LifeTime);

    /// <summary>Presses melee for one tick and lets go the next, as in the game; returns both ticks' events.</summary>
    private static List<GameEvent> Swing(GameWorld world)
    {
        var seen = Run(world, Press, 1);
        seen.AddRange(Run(world, PlayerInput.None, 1));
        Assert.Contains(seen, e => e is MeleeSwung);
        return seen;
    }

    /// <summary>A corridor world whose player carries the Ashen Heart, found in a chest (so it left the loot pool).</summary>
    private static GameWorld WorldWithHeart()
    {
        var world = new GameWorld(Corridor);
        Assert.True(world.LootPool.Take(Equipment.Of(ItemKind.AshenHeart)));
        world.Player.Take(ItemKind.AshenHeart);
        return world;
    }

    /// <summary>A bolt of <paramref name="damage"/> flies at the player from 2 m ahead; returns the events up to its hit.</summary>
    private static List<GameEvent> HitByBolt(GameWorld world, int damage)
    {
        var eye = world.Player.EyePosition;
        var bolt = world.Fire(eye + new Vector3(0f, 0f, 2f), -Vector3.UnitZ, Bolt.Speed, damage, Bolt.Radius, Bolt.LifeTime);
        var events = Run(world, PlayerInput.None, Second, () => bolt.IsGone);
        Assert.Contains(events, e => e is ProjectileHitPlayer hit && hit.ProjectileId == bolt.Id);
        return events;
    }

    /// <summary>A pistol headshot on a sturdy ghoul by a player with <paramref name="item"/> (if any); returns its hit.</summary>
    private static GhoulHit HeadshotOnAGhoul(ItemKind? item)
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\n.\n.\n.\n.\n.\nG"), balance: Sturdy);
        if (item is { } kind)
        {
            world.Player.Take(kind);
        }

        var ghoul = world.Ghouls[0];
        Face(world.Player, ghoul.Position + ghoul.Facing * BodySizes.Ghoul.HeadForward
            + new Vector3(0f, BodySizes.Ghoul.HeadTop - BodySizes.Ghoul.HeadHeight / 2f, 0f));
        world.Tick(Shoot);
        return Assert.Single(world.Events.OfType<GhoulHit>());
    }

    /// <summary>A blade swing at a sturdy ghoul's head by a player with <paramref name="item"/> (if any); returns its hit.</summary>
    private static GhoulHit SwingAtAGhoul(ItemKind? item)
    {
        var world = new GameWorld(LevelParser.Parse("P\nG"), balance: Sturdy);
        if (item is { } kind)
        {
            world.Player.Take(kind);
        }

        var ghoul = world.Ghouls[0];
        Face(world.Player, ghoul.Position + new Vector3(0f, BodySizes.Ghoul.HeadTop - BodySizes.Ghoul.HeadHeight / 2f, 0f));
        var seen = Swing(world);
        return Assert.Single(seen.OfType<GhoulHit>());
    }

    /// <summary>Turns the player to the ghoul's torso and fires once; returns that tick's events.</summary>
    private static List<GameEvent> ShootTorso(GameWorld world)
    {
        Face(world.Player, Torso(world.Ghouls[0]));
        return Run(world, Shoot, 1);
    }

    /// <summary>The middle of a ghoul's torso (well below its head: <see cref="BodySizes.Ghoul.TorsoHeight"/> is the torso's top).</summary>
    private static Vector3 Torso(Ghoul ghoul) =>
        ghoul.Position + ghoul.Facing * BodySizes.Ghoul.TorsoForward + new Vector3(0f, BodySizes.Ghoul.TorsoHeight / 2f, 0f);

    /// <summary>The first world <paramref name="make"/> builds (seeds from 1 on) in which a find of <paramref name="weapon"/> rolls Leech.</summary>
    private static GameWorld FoundWithLeech(WeaponKind weapon, Func<int, GameWorld> make)
    {
        for (var seed = 1; ; seed++)
        {
            Assert.True(seed < 200, $"No seed gives a {weapon} with Leech.");
            var world = make(seed);
            if (world.WeaponEffects.RollFind(weapon) is StatusKind.Leech)
            {
                return world;
            }
        }
    }

    /// <summary>Turns the player's view (through the mouse, as in the game) straight at <paramref name="point"/>.</summary>
    private static void Face(Player player, Vector3 point)
    {
        var direction = Vector3.Normalize(point - player.EyePosition);
        player.Look(PlayerInput.None with
        {
            Look = new Vector2(
                -(MathF.Atan2(direction.X, direction.Z) - player.View.YawRadians) / ControlSettings.MouseSensitivity,
                -(MathF.Asin(direction.Y) - player.View.PitchRadians) / ControlSettings.MouseSensitivity),
        });
    }
}
