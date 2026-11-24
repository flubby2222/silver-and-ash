using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class PickupTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };

    /// <summary>Every kill drops something (to test what happens with a drop).</summary>
    private static readonly Balance AlwaysDrop = Balance.Default with
    {
        Drops = Balance.Default.Drops with { GhoulChance = 1f, BatChance = 1f },
    };

    /// <summary>A corridor along +Z: the player in row 0, a ghoul 4 m ahead, room to walk on.</summary>
    private static readonly Level GhoulAhead = LevelParser.Parse("P\n.\nG\n.\n.");

    [Fact]
    public void A_killed_ghoul_can_drop_a_blood_vial_where_it_died()
    {
        var world = new GameWorld(GhoulAhead, balance: AlwaysDrop);
        var ghoul = world.Ghouls[0];

        Kill(ghoul);
        world.Tick(PlayerInput.None);

        var pickup = Assert.Single(world.Pickups);
        Assert.Equal(ConsumableKind.BloodVial, pickup.Kind);
        Assert.Equal(ghoul.Position.X, pickup.Position.X, 0.01f);
        Assert.Equal(ghoul.Position.Z, pickup.Position.Z, 0.01f);
        Assert.Contains(world.Events, e => e is PickupDropped dropped && dropped.PickupId == pickup.Id);
    }

    [Fact]
    public void A_kill_drops_at_most_once()
    {
        var world = new GameWorld(GhoulAhead, balance: AlwaysDrop);
        Kill(world.Ghouls[0]);

        Run(world, PlayerInput.None, Second);

        Assert.Single(world.Pickups);
    }

    [Fact]
    public void Without_a_chance_nothing_drops()
    {
        var never = Balance.Default with { Drops = Balance.Default.Drops with { GhoulChance = 0f, BatChance = 0f } };
        var world = new GameWorld(LevelParser.Parse("P\n.\nG\n.\nB"), balance: never);
        Kill(world.Ghouls[0]);
        Kill(world.Bats[0]);

        Run(world, PlayerInput.None, Second);

        Assert.Empty(world.Pickups);
    }

    [Fact]
    public void Each_kind_of_enemy_has_its_own_chance()
    {
        var onlyBats = Balance.Default with { Drops = Balance.Default.Drops with { GhoulChance = 0f, BatChance = 1f } };
        var world = new GameWorld(LevelParser.Parse("P\n.\nG\n.\nB"), balance: onlyBats);
        Kill(world.Ghouls[0]);
        Kill(world.Bats[0]);

        world.Tick(PlayerInput.None);

        var pickup = Assert.Single(world.Pickups);
        Assert.Equal(world.Bats[0].Position.Z, pickup.Position.Z, 0.01f);
    }

    [Fact]
    public void The_standard_chances_hold_over_many_kills()
    {
        // 600 worlds, one ghoul and one bat each, each with its standard chance.
        const int Worlds = 600;
        var ghoulDrops = 0;
        var batDrops = 0;
        for (var seed = 0; seed < Worlds; seed++)
        {
            var world = new GameWorld(LevelParser.Parse("P#G\n.#.\n.#B"), seed);
            Kill(world.Ghouls[0]);
            Kill(world.Bats[0]);
            world.Tick(PlayerInput.None);
            ghoulDrops += world.Pickups.Count(p => p.Position.Z < Level.CellSize);
            batDrops += world.Pickups.Count(p => p.Position.Z > Level.CellSize);
        }

        var (ghoulLow, ghoulHigh) = Band(Worlds, Balance.Default.Drops.GhoulChance);
        var (batLow, batHigh) = Band(Worlds, Balance.Default.Drops.BatChance);
        Assert.InRange(ghoulDrops, ghoulLow, ghoulHigh);
        Assert.InRange(batDrops, batLow, batHigh);
    }

    /// <summary>Where a count of hits out of <paramref name="tries"/> with <paramref name="chance"/> lies: expected value +- 3.5 standard deviations.</summary>
    private static (double Low, double High) Band(int tries, float chance)
    {
        var expected = tries * (double)chance;
        var deviation = Math.Sqrt(tries * chance * (1.0 - chance));
        return (expected - (3.5 * deviation), expected + (3.5 * deviation));
    }

    [Fact]
    public void The_same_seed_drops_the_same()
    {
        Assert.Equal(DropsAfterKillingEverything(11), DropsAfterKillingEverything(11));
    }

    [Fact]
    public void Dummies_drop_nothing()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nD"), balance: AlwaysDrop);
        world.Dummies[0].TakeDamage(Balance.Default.Dummy.MaxHealth, []);

        Run(world, PlayerInput.None, Second);

        Assert.Empty(world.Pickups);
    }

    [Fact]
    public void A_drop_from_the_air_falls_to_the_floor()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nB"), balance: AlwaysDrop);
        Kill(world.Bats[0]);

        Run(world, PlayerInput.None, Second);

        Assert.Equal(0f, Assert.Single(world.Pickups).Position.Y, 0.001f);
    }

    [Fact]
    public void Walking_over_a_blood_vial_heals_and_takes_it()
    {
        var world = new GameWorld(GhoulAhead, balance: AlwaysDrop);
        var player = world.Player;
        player.TakeDamage(70, []);
        Kill(world.Ghouls[0]);
        world.Tick(PlayerInput.None);
        var vial = world.Pickups[0];

        var seen = Run(world, Forward, Second);

        Assert.Equal(30 + Balance.Default.BloodVial.Heal, player.Health);
        Assert.Empty(world.Pickups);
        Assert.True(vial.IsTaken);
        Assert.Contains(seen, e => e == new PickupTaken(vial.Id, player.Id, ConsumableKind.BloodVial));
        Assert.Contains(seen, e => e == new PlayerHealed(player.Id, Balance.Default.BloodVial.Heal));
    }

    [Fact]
    public void Healing_stops_at_the_maximum()
    {
        var world = new GameWorld(GhoulAhead, balance: AlwaysDrop);
        var player = world.Player;
        player.TakeDamage(20, []);
        Kill(world.Ghouls[0]);

        var seen = Run(world, Forward, Second);

        Assert.Equal(player.MaxHealth, player.Health);
        Assert.Contains(seen, e => e == new PlayerHealed(player.Id, 20));
    }

    [Fact]
    public void With_full_health_the_vial_stays_where_it_is()
    {
        var world = new GameWorld(GhoulAhead, balance: AlwaysDrop);
        Kill(world.Ghouls[0]);

        var seen = Run(world, Forward, Second);

        Assert.Single(world.Pickups);
        Assert.DoesNotContain(seen, e => e is PickupTaken);
    }

    [Fact]
    public void A_vial_heals_as_much_as_the_world_says()
    {
        var world = new GameWorld(GhoulAhead, balance: AlwaysDrop with { BloodVial = new BloodVialBalance { Heal = 25 } });
        world.Player.TakeDamage(70, []);
        Kill(world.Ghouls[0]);

        Run(world, Forward, Second);

        Assert.Equal(55, world.Player.Health);
    }

    [Fact]
    public void A_dead_player_picks_nothing_up()
    {
        // The player runs at the vial and dies just before it (0.8 m). The body still slides on over it
        // (about 0.3 m: close enough to touch it) - but the dead take nothing.
        var world = new GameWorld(GhoulAhead, balance: AlwaysDrop);
        var player = world.Player;
        player.TakeDamage(50, []);
        Kill(world.Ghouls[0]);
        world.Tick(PlayerInput.None);
        var vial = world.Pickups[0];
        for (var tick = 0; FlatDistance(player.Position, vial.Position) > 0.8f; tick++)
        {
            Assert.True(tick < 3 * Second, "The player never got close to the vial.");
            world.Tick(Forward);
        }

        player.TakeDamage(player.Health, []);
        var seen = Run(world, Forward, Second);

        Assert.True(FlatDistance(player.Position, vial.Position) < BodySizes.Player.Radius + BodySizes.Pickup.Radius,
            "The body did not slide far enough to touch the vial - the test would prove nothing.");
        Assert.DoesNotContain(seen, e => e is PickupTaken);
    }

    private static void Kill(ITarget target) => target.TakeDamage(10_000, []);

    private static List<Vector3> DropsAfterKillingEverything(int seed)
    {
        var world = new GameWorld(LevelParser.Parse("PGGGG\nBBBBB"), seed);
        foreach (var target in world.Targets.ToList())
        {
            Kill(target);
        }

        world.Tick(PlayerInput.None);
        return world.Pickups.Select(p => p.Position).ToList();
    }
}
