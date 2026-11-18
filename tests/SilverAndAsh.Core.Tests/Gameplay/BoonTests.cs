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

    [Fact]
    public void A_level_up_offers_three_different_boons_from_the_pool()
    {
        var player = new Player(LevelParser.Parse("P"));

        player.AddExperience(100, []);

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

        world.Player.AddExperience(100, []);

        Assert.Equal(2, world.Player.Level);
        Assert.Equal(0, world.Player.PendingBoons);
    }

    [Fact]
    public void A_boon_that_was_not_offered_cannot_be_chosen()
    {
        var player = new Player(LevelParser.Parse("P"));
        player.AddExperience(100, []);
        var missing = Pool.Except(player.CurrentOffer).First();

        Assert.False(player.Choose(missing, []));
        Assert.Equal(1, player.PendingBoons);
    }

    [Fact]
    public void Two_levels_at_once_are_chosen_one_after_the_other()
    {
        var world = new GameWorld(LevelParser.Parse("P"), balance: OfferAll);
        var player = world.Player;
        player.AddExperience(225, []);          // 100 + 125
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

        Assert.True(player.Position.Z > playerAt.Z + 0.5f, "The player did not walk.");
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
        var world = new GameWorld(LevelParser.Parse("P\n.\n."));
        var player = world.Player;
        player.AddExperience(Balance.Default.Experience.FirstLevel, []);
        var first = player.CurrentOffer.ToList();
        world.Tick(PlayerInput.None);

        player.AddExperience(10_000, []);
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
        player.AddExperience(100, []);
        var start = player.Position;

        world.Tick(PlayerInput.None with { ChooseBoon = player.CurrentOffer[0] });
        for (var tick = 0; tick < Second / 4; tick++)
        {
            world.Tick(Forward);
        }

        Assert.True(player.Position.Z > start.Z + 0.5f, "The player did not walk after choosing.");
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
        player.AddExperience(100, []);
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
        world.Player.AddExperience(100, []);
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
        world.Player.AddExperience(100, []);
        var boon = world.Player.CurrentOffer[0];

        world.Tick(PlayerInput.None with { ChooseBoon = boon });

        Assert.Contains(new BoonChosen(world.Player.Id, boon), world.Events);
        Assert.Equal(1, world.Player.BoonCount(boon));
    }

    [Fact]
    public void Health_adds_to_the_maximum_and_heals_as_much()
    {
        var player = PlayerWith(Boon.Health, times: 3, damageFirst: 50);

        // +5 three times, always from the base: 115 maximum, 50 + 15 health.
        Assert.Equal(115, player.MaxHealth);
        Assert.Equal(65, player.Health);
    }

    [Fact]
    public void Damage_adds_five_percent_of_the_base_each_time()
    {
        var rules = DamageRules.Standard();

        Assert.Equal(42, rules.Calculate(new Hit(Weapon.Blade, HitZone.Body, 40, DamageBoons: 1)).Amount);   // 40 x 1.05
        Assert.Equal(46, rules.Calculate(new Hit(Weapon.Blade, HitZone.Body, 40, DamageBoons: 3)).Amount);   // 40 x 1.15
        Assert.Equal(60, rules.Calculate(new Hit(Weapon.Blade, HitZone.Body, 40, DamageBoons: 10)).Amount);  // 40 x 1.5, not 1.05^10 = 65
        Assert.Equal(37, rules.Calculate(new Hit(Weapon.Pistol, HitZone.Body, 35, DamageBoons: 1)).Amount);  // 36.75
        Assert.Equal(40, rules.Calculate(new Hit(Weapon.Blade, HitZone.Body, 40)).Amount);
    }

    [Fact]
    public void A_stronger_player_hits_harder_in_the_world()
    {
        var world = new GameWorld(LevelParser.Parse("P\nD"), balance: OfferAll);
        var dummy = world.Dummies[0];
        world.Player.AddExperience(100, []);
        world.Tick(PlayerInput.None with { ChooseBoon = Boon.Damage });
        Face(world.Player, dummy.Position + new Vector3(0f, 1f, 0f));

        world.Tick(PlayerInput.None with { Melee = true });
        world.Tick(PlayerInput.None);

        Assert.Equal(Balance.Default.Dummy.MaxHealth - 42, dummy.Health);
    }

    [Fact]
    public void Stamina_regeneration_refills_the_bar_faster()
    {
        var normal = TicksToRefill(PlayerWith(Boon.StaminaRegen, times: 0));
        var faster = TicksToRefill(PlayerWith(Boon.StaminaRegen, times: 2));

        // 3 s = 180 ticks; with +10 % from the base: 180 / 1.1 = 163.6 -> 164 ticks.
        Assert.Equal(180, normal);
        Assert.Equal(164, faster);
    }

    [Fact]
    public void Experience_brings_more_experience_from_the_base()
    {
        var player = PlayerWith(Boon.Experience, times: 4);
        var events = new List<GameEvent>();

        player.AddExperience(30, events);       // 30 x (1 + 4 x 0.025) = 33

        Assert.Contains(new ExperienceGained(player.Id, 33), events);
    }

    [Fact]
    public void A_restart_takes_every_boon_away()
    {
        var world = new GameWorld(LevelParser.Parse("P"), balance: OfferAll);
        world.Player.AddExperience(225, []);
        world.Tick(PlayerInput.None with { ChooseBoon = Boon.Health });
        world.Tick(PlayerInput.None with { ChooseBoon = Boon.Damage });

        var restarted = world.Restarted().Player;

        Assert.Equal(Balance.Default.Player.MaxHealth, restarted.MaxHealth);
        Assert.All(Pool, boon => Assert.Equal(0, restarted.BoonCount(boon)));
        Assert.Equal(0, restarted.PendingBoons);
    }

    [Fact]
    public void The_boons_come_from_the_world()
    {
        var balance = OfferAll with
        {
            Boons = OfferAll.Boons with { Health = 30, Damage = 0.5f, Experience = 1f },
        };
        var player = new Player(LevelParser.Parse("P"), balance: balance);
        player.AddExperience(100, []);
        player.Choose(Boon.Health, []);

        Assert.Equal(130, player.MaxHealth);
        Assert.Equal(60, DamageRules.Standard(balance).Calculate(new Hit(Weapon.Blade, HitZone.Body, 40, DamageBoons: 1)).Amount);
    }

    private static IReadOnlyList<Boon> FirstOffer(int seed)
    {
        var world = new GameWorld(LevelParser.Parse("P"), seed);
        world.Player.AddExperience(100, []);
        return world.Player.CurrentOffer.ToList();
    }

    /// <summary>A player who chose <paramref name="boon"/> <paramref name="times"/> times (the whole pool is offered).</summary>
    private static Player PlayerWith(Boon boon, int times, int damageFirst = 0)
    {
        var player = new Player(LevelParser.Parse("P"), balance: OfferAll);
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
