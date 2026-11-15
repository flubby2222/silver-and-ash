using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests;

public class BalanceTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;

    [Fact]
    public void Without_own_values_a_world_uses_the_standard_values()
    {
        var world = new GameWorld(LevelParser.Parse("P"));

        Assert.Same(Balance.Default, world.Balance);
        Assert.Equal(Balance.Default.Player.MaxHealth, world.Player.Health);
    }

    [Fact]
    public void Own_values_change_a_copy_never_the_standard()
    {
        var tough = Balance.Default with { Player = Balance.Default.Player with { MaxHealth = 150 } };

        Assert.Equal(150, tough.Player.MaxHealth);
        Assert.Equal(100, Balance.Default.Player.MaxHealth);
    }

    [Fact]
    public void The_player_starts_with_the_health_of_the_world()
    {
        var world = new GameWorld(LevelParser.Parse("P"), balance: Balance.Default with
        {
            Player = Balance.Default.Player with { MaxHealth = 150 },
        });

        Assert.Equal(150, world.Player.Health);
    }

    [Fact]
    public void A_faster_ghoul_crawls_faster()
    {
        var normal = DistanceAfterHalfASecond(Balance.Default);
        var fast = DistanceAfterHalfASecond(Balance.Default with { Ghoul = Balance.Default.Ghoul with { Speed = 6f } });

        // 6 instead of 4.5 m/s: 0.75 m more in half a second.
        Assert.Equal(normal - 0.75f, fast, 0.1f);
    }

    [Fact]
    public void A_stronger_blade_does_more_damage()
    {
        var world = new GameWorld(LevelParser.Parse("P\nD"), balance: Balance.Default with
        {
            Blade = Balance.Default.Blade with { Damage = 70 },
        });
        var dummy = world.Dummies[0];
        Face(world.Player, dummy.Position + new Vector3(0f, 1f, 0f));

        world.Tick(PlayerInput.None with { Melee = true });
        world.Tick(PlayerInput.None);

        Assert.Equal(Balance.Default.Dummy.MaxHealth - 70, dummy.Health);
    }

    [Fact]
    public void Bats_spit_at_the_rhythm_of_the_world()
    {
        var quick = Balance.Default with { Bat = Balance.Default.Bat with { SpitInterval = 1f } };
        var world = new GameWorld(LevelParser.Parse("P...B"), balance: quick);

        var firstSpit = -1;
        for (var tick = 0; tick < 2 * Second && firstSpit < 0; tick++)
        {
            world.Tick(PlayerInput.None);
            if (world.Events.Any(e => e is BatSpat))
            {
                firstSpit = tick;
            }
        }

        // With the standard 2 s it would come at tick 119; with 1 s at tick 59.
        Assert.Equal(Second - 1, firstSpit);
    }

    [Fact]
    public void Blood_bolts_fly_with_the_speed_of_the_world()
    {
        var world = new GameWorld(LevelParser.Parse("P....."), balance: Balance.Default with
        {
            BloodBolt = Balance.Default.BloodBolt with { Speed = 30f },
        });

        var bolt = world.Fire(new Vector3(9f, 1.4f, 1f), -Vector3.UnitX);

        Assert.Equal(30f, bolt.Velocity.Length(), 0.001f);
    }

    [Fact]
    public void A_restart_keeps_the_values()
    {
        var tough = Balance.Default with { Player = Balance.Default.Player with { MaxHealth = 150 } };
        var world = new GameWorld(LevelParser.Parse("P"), balance: tough);

        var restarted = world.Restarted();

        Assert.Same(tough, restarted.Balance);
        Assert.Equal(150, restarted.Player.Health);
    }

    [Fact]
    public void Dummies_get_the_health_of_the_world()
    {
        var world = new GameWorld(LevelParser.Parse("P\nD"), balance: Balance.Default with
        {
            Dummy = Balance.Default.Dummy with { MaxHealth = 250 },
        });

        Assert.Equal(250, world.Dummies[0].Health);
    }

    [Fact]
    public void Headshots_use_the_multiplier_of_the_world()
    {
        var world = new GameWorld(LevelParser.Parse("P"), balance: Balance.Default with
        {
            Pistol = Balance.Default.Pistol with { HeadshotMultiplier = 5f },
        });

        Assert.Equal(50, world.DamageRules.Calculate(new Hit(Weapon.Pistol, HitZone.Head, 10)).Amount);
    }

    [Fact]
    public void A_longer_blade_reaches_farther()
    {
        // The dummy stands 4 m ahead: out of reach of the standard 2 m blade, in reach of a 4 m one.
        var standard = SwingAtDummyFourMetresAhead(Balance.Default);
        var longer = SwingAtDummyFourMetresAhead(Balance.Default with { Blade = Balance.Default.Blade with { Range = 4f } });

        Assert.Equal(Balance.Default.Dummy.MaxHealth, standard);
        Assert.True(longer < Balance.Default.Dummy.MaxHealth, "The longer blade did not hit.");
    }

    [Fact]
    public void A_stronger_pistol_does_more_damage()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nD"), balance: Balance.Default with
        {
            Pistol = Balance.Default.Pistol with { Damage = 60 },
        });
        var dummy = world.Dummies[0];
        Face(world.Player, dummy.Position + new Vector3(0f, 1f, 0f));

        world.Tick(PlayerInput.None with { Shoot = true });

        Assert.Equal(Balance.Default.Dummy.MaxHealth - 60, dummy.Health);
    }

    [Fact]
    public void Weapons_wait_as_long_as_the_world_says()
    {
        var world = new GameWorld(LevelParser.Parse("P"), balance: Balance.Default with
        {
            Blade = Balance.Default.Blade with { Cooldown = 1f },
            Pistol = Balance.Default.Pistol with { Cooldown = 2f },
        });

        Assert.Equal(Second, world.Player.BladeCooldown.DurationTicks);
        Assert.Equal(2 * Second, world.Player.PistolCooldown.DurationTicks);
    }

    [Fact]
    public void The_blade_charges_as_fast_as_the_world_says()
    {
        var world = new GameWorld(LevelParser.Parse("P"), balance: Balance.Default with
        {
            Blade = Balance.Default.Blade with { ChargeTime = 0.5f },
        });
        var hold = PlayerInput.None with { Melee = true };

        for (var tick = 0; tick <= Second / 2; tick++)
        {
            world.Tick(hold);
        }

        Assert.Equal(1f, world.Player.BladeCharge.Fraction, 0.001f);
    }

    [Fact]
    public void The_stamina_bar_has_as_many_segments_as_the_world_says()
    {
        var world = new GameWorld(LevelParser.Parse("P"), balance: Balance.Default with
        {
            Player = Balance.Default.Player with { StaminaJumps = 3 },
        });

        Assert.Equal(3, world.Player.Stamina.Segments);
    }

    [Fact]
    public void The_restart_comes_as_soon_as_the_world_says()
    {
        var world = new GameWorld(LevelParser.Parse("P"), balance: Balance.Default with { RestartDelay = 0.5f });
        world.Player.TakeDamage(world.Player.MaxHealth, []);

        for (var tick = 0; tick < Second / 2; tick++)
        {
            world.Tick(PlayerInput.None);
        }

        Assert.True(world.ShouldRestart);
    }

    private static int SwingAtDummyFourMetresAhead(Balance balance)
    {
        // Cells are 2 m: the dummy in row 2 stands 4 m from the player in row 0.
        var world = new GameWorld(LevelParser.Parse("P\n.\nD"), balance: balance);
        var dummy = world.Dummies[0];
        Face(world.Player, dummy.Position + new Vector3(0f, 1f, 0f));

        world.Tick(PlayerInput.None with { Melee = true });
        world.Tick(PlayerInput.None);

        return dummy.Health;
    }

    private static float DistanceAfterHalfASecond(Balance balance)
    {
        var world = new GameWorld(LevelParser.Parse("P...G"), balance: balance);
        for (var tick = 0; tick < Second / 2; tick++)
        {
            world.Tick(PlayerInput.None);
        }

        return Vector3.Distance(world.Player.Position with { Y = 0f }, world.Ghouls[0].Position with { Y = 0f });
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
