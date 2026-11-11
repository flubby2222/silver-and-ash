using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class GameWorldPistolTests
{
    private static readonly PlayerInput Shoot = PlayerInput.None with { Shoot = true };
    private static readonly int PistolTicks = SimulationClock.SecondsToTicks(Tuning.Combat.PistolCooldown);

    [Fact]
    public void A_shot_at_a_dummy_ahead_does_the_pistol_damage()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nD\n."));   // 6 m ahead: far beyond the blade

        world.Tick(Shoot);

        Assert.Equal(Tuning.Combat.DummyMaxHealth - Tuning.Combat.PistolDamage, world.Dummies[0].Health);
        Assert.Contains(new DummyHit(0, Tuning.Combat.PistolDamage), world.Events);
        Assert.DoesNotContain(world.Events, e => e is BulletImpact);
    }

    [Fact]
    public void Four_shots_knock_a_dummy_down()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nD"));

        for (var shot = 0; shot < 4; shot++)
        {
            world.Tick(Shoot);
            RunTicks(world, PlayerInput.None, PistolTicks);
        }

        Assert.True(world.Dummies[0].IsDown);
    }

    [Fact]
    public void A_wall_in_front_takes_the_shot_instead()
    {
        // A pillar between player and dummy: the shot hits the pillar and leaves an impact there.
        var world = new GameWorld(LevelParser.Parse("P\n#\nD"));

        world.Tick(Shoot);

        Assert.Equal(Tuning.Combat.DummyMaxHealth, world.Dummies[0].Health);
        var impact = Assert.Single(world.Events.OfType<BulletImpact>());
        Assert.Equal(2f, impact.Point.Z, 0.01f);
        Assert.Equal(-Vector3.UnitZ, impact.Normal);
    }

    [Fact]
    public void A_shot_hits_only_the_first_dummy_in_line()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nD\n.\nD"));

        world.Tick(Shoot);

        Assert.Equal(Tuning.Combat.DummyMaxHealth - Tuning.Combat.PistolDamage, world.Dummies[0].Health);
        Assert.Equal(Tuning.Combat.DummyMaxHealth, world.Dummies[1].Health);
    }

    [Fact]
    public void A_shot_passes_over_a_dummy_on_the_ground_to_the_wall_behind()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nD\n#"));
        world.Dummies[0].TakeDamage(Tuning.Combat.DummyMaxHealth, []);

        world.Tick(Shoot);

        var impact = Assert.Single(world.Events.OfType<BulletImpact>());
        Assert.Equal(6f, impact.Point.Z, 0.01f);
    }

    [Fact]
    public void Nothing_is_hit_beyond_the_pistol_range()
    {
        // A long corridor: the dummy (and the end wall) are farther away than the range.
        var cells = (int)(Tuning.Combat.PistolRange / Level.CellSize) + 3;
        var rows = Enumerable.Repeat(".", cells).Prepend("P").Append("D");
        var world = new GameWorld(LevelParser.Parse(string.Join('\n', rows)));

        world.Tick(Shoot);

        Assert.Equal(Tuning.Combat.DummyMaxHealth, world.Dummies[0].Health);
        Assert.DoesNotContain(world.Events, e => e is BulletImpact);
    }

    private static void RunTicks(GameWorld world, PlayerInput input, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            world.Tick(input);
        }
    }
}
