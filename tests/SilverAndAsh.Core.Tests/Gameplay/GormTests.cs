using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class GormTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };

    /// <summary>A Gorm who never attacks (no range for any attack) - to look at his walking alone.</summary>
    private static readonly Balance NoAttacks = Balance.Default with { Gorm = Balance.Default.Gorm with { NearRange = 0f, FarRange = 0f } };

    private static List<GameEvent> Enter(GameWorld world)
    {
        var events = Run(world, PressF, 1);
        events.AddRange(Run(world, Forward, 3 * Second, () => world.Rooms[1].IsSealed));
        Assert.True(world.Rooms[1].IsSealed, "The boss room never sealed - the test would prove nothing.");
        return events;
    }

    [Fact]
    public void Gorm_stands_on_his_place_with_full_health_and_his_own_number()
    {
        var world = GormHallWorld();

        var gorm = Assert.IsType<Gorm>(world.Gorm);
        Assert.Equal(world.Level.CellCenter(3, 10), gorm.Position);
        Assert.Equal(Balance.Default.Gorm.MaxHealth, gorm.Health);
        Assert.Contains(gorm, world.Targets);
        Assert.Equal(world.Targets.Count(), world.Targets.Select(target => target.Id).Distinct().Count());
    }

    [Fact]
    public void Gorm_sleeps_until_the_boss_room_is_entered()
    {
        var world = GormHallWorld();
        var start = world.Gorm!.Position;

        Run(world, PressF, 1);
        Run(world, PlayerInput.None, 2 * Second);

        Assert.True(world.IsAsleep(world.Gorm));
        Assert.Equal(start, world.Gorm.Position);
    }

    [Fact]
    public void A_sleeping_gorm_takes_no_step()
    {
        // Issue #298: his steps are heard only once he walks.
        var world = GormHallWorld(balance: NoAttacks);

        var events = Run(world, PlayerInput.None, 3 * Second);

        Assert.DoesNotContain(events, e => e is GormStepped);
    }

    [Fact]
    public void Entering_starts_the_boss_fight()
    {
        var world = GormHallWorld();

        var events = Enter(world);

        Assert.Contains(events, e => e is BossFightStarted);
        Assert.True(world.IsBossFightOn);
        Assert.False(world.IsAsleep(world.Gorm!));
    }

    [Fact]
    public void Gorm_walks_up_to_the_player_and_stops_close_in_front()
    {
        var world = GormHallWorld(balance: NoAttacks);
        Enter(world);
        var before = FlatDistance(world.Gorm!.Position, world.Player.Position);

        Run(world, PlayerInput.None, 6 * Second);

        var after = FlatDistance(world.Gorm.Position, world.Player.Position);
        Assert.True(after < before, "Gorm did not come closer.");
        Assert.InRange(after, Balance.Default.Gorm.StopDistance - 0.3f, Balance.Default.Gorm.StopDistance + 0.2f);
    }

    [Fact]
    public void Gorm_walks_at_his_speed()
    {
        var world = GormHallWorld(balance: NoAttacks);
        Enter(world);
        var start = world.Gorm!.Position;

        Run(world, PlayerInput.None, Second / 2);

        Assert.Equal(Balance.Default.Gorm.Speed / 2f, FlatDistance(start, world.Gorm.Position), 0.15f);
    }

    [Fact]
    public void His_head_is_the_top_front_of_his_body_and_counts_one_and_a_half()
    {
        var gorm = new Gorm(9, Vector3.Zero, Vector3.UnitX);
        var high = BodySizes.Gorm.Height - 0.1f;
        var r = BodySizes.Gorm.Radius;

        Assert.Equal(HitZone.BossHead, gorm.ZoneAt(new Vector3(r, high, 0f)));
        Assert.Equal(HitZone.Body, gorm.ZoneAt(new Vector3(r, 1.5f, 0f)));                 // front, low
        Assert.Equal(HitZone.Body, gorm.ZoneAt(new Vector3(-r, high, 0f)));                // back
        Assert.Equal(HitZone.Body, gorm.ZoneAt(new Vector3(0.3f, high, 0.6f)));            // beside the head

        var shot = DamageRules.Standard().Calculate(new Hit(Weapon.Pistol, HitZone.BossHead, 40));
        Assert.Equal(new DamageResult(60, true), shot);
    }

    [Fact]
    public void Damage_kills_him_once_and_is_reported()
    {
        var gorm = new Gorm(9, Vector3.Zero);
        var events = new List<GameEvent>();

        // Three blows of 60 % of his health: the second one only takes what is left.
        var blow = Balance.Default.Gorm.MaxHealth * 6 / 10;
        gorm.TakeDamage(blow, events);
        gorm.TakeDamage(blow, events);
        gorm.TakeDamage(blow, events);

        Assert.True(gorm.IsDead);
        Assert.False(gorm.IsHittable);
        // The first blow takes him past half his health: he roars (phase 2, issue #166).
        var rest = Balance.Default.Gorm.MaxHealth - blow;
        Assert.Equal([new GormHit(9, blow), new GormRoared(9), new GormHit(9, rest), new GormDied(9)], events);
    }

    [Fact]
    public void Dead_he_leaves_a_silver_pile_and_a_blood_vial_once()
    {
        var world = GormHallWorld();
        Enter(world);

        world.Gorm!.TakeDamage(10_000, []);
        var events = Run(world, PlayerInput.None, 30);

        var silver = Assert.Single(world.Loot, loot => loot.Kind == LootKind.Silver && loot.Amount >= 100);
        Assert.InRange(silver.Amount, Balance.Default.Gorm.SilverMin, Balance.Default.Gorm.SilverMax);
        Assert.Single(world.Pickups, pickup => pickup.Kind == ConsumableKind.BloodVial);
        Assert.Single(events, e => e is SilverDropped { Amount: >= 100 });
    }

    [Fact]
    public void His_death_clears_the_room_ends_the_fight_and_raises_the_stairs()
    {
        var world = GormHallWorld();
        Enter(world);

        world.Gorm!.TakeDamage(10_000, []);
        var events = Run(world, PlayerInput.None, 1);

        Assert.True(world.Rooms[1].IsCleared);
        Assert.False(world.IsBossFightOn);
        Assert.Contains(events, e => e is StairsRose);
        Assert.False(world.IsWon);
    }

    [Fact]
    public void The_statistics_know_whether_he_died_and_how_long_the_fight_took()
    {
        // A weak Gorm, so one pistol shot (straight ahead at his body) kills him - through the world, as in the game.
        var world = GormHallWorld(balance: Balance.Default with { Gorm = Balance.Default.Gorm with { MaxHealth = 10 } });
        Enter(world);
        Run(world, PlayerInput.None, 2 * Second);

        var events = Run(world, PlayerInput.None with { Shoot = true }, 1);
        Run(world, PlayerInput.None, Second);

        Assert.Contains(events, e => e is GormDied);
        Assert.True(world.Statistics.GormSlain);
        Assert.InRange(world.Statistics.BossFightSeconds, 2f, 2.1f);
        var summary = world.Summary();
        Assert.True(summary.GormSlain);
        Assert.Equal("0:02", summary.GormFightTime);
    }

    [Fact]
    public void A_run_without_the_boss_fight_says_so()
    {
        var world = GormHallWorld();

        Run(world, PlayerInput.None, Second);

        Assert.False(world.Statistics.GormSlain);
        Assert.Equal(0, world.Statistics.BossFightTicks);
    }

    [Fact]
    public void Gorm_s_values_come_from_the_balancing()
    {
        var balance = Balance.Default with { Gorm = Balance.Default.Gorm with { MaxHealth = 500, SilverMin = 7, SilverMax = 7 } };
        var world = GormHallWorld(balance: balance);
        Enter(world);

        Assert.Equal(500, world.Gorm!.Health);
        world.Gorm.TakeDamage(10_000, []);
        Run(world, PlayerInput.None, 2);
        Assert.Contains(world.Loot, loot => loot.Kind == LootKind.Silver && loot.Amount == 7);
    }

    [Fact]
    public void For_drawing_he_tells_how_far_and_how_high_his_swing_and_his_wave_reach()
    {
        var balance = Balance.Default with
        {
            Gorm = Balance.Default.Gorm with { SwingReach = 5.5f, SwingLow = 0.9f, SwingHigh = 1.7f, ShockwaveRadius = 9f },
        };

        var gorm = new Gorm(9, Vector3.Zero, balance: balance);

        Assert.Equal((5.5f, 0.9f, 1.7f), gorm.SwingArea);
        Assert.Equal(9f, gorm.ShockwaveReach);
    }
}
