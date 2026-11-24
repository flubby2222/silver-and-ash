using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class ConsecratedAmmoTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly PlayerInput Shoot = PlayerInput.None with { Shoot = true };
    private static readonly int PistolTicks = SimulationClock.SecondsToTicks(Balance.Default.Pistol.Cooldown);

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
    public void Picking_it_up_doubles_the_fire_rate_at_once()
    {
        var world = WorldWithAmmoPickedUp(AlwaysAmmo);

        Assert.True(world.Player.IsConsecrated);
        Assert.Equal(PistolTicks / 2, TicksUntilThePistolIsReadyAgain(world));
    }

    [Fact]
    public void Without_it_the_pistol_waits_as_usual()
    {
        var world = new GameWorld(Corridor);

        Assert.Equal(PistolTicks, TicksUntilThePistolIsReadyAgain(world));
    }

    [Fact]
    public void The_damage_stays_the_same()
    {
        var world = WorldWithAmmoPickedUp(AlwaysAmmo);

        Assert.Equal(Balance.Default.Dummy.MaxHealth - Balance.Default.Pistol.Damage, ShootTheDummy(world));
    }

    [Fact]
    public void Two_quick_shots_fit_into_one_normal_wait()
    {
        // Holding the trigger is not enough (a fresh press per shot): shoot, let go, shoot again as soon as possible.
        var world = WorldWithAmmoPickedUp(AlwaysAmmo);
        var shots = 0;
        for (var tick = 0; tick < PistolTicks; tick++)
        {
            world.Tick(tick % 2 == 0 ? Shoot : PlayerInput.None);
            shots += world.Events.Count(e => e is ShotFired);
        }

        Assert.Equal(2, shots);
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

        RunTicks(world, PlayerInput.None, SimulationClock.SecondsToTicks(Balance.Default.ConsecratedAmmo.Duration));

        Assert.False(world.Player.IsConsecrated);
        Assert.Equal(PistolTicks, TicksUntilThePistolIsReadyAgain(world));
    }

    [Fact]
    public void The_time_left_counts_down()
    {
        var world = WorldWithAmmoPickedUp(AlwaysAmmo);
        var before = world.Player.ConsecratedSecondsLeft;

        RunTicks(world, PlayerInput.None, 2 * Second);

        Assert.Equal(before - 2f, world.Player.ConsecratedSecondsLeft, 0.02f);
    }

    [Fact]
    public void The_blade_is_not_faster()
    {
        var world = WorldWithAmmoPickedUp(AlwaysAmmo);

        world.Tick(PlayerInput.None with { Melee = true });
        world.Tick(PlayerInput.None);

        Assert.Equal(1f, world.Player.MeleeCooldown.RemainingFraction, 0.05f);
        Assert.Equal(SimulationClock.SecondsToTicks(Balance.Default.Blade.Cooldown), world.Player.MeleeCooldown.DurationTicks);
    }

    [Fact]
    public void A_second_pickup_restarts_the_time_but_is_not_faster()
    {
        var world = WorldWithAmmoPickedUp(AlwaysAmmo);
        RunTicks(world, PlayerInput.None, Second);

        world.Player.Consecrate(Balance.Default.ConsecratedAmmo.Duration);

        Assert.Equal(Balance.Default.ConsecratedAmmo.Duration, world.Player.ConsecratedSecondsLeft, 0.001f);
        Assert.Equal(PistolTicks / 2, TicksUntilThePistolIsReadyAgain(world));
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

        world.Tick(Shoot);

        var shot = Assert.Single(world.Events.OfType<ShotFired>());
        Assert.True(shot.Consecrated);
    }

    [Fact]
    public void Speed_and_time_come_from_the_world()
    {
        var world = WorldWithAmmoPickedUp(AlwaysAmmo with
        {
            ConsecratedAmmo = new ConsecratedAmmoBalance { FireRateMultiplier = 4f, Duration = 4f },
        });

        // 4 s from the pickup, minus the quarter second the helper waits afterwards.
        Assert.Equal(4f - 0.25f, world.Player.ConsecratedSecondsLeft, 0.05f);
        Assert.Equal(PistolTicks / 4, TicksUntilThePistolIsReadyAgain(world));
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
        RunTicks(world, PlayerInput.None, Second / 4);     // come to a stop
        return world;
    }

    /// <summary>Fires once and counts the ticks until the pistol could fire again.</summary>
    private static int TicksUntilThePistolIsReadyAgain(GameWorld world)
    {
        world.Tick(Shoot);
        Assert.Contains(world.Events, e => e is ShotFired);
        var ticks = 0;
        for (; !world.Player.Ranged.Cooldown.IsReady; ticks++)
        {
            Assert.True(ticks < 2 * Second, "The pistol never got ready again.");
            world.Tick(PlayerInput.None);
        }

        return ticks;
    }

    /// <summary>Aims at the dummy's chest, shoots once and returns its health afterwards.</summary>
    private static int ShootTheDummy(GameWorld world)
    {
        var dummy = world.Dummies[0];
        var direction = Vector3.Normalize(dummy.Position + new Vector3(0f, 1f, 0f) - world.Player.EyePosition);
        world.Player.Look(PlayerInput.None with
        {
            Look = new Vector2(
                -(MathF.Atan2(direction.X, direction.Z) - world.Player.View.YawRadians) / ControlSettings.MouseSensitivity,
                -(MathF.Asin(direction.Y) - world.Player.View.PitchRadians) / ControlSettings.MouseSensitivity),
        });
        world.Tick(Shoot);
        return dummy.Health;
    }
}
