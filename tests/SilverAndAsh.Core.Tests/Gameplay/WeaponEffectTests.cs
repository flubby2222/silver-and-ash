using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Issue #505: weapons with a status effect (skills draft, section 5). A found weapon carries an effect with the share
/// <see cref="StatusEffectsBalance.WeaponShare"/>; a hit sets it off with the chance of the weapon's tier. The tests
/// find weapons the way the game does (<see cref="WeaponEffects.RollFind"/>, as a chest or Gorm drops one), put them
/// into the player's hand and fire them tick by tick.
/// </summary>
public class WeaponEffectTests
{
    private static readonly PlayerInput Shoot = PlayerInput.None with { Shoot = true };
    private static readonly StatusEffectsBalance Effects = Balance.Default.StatusEffects;
    private static readonly int MarkTicks = SimulationClock.SecondsToTicks(Effects.Marked.Seconds);
    private static readonly WeaponKind[] StartingWeapons = [WeaponKind.SilverBlade, WeaponKind.Pistol];

    // From 16 m away (as in the status effect tests): the ray runs almost flat over the ghoul.
    private const string LongCorridor = "P\n.\n.\n.\n.\n.\n.\n.\nG";

    /// <summary>
    /// Every found weapon carries an effect, and a hit sets it off with <paramref name="chance"/> in every tier. The ghoul
    /// stands still and survives many hits, so every shot can be counted. Only these values are set; the rest is standard.
    /// </summary>
    private static Balance Always(float chance) => Balance.Default with
    {
        StatusEffects = Effects with
        {
            WeaponShare = 1f,
            WeaponHitChance = new WeaponHitChanceBalance { Worn = chance, Forged = chance, Royal = chance, Vampiric = chance },
        },
        Ghoul = Balance.Default.Ghoul with { Speed = 0f, MaxHealth = 100_000 },
    };

    /// <summary>A world of <paramref name="map"/> whose player holds a found <paramref name="weapon"/> in ranged place 0.</summary>
    private static GameWorld WorldWithFound(WeaponKind weapon, Balance balance, string map = LongCorridor, int seed = 1)
    {
        var world = new GameWorld(LevelParser.Parse(map), seed, balance);
        world.WeaponEffects.RollFind(weapon);
        world.Player.Equip(0, weapon);
        return world;
    }

    private static int RifleDamage => Balance.Default.Rifle.Damage;

    private static int MarkedRifleDamage =>
        (int)MathF.Round(Balance.Default.Rifle.Damage * (1f + Effects.Marked.Bonus), MidpointRounding.AwayFromZero);

    [Fact]
    public void The_share_of_found_weapons_with_an_effect_is_near_the_balance_value()
    {
        const int Seeds = 4000;
        var withEffect = Enumerable.Range(1, Seeds)
            .Count(seed => new GameWorld(LevelParser.Parse("P"), seed).WeaponEffects.RollFind(WeaponKind.Club) is not null);

        // Binomial spread at 30 % over 4000 runs is about 0.7 percentage points; 3 points is far outside it.
        Assert.InRange((float)withEffect / Seeds, Effects.WeaponShare - 0.03f, Effects.WeaponShare + 0.03f);
    }

    [Fact]
    public void A_found_weapon_carries_one_of_the_choosable_effects_and_keeps_it()
    {
        var effects = new WeaponEffects(7, Effects with { WeaponShare = 1f }, StartingWeapons);

        var club = effects.RollFind(WeaponKind.Club);

        Assert.NotNull(club);
        Assert.Contains(club.Value, WeaponEffects.Choosable);
        Assert.Equal(club, effects.Of(WeaponKind.Club));

        // Found again (dropped and laid down once more): the same weapon, the same effect - no new roll.
        Assert.Equal(club, effects.RollFind(WeaponKind.Club));
        Assert.Null(effects.Of(WeaponKind.Rifle));   // not found yet
    }

    [Fact]
    public void With_a_share_of_zero_no_found_weapon_carries_an_effect()
    {
        var effects = new WeaponEffects(7, Effects with { WeaponShare = 0f }, StartingWeapons);

        Assert.All([WeaponKind.Club, WeaponKind.Rifle, WeaponKind.Blunderbuss], kind => Assert.Null(effects.RollFind(kind)));
    }

    [Fact]
    public void The_starting_weapons_never_carry_an_effect()
    {
        for (var seed = 1; seed <= 50; seed++)
        {
            var effects = new WeaponEffects(seed, Effects with { WeaponShare = 1f }, StartingWeapons);

            Assert.Null(effects.RollFind(WeaponKind.SilverBlade));
            Assert.Null(effects.RollFind(WeaponKind.Pistol));
            Assert.NotNull(effects.RollFind(WeaponKind.Club));   // the share is 1: a found weapon always has one
        }
    }

    [Fact]
    public void The_world_s_starting_pistol_never_marks_even_when_every_hit_would()
    {
        var world = new GameWorld(LevelParser.Parse(LongCorridor), 1, Always(1f));
        Assert.Equal(WeaponKind.Pistol, world.Player.Ranged.Kind);

        ShootAtTheBody(world);

        Assert.Contains(world.Events, e => e is GhoulHit);
        Assert.Null(world.WeaponEffects.Of(WeaponKind.Pistol));
        Assert.DoesNotContain(world.Events, e => e is StatusApplied);
        Assert.False(world.Ghouls[0].Status.Has(StatusKind.Marked));
    }

    [Fact]
    public void A_hit_chance_of_zero_never_sets_the_effect_off_and_one_always_does()
    {
        var never = new WeaponEffects(3, Effects with { WeaponShare = 1f, WeaponHitChance = new WeaponHitChanceBalance { Worn = 0f } }, StartingWeapons);
        var always = new WeaponEffects(3, Effects with { WeaponShare = 1f, WeaponHitChance = new WeaponHitChanceBalance { Worn = 1f } }, StartingWeapons);
        never.RollFind(WeaponKind.Club);
        always.RollFind(WeaponKind.Club);

        for (var hit = 0; hit < 1000; hit++)
        {
            Assert.Null(never.RollHit(WeaponKind.Club));
            Assert.Equal(StatusKind.Marked, always.RollHit(WeaponKind.Club));
        }
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(1f, 0f)]
    public void A_hit_rolls_with_the_chance_of_the_weapon_s_own_tier(float worn, float forged)
    {
        // The club is Worn, the rifle Forged: each must roll with its own tier's chance, not with one for all.
        Assert.Equal(ItemTier.Worn, Weapons.Info(WeaponKind.Club).Tier);
        Assert.Equal(ItemTier.Forged, Weapons.Info(WeaponKind.Rifle).Tier);
        var effects = new WeaponEffects(
            3, Effects with { WeaponShare = 1f, WeaponHitChance = new WeaponHitChanceBalance { Worn = worn, Forged = forged } }, StartingWeapons);
        effects.RollFind(WeaponKind.Club);
        effects.RollFind(WeaponKind.Rifle);

        for (var hit = 0; hit < 1000; hit++)
        {
            Assert.Equal(worn == 1f ? StatusKind.Marked : null, effects.RollHit(WeaponKind.Club));
            Assert.Equal(forged == 1f ? StatusKind.Marked : null, effects.RollHit(WeaponKind.Rifle));
        }
    }

    [Fact]
    public void Rolling_hits_never_shifts_what_later_finds_carry()
    {
        // Two runs with the same seed; in one, the weapons found so far are fired between the finds. The finds roll
        // with a generator of their own, so both runs find the same effects.
        var values = Effects with { WeaponShare = 0.5f, WeaponHitChance = new WeaponHitChanceBalance { Worn = 0.5f, Forged = 0.5f } };
        WeaponKind[] found = [WeaponKind.Club, WeaponKind.Rifle, WeaponKind.Blunderbuss];
        var hitsRolled = 0;
        for (var seed = 1; seed <= 50; seed++)
        {
            var quiet = new WeaponEffects(seed, values, StartingWeapons);
            var busy = new WeaponEffects(seed, values, StartingWeapons);
            foreach (var kind in found)
            {
                Assert.Equal(quiet.RollFind(kind), busy.RollFind(kind));
                foreach (var held in found.Where(held => busy.Of(held) is not null))
                {
                    for (var hit = 0; hit < 5; hit++, hitsRolled++)
                    {
                        busy.RollHit(held);
                    }
                }
            }
        }

        Assert.True(hitsRolled > 0, "No weapon carried an effect - the test would prove nothing.");
    }

    [Fact]
    public void A_weapon_without_an_effect_never_sets_one_off()
    {
        var effects = new WeaponEffects(3, Effects with { WeaponShare = 0f, WeaponHitChance = new WeaponHitChanceBalance { Worn = 1f } }, StartingWeapons);
        effects.RollFind(WeaponKind.Club);

        Assert.Null(effects.RollHit(WeaponKind.Club));
        Assert.Null(effects.RollHit(WeaponKind.Rifle));   // never found at all
    }

    [Theory]
    [InlineData(ItemTier.Worn)]
    [InlineData(ItemTier.Forged)]
    [InlineData(ItemTier.Royal)]
    [InlineData(ItemTier.Vampiric)]
    public void Each_tier_has_its_own_hit_chance(ItemTier tier)
    {
        var chances = Effects.WeaponHitChance;
        var expected = tier switch
        {
            ItemTier.Worn => chances.Worn,
            ItemTier.Forged => chances.Forged,
            ItemTier.Royal => chances.Royal,
            _ => chances.Vampiric,
        };

        Assert.Equal(expected, chances.Of(tier));
    }

    [Fact]
    public void Rarer_tiers_set_their_effect_off_more_often()
    {
        var chances = Effects.WeaponHitChance;

        Assert.True(chances.Worn < chances.Forged && chances.Forged < chances.Royal && chances.Royal < chances.Vampiric);
    }

    [Fact]
    public void A_rifle_hit_marks_the_ghoul_with_the_shooter_as_source()
    {
        var world = WorldWithFound(WeaponKind.Rifle, Always(1f));
        var ghoul = world.Ghouls[0];

        ShootAtTheBody(world);

        Assert.Equal([new StatusApplied(ghoul.Id, StatusKind.Marked, world.Player.Id)], world.Events.OfType<StatusApplied>());
        Assert.Equal(world.Player.Id, ghoul.Status.Get(StatusKind.Marked)?.SourcePlayerId);
    }

    [Fact]
    public void A_rifle_with_a_hit_chance_of_zero_never_marks_in_a_whole_magazine()
    {
        var world = WorldWithFound(WeaponKind.Rifle, Always(0f));
        var events = new List<GameEvent>();

        for (var shot = 0; shot < Balance.Default.Rifle.MagazineSize; shot++)
        {
            ShootAtTheBody(world);
            events.AddRange(world.Events);
            events.AddRange(Run(world, PlayerInput.None, SimulationClock.SecondsToTicks(Balance.Default.Rifle.Cooldown)));
        }

        Assert.Equal(Balance.Default.Rifle.MagazineSize, events.OfType<GhoulHit>().Count());
        Assert.DoesNotContain(events, e => e is StatusApplied);
        Assert.False(world.Ghouls[0].Status.Has(StatusKind.Marked));
    }

    [Fact]
    public void The_source_is_the_number_of_the_player_who_hit_not_a_fixed_zero()
    {
        var world = WorldWithFound(WeaponKind.Rifle, Always(1f));
        world.Player.Id = 2;
        var ghoul = world.Ghouls[0];

        ShootAtTheBody(world);

        Assert.Contains(new StatusApplied(ghoul.Id, StatusKind.Marked, 2), world.Events);
        Assert.Equal(new StatusEffect(MarkTicks - 1, 2, false), ghoul.Status.Get(StatusKind.Marked));
    }

    [Fact]
    public void The_hit_that_sets_the_mark_off_gets_no_bonus_the_mark_counts_from_the_next_hit()
    {
        // Decision (provisional, issue #505): the effect comes after the whole shot. The hit that sets it off does plain
        // damage; the tick of that hit is the first of the mark's ticks, so it lasts for the hits of ticks T .. T+N-1.
        Assert.NotEqual(RifleDamage, MarkedRifleDamage);   // otherwise the test could not tell the two apart

        var last = WorldWithFound(WeaponKind.Rifle, Always(1f));
        var ghoul = last.Ghouls[0];
        ShootAtTheBody(last);                                       // tick T

        var trigger = last.Events.ToList();
        Assert.Contains(new GhoulHit(ghoul.Id, RifleDamage), trigger);
        Assert.True(
            trigger.FindIndex(e => e is GhoulHit) < trigger.FindIndex(e => e is StatusApplied),
            "The mark must come after the hit that set it off.");
        Assert.Equal(MarkTicks - 1, ghoul.Status.Get(StatusKind.Marked)?.TicksLeft);   // tick T already counted

        RunTicks(last, PlayerInput.None, MarkTicks - 2);
        ShootAtTheBody(last);                                       // tick T+N-1: the last tick of the mark
        Assert.Contains(new GhoulHit(ghoul.Id, MarkedRifleDamage), last.Events);

        var after = WorldWithFound(WeaponKind.Rifle, Always(1f));
        ShootAtTheBody(after);                                      // tick T
        RunTicks(after, PlayerInput.None, MarkTicks - 1);
        Assert.False(after.Ghouls[0].Status.Has(StatusKind.Marked));
        ShootAtTheBody(after);                                      // tick T+N: the mark is over
        Assert.Contains(new GhoulHit(after.Ghouls[0].Id, RifleDamage), after.Events);
    }

    [Fact]
    public void A_blunderbuss_shot_sets_the_effect_off_once_per_enemy_however_many_pellets_hit()
    {
        var world = WorldWithFound(WeaponKind.Blunderbuss, Always(1f), "P\n.\nG");
        var ghoul = world.Ghouls[0];
        Face(world.Player, Torso(ghoul.Position));

        world.Tick(Shoot);

        Assert.True(world.Events.OfType<GhoulHit>().Count() > 1, "Only one pellet hit - the test would prove nothing.");
        Assert.Equal([new StatusApplied(ghoul.Id, StatusKind.Marked, world.Player.Id)], world.Events.OfType<StatusApplied>());

        // Every pellet of the shot that set the mark off did plain damage.
        Assert.All(world.Events.OfType<GhoulHit>(), hit => Assert.True(
            hit.Damage == Balance.Default.Blunderbuss.Damage || hit.Critical, $"A pellet did {hit.Damage}."));
    }

    [Fact]
    public void A_blunderbuss_shot_marks_each_enemy_it_hits()
    {
        // Two ghouls side by side, 10 m away: there the cone is wide enough for pellets to hit both.
        var world = WorldWithFound(WeaponKind.Blunderbuss, Always(1f), "#P.#\n#..#\n#..#\n#..#\n#..#\n#GG#");
        Face(world.Player, Torso((world.Ghouls[0].Position + world.Ghouls[1].Position) / 2f));

        world.Tick(Shoot);

        var hitGhouls = world.Events.OfType<GhoulHit>().Select(hit => hit.GhoulId).Distinct().Order().ToList();
        var marked = world.Events.OfType<StatusApplied>().Select(applied => applied.TargetId).Order().ToList();
        Assert.True(hitGhouls.Count == 2, "The pellets did not hit both ghouls - the test would prove nothing.");
        Assert.Equal(hitGhouls, marked);   // each hit ghoul is marked, once (several pellets on one: see the test above)
    }

    [Fact]
    public void A_club_swing_marks_the_enemy_it_strikes()
    {
        var balance = Always(1f);
        var world = new GameWorld(LevelParser.Parse("P\nG"), 1, balance);
        world.WeaponEffects.RollFind(WeaponKind.Club);
        world.Player.EquipMelee(WeaponKind.Club);
        var ghoul = world.Ghouls[0];
        Face(world.Player, Torso(ghoul.Position));

        world.Tick(PlayerInput.None with { Melee = true });
        var events = world.Events.ToList();
        events.AddRange(Run(world, PlayerInput.None, SimulationClock.TicksPerSecond, () => world.Events.Any(e => e is GhoulHit)));

        Assert.Contains(events, e => e is GhoulHit);
        Assert.Equal([new StatusApplied(ghoul.Id, StatusKind.Marked, world.Player.Id)], events.OfType<StatusApplied>());
    }

    [Fact]
    public void A_kill_sets_no_effect_off()
    {
        var balance = Always(1f) with { Ghoul = Balance.Default.Ghoul with { Speed = 0f } };
        var world = WorldWithFound(WeaponKind.Rifle, balance);
        world.Ghouls[0].TakeDamage(Balance.Default.Ghoul.MaxHealth - 1, []);

        ShootAtTheBody(world);

        Assert.True(world.Ghouls[0].IsDead);
        Assert.DoesNotContain(world.Events, e => e is StatusApplied);
    }

    [Fact]
    public void The_effect_rolls_never_shift_the_pellets()
    {
        // Same seed, once with a hit chance of 0 and once of 1: the pellets fly the same way and hit as often.
        static List<string> Pellets(float chance)
        {
            var world = WorldWithFound(WeaponKind.Blunderbuss, Always(chance), "P\n.\n.\nG", seed: 5);
            var pellets = new List<string>();
            for (var shot = 0; shot < 3; shot++)
            {
                world.Tick(Shoot);
                pellets.AddRange(world.Events.Where(e => e is BulletImpact or GhoulHit).Select(e => e is GhoulHit hit ? $"hit {hit.GhoulId}" : e.ToString()!));
                RunTicks(world, PlayerInput.None, SimulationClock.SecondsToTicks(Balance.Default.Blunderbuss.ReloadTime + Balance.Default.Blunderbuss.Cooldown) + 1);
            }

            return pellets;
        }

        var without = Pellets(0f);
        Assert.Contains(without, pellet => pellet.StartsWith("hit", StringComparison.Ordinal));
        Assert.Equal(without, Pellets(1f));
    }

    [Fact]
    public void The_find_roll_never_shifts_the_loot()
    {
        // Same seed, once with and once without weapon effects: the chest gives the same pieces and silver.
        static List<GameEvent> Loot(float share)
        {
            var balance = Balance.Default with { StatusEffects = Effects with { WeaponShare = share } };
            var world = new GameWorld(LevelParser.Parse("P"), 11, balance);
            var events = new List<GameEvent>();
            for (var draw = 0; draw < 6; draw++)
            {
                if (world.LootPool.Draw(ItemTier.Worn) is { } piece && piece.Weapon is { } weapon)
                {
                    world.WeaponEffects.RollFind(weapon);
                    events.Add(new EquipmentDropped(draw, piece, Vector3.Zero));
                }
            }

            return events;
        }

        Assert.Equal(Loot(0f), Loot(1f));
    }

    [Fact]
    public void A_weapon_from_a_chest_carries_its_effect_into_the_toast_event()
    {
        var balance = Balance.Default with
        {
            Drops = Balance.Default.Drops with { GhoulChance = 0f, BatChance = 0f },
            Silver = Balance.Default.Silver with { GhoulChance = 0f, BatChance = 0f },
            Keys = new KeysBalance { RoomChance = 0f, EnemyChance = 0f },
            Loot = Balance.Default.Loot with { ItemShare = 0f, HigherTierChance = 0f },
            Chests = Balance.Default.Chests with { Beggar = Balance.Default.Chests.Beggar with { EquipmentChance = 1f } },
            StatusEffects = Effects with { WeaponShare = 1f },
        };
        var world = new GameWorld(
            LevelParser.Parse("#P#\n#.#\n#.#\n#.#\n#.#"), 1, balance, chests: [new ChestPlan(1, 3, 0, ChestTier.Beggar, false, 25, false, false)]);

        WalkUpAndOpen(world);
        var dropped = Assert.Single(world.Events.OfType<EquipmentDropped>());
        var weapon = Assert.IsType<WeaponKind>(dropped.Piece.Weapon);
        Assert.Equal(StatusKind.Marked, world.WeaponEffects.Of(weapon));

        RunTicks(world, PlayerInput.None, SimulationClock.TicksPerSecond);
        Face(world.Player, world.EquipmentDrops[0].Position);
        world.Tick(PlayerInput.None with { Interact = true });

        var taken = Assert.Single(world.Events.OfType<EquipmentTaken>());
        Assert.Equal(StatusKind.Marked, taken.Effect);
        Assert.Equal($"{dropped.Piece.Name} - Marked", taken.Label);
    }

    private static void WalkUpAndOpen(GameWorld world)
    {
        var player = world.Player;
        var forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
        for (var tick = 0; player.Position.Z < 5f; tick++)
        {
            Assert.True(tick < 3 * SimulationClock.TicksPerSecond, "The player never got close to the chest.");
            world.Tick(forward);
        }

        RunTicks(world, PlayerInput.None, SimulationClock.TicksPerSecond / 2);
        player.Look(PlayerInput.None with { Look = new Vector2(0f, 35f * MathF.PI / 180f / ControlSettings.MouseSensitivity) });
        world.Tick(PlayerInput.None with { Interact = true });
        Assert.True(world.Chests[0].IsOpen, "The chest never opened - the test would prove nothing.");
    }

    /// <summary>The middle of the torso of a ghoul standing at <paramref name="position"/>.</summary>
    private static Vector3 Torso(Vector3 position) => position + new Vector3(0f, BodySizes.Ghoul.TorsoHeight, 0f);

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

    /// <summary>
    /// Aims at the ghoul's shoulder, beside the head (as in the status effect tests: straight at the torso the ray meets
    /// the head first), and fires the weapon in hand - a body shot, not critical.
    /// </summary>
    private static void ShootAtTheBody(GameWorld world)
    {
        var ghoul = world.Ghouls[0];
        var point = ghoul.Position + ghoul.Facing * BodySizes.Ghoul.TorsoForward
            + new Vector3(0f, BodySizes.Ghoul.TorsoHeight - 0.1f, 0f)
            + new Vector3(ghoul.Facing.Z, 0f, -ghoul.Facing.X) * (BodySizes.Ghoul.TorsoRadius - 0.1f);
        var direction = Vector3.Normalize(point - world.Player.EyePosition);
        world.Player.Look(PlayerInput.None with
        {
            Look = new Vector2(
                -(MathF.Atan2(direction.X, direction.Z) - world.Player.View.YawRadians) / ControlSettings.MouseSensitivity,
                -(MathF.Asin(direction.Y) - world.Player.View.PitchRadians) / ControlSettings.MouseSensitivity),
        });
        world.Tick(Shoot);
    }
}
