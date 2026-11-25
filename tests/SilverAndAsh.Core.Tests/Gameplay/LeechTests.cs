using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Issue #510: Leech heals the player who dealt a hit by a share of its damage - at once, with no duration, never above
/// that player's most health; the fractions are saved up per player. The tests find a weapon with Leech the way the game
/// does (<see cref="WeaponEffects.RollFind"/>), put it into the player's hand and fire it tick by tick. What a hit gives
/// is computed from the damage the world really reported (<see cref="GhoulHit"/>) and the share in
/// <see cref="Balance.Default"/>, never from fixed numbers.
/// </summary>
public class LeechTests
{
    private static readonly PlayerInput Shoot = PlayerInput.None with { Shoot = true };
    private static readonly StatusEffectsBalance Effects = Balance.Default.StatusEffects;
    private static readonly LeechBalance Values = Effects.Leech;
    private static readonly decimal Share = (decimal)Values.Share;

    /// <summary>6 m down a corridor: close enough for several pellets of a blunderbuss shot, too far for a still ghoul to strike.</summary>
    private const string Corridor = "P\n.\n.\nG";

    /// <summary>Ticks from one blunderbuss shot until the next can fire (reloaded and cooled down).</summary>
    private static readonly int BetweenShots =
        SimulationClock.SecondsToTicks(Balance.Default.Blunderbuss.ReloadTime + Balance.Default.Blunderbuss.Cooldown) + 1;

    /// <summary>
    /// Every found weapon carries an effect, and every hit sets it off. The ghoul stands still and survives many hits.
    /// Only these values are set; the share of Leech is the standard.
    /// </summary>
    private static readonly Balance Always = Balance.Default with
    {
        StatusEffects = Effects with
        {
            WeaponShare = 1f,
            WeaponHitChance = new WeaponHitChanceBalance { Worn = 1f, Forged = 1f, Royal = 1f, Vampiric = 1f },
        },
        Ghoul = Balance.Default.Ghoul with { Speed = 0f, MaxHealth = 100_000 },
    };

    /// <summary>
    /// A world of <paramref name="map"/> whose player holds <paramref name="weapon"/> in ranged place 0, found with Leech
    /// (the first seed from 1 on whose find rolls Leech) and aimed at the ghoul's torso.
    /// </summary>
    private static GameWorld WorldWithLeech(WeaponKind weapon, Balance balance, string map = Corridor)
    {
        var world = FoundWithLeech(weapon, seed => new GameWorld(LevelParser.Parse(map), seed, balance));
        world.Player.Equip(0, weapon);
        Face(world.Player, Torso(world.Ghouls[0].Position));
        return world;
    }

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

    /// <summary>
    /// Checks one tick's heal of <paramref name="player"/>: what was saved before plus the share of <paramref name="damage"/>
    /// (the damage the hit event reported), whole points healed and reported, the fraction saved.
    /// </summary>
    private static void AssertHealedFor(Player player, int healthBefore, decimal savedBefore, int damage, IEnumerable<GameEvent> events)
    {
        var health = savedBefore + (damage * Share);
        var whole = (int)decimal.Floor(health);
        Assert.Equal(whole, HealedIn(events));
        Assert.Equal(healthBefore + whole, player.Health);
        Assert.Equal(health - whole, player.LeechSaved);
    }

    /// <summary>
    /// Fires <paramref name="shots"/> blunderbuss shots one after another, reloading after each, and returns the events of
    /// each shot's tick.
    /// </summary>
    private static List<List<GameEvent>> FireShots(GameWorld world, int shots)
    {
        var each = new List<List<GameEvent>>();
        for (var shot = 0; shot < shots; shot++)
        {
            world.Tick(Shoot);
            each.Add(world.Events.ToList());
            world.Tick(PlayerInput.None with { Reload = true });   // one round in the magazine: reload by hand
            RunTicks(world, PlayerInput.None, BetweenShots);
            Assert.False(world.Player.Ranged.Magazine.IsReloading, "The blunderbuss is still reloading.");
        }

        return each;
    }

    /// <summary>All the damage the ghouls took in <paramref name="events"/>.</summary>
    private static int DamageIn(IEnumerable<GameEvent> events) => events.OfType<GhoulHit>().Sum(hit => hit.Damage);

    /// <summary>All the health the player got back in <paramref name="events"/>.</summary>
    private static int HealedIn(IEnumerable<GameEvent> events) => events.OfType<PlayerHealed>().Sum(healed => healed.Amount);

    // --- The rule alone ---

    [Fact]
    public void The_health_of_a_hit_is_its_damage_times_the_share()
    {
        Assert.Equal(0m, Leech.HealthFrom(0, Values));
        Assert.Equal(Balance.Default.Blunderbuss.Damage * Share, Leech.HealthFrom(Balance.Default.Blunderbuss.Damage, Values));
        Assert.Equal(1000 * Share, Leech.HealthFrom(1000, Values));
    }

    [Fact]
    public void Many_small_hits_are_worth_exactly_what_one_big_hit_is()
    {
        // In decimal, 0.025 stays 0.025: forty hits of 1 are exactly one hit of 40, on every machine.
        var small = Enumerable.Range(0, 40).Aggregate(0m, (sum, _) => sum + Leech.HealthFrom(1, Values));
        Assert.Equal(Leech.HealthFrom(40, Values), small);
    }

    [Fact]
    public void Saved_health_splits_into_whole_points_and_the_fraction()
    {
        Assert.Equal((0, 0m), Leech.Split(0m));
        Assert.Equal((0, 0.3m), Leech.Split(0.3m));
        Assert.Equal((2, 0.25m), Leech.Split(2.25m));
        Assert.Equal((1, 0m), Leech.Split(1m));
    }

    [Fact]
    public void Negative_damage_or_saved_health_is_an_error()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Leech.HealthFrom(-1, Values));
        Assert.Throws<ArgumentOutOfRangeException>(() => Leech.Split(-0.1m));
        Assert.Throws<ArgumentNullException>(() => Leech.HealthFrom(1, null!));
    }

    // --- In the world, tick by tick ---

    [Fact]
    public void Several_small_hits_add_up_to_the_expected_heal()
    {
        var world = WorldWithLeech(WeaponKind.Blunderbuss, Always);
        var player = world.Player;
        player.TakeDamage(player.MaxHealth / 2, []);
        var before = player.Health;

        var shots = FireShots(world, 4);

        var perShot = shots.Select(DamageIn).ToList();   // pellets scatter: a shot may also miss altogether
        var expected = (int)decimal.Floor(perShot.Sum() * Share);

        // Without saving the fractions, each shot would heal only its whole points - less. Otherwise the test proves nothing.
        var withoutSaving = perShot.Sum(damage => (int)decimal.Floor(damage * Share));
        Assert.True(withoutSaving < expected, $"The shots give whole points alone ({withoutSaving}) - the test would prove nothing.");
        Assert.True(before + expected < player.MaxHealth, "The heal would reach the most health - the test would prove nothing.");

        Assert.Equal(before + expected, player.Health);
        Assert.Equal(expected, HealedIn(shots.SelectMany(events => events)));
        Assert.Equal(perShot.Sum() * Share - expected, player.LeechSaved);
    }

    [Fact]
    public void The_heal_is_reported_for_the_attacker_in_the_tick_of_the_hit()
    {
        var world = WorldWithLeech(WeaponKind.Blunderbuss, Always);
        world.Player.Id = 2;
        world.Player.TakeDamage(world.Player.MaxHealth / 2, []);

        var shots = FireShots(world, 4);

        var healed = shots.SelectMany(events => events.OfType<PlayerHealed>()).ToList();
        Assert.NotEmpty(healed);
        Assert.All(healed, heal => Assert.Equal(2, heal.PlayerId));

        // Leech never sits on an enemy: nothing is applied to the ghoul, it has no Leech to count down.
        Assert.DoesNotContain(shots.SelectMany(events => events), e => e is StatusApplied);
        Assert.False(world.Ghouls[0].Status.Has(StatusKind.Leech));
    }

    [Fact]
    public void Full_health_stays_full_and_the_most_health_stays_the_same()
    {
        var world = WorldWithLeech(WeaponKind.Blunderbuss, Always);
        var player = world.Player;
        var most = player.MaxHealth;
        Assert.Equal(most, player.Health);

        var shots = FireShots(world, 4);

        var damage = shots.Sum(DamageIn);
        Assert.True(damage * Share >= 1m, "The shots give no whole point - the test would prove nothing.");
        Assert.Equal(most, player.Health);
        Assert.Equal(most, player.MaxHealth);                       // no more most health, and so nothing on boons.healthCap
        Assert.Equal(Balance.Default.Player.MaxHealth, player.MaxHealth);
        Assert.DoesNotContain(shots.SelectMany(events => events), e => e is PlayerHealed);

        // The whole points are gone, only the fraction stays saved.
        Assert.Equal(damage * Share - decimal.Floor(damage * Share), player.LeechSaved);
    }

    [Fact]
    public void A_heal_near_the_most_health_stops_there()
    {
        var world = WorldWithLeech(WeaponKind.Blunderbuss, Always);
        var player = world.Player;
        player.TakeDamage(1, []);

        var shots = FireShots(world, 10);

        Assert.True(shots.Sum(DamageIn) * Share >= 2m, "The shots give less than two points - the test would prove nothing.");
        Assert.Equal(player.MaxHealth, player.Health);
        Assert.Equal(1, HealedIn(shots.SelectMany(events => events)));
    }

    [Fact]
    public void The_hit_that_kills_an_enemy_heals_too()
    {
        var world = WorldWithLeech(WeaponKind.Rifle, Always, "P\n.\nG");
        var player = world.Player;
        player.TakeDamage(player.MaxHealth / 2, []);
        var before = player.Health;
        var ghoul = world.Ghouls[0];
        ghoul.TakeDamage(Always.Ghoul.MaxHealth - 1, []);

        world.Tick(Shoot);

        Assert.True(ghoul.IsDead, "The shot did not kill the ghoul - the test would prove nothing.");
        var health = DamageIn(world.Events) * Share;
        Assert.True(health >= 1m, "The killing hit gives no whole point - the test would prove nothing.");
        Assert.Equal(before + (int)decimal.Floor(health), player.Health);
        Assert.Equal(health - decimal.Floor(health), player.LeechSaved);
    }

    [Fact]
    public void A_hit_chance_of_zero_never_heals()
    {
        var never = Always with
        {
            StatusEffects = Always.StatusEffects with { WeaponHitChance = new WeaponHitChanceBalance { Worn = 0f, Forged = 0f, Royal = 0f, Vampiric = 0f } },
        };
        var world = WorldWithLeech(WeaponKind.Blunderbuss, never);
        var player = world.Player;
        player.TakeDamage(player.MaxHealth / 2, []);
        var before = player.Health;

        var shots = FireShots(world, 4);

        Assert.True(shots.Sum(DamageIn) > 0, "No shot hit - the test would prove nothing.");
        Assert.Equal(before, player.Health);
        Assert.Equal(0m, player.LeechSaved);
    }

    [Fact]
    public void A_club_swing_heals_by_the_damage_it_dealt()
    {
        var world = FoundWithLeech(WeaponKind.Club, seed => new GameWorld(LevelParser.Parse("P\nG"), seed, Always));
        var player = world.Player;
        player.EquipMelee(WeaponKind.Club);
        Face(player, Torso(world.Ghouls[0].Position));
        player.TakeDamage(player.MaxHealth / 2, []);
        var before = player.Health;

        world.Tick(PlayerInput.None with { Melee = true });
        var events = world.Events.ToList();
        events.AddRange(Run(world, PlayerInput.None, SimulationClock.TicksPerSecond, () => world.Events.Any(e => e is GhoulHit)));

        var damage = DamageIn(events);
        Assert.True(damage * Share >= 1m, "The swing gives no whole point - the test would prove nothing.");
        AssertHealedFor(player, before, 0m, damage, events);
    }

    // --- Leech counts what the enemy's hit event reports (provisional: the reports are not aligned between the kinds) ---

    /// <summary>Gorm's hall with a player who survives his attacks and a rifle with Leech in ranged place 0.</summary>
    private static GameWorld GormHallWithLeech(Balance balance)
    {
        var world = FoundWithLeech(WeaponKind.Rifle, seed => GormHallWorld(seed: seed, balance: balance));
        world.Player.Equip(0, WeaponKind.Rifle);
        EnterGormHall(world);
        world.Player.TakeDamage(world.Player.MaxHealth / 2, []);
        return world;
    }

    /// <summary>Every hit sets its effect off, the player survives Gorm.</summary>
    private static readonly Balance AlwaysTough = Always with { Player = Tough.Player };

    /// <summary>Turns the player to Gorm's body, fires the rifle once and returns that tick's events.</summary>
    private static List<GameEvent> ShootGorm(GameWorld world)
    {
        var gorm = world.Gorm!;
        Face(world.Player, gorm.Position + new Vector3(0f, BodySizes.Gorm.Height * 0.4f, 0f));
        world.Tick(Shoot);
        var events = world.Events.ToList();
        Assert.Contains(events, e => e is GormHit);
        return events;
    }

    [Fact]
    public void A_hit_on_gorm_heals_by_the_damage_his_hit_reports()
    {
        var world = GormHallWithLeech(AlwaysTough);
        var player = world.Player;
        Assert.NotEqual(GormState.Stuck, world.Gorm!.State);
        var (before, saved) = (player.Health, player.LeechSaved);

        var events = ShootGorm(world);

        var damage = events.OfType<GormHit>().Sum(hit => hit.Damage);
        Assert.True(damage * Share >= 1m, "The shot gives no whole point - the test would prove nothing.");
        AssertHealedFor(player, before, saved, damage, events);
    }

    [Fact]
    public void A_hit_on_gorm_with_his_club_stuck_heals_by_the_raised_damage_his_hit_reports()
    {
        // The shock wave reaches nobody: after every hammer blow the club sticks.
        var balance = AlwaysTough with { Gorm = AlwaysTough.Gorm with { ShockwaveRadius = 1f } };
        var world = GormHallWithLeech(balance);
        var player = world.Player;
        var gorm = world.Gorm!;
        Assert.True(RunUntil(world, PlayerInput.None, 60 * SimulationClock.TicksPerSecond, _ => gorm.State == GormState.Stuck), "His club never stuck.");
        var (before, saved) = (player.Health, player.LeechSaved);

        var events = ShootGorm(world);

        // The stuck club raises the damage after the world has worked it out: only his report knows the real amount.
        var damage = events.OfType<GormHit>().Sum(hit => hit.Damage);
        var unraised = (int)MathF.Round(damage / balance.Gorm.StuckDamageMultiplier, MidpointRounding.AwayFromZero);
        Assert.True(
            decimal.Floor(saved + (damage * Share)) != decimal.Floor(saved + (unraised * Share)),
            "The raised damage heals as much as the plain one - the test would prove nothing.");
        AssertHealedFor(player, before, saved, damage, events);
    }

    [Fact]
    public void The_hit_that_kills_a_bat_heals_by_the_health_it_had_left()
    {
        // A bat has less health than a rifle shot deals: its hit reports only what it had left, and Leech counts that.
        var world = FoundWithLeech(WeaponKind.Rifle, seed => new GameWorld(LevelParser.Parse(BatRoom), seed, AlwaysTough));
        var player = world.Player;
        player.Equip(0, WeaponKind.Rifle);
        player.TakeDamage(player.MaxHealth / 2, []);
        var bat = world.Bats[0];
        var (before, saved) = (player.Health, player.LeechSaved);

        Face(player, bat.Centre);
        world.Tick(Shoot);
        var events = world.Events.ToList();

        Assert.True(bat.IsDead, "The shot did not kill the bat - the test would prove nothing.");
        var damage = events.OfType<BatHit>().Sum(hit => hit.Damage);
        Assert.True(damage < Balance.Default.Rifle.Damage, "The bat's hit reports the whole shot - the test would prove nothing.");
        Assert.True(
            decimal.Floor(damage * Share) != decimal.Floor(Balance.Default.Rifle.Damage * Share),
            "The whole shot heals as much as what the bat had left - the test would prove nothing.");
        AssertHealedFor(player, before, saved, damage, events);
    }

    /// <summary>A 16 x 10 m room, the bat 14 m from the player.</summary>
    private static readonly string BatRoom = string.Join('\n',
        "........",
        "........",
        "P......B",
        "........",
        "........");

    [Fact]
    public void Leech_put_on_an_enemy_directly_takes_no_hold_and_reports_nothing()
    {
        // Leech acts on the attacker; it never sits on an enemy - not even when asked for directly.
        var world = new GameWorld(LevelParser.Parse(Corridor), 1, Always);
        var ghoul = world.Ghouls[0];

        Assert.False(world.ApplyStatus(ghoul, StatusKind.Leech, world.Player));

        Assert.False(ghoul.Status.Has(StatusKind.Leech));
        Assert.DoesNotContain(world.Events, e => e is StatusApplied);
    }

    // --- The player alone: what one player cannot show in the world yet ---

    [Fact]
    public void A_dead_player_neither_heals_nor_saves_anything()
    {
        // A dead player cannot fire in the world, so the rule is asked directly.
        var player = new Player(LevelParser.Parse("P"));
        player.TakeDamage(player.MaxHealth, []);
        Assert.True(player.IsDead);
        var events = new List<GameEvent>();

        // One hit worth a fraction, one worth whole points: neither heals, neither is saved.
        Assert.Equal(0, player.HealByLeech(1, Values, events));
        Assert.Equal(0, player.HealByLeech(1000, Values, events));

        Assert.Equal(0, player.Health);
        Assert.Equal(0m, player.LeechSaved);
        Assert.Empty(events);
    }

    [Fact]
    public void Each_player_saves_his_own_fractions()
    {
        // Online co-op: one player's hits never heal another. Today the world has one player, so two players stand alone.
        var hitter = new Player(LevelParser.Parse("P")) { Id = 1 };
        var other = new Player(LevelParser.Parse("P")) { Id = 2 };
        hitter.TakeDamage(hitter.MaxHealth / 2, []);
        other.TakeDamage(other.MaxHealth / 2, []);
        var otherHealth = other.Health;
        var events = new List<GameEvent>();

        // Hits of 1 damage until one whole point is saved up.
        var hits = (int)decimal.Ceiling(1m / Share);
        for (var hit = 0; hit < hits; hit++)
        {
            hitter.HealByLeech(1, Values, events);
        }

        Assert.Equal([new PlayerHealed(1, 1)], events);
        Assert.Equal(hits * Share - 1m, hitter.LeechSaved);
        Assert.Equal(0m, other.LeechSaved);
        Assert.Equal(otherHealth, other.Health);

        // The other player's first small hit starts from nothing: it heals nothing, the hitter's savings do not help.
        Assert.Equal(0, other.HealByLeech(1, Values, events));
        Assert.Equal(Share, other.LeechSaved);
    }

    [Fact]
    public void Heal_by_leech_without_events_is_an_error()
    {
        var player = new Player(LevelParser.Parse("P"));
        Assert.Throws<ArgumentNullException>(() => player.HealByLeech(1, Values, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => player.HealByLeech(-1, Values, []));
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
}
