using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>The rifle: heavy aimed shots that go through one target into the next (decision record 0008, issue #191).</summary>
public class RifleTests
{
    private static readonly RangedBalance Rifle = Balance.Default.Rifle;

    /// <summary>Dummies that never fall over: so every shot can be counted in their health.</summary>
    private static readonly Balance Sturdy = Balance.Default with { Dummy = Balance.Default.Dummy with { MaxHealth = 100_000 } };

    /// <summary>A one-cell corridor along +Z from the given rows; the player looks along it.</summary>
    private static GameWorld World(string map, WeaponKind weapon = WeaponKind.Rifle)
    {
        var world = new GameWorld(LevelParser.Parse(map), 1, Sturdy);
        world.Player.Equip(0, weapon);
        return world;
    }

    private static List<GameEvent> Shoot(GameWorld world)
    {
        world.Tick(PlayerInput.None with { Shoot = true });
        var events = world.Events.ToList();
        world.Tick(PlayerInput.None);
        return events;
    }

    private static int Lost(TrainingDummy dummy) => Sturdy.Dummy.MaxHealth - dummy.Health;

    [Fact]
    public void The_rifle_is_a_forged_ranged_weapon_with_its_own_values()
    {
        Assert.Equal(new WeaponInfo("Rifle", WeaponHand.Ranged, ItemTier.Forged), Weapons.Info(WeaponKind.Rifle));
        Assert.Same(Balance.Default.Rifle, Balance.Default.Ranged(WeaponKind.Rifle));
        Assert.Same(Balance.Default.Pistol, Balance.Default.Ranged(WeaponKind.Pistol));
    }

    [Fact]
    public void A_rifle_in_a_place_has_its_magazine_and_cooldown()
    {
        var player = new Player(LevelParser.Parse("P"));

        player.Equip(1, WeaponKind.Rifle);

        var rifle = player.RangedSlots[1]!;
        Assert.Equal(Rifle.MagazineSize, rifle.Magazine.Size);
        Assert.Equal(SimulationClock.SecondsToTicks(Rifle.Cooldown), rifle.Cooldown.DurationTicks);
    }

    [Fact]
    public void A_shot_says_which_weapon_fired()
    {
        var world = World("P\n.");

        var events = Shoot(world);

        Assert.Equal(WeaponKind.Rifle, Assert.Single(events.OfType<ShotFired>()).Weapon);
    }

    [Fact]
    public void Headshots_and_Gorm_s_head_count_for_the_rifle_too()
    {
        var rules = DamageRules.Standard();

        Assert.Equal(new DamageResult((int)MathF.Round(Rifle.Damage * Rifle.HeadshotMultiplier), true), rules.Calculate(new Hit(Weapon.Rifle, HitZone.Head, Rifle.Damage)));
        Assert.Equal(Rifle.Damage, rules.Calculate(new Hit(Weapon.Rifle, HitZone.Body, Rifle.Damage)).Amount);
        Assert.True(rules.Calculate(new Hit(Weapon.Rifle, HitZone.BossHead, Rifle.Damage)).Amount > Rifle.Damage);
    }

    [Fact]
    public void The_rifle_goes_through_the_first_target_into_the_second_but_no_further()
    {
        var world = World("P\nD\nD\nD\n.");
        var dummies = world.Dummies;

        Shoot(world);

        Assert.True(Lost(dummies[0]) > 0, "The first target was not hit.");
        Assert.True(Lost(dummies[1]) > 0, "The rifle did not go through into the second.");
        Assert.Equal(0, Lost(dummies[2]));
    }

    [Fact]
    public void The_pistol_stops_in_the_first_target()
    {
        var world = World("P\nD\nD\n.", WeaponKind.Pistol);

        Shoot(world);

        Assert.True(Lost(world.Dummies[0]) > 0);
        Assert.Equal(0, Lost(world.Dummies[1]));
    }

    [Fact]
    public void A_wall_stops_the_rifle()
    {
        var world = World("P\nD\n#\nD");

        Shoot(world);

        Assert.True(Lost(world.Dummies[0]) > 0);
        Assert.Equal(0, Lost(world.Dummies[1]));
    }

    [Fact]
    public void Through_one_target_the_bullet_ends_in_the_wall()
    {
        var world = World("P\nD\n.\n#");

        var events = Shoot(world);

        Assert.Contains(events, e => e is BulletImpact);
    }

    [Fact]
    public void A_pistol_bullet_in_a_target_leaves_no_mark_on_the_wall()
    {
        var world = World("P\nD\n.\n#", WeaponKind.Pistol);

        var events = Shoot(world);

        Assert.DoesNotContain(events, e => e is BulletImpact);
    }

    [Fact]
    public void The_rifle_reaches_further_than_the_pistol()
    {
        // A target between the two ranges.
        var cells = (int)MathF.Ceiling(((Balance.Default.Pistol.Range + Rifle.Range) / 2f) / Level.CellSize);
        var map = "P\n" + string.Concat(Enumerable.Repeat(".\n", cells - 1)) + "D";

        var rifle = World(map);
        Shoot(rifle);
        var pistol = World(map, WeaponKind.Pistol);
        Shoot(pistol);

        Assert.True(Lost(rifle.Dummies[0]) > 0, "The rifle did not reach.");
        Assert.Equal(0, Lost(pistol.Dummies[0]));
    }

    [Fact]
    public void A_rifle_headshot_in_the_world_uses_the_rifle_s_multiplier()
    {
        // The pistol gets no head bonus here, so a rifle shot counted as a pistol shot would show.
        var balance = Sturdy with { Pistol = Sturdy.Pistol with { HeadshotMultiplier = 1f } };
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nD\n."), 1, balance);
        world.Player.Equip(0, WeaponKind.Rifle);
        AimAt(world, world.Dummies[0], BodySizes.Dummy.Height - BodySizes.Dummy.HeadHeight / 2f);

        world.Tick(PlayerInput.None with { Shoot = true });

        var damage = (int)MathF.Round(Rifle.Damage * Rifle.HeadshotMultiplier, MidpointRounding.AwayFromZero);
        Assert.Contains(new DummyHit(0, damage, Critical: true), world.Events);
    }

    /// <summary>Turns the view (like the mouse would) so it looks at <paramref name="height"/> above the dummy's feet.</summary>
    private static void AimAt(GameWorld world, TrainingDummy dummy, float height)
    {
        var player = world.Player;
        var direction = System.Numerics.Vector3.Normalize(dummy.Position with { Y = dummy.Position.Y + height } - player.EyePosition);
        var yaw = MathF.Atan2(direction.X, direction.Z);
        var pitch = MathF.Asin(direction.Y);
        var pixels = new System.Numerics.Vector2(
            -(yaw - player.View.YawRadians) / ControlSettings.MouseSensitivity,
            -(pitch - player.View.PitchRadians) / ControlSettings.MouseSensitivity);
        player.Look(PlayerInput.None with { Look = pixels });
    }

    [Fact]
    public void Each_target_takes_the_rifle_s_damage()
    {
        // The two dummies are hit at the same height, so both lose the same: the rifle's damage for that zone.
        var world = World("P\nD\nD\n.");

        Shoot(world);

        Assert.Equal(Lost(world.Dummies[0]), Lost(world.Dummies[1]));
        Assert.True(Lost(world.Dummies[0]) >= Rifle.Damage);
    }
}
