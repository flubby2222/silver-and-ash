using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class GameWorldBladeTests
{
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly PlayerInput Melee = PlayerInput.None with { Melee = true };
    private static readonly int BladeTicks = SimulationClock.SecondsToTicks(Tuning.Combat.BladeCooldown);

    [Fact]
    public void A_swing_at_a_dummy_in_front_does_the_blade_damage()
    {
        var world = InFrontOfADummy();

        world.Tick(Melee);

        Assert.Equal(Tuning.Combat.DummyMaxHealth - Tuning.Combat.BladeDamage, world.Dummies[0].Health);
        Assert.Contains(new DummyHit(0, Tuning.Combat.BladeDamage), world.Events);
    }

    [Fact]
    public void Three_swings_knock_a_dummy_down()
    {
        var world = InFrontOfADummy();
        var seen = new List<GameEvent>();

        for (var swing = 0; swing < 3; swing++)
        {
            world.Tick(Melee);
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

        world.Tick(Melee);

        Assert.Equal(Tuning.Combat.DummyMaxHealth, world.Dummies[0].Health);
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
        world.Tick(Melee);

        Assert.All(world.Dummies, dummy => Assert.Equal(Tuning.Combat.DummyMaxHealth - Tuning.Combat.BladeDamage, dummy.Health));
    }

    [Fact]
    public void A_swing_during_the_cooldown_does_no_damage()
    {
        var world = InFrontOfADummy();
        world.Tick(Melee);
        world.Tick(PlayerInput.None);

        world.Tick(Melee);

        Assert.Equal(Tuning.Combat.DummyMaxHealth - Tuning.Combat.BladeDamage, world.Dummies[0].Health);
    }

    /// <summary>The player walked up to a dummy and stands right in front of it.</summary>
    private static GameWorld InFrontOfADummy()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nD"));
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
