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
    private static readonly int FirstLevel = Balance.Default.Experience.LevelCosts[0].Cost;

    /// <summary>A round level curve of its own (issue #586, as a table since #503): 100 for the first level, 200 for the second, 400 from the third on.</summary>
    private static readonly Balance RoundCurve = OfferAll with
    {
        Experience = new ExperienceBalance { LevelCosts = [new(0, 100), new(1, 200), new(2, 400)] },
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
        player.AddExperience(Balance.Default.Experience.LevelCosts[0].Cost, []);
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
        // The twin is the same world without the level up (issue #687).
        const string Level = "P\n.\n.\n.\n.\n.\nG";
        var world = new GameWorld(LevelParser.Parse(Level));
        var twin = new GameWorld(LevelParser.Parse(Level));
        var player = world.Player;
        var ghoul = world.Ghouls[0];
        player.AddExperience(Balance.Default.Experience.LevelCosts[0].Cost, []);
        var start = Start(world, twin);
        var ghoulAt = ghoul.Position;

        WalkBoth(world, twin, Second / 2);

        AssertWalkedAsFar(world, twin, start, "while a boon waits");
        Assert.NotEqual(ghoulAt, ghoul.Position);
        Assert.Equal(1, player.PendingBoons);
    }

    [Fact]
    public void A_boon_can_be_chosen_long_after_the_level_up()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n."), balance: OfferAll);
        var player = world.Player;
        player.AddExperience(Balance.Default.Experience.LevelCosts[0].Cost, []);
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
        const string Level = "P\n.\n.";
        var world = new GameWorld(LevelParser.Parse(Level));
        var twin = new GameWorld(LevelParser.Parse(Level));
        var player = world.Player;
        player.AddExperience(FirstLevel, []);
        var start = Start(world, twin);

        // Whatever the balance and the seed offer (issue #664): the first boon on offer, and the choice must succeed.
        // The twin gets the same input without the choice: the two worlds differ in just what is tested, so a fault
        // that hangs on pressing the choice (not only on taking the offer) does not hit both alike.
        var boon = player.CurrentOffer[0];
        TickBoth(world, twin, PlayerInput.None with { ChooseBoon = boon }, PlayerInput.None);
        Assert.Equal(0, player.PendingBoons);
        Assert.Equal(1, player.BoonCount(boon));
        WalkBoth(world, twin, Second / 4);

        AssertWalkedAsFar(world, twin, start, "after choosing");
    }

    [Fact]
    public void A_player_who_chooses_while_walking_keeps_the_pace()
    {
        // Issue #687: the choice comes in the middle of a run, at top speed - it must not cost any of it.
        const string Level = "P\n.\n.\n.\n.\n.\n.\n.\n.\n.";
        var world = new GameWorld(LevelParser.Parse(Level));
        var twin = new GameWorld(LevelParser.Parse(Level));
        var player = world.Player;
        player.AddExperience(FirstLevel, []);
        var boon = player.CurrentOffer[0];
        WalkBoth(world, twin, Second / 4);
        var start = Start(world, twin);
        var speedBefore = player.HorizontalSpeed;

        // The twin walks on without pressing the choice - the only difference between the worlds is the choice itself.
        TickBoth(world, twin, Forward with { ChooseBoon = boon }, Forward);
        Assert.Equal(0, player.PendingBoons);
        Assert.Equal(1, player.BoonCount(boon));
        var speed = player.HorizontalSpeed;
        var twinSpeed = twin.Player.HorizontalSpeed;
        Assert.True(
            speed >= WalkShare * twinSpeed,
            $"The choice cost the player's pace: {speed:0.000} m/s, the run without the level up {twinSpeed:0.000} m/s.");
        Assert.True(
            speed >= WalkShare * speedBefore,
            $"The choice cost the player's pace: {speed:0.000} m/s, before it {speedBefore:0.000} m/s.");
        WalkBoth(world, twin, Second / 4);

        AssertWalkedAsFar(world, twin, start, "after choosing while walking");
    }

    /// <summary>Share of the twin's way (and pace) a player with a level up must reach - see <see cref="AssertWalkedAsFar"/>.</summary>
    private const float WalkShare = 0.99f;

    /// <summary>Where the players of <paramref name="world"/> and <paramref name="twin"/> stand now.</summary>
    private static (Vector3 World, Vector3 Twin) Start(GameWorld world, GameWorld twin) =>
        (world.Player.Position, twin.Player.Position);

    /// <summary>One tick with the same <paramref name="input"/> in both worlds.</summary>
    private static void TickBoth(GameWorld world, GameWorld twin, PlayerInput input) => TickBoth(world, twin, input, input);

    /// <summary>One tick: <paramref name="input"/> in <paramref name="world"/>, <paramref name="twinInput"/> in <paramref name="twin"/>.</summary>
    private static void TickBoth(GameWorld world, GameWorld twin, PlayerInput input, PlayerInput twinInput)
    {
        world.Tick(input);
        twin.Tick(twinInput);
    }

    /// <summary>
    /// Both worlds walk straight ahead for <paramref name="ticks"/> ticks. The twin must end at its top speed - while
    /// the player is still speeding up or stopped by a wall, a slower top speed would not show and the case would prove
    /// nothing. That is measured in the twin itself (its speed no longer grows), not counted from the balance.
    /// </summary>
    private static void WalkBoth(GameWorld world, GameWorld twin, int ticks)
    {
        var speedBefore = twin.Player.HorizontalSpeed;
        for (var tick = 0; tick < ticks; tick++)
        {
            speedBefore = twin.Player.HorizontalSpeed;
            TickBoth(world, twin, Forward);
        }

        var speed = twin.Player.HorizontalSpeed;
        Assert.True(
            speed > 0f && speed == speedBefore,
            $"The run without the level up is not at its top speed ({speedBefore:0.000} -> {speed:0.000} m/s) - the case would prove nothing.");
    }

    /// <summary>
    /// The player of <paramref name="world"/> (with a level up) covered at least 99 % of the way the player of
    /// <paramref name="twin"/> covered since <paramref name="start"/> - the twin is the same level with the same seed,
    /// balance and input, but without the level up and without pressing the choice (issue #687).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both players run through the same code with the same input, and nothing in walking reads the boons: on the
    /// flat floor of these tests both end on the same spot (measured: 100.0 % in all three tests). Unlike the ideal
    /// way counted from the balance before (issue #677, 90 %), the twin moves with every change to walking, so the
    /// share need not leave room for how a tick is counted.
    /// </para>
    /// <para>
    /// What the 1 % lets through, counted with the standard balance (top speed 6 m/s, +1 m/s per tick, so 0.1 m per
    /// tick at top speed). The shortest run is a quarter of a second from a standstill: (1+2+3+4+5 + 10 x 6) / 60 =
    /// 1.25 m, of which 1 % is 1.25 cm - an eighth of one tick at top speed. A top speed lower by the factor f after
    /// the level up gives (15 + 60 f) / 60 m there; below 99 % of 1.25 m once f &lt; 0.9875. That 1.25 % is only the
    /// loosest bound: while a boon waits (half a second, (15 + 150 f) / 165 &lt; 0.99) the way is short from f &lt; 0.989,
    /// so from about 1.1 % slower; after a choice made while walking the pace itself is compared at top speed, so
    /// anything more than 1 % slower is caught already.
    /// </para>
    /// <para>
    /// What must be caught lies far below 99 %: 7 % slower after the choice gives (15 + 60 x 0.93) / 60 = 1.18 m,
    /// 94.4 %; one walking tick lost costs a tick at top speed, 0.1 m: 1.15 m of 1.25 m (92 %), 2.65 m of 2.75 m
    /// (96.4 %); the pace set to 0 at the choice costs the whole run-up, (6 x 6 - 21) / 60 = 0.25 m.
    /// </para>
    /// <para>
    /// Why not 100 %: the two worlds are not the same on purpose - only one of them drew an offer from the world's
    /// random generator, so whatever draws from it later goes its own way. None of that may touch the walk, and the
    /// room it gets is far below any fault worth catching.
    /// </para>
    /// </remarks>
    private static void AssertWalkedAsFar(GameWorld world, GameWorld twin, (Vector3 World, Vector3 Twin) start, string when)
    {
        var walked = world.Player.Position.Z - start.World.Z;
        var twinWalked = twin.Player.Position.Z - start.Twin.Z;
        Assert.True(
            walked >= WalkShare * twinWalked,
            $"The player did not walk {when}: {walked:0.000} m, the run without the level up {twinWalked:0.000} m.");
    }

    [Fact]
    public void The_view_turns_while_a_boon_waits()
    {
        var player = new Player(LevelParser.Parse("P"));
        player.AddExperience(Balance.Default.Experience.LevelCosts[0].Cost, []);
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
        world.Player.AddExperience(balance.Experience.LevelCosts[0].Cost, []);
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
        player.AddExperience(balance.Experience.LevelCosts[0].Cost, []);
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
