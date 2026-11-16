using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>The blunderbuss: a cone of pellets, devastating up close, useless far away (decision record 0008, issue #192).</summary>
public class BlunderbussTests
{
    private static readonly RangedBalance Blunderbuss = Balance.Default.Blunderbuss;

    /// <summary>Dummies that never fall over: so every pellet can be counted.</summary>
    private static readonly Balance Sturdy = Balance.Default with { Dummy = Balance.Default.Dummy with { MaxHealth = 100_000 } };

    /// <summary>The player at the top of the map looks along +Z, down the rows.</summary>
    private static GameWorld World(string map, int seed = 1, WeaponKind weapon = WeaponKind.Blunderbuss)
    {
        var world = new GameWorld(LevelParser.Parse(map), seed, Sturdy);
        world.Player.Equip(0, weapon);
        return world;
    }

    private static List<GameEvent> Shoot(GameWorld world)
    {
        world.Tick(PlayerInput.None with { Shoot = true });
        return world.Events.ToList();
    }

    /// <summary>A map with the target this many metres in front of the player's cell centre (rounded up to whole cells).</summary>
    private static string TargetAt(float metres, char target = 'D')
    {
        var cells = (int)MathF.Ceiling(metres / Level.CellSize);
        return "P\n" + string.Concat(Enumerable.Repeat(".\n", cells - 1)) + target;
    }

    [Fact]
    public void The_blunderbuss_is_a_forged_ranged_weapon_with_its_own_values()
    {
        Assert.Equal(new WeaponInfo("Blunderbuss", WeaponHand.Ranged, ItemTier.Forged), Weapons.Info(WeaponKind.Blunderbuss));
        Assert.Same(Balance.Default.Blunderbuss, Balance.Default.Ranged(WeaponKind.Blunderbuss));

        var player = new Player(LevelParser.Parse("P"));
        player.Equip(1, WeaponKind.Blunderbuss);
        Assert.Equal(Blunderbuss.MagazineSize, player.RangedSlots[1]!.Magazine.Size);
    }

    [Fact]
    public void Up_close_almost_every_pellet_hits()
    {
        var world = World("P\nD\n.");

        var hits = Shoot(world).OfType<DummyHit>().ToList();

        Assert.True(hits.Count >= Blunderbuss.Pellets - 1, $"Only {hits.Count} of {Blunderbuss.Pellets} pellets hit.");
        Assert.True(hits.Count <= Blunderbuss.Pellets);
        Assert.Equal(hits.Count * Blunderbuss.Damage, Sturdy.Dummy.MaxHealth - world.Dummies[0].Health);
    }

    [Fact]
    public void Beyond_its_range_no_pellet_does_anything()
    {
        var world = World(TargetAt(Blunderbuss.Range + Level.CellSize));

        var events = Shoot(world);

        Assert.DoesNotContain(events, e => e is DummyHit);
        Assert.Equal(Sturdy.Dummy.MaxHealth, world.Dummies[0].Health);
    }

    [Fact]
    public void The_pellets_fly_apart_but_stay_in_the_cone()
    {
        var world = World("P\n.\n.\n#");
        var eye = world.Player.EyePosition;
        var forward = world.Player.View.Forward;

        var impacts = Shoot(world).OfType<BulletImpact>().ToList();

        Assert.Equal(Blunderbuss.Pellets, impacts.Count);
        var angles = impacts.Select(i => AngleDegrees(forward, i.Point - eye)).ToList();
        Assert.All(angles, angle => Assert.True(angle <= Blunderbuss.Spread / 2f + 0.01f, $"A pellet flew {angle} degrees off."));
        Assert.True(angles.Max() > Blunderbuss.Spread / 8f, "The pellets do not spread.");
        Assert.True(impacts.Select(i => i.Point).Distinct().Count() > 1);
    }

    [Fact]
    public void The_same_seed_gives_the_same_spread()
    {
        var map = "P\n.\n.\n#";

        var first = Shoot(World(map, seed: 7)).OfType<BulletImpact>().Select(i => i.Point).ToList();
        var again = Shoot(World(map, seed: 7)).OfType<BulletImpact>().Select(i => i.Point).ToList();
        var other = Shoot(World(map, seed: 8)).OfType<BulletImpact>().Select(i => i.Point).ToList();

        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void Shooting_does_not_change_the_other_random_rolls()
    {
        // The spread has its own random source: a bat behind the player flies the same whether the player fired or not.
        const string map = ".....\n..B..\n.....\n..P..\n.....\n.....\n#####";
        var fired = World(map, seed: 3);
        var quiet = World(map, seed: 3);

        fired.Tick(PlayerInput.None with { Shoot = true });
        quiet.Tick(PlayerInput.None);
        for (var tick = 0; tick < SimulationClock.SecondsToTicks(2f * Balance.Default.Bat.TurnRoundMax); tick++)
        {
            fired.Tick(PlayerInput.None);
            quiet.Tick(PlayerInput.None);
        }

        Assert.Equal(quiet.Bats[0].Position, fired.Bats[0].Position);
    }

    [Fact]
    public void One_shot_empties_it_and_the_reload_takes_its_time()
    {
        var world = World("P\n.\n.\n#");
        Shoot(world);
        world.Tick(PlayerInput.None);
        var blunderbuss = world.Player.Ranged!;
        Assert.Equal(0, blunderbuss.Magazine.Rounds);

        // The next shot on an empty magazine starts the reload ...
        world.Tick(PlayerInput.None with { Shoot = true });
        Assert.True(blunderbuss.Magazine.IsReloading);

        // ... which takes the blunderbuss's reload time.
        for (var tick = 1; tick < SimulationClock.SecondsToTicks(Blunderbuss.ReloadTime); tick++)
        {
            world.Tick(PlayerInput.None);
        }

        Assert.True(blunderbuss.Magazine.IsReloading, "The reload ended too early.");
        world.Tick(PlayerInput.None);
        Assert.Equal(Blunderbuss.MagazineSize, blunderbuss.Magazine.Rounds);
    }

    [Fact]
    public void Each_pellet_hits_for_the_blunderbuss_damage()
    {
        var world = World("P\nD\n.");

        var hits = Shoot(world).OfType<DummyHit>().ToList();

        Assert.NotEmpty(hits);
        Assert.All(hits, hit => Assert.True(hit.Damage == Blunderbuss.Damage || hit.Critical, $"A pellet did {hit.Damage}."));
    }

    [Fact]
    public void A_pellet_counts_the_head_and_Gorm_s_head_like_every_gun()
    {
        // The standard factor is 1 (no head bonus); with another one the rule shows.
        var balance = Balance.Default with { Blunderbuss = Blunderbuss with { HeadshotMultiplier = 2f } };
        var rules = DamageRules.Standard(balance);

        Assert.Equal(new DamageResult((int)MathF.Round(Blunderbuss.Damage * 2f), true), rules.Calculate(new Hit(Weapon.Blunderbuss, HitZone.Head, Blunderbuss.Damage)));
        Assert.True(rules.Calculate(new Hit(Weapon.Blunderbuss, HitZone.BossHead, Blunderbuss.Damage)).Amount > Blunderbuss.Damage);
    }

    private static float AngleDegrees(Vector3 a, Vector3 b) =>
        MathF.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(a), Vector3.Normalize(b)), -1f, 1f)) * 180f / MathF.PI;
}
