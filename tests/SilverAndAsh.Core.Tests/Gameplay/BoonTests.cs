using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class BoonTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly Boon[] Pool = Enum.GetValues<Boon>();

    /// <summary>Every level up offers the whole pool - so a test can choose any boon.</summary>
    private static readonly Balance OfferAll = Balance.Default with
    {
        Boons = Balance.Default.Boons with { Offered = Enum.GetValues<Boon>().Length },
    };

    /// <summary>Experience of the first level in the standard balance - enough for exactly one level up.</summary>
    private static readonly int FirstLevel = Balance.Default.Experience.FirstLevel;

    /// <summary>A round level curve of its own (issue #586): 100 for the first level, 200 for the second, 400 ...</summary>
    private static readonly Balance RoundCurve = OfferAll with
    {
        Experience = new ExperienceBalance { FirstLevel = 100, Growth = 2f },
    };

    [Fact]
    public void A_level_up_offers_as_many_different_boons_from_the_pool_as_the_balance_says()
    {
        var player = new Player(LevelParser.Parse("P"));

        player.AddExperience(FirstLevel, []);

        Assert.Equal(1, player.PendingBoons);
        Assert.Equal(Balance.Default.Boons.Offered, player.CurrentOffer.Count);
        Assert.Equal(player.CurrentOffer.Count, player.CurrentOffer.Distinct().Count());
        Assert.All(player.CurrentOffer, boon => Assert.Contains(boon, Pool));
    }

    [Fact]
    public void The_same_seed_offers_the_same()
    {
        Assert.Equal(FirstOffer(5), FirstOffer(5));
    }

    [Fact]
    public void Other_seeds_offer_other_boons()
    {
        var reference = FirstOffer(5);

        Assert.Contains(Enumerable.Range(0, 10), seed => !FirstOffer(seed).SequenceEqual(reference));
    }

    [Fact]
    public void Over_many_level_ups_every_boon_is_offered()
    {
        var offered = new HashSet<Boon>();
        for (var seed = 0; seed < 20; seed++)
        {
            offered.UnionWith(FirstOffer(seed));
        }

        Assert.Equal(Pool.Length, offered.Count);
    }

    [Fact]
    public void Without_anything_to_offer_the_world_does_not_wait()
    {
        // The balancing file refuses 0, but code could still set it.
        var world = new GameWorld(LevelParser.Parse("P"), balance: Balance.Default with
        {
            Boons = Balance.Default.Boons with { Offered = 0 },
        });

        world.Player.AddExperience(FirstLevel, []);

        Assert.Equal(1, world.Player.Level);
        Assert.Equal(0, world.Player.PendingBoons);
    }

    [Fact]
    public void A_boon_that_was_not_offered_cannot_be_chosen()
    {
        // An offer of its own that leaves exactly one boon out (issue #586) - with the whole pool on offer,
        // nothing would be missing.
        var player = new Player(LevelParser.Parse("P"), balance: Balance.Default with
        {
            Boons = Balance.Default.Boons with { Offered = Pool.Length - 1 },
        });
        player.AddExperience(Balance.Default.Experience.FirstLevel, []);
        var missing = Assert.Single(Pool.Except(player.CurrentOffer));

        Assert.False(player.Choose(missing, []));
        Assert.Equal(1, player.PendingBoons);
    }

    [Fact]
    public void Two_levels_at_once_are_chosen_one_after_the_other()
    {
        var world = new GameWorld(LevelParser.Parse("P"), balance: RoundCurve);
        var player = world.Player;
        player.AddExperience(300, []);          // 100 + 200
        Assert.Equal(2, player.PendingBoons);

        world.Tick(PlayerInput.None with { ChooseBoon = Boon.Health });
        Assert.Equal(1, player.PendingBoons);

        world.Tick(PlayerInput.None with { ChooseBoon = Boon.Damage });
        Assert.Equal(0, player.PendingBoons);
        Assert.Empty(player.CurrentOffer);
    }

    [Fact]
    public void While_a_boon_waits_the_world_goes_on()
    {
        // Decision record 0008: no standstill - the player walks, the ghoul crawls, the boon keeps waiting.
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\n.\n.\n.\nG"));
        var player = world.Player;
        var ghoul = world.Ghouls[0];
        player.AddExperience(Balance.Default.Experience.FirstLevel, []);
        var playerAt = player.Position;
        var ghoulAt = ghoul.Position;

        for (var tick = 0; tick < Second / 2; tick++)
        {
            world.Tick(Forward);
        }

        var walked = player.Position.Z - playerAt.Z;
        var least = LeastWalk(world.Balance, Second / 2);
        Assert.True(walked > least, $"The player did not walk: {walked:0.00} m, at least {least:0.00} m.");
        Assert.NotEqual(ghoulAt, ghoul.Position);
        Assert.Equal(1, player.PendingBoons);
    }

    [Fact]
    public void A_boon_can_be_chosen_long_after_the_level_up()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n."), balance: OfferAll);
        var player = world.Player;
        player.AddExperience(Balance.Default.Experience.FirstLevel, []);
        for (var tick = 0; tick < 3 * Second; tick++)
        {
            world.Tick(PlayerInput.None);
        }

        world.Tick(PlayerInput.None with { ChooseBoon = Boon.Damage });

        Assert.Equal(0, player.PendingBoons);
        Assert.Equal(1, player.BoonCount(Boon.Damage));
    }

    [Fact]
    public void A_level_up_while_a_boon_waits_queues_up_behind_it()
    {
        // The second level up comes later, during play: its offer waits behind the first and comes after it.
        var world = new GameWorld(LevelParser.Parse("P\n.\n."), balance: RoundCurve with { Boons = Balance.Default.Boons });
        var player = world.Player;
        player.AddExperience(100, []);
        var first = player.CurrentOffer.ToList();
        world.Tick(PlayerInput.None);

        player.AddExperience(200 + 400, []);    // levels 2 and 3
        var waiting = player.PendingBoons;
        Assert.True(waiting >= 2, $"Only {waiting} level ups wait.");
        Assert.Equal(first, player.CurrentOffer);

        world.Tick(PlayerInput.None with { ChooseBoon = first[0] });

        Assert.Equal(waiting - 1, player.PendingBoons);
        Assert.NotEmpty(player.CurrentOffer);
    }

    [Fact]
    public void After_the_choice_the_world_goes_on()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n."));
        var player = world.Player;
        player.AddExperience(FirstLevel, []);
        var start = player.Position;

        // Whatever the balance and the seed offer (issue #664): the first boon on offer, and the choice must succeed.
        var boon = player.CurrentOffer[0];
        world.Tick(PlayerInput.None with { ChooseBoon = boon });
        Assert.Equal(0, player.PendingBoons);
        Assert.Equal(1, player.BoonCount(boon));
        for (var tick = 0; tick < Second / 4; tick++)
        {
            world.Tick(Forward);
        }

        var walked = player.Position.Z - start.Z;
        var least = LeastWalk(world.Balance, Second / 4);
        Assert.True(walked > least, $"The player did not walk after choosing: {walked:0.00} m, at least {least:0.00} m.");
    }

    /// <summary>Share of the ideal way a walking player must cover - see <see cref="LeastWalk"/>.</summary>
    private const float LeastWalkShare = 0.9f;

    /// <summary>
    /// 90 % of the way a player covers walking straight ahead from a standstill for <paramref name="ticks"/> ticks
    /// with <paramref name="balance"/> (issues #664, #677), counted tick by tick as the game moves: each tick the
    /// speed first grows by <see cref="PlayerBalance.Acceleration"/> up to <see cref="PlayerBalance.MaxSpeed"/>,
    /// then the player moves one tick at that speed.
    /// </summary>
    /// <remarks>
    /// On the flat, open floor of these tests nothing in the game costs any of the way: with the standard balance
    /// the player covers exactly this ideal way (2.75 m in half a second, 1.25 m in a quarter). The 10 % left are
    /// room for how a tick is counted, not for the walking itself: moving first and speeding up after would move
    /// each tick at the speed of the tick before and so cost a whole tick at top speed (10 cm) - 1.15 m instead of
    /// 1.25 m in a quarter of a second (92 %), 2.65 m instead of 2.75 m in half a second (96.4 %). That is only
    /// 2.5 cm above the threshold of 1.125 m, so the share must not go above 90 %. A player who walks a quarter or
    /// a third slower after the level up falls far below.
    /// </remarks>
    private static float LeastWalk(Balance balance, int ticks)
    {
        var walking = balance.Player;
        Assert.True(
            ticks * SimulationClock.SecondsPerTick > walking.AccelerationTime,
            "The player is still speeding up - the case would prove nothing.");

        var speed = 0f;
        var way = 0f;
        for (var tick = 0; tick < ticks; tick++)
        {
            speed = MathF.Min(speed + (walking.Acceleration * SimulationClock.SecondsPerTick), walking.MaxSpeed);
            way += speed * SimulationClock.SecondsPerTick;
        }

        return LeastWalkShare * way;
    }

    [Fact]
    public void The_view_turns_while_a_boon_waits()
    {
        var player = new Player(LevelParser.Parse("P"));
        player.AddExperience(Balance.Default.Experience.FirstLevel, []);
        var yaw = player.View.YawRadians;

        player.Look(PlayerInput.None with { Look = new Vector2(200f, 0f) });

        Assert.NotEqual(yaw, player.View.YawRadians);
    }

    [Fact]
    public void A_player_without_a_level_up_cannot_choose()
    {
        var player = new Player(LevelParser.Parse("P"));
        var events = new List<GameEvent>();

        var chosen = player.Choose(Boon.Health, events);

        Assert.False(chosen);
        Assert.Equal(Balance.Default.Player.MaxHealth, player.MaxHealth);
        Assert.Empty(events);
    }

    [Fact]
    public void A_choice_without_a_level_up_does_nothing_in_the_world()
    {
        var world = new GameWorld(LevelParser.Parse("P"));

        world.Tick(PlayerInput.None with { ChooseBoon = Boon.Health });

        Assert.Equal(Balance.Default.Player.MaxHealth, world.Player.MaxHealth);
        Assert.DoesNotContain(world.Events, e => e is BoonChosen);
    }

    [Fact]
    public void A_dead_player_cannot_choose_health_to_live_again()
    {
        // The bonus choice stays open when hit (issue #346) - also by the deadly hit. Choosing then changes nothing.
        var player = new Player(LevelParser.Parse("P"), balance: OfferAll);
        player.AddExperience(FirstLevel, []);
        player.TakeDamage(player.MaxHealth, []);
        var events = new List<GameEvent>();

        var chosen = player.Choose(Boon.Health, events);

        Assert.False(chosen);
        Assert.True(player.IsDead);
        Assert.Equal(0, player.Health);
        Assert.Equal(1, player.PendingBoons);
        Assert.Empty(events);
    }

    [Fact]
    public void A_dead_player_choosing_health_in_the_world_stays_dead()
    {
        var world = new GameWorld(LevelParser.Parse("P"), balance: OfferAll);
        world.Player.AddExperience(FirstLevel, []);
        world.Player.TakeDamage(world.Player.MaxHealth, []);

        // Choosing in every tick until the end screen is due: the run still ends.
        for (var tick = 0; tick < SimulationClock.SecondsToTicks(OfferAll.RestartDelay); tick++)
        {
            world.Tick(PlayerInput.None with { ChooseBoon = Boon.Health });
            Assert.DoesNotContain(world.Events, e => e is BoonChosen);
        }

        Assert.True(world.Player.IsDead);
        Assert.Equal(1, world.Player.PendingBoons);
        Assert.True(world.ShouldRestart);
    }

    [Fact]
    public void Choosing_is_reported()
    {
        var world = new GameWorld(LevelParser.Parse("P"));
        world.Player.AddExperience(FirstLevel, []);
        var boon = world.Player.CurrentOffer[0];

        world.Tick(PlayerInput.None with { ChooseBoon = boon });

        Assert.Contains(new BoonChosen(world.Player.Id, boon), world.Events);
        Assert.Equal(1, world.Player.BoonCount(boon));
    }

    [Fact]
    public void Health_adds_to_the_maximum_and_heals_as_much()
    {
        var balance = OfferAll with
        {
            Player = OfferAll.Player with { MaxHealth = 100 },
            Boons = OfferAll.Boons with { Health = 10, HealthCap = 1000 },
        };
        var player = PlayerWith(Boon.Health, times: 3, damageFirst: 50, balance);

        // +10 three times, always from the base: 130 maximum, 50 + 30 health.
        Assert.Equal(130, player.MaxHealth);
        Assert.Equal(80, player.Health);
    }

    [Fact]
    public void Damage_adds_its_share_of_the_base_each_time()
    {
        var rules = DamageRules.Standard(Balance.Default with
        {
            Boons = Balance.Default.Boons with { Damage = 0.1f },
        });

        Assert.Equal(44, rules.Calculate(new Hit(Weapon.Blade, HitZone.Body, 40, DamageBoons: 1)).Amount);   // 40 x 1.1
        Assert.Equal(52, rules.Calculate(new Hit(Weapon.Blade, HitZone.Body, 40, DamageBoons: 3)).Amount);   // 40 x 1.3
        Assert.Equal(80, rules.Calculate(new Hit(Weapon.Blade, HitZone.Body, 40, DamageBoons: 10)).Amount);  // 40 x 2, not 1.1^10 = 104
        Assert.Equal(41, rules.Calculate(new Hit(Weapon.Pistol, HitZone.Body, 37, DamageBoons: 1)).Amount);  // 40.7
        Assert.Equal(40, rules.Calculate(new Hit(Weapon.Blade, HitZone.Body, 40)).Amount);
    }

    [Fact]
    public void A_stronger_player_hits_harder_in_the_world()
    {
        var balance = OfferAll with
        {
            Blade = OfferAll.Blade with { Damage = 40 },
            Dummy = OfferAll.Dummy with { MaxHealth = 100 },
            Boons = OfferAll.Boons with { Damage = 0.1f },
        };
        var world = new GameWorld(LevelParser.Parse("P\nD"), balance: balance);
        var dummy = world.Dummies[0];
        world.Player.AddExperience(balance.Experience.FirstLevel, []);
        world.Tick(PlayerInput.None with { ChooseBoon = Boon.Damage });
        Face(world.Player, dummy.Position + new Vector3(0f, 1f, 0f));

        world.Tick(PlayerInput.None with { Melee = true });
        world.Tick(PlayerInput.None);

        Assert.Equal(100 - 44, dummy.Health);  // 40 x 1.1
    }

    [Fact]
    public void Stamina_regeneration_refills_the_bar_faster()
    {
        var balance = OfferAll with
        {
            Player = OfferAll.Player with { StaminaRechargeTime = 2f, StaminaJumps = 4 },
            Boons = OfferAll.Boons with { StaminaRegen = 0.25f },
        };
        var normal = TicksToRefill(PlayerWith(Boon.StaminaRegen, times: 0, balance: balance));
        var faster = TicksToRefill(PlayerWith(Boon.StaminaRegen, times: 2, balance: balance));

        // 2 s = 120 ticks; with +50 % from the base: 120 / 1.5 = 80 ticks (not 120 / 1.25^2 = 76.8 -> 77).
        Assert.Equal(120, normal);
        Assert.Equal(80, faster);
    }

    [Fact]
    public void Experience_brings_more_experience_from_the_base()
    {
        var player = PlayerWith(Boon.Experience, times: 4, balance: OfferAll with
        {
            Boons = OfferAll.Boons with { Experience = 0.25f },
        });
        var events = new List<GameEvent>();

        player.AddExperience(30, events);       // 30 x (1 + 4 x 0.25) = 60, not 30 x 1.25^4 = 73

        Assert.Contains(new ExperienceGained(player.Id, 60), events);
    }

    [Fact]
    public void A_restart_takes_every_boon_away()
    {
        var world = new GameWorld(LevelParser.Parse("P"), balance: RoundCurve);
        world.Player.AddExperience(300, []);    // 100 + 200: two level ups
        world.Tick(PlayerInput.None with { ChooseBoon = Boon.Health });
        world.Tick(PlayerInput.None with { ChooseBoon = Boon.Damage });

        var restarted = world.Restarted().Player;

        Assert.Equal(RoundCurve.Player.MaxHealth, restarted.MaxHealth);
        Assert.All(Pool, boon => Assert.Equal(0, restarted.BoonCount(boon)));
        Assert.Equal(0, restarted.PendingBoons);
    }

    [Fact]
    public void The_boons_come_from_the_world()
    {
        var balance = OfferAll with
        {
            Player = OfferAll.Player with { MaxHealth = 100 },
            Boons = OfferAll.Boons with { Health = 30, HealthCap = 1000, Damage = 0.5f, Experience = 1f },
        };
        var player = new Player(LevelParser.Parse("P"), balance: balance);
        player.AddExperience(balance.Experience.FirstLevel, []);
        player.Choose(Boon.Health, []);

        Assert.Equal(130, player.MaxHealth);
        Assert.Equal(60, DamageRules.Standard(balance).Calculate(new Hit(Weapon.Blade, HitZone.Body, 40, DamageBoons: 1)).Amount);
    }

    private static IReadOnlyList<Boon> FirstOffer(int seed)
    {
        var world = new GameWorld(LevelParser.Parse("P"), seed);
        world.Player.AddExperience(FirstLevel, []);
        return world.Player.CurrentOffer.ToList();
    }

    /// <summary>
    /// A player who chose <paramref name="boon"/> <paramref name="times"/> times, with <paramref name="balance"/>
    /// (<see cref="OfferAll"/> if none is given; the whole pool must be offered).
    /// </summary>
    private static Player PlayerWith(Boon boon, int times, int damageFirst = 0, Balance? balance = null)
    {
        var player = new Player(LevelParser.Parse("P"), balance: balance ?? OfferAll);
        if (damageFirst > 0)
        {
            player.TakeDamage(damageFirst, []);
        }

        for (var i = 0; i < times; i++)
        {
            player.AddExperience(player.ExperienceToNextLevel - player.Experience, []);
            Assert.True(player.Choose(boon, []));
        }

        return player;
    }

    /// <summary>Empties the stamina bar, lets it refill and counts the ticks until it is full again.</summary>
    private static int TicksToRefill(Player player)
    {
        var stamina = player.Stamina;
        Assert.True(stamina.TryPay(stamina.Segments));
        stamina.SetRecharging(true);
        var ticks = 0;
        for (; stamina.Fraction < 1f; ticks++)
        {
            Assert.True(ticks < 10 * Second, "The bar never filled up.");
            stamina.Tick();
        }

        return ticks;
    }

    [Fact]
    public void Health_is_offered_up_to_the_cap_and_never_beyond_it()
    {
        // Issue #435: the life bar is one pixel per point, so the maximum health stops at the cap.
        var boons = OfferAll.Boons;
        var world = new GameWorld(LevelParser.Parse("P"), balance: OfferAll);
        var player = world.Player;
        var steps = (boons.HealthCap - OfferAll.Player.MaxHealth) / boons.Health;
        Assert.True(steps > 1, "The cap leaves no room for health boons - the case would prove nothing.");

        for (var step = 0; step < steps; step++)
        {
            LevelUp(player);
            Assert.Contains(Boon.Health, player.CurrentOffer);
            world.Tick(PlayerInput.None with { ChooseBoon = Boon.Health });
        }

        Assert.Equal(OfferAll.Player.MaxHealth + (steps * boons.Health), player.MaxHealth);
        Assert.True(player.MaxHealth + boons.Health > boons.HealthCap, "One more health boon would still fit.");
        for (var more = 0; more < 3; more++)
        {
            LevelUp(player);
            Assert.DoesNotContain(Boon.Health, player.CurrentOffer);
            Assert.NotEmpty(player.CurrentOffer);
            world.Tick(PlayerInput.None with { ChooseBoon = Boon.Damage });
        }

        Assert.True(player.MaxHealth <= boons.HealthCap);
    }

    [Fact]
    public void Two_levels_at_once_offer_health_only_as_often_as_it_still_fits()
    {
        // With room for one more health boon, two waiting offers must not both hold it - else both could be taken.
        var boons = OfferAll.Boons;
        var world = new GameWorld(LevelParser.Parse("P"), balance: OfferAll);
        var player = world.Player;
        var steps = (boons.HealthCap - OfferAll.Player.MaxHealth) / boons.Health;
        for (var step = 0; step < steps - 1; step++)
        {
            LevelUp(player);
            world.Tick(PlayerInput.None with { ChooseBoon = Boon.Health });
        }

        player.AddExperience(player.ExperienceToNextLevel - player.Experience, []);
        player.AddExperience(player.ExperienceToNextLevel - player.Experience, []);
        Assert.Equal(2, player.PendingBoons);

        Assert.Contains(Boon.Health, player.CurrentOffer);
        world.Tick(PlayerInput.None with { ChooseBoon = Boon.Health });
        Assert.Equal(1, player.PendingBoons);
        Assert.DoesNotContain(Boon.Health, player.CurrentOffer);
        Assert.Equal(boons.HealthCap - ((boons.HealthCap - OfferAll.Player.MaxHealth) % boons.Health), player.MaxHealth);
    }

    [Fact]
    public void A_cap_below_the_starting_health_never_offers_health()
    {
        var balance = OfferAll with { Boons = OfferAll.Boons with { HealthCap = OfferAll.Player.MaxHealth - 1 } };
        var world = new GameWorld(LevelParser.Parse("P"), balance: balance);
        var player = world.Player;

        for (var level = 0; level < 5; level++)
        {
            LevelUp(player);
            Assert.DoesNotContain(Boon.Health, player.CurrentOffer);
            Assert.Equal(Pool.Length - 1, player.CurrentOffer.Count); // the rest of the pool is still offered
            world.Tick(PlayerInput.None with { ChooseBoon = Boon.Damage });
        }

        Assert.Equal(balance.Player.MaxHealth, player.MaxHealth);
    }

    // Exactly one level up, with the experience the game itself asks for.
    private static void LevelUp(Player player)
    {
        Assert.Equal(0, player.PendingBoons);
        player.AddExperience(player.ExperienceToNextLevel - player.Experience, []);
        Assert.Equal(1, player.PendingBoons);
    }

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
