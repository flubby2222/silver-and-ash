using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class ConsecratedAmmoTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly int Pistol = Balance.Default.Pistol.Damage;

    /// <summary>Every kill drops consecrated ammo.</summary>
    private static readonly Balance AlwaysAmmo = Balance.Default with
    {
        Drops = Balance.Default.Drops with { GhoulChance = 1f, BatChance = 1f, ConsecratedAmmoShare = 1f },
    };

    /// <summary>A corridor along +Z: player, a ghoul 4 m ahead, a dummy 8 m ahead.</summary>
    private static readonly Level Corridor = LevelParser.Parse("P\n.\nG\n.\nD\n.");

    [Fact]
    public void The_share_decides_which_consumable_drops()
    {
        Assert.Equal(ConsumableKind.ConsecratedAmmo, DropAfterKillingTheGhoul(AlwaysAmmo));
        Assert.Equal(ConsumableKind.BloodVial, DropAfterKillingTheGhoul(AlwaysAmmo with
        {
            Drops = AlwaysAmmo.Drops with { ConsecratedAmmoShare = 0f },
        }));
    }

    [Fact]
    public void The_standard_share_drops_both_kinds()
    {
        // 200 worlds, each ghoul always drops: about 30 % ammo, the rest vials.
        var always = Balance.Default with { Drops = Balance.Default.Drops with { GhoulChance = 1f } };
        var ammo = 0;
        for (var seed = 0; seed < 200; seed++)
        {
            var world = new GameWorld(Corridor, seed, always);
            world.Ghouls[0].TakeDamage(10_000, []);
            world.Tick(PlayerInput.None);
            ammo += world.Pickups.Count(p => p.Kind == ConsumableKind.ConsecratedAmmo);
        }

        Assert.InRange(ammo, 40, 80);
    }

    [Fact]
    public void Picking_it_up_doubles_the_pistol_damage_at_once()
    {
        var world = WorldWithAmmoPickedUp(AlwaysAmmo);

        Assert.True(world.Player.IsConsecrated);
        Assert.Equal(Balance.Default.Dummy.MaxHealth - 2 * Pistol, ShootTheDummy(world));
    }

    [Fact]
    public void It_is_picked_up_even_with_full_health()
    {
        var world = WorldWithAmmoPickedUp(AlwaysAmmo);

        Assert.Equal(world.Player.MaxHealth, world.Player.Health);
        Assert.Empty(world.Pickups);
    }

    [Fact]
    public void After_its_time_the_pistol_is_normal_again()
    {
        var world = WorldWithAmmoPickedUp(AlwaysAmmo);

        Run(world, PlayerInput.None, SimulationClock.SecondsToTicks(Balance.Default.ConsecratedAmmo.Duration));

        Assert.False(world.Player.IsConsecrated);
        Assert.Equal(Balance.Default.Dummy.MaxHealth - Pistol, ShootTheDummy(world));
    }

    [Fact]
    public void The_time_left_counts_down()
    {
        var world = WorldWithAmmoPickedUp(AlwaysAmmo);
        var before = world.Player.ConsecratedSecondsLeft;

        Run(world, PlayerInput.None, 2 * Second);

        Assert.Equal(before - 2f, world.Player.ConsecratedSecondsLeft, 0.02f);
    }

    [Fact]
    public void The_blade_is_not_consecrated()
    {
        var world = WorldWithAmmoPickedUp(AlwaysAmmo);
        var dummy = world.Dummies[0];
        for (var tick = 0; Vector3.Distance(world.Player.Position, dummy.Position) > 1.5f; tick++)
        {
            Assert.True(tick < 2 * Second, "The player never got close to the dummy.");
            world.Tick(Forward);
        }

        Face(world.Player, dummy.Position + new Vector3(0f, 1f, 0f));
        world.Tick(PlayerInput.None with { Melee = true });
        world.Tick(PlayerInput.None);

        Assert.Equal(Balance.Default.Dummy.MaxHealth - Balance.Default.Blade.Damage, dummy.Health);
    }

    [Fact]
    public void A_second_pickup_restarts_the_time_but_does_not_double_again()
    {
        var player = new Player(LevelParser.Parse("P"));
        player.Consecrate(15f);
        player.Tick(PlayerInput.None);
        player.Tick(PlayerInput.None);

        player.Consecrate(15f);

        Assert.Equal(15f, player.ConsecratedSecondsLeft, 0.001f);
        var rules = DamageRules.Standard();
        Assert.Equal(2 * Pistol, rules.Calculate(new Hit(Weapon.Pistol, HitZone.Body, Pistol, Consecrated: true)).Amount);
    }

    [Fact]
    public void Together_with_a_headshot_it_multiplies()
    {
        var rules = DamageRules.Standard();

        var hit = rules.Calculate(new Hit(Weapon.Pistol, HitZone.Head, Pistol, Consecrated: true));

        Assert.Equal(Pistol * 3 * 2, hit.Amount);
        Assert.True(hit.IsCritical);
    }

    [Fact]
    public void Only_consecrated_pistol_hits_count()
    {
        var rules = DamageRules.Standard();

        Assert.Equal(Pistol, rules.Calculate(new Hit(Weapon.Pistol, HitZone.Body, Pistol)).Amount);
        Assert.Equal(40, rules.Calculate(new Hit(Weapon.Blade, HitZone.Body, 40, Consecrated: true)).Amount);
    }

    [Fact]
    public void Dying_ends_it()
    {
        var world = WorldWithAmmoPickedUp(AlwaysAmmo);

        world.Player.TakeDamage(world.Player.MaxHealth, []);
        world.Tick(PlayerInput.None);

        Assert.False(world.Player.IsConsecrated);
    }

    [Fact]
    public void A_consecrated_shot_says_so()
    {
        var world = WorldWithAmmoPickedUp(AlwaysAmmo);

        world.Tick(PlayerInput.None with { Shoot = true });

        var shot = Assert.Single(world.Events.OfType<ShotFired>());
        Assert.True(shot.Consecrated);
    }

    [Fact]
    public void Strength_and_time_come_from_the_world()
    {
        var world = WorldWithAmmoPickedUp(AlwaysAmmo with
        {
            ConsecratedAmmo = new ConsecratedAmmoBalance { DamageMultiplier = 1.5f, Duration = 4f },
        });

        // 4 s from the pickup, minus the quarter second the helper waits afterwards.
        Assert.Equal(4f - 0.25f, world.Player.ConsecratedSecondsLeft, 0.05f);

        // 35 x 1.5 = 52.5, rounded to 53 (not x3: 105 would knock the 100-health dummy down and hide the number).
        Assert.Equal(Balance.Default.Dummy.MaxHealth - 53, ShootTheDummy(world));
    }

    private static ConsumableKind DropAfterKillingTheGhoul(Balance balance)
    {
        var world = new GameWorld(Corridor, balance: balance);
        world.Ghouls[0].TakeDamage(10_000, []);
        world.Tick(PlayerInput.None);
        return Assert.Single(world.Pickups).Kind;
    }

    /// <summary>Kills the ghoul and walks over its drop (ammo with <see cref="AlwaysAmmo"/>), stopping just after it.</summary>
    private static GameWorld WorldWithAmmoPickedUp(Balance balance)
    {
        var world = new GameWorld(Corridor, balance: balance);
        world.Ghouls[0].TakeDamage(10_000, []);
        world.Tick(PlayerInput.None);
        for (var tick = 0; tick < 2 * Second && world.Pickups.Count > 0; tick++)
        {
            world.Tick(Forward);
        }

        Assert.Empty(world.Pickups);
        Run(world, PlayerInput.None, Second / 4);     // come to a stop
        return world;
    }

    /// <summary>Aims at the dummy's chest, shoots once and returns its health afterwards.</summary>
    private static int ShootTheDummy(GameWorld world)
    {
        var dummy = world.Dummies[0];
        Face(world.Player, dummy.Position + new Vector3(0f, 1f, 0f));
        world.Tick(PlayerInput.None with { Shoot = true });
        return dummy.Health;
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

    private static void Run(GameWorld world, PlayerInput input, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++)
        {
            world.Tick(input);
        }
    }
}
