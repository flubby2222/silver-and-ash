using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class GameWorldBladeTests
{
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly PlayerInput Melee = PlayerInput.None with { Melee = true };
    private static readonly int BladeTicks = SimulationClock.SecondsToTicks(Balance.Default.Blade.Cooldown);

    [Fact]
    public void A_swing_at_a_dummy_in_front_does_the_blade_damage()
    {
        var world = InFrontOfADummy();

        Swing(world);

        Assert.Equal(Balance.Default.Dummy.MaxHealth - Balance.Default.Blade.Damage, world.Dummies[0].Health);
        Assert.Contains(new DummyHit(0, Balance.Default.Blade.Damage), world.Events);
    }

    [Fact]
    public void Three_swings_knock_a_dummy_down()
    {
        var world = InFrontOfADummy();
        var seen = new List<GameEvent>();

        for (var swing = 0; swing < 3; swing++)
        {
            Swing(world);
            seen.AddRange(world.Events);
            RunTicks(world, PlayerInput.None, BladeTicks);
        }

        Assert.True(world.Dummies[0].IsDown);
        Assert.Contains(new DummyKnockedDown(0), seen);
    }

    [Fact]
    public void A_swing_out_of_reach_does_nothing()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nD"));   // dummy 4 m ahead

        Swing(world);

        Assert.Equal(Balance.Default.Dummy.MaxHealth, world.Dummies[0].Health);
        Assert.DoesNotContain(world.Events, e => e is DummyHit);
    }

    [Fact]
    public void One_swing_hits_every_dummy_in_the_cone()
    {
        // Two dummies side by side ahead; the player steps to the right until it stands between them.
        var world = new GameWorld(LevelParser.Parse(".P\nDD"));
        var right = PlayerInput.None with { Move = new Vector2(1f, 0f) };
        var middle = (world.Dummies[0].Position.X + world.Dummies[1].Position.X) / 2f;
        for (var i = 0; i < 60 && MathF.Abs(world.Player.Position.X - middle) > 0.3f; i++)
        {
            world.Tick(right);
        }

        RunTicks(world, PlayerInput.None, 10);
        Swing(world);

        Assert.All(world.Dummies, dummy => Assert.Equal(Balance.Default.Dummy.MaxHealth - Balance.Default.Blade.Damage, dummy.Health));
    }

    [Fact]
    public void A_swing_during_the_cooldown_does_no_damage()
    {
        var world = InFrontOfADummy();
        Swing(world);
        world.Tick(PlayerInput.None);

        Swing(world);

        Assert.Equal(Balance.Default.Dummy.MaxHealth - Balance.Default.Blade.Damage, world.Dummies[0].Health);
    }

    [Fact]
    public void Two_fully_charged_swings_knock_a_dummy_down()
    {
        // Full charge: 200 % of the blade damage (80); the dummy (100) falls with the second swing.
        var world = InFrontOfADummy();
        var fullTicks = SimulationClock.SecondsToTicks(Balance.Default.Blade.ChargeTime);

        world.Tick(Melee);
        RunTicks(world, Melee, fullTicks);
        world.Tick(PlayerInput.None);

        var charged = (int)(Balance.Default.Blade.Damage * Balance.Default.Blade.ChargeMaxMultiplier);
        Assert.Contains(new DummyHit(0, charged), world.Events);
        Assert.False(world.Dummies[0].IsDown);

        RunTicks(world, PlayerInput.None, BladeTicks);
        world.Tick(Melee);
        RunTicks(world, Melee, fullTicks);
        world.Tick(PlayerInput.None);

        Assert.True(world.Dummies[0].IsDown);
    }

    [Fact]
    public void The_blade_reaches_exactly_as_far_as_the_world_s_balance_says()
    {
        // Issue #425: the reach comes from the balance, to the dummy's surface - a blade a little longer than the
        // gap hits, one a little shorter does not. A reach fixed in the code cannot pass both.
        var standing = InFrontOfADummy();
        var dummy = standing.Dummies[0].Body;
        var eye = standing.Player.EyePosition;
        var axis = dummy.Position with { Y = Math.Clamp(eye.Y, dummy.Position.Y, dummy.Position.Y + dummy.Height) };
        var gap = Vector3.Distance(eye, axis) - dummy.Radius;

        Assert.Equal(Balance.Default.Dummy.MaxHealth - Balance.Default.Blade.Damage, HealthAfterASwing(gap + 0.05f));
        Assert.Equal(Balance.Default.Dummy.MaxHealth, HealthAfterASwing(gap - 0.05f));

        static int HealthAfterASwing(float range)
        {
            var world = InFrontOfADummy(Balance.Default with { Blade = Balance.Default.Blade with { Range = range } });
            Swing(world);
            return world.Dummies[0].Health;
        }
    }

    /// <summary>A quick (uncharged) swing: button pressed in one tick, let go in the next - the swing happens then.</summary>
    private static void Swing(GameWorld world)
    {
        world.Tick(Melee);
        world.Tick(PlayerInput.None);
    }

    /// <summary>The player walked up to a dummy and stands right in front of it.</summary>
    private static GameWorld InFrontOfADummy(Balance? balance = null)
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nD"), balance: balance ?? Balance.Default);
        RunTicks(world, Forward, SimulationClock.TicksPerSecond);
        RunTicks(world, PlayerInput.None, 10);
        return world;
    }

    private static void RunTicks(GameWorld world, PlayerInput input, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            world.Tick(input);
        }
    }
}
