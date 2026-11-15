using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class SilverTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };

    /// <summary>A corridor along +Z: the player in row 0, a ghoul 4 m ahead, room to walk on.</summary>
    private static readonly Level GhoulAhead = LevelParser.Parse("P\n.\nG\n.\n.");

    /// <summary>Every kill drops silver (to test what happens with a drop).</summary>
    private static readonly Balance AlwaysSilver = Balance.Default with
    {
        Silver = Balance.Default.Silver with { GhoulChance = 1f, BatChance = 1f },
    };

    private static Balance SilverPerKill(int ghoulMin, int ghoulMax, int batMin = 1, int batMax = 3) => Balance.Default with
    {
        Silver = new SilverBalance
        {
            GhoulChance = 1f, BatChance = 1f, GhoulMin = ghoulMin, GhoulMax = ghoulMax, BatMin = batMin, BatMax = batMax,
        },
    };

    [Fact]
    public void A_killed_ghoul_drops_silver_where_it_died()
    {
        var world = new GameWorld(GhoulAhead, balance: AlwaysSilver);
        var ghoul = world.Ghouls[0];

        Kill(ghoul);
        world.Tick(PlayerInput.None);

        var pile = Assert.Single(world.SilverPiles);
        Assert.InRange(pile.Amount, 1, 3);
        Assert.Equal(ghoul.Position.X, pile.Position.X, 0.01f);
        Assert.Equal(ghoul.Position.Z, pile.Position.Z, 0.01f);
        Assert.Contains(world.Events, e => e == new SilverDropped(pile.Id, pile.Amount, ghoul.Position));
    }

    [Fact]
    public void A_killed_bat_drops_silver_too_and_it_falls_to_the_floor()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nB"), balance: AlwaysSilver);
        Kill(world.Bats[0]);

        Run(world, PlayerInput.None, Second);

        var pile = Assert.Single(world.SilverPiles);
        Assert.InRange(pile.Amount, 1, 3);
        Assert.Equal(0f, pile.Position.Y, 0.001f);
    }

    [Fact]
    public void A_kill_drops_silver_only_once()
    {
        var world = new GameWorld(GhoulAhead, balance: AlwaysSilver);
        Kill(world.Ghouls[0]);

        Run(world, PlayerInput.None, Second);

        Assert.Single(world.SilverPiles);
    }

    [Fact]
    public void Dummies_drop_no_silver()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nD"), balance: AlwaysSilver);
        world.Dummies[0].TakeDamage(Balance.Default.Dummy.MaxHealth, []);

        Run(world, PlayerInput.None, Second);

        Assert.Empty(world.SilverPiles);
    }

    [Fact]
    public void Over_many_kills_every_amount_of_the_span_turns_up_and_nothing_else()
    {
        var amounts = new HashSet<int>();
        for (var seed = 0; seed < 60; seed++)
        {
            var world = new GameWorld(LevelParser.Parse("P#G\n.#.\n.#B"), seed, AlwaysSilver);
            Kill(world.Ghouls[0]);
            Kill(world.Bats[0]);
            world.Tick(PlayerInput.None);
            amounts.UnionWith(world.SilverPiles.Select(pile => pile.Amount));
        }

        Assert.Equal([1, 2, 3], amounts.Order());
    }

    [Fact]
    public void Each_kind_of_enemy_has_its_own_span()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nG\n.\nB"), balance: SilverPerKill(5, 5, 9, 9));
        Kill(world.Ghouls[0]);
        Kill(world.Bats[0]);

        world.Tick(PlayerInput.None);

        Assert.Equal(5, world.SilverPiles.Single(pile => pile.Position.Z < 3 * Level.CellSize).Amount);
        Assert.Equal(9, world.SilverPiles.Single(pile => pile.Position.Z > 3 * Level.CellSize).Amount);
    }

    [Fact]
    public void Without_a_chance_no_silver_drops()
    {
        var never = AlwaysSilver with { Silver = AlwaysSilver.Silver with { GhoulChance = 0f, BatChance = 0f } };
        var world = new GameWorld(LevelParser.Parse("P\n.\nG\n.\nB"), balance: never);
        Kill(world.Ghouls[0]);
        Kill(world.Bats[0]);

        Run(world, PlayerInput.None, Second);

        Assert.Empty(world.SilverPiles);
    }

    [Fact]
    public void Each_kind_of_enemy_has_its_own_silver_chance()
    {
        var onlyBats = AlwaysSilver with { Silver = AlwaysSilver.Silver with { GhoulChance = 0f } };
        var world = new GameWorld(LevelParser.Parse("P\n.\nG\n.\nB"), balance: onlyBats);
        Kill(world.Ghouls[0]);
        Kill(world.Bats[0]);

        world.Tick(PlayerInput.None);

        var pile = Assert.Single(world.SilverPiles);
        Assert.Equal(world.Bats[0].Position.Z, pile.Position.Z, 0.01f);
    }

    [Fact]
    public void About_half_of_the_kills_drop_silver_by_standard()
    {
        // 200 worlds, one ghoul and one bat each: about 50 % of each drop silver.
        var ghoulDrops = 0;
        var batDrops = 0;
        for (var seed = 0; seed < 200; seed++)
        {
            var world = new GameWorld(LevelParser.Parse("P#G\n.#.\n.#B"), seed);
            Kill(world.Ghouls[0]);
            Kill(world.Bats[0]);
            world.Tick(PlayerInput.None);
            ghoulDrops += world.SilverPiles.Count(pile => pile.Position.Z < Level.CellSize);
            batDrops += world.SilverPiles.Count(pile => pile.Position.Z > Level.CellSize);
        }

        Assert.InRange(ghoulDrops, 75, 125);
        Assert.InRange(batDrops, 75, 125);
    }

    [Fact]
    public void A_span_of_nothing_drops_nothing()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nG\n.\nB"), balance: SilverPerKill(0, 0, 0, 0));
        Kill(world.Ghouls[0]);
        Kill(world.Bats[0]);

        Run(world, PlayerInput.None, Second);

        Assert.Empty(world.SilverPiles);
    }

    [Fact]
    public void The_same_seed_drops_the_same_silver()
    {
        Assert.Equal(SilverAfterKillingEverything(11), SilverAfterKillingEverything(11));
        Assert.NotEqual(SilverAfterKillingEverything(11), SilverAfterKillingEverything(12));
    }

    [Fact]
    public void Consumable_chances_do_not_change_the_silver()
    {
        // Silver has its own random numbers: how often vials and ammunition drop (more or fewer rolls) never shifts
        // the amounts of silver a seed gives.
        static Balance Chance(float chance) => Balance.Default with
        {
            Drops = Balance.Default.Drops with { GhoulChance = chance, BatChance = chance },
        };

        Assert.Equal(SilverAfterKillingEverything(5, Chance(0f)), SilverAfterKillingEverything(5, Chance(1f)));
    }

    [Fact]
    public void Walking_over_silver_takes_it_and_counts_it()
    {
        var world = new GameWorld(GhoulAhead, balance: SilverPerKill(3, 3));
        var player = world.Player;
        Kill(world.Ghouls[0]);
        world.Tick(PlayerInput.None);
        var pile = world.SilverPiles[0];

        var seen = Run(world, Forward, Second);

        Assert.Equal(3, player.Silver);
        Assert.Empty(world.SilverPiles);
        Assert.True(pile.IsTaken);
        Assert.Contains(seen, e => e == new SilverTaken(pile.Id, player.Id, 3));
    }

    [Fact]
    public void Silver_is_taken_even_with_full_health()
    {
        var world = new GameWorld(GhoulAhead, balance: SilverPerKill(2, 2));
        Kill(world.Ghouls[0]);

        Run(world, Forward, Second);

        Assert.Equal(world.Player.MaxHealth, world.Player.Health);
        Assert.Equal(2, world.Player.Silver);
    }

    [Fact]
    public void Silver_adds_up()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nG\n.\nG\n.\n."), balance: SilverPerKill(2, 2));
        Kill(world.Ghouls[0]);
        Kill(world.Ghouls[1]);

        Run(world, Forward, 2 * Second);

        Assert.Equal(4, world.Player.Silver);
    }

    [Fact]
    public void Silver_lies_still_until_someone_comes()
    {
        var world = new GameWorld(GhoulAhead, balance: AlwaysSilver);
        Kill(world.Ghouls[0]);

        Run(world, PlayerInput.None, 3 * Second);

        Assert.Single(world.SilverPiles);
        Assert.Equal(0, world.Player.Silver);
    }

    [Fact]
    public void The_dead_take_no_silver()
    {
        // The player runs at the silver and dies just before it (0.8 m). The body still slides on over it
        // (about 0.3 m: close enough to touch it) - but the dead take nothing.
        var world = new GameWorld(GhoulAhead, balance: AlwaysSilver);
        var player = world.Player;
        Kill(world.Ghouls[0]);
        world.Tick(PlayerInput.None);
        var pile = world.SilverPiles[0];
        for (var tick = 0; FlatDistance(player.Position, pile.Position) > 0.8f; tick++)
        {
            Assert.True(tick < 3 * Second, "The player never got close to the silver.");
            world.Tick(Forward);
        }

        player.TakeDamage(player.Health, []);
        var seen = Run(world, Forward, Second);

        Assert.True(FlatDistance(player.Position, pile.Position) < BodySizes.Player.Radius + BodySizes.Pickup.Radius,
            "The body did not slide far enough to touch the silver - the test would prove nothing.");
        Assert.DoesNotContain(seen, e => e is SilverTaken);
        Assert.Equal(0, player.Silver);
    }

    [Fact]
    public void A_new_run_starts_without_silver()
    {
        var world = new GameWorld(GhoulAhead, balance: SilverPerKill(3, 3));
        Kill(world.Ghouls[0]);
        Run(world, Forward, Second);
        Assert.Equal(3, world.Player.Silver);

        var restarted = world.Restarted();

        Assert.Equal(0, restarted.Player.Silver);
        Assert.Equal(0, restarted.Statistics.SilverCollected);
    }

    [Fact]
    public void The_statistics_count_collected_silver_and_the_summary_shows_it()
    {
        var world = new GameWorld(GhoulAhead, balance: SilverPerKill(3, 3));
        Kill(world.Ghouls[0]);

        Run(world, Forward, Second);

        Assert.Equal(3, world.Statistics.SilverCollected);
        Assert.Equal(3, world.Summary().Silver);
    }

    [Fact]
    public void Silver_lying_around_is_not_counted()
    {
        var world = new GameWorld(GhoulAhead, balance: AlwaysSilver);
        Kill(world.Ghouls[0]);

        Run(world, PlayerInput.None, Second);

        Assert.Equal(0, world.Statistics.SilverCollected);
    }

    private static void Kill(ITarget target) => target.TakeDamage(10_000, []);

    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    private static List<(Vector3, int)> SilverAfterKillingEverything(int seed, Balance? balance = null)
    {
        var world = new GameWorld(LevelParser.Parse("PGGGG\nBBBBB"), seed, balance);
        foreach (var target in world.Targets.ToList())
        {
            Kill(target);
        }

        world.Tick(PlayerInput.None);
        return world.SilverPiles.Select(pile => (pile.Position, pile.Amount)).ToList();
    }

    private static List<GameEvent> Run(GameWorld world, PlayerInput input, int ticks)
    {
        var seen = new List<GameEvent>();
        for (var tick = 0; tick < ticks; tick++)
        {
            world.Tick(input);
            seen.AddRange(world.Events);
        }

        return seen;
    }
}
