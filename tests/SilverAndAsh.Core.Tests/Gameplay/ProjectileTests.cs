using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class ProjectileTests
{
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly int Second = SimulationClock.TicksPerSecond;

    [Fact]
    public void It_flies_in_a_straight_line_with_its_speed()
    {
        var world = new GameWorld(Corridor(30));
        var bolt = Fire(world, new Vector3(1f, 1.4f, 10f), Vector3.UnitZ);

        Run(world, PlayerInput.None, Second / 2);

        Assert.Equal(new Vector3(1f, 1.4f, 10f + Tuning.BloodBolt.Speed / 2f), bolt.Position, new Vector3Comparer(0.01f));
        Assert.Contains(bolt, world.Projectiles);
    }

    [Fact]
    public void It_hits_a_standing_player_for_its_damage_and_is_gone()
    {
        var world = new GameWorld(Corridor(4));
        var bolt = Fire(world, new Vector3(1f, 1.2f, 7f), -Vector3.UnitZ);

        var seen = Run(world, PlayerInput.None, Second);

        Assert.Contains(new PlayerHit(world.Player.Id, Tuning.BloodBolt.Damage), seen);
        Assert.Contains(seen, e => e is ProjectileHitPlayer hit && hit.ProjectileId == bolt.Id);
        Assert.Equal(Tuning.Combat.PlayerMaxHealth - Tuning.BloodBolt.Damage, world.Player.Health);
        Assert.True(bolt.IsGone);
        Assert.Empty(world.Projectiles);
    }

    [Fact]
    public void A_wall_stops_it()
    {
        // Wall cell from x = 6 m. Fired along +X, away from the player.
        var world = new GameWorld(LevelParser.Parse("P..#"));
        var bolt = Fire(world, new Vector3(3f, 1.4f, 1f), Vector3.UnitX);

        var seen = Run(world, PlayerInput.None, Second);

        var impact = Assert.Single(seen.OfType<ProjectileImpact>());
        Assert.Equal(bolt.Id, impact.ProjectileId);
        Assert.InRange(impact.Point.X, 5.7f, 6.01f);
        Assert.DoesNotContain(seen, e => e is PlayerHit);
        Assert.True(bolt.IsGone);
    }

    [Fact]
    public void Dodging_ducks_under_it_walking_does_not()
    {
        // Bolt at 1.4 m height (its bottom at 1.25 m); ducked during a dodge the player is only 1 m tall.
        var ducking = new GameWorld(Corridor(6));
        Fire(ducking, new Vector3(1f, 1.4f, 9f), -Vector3.UnitZ);
        var seenDucking = Run(ducking, Forward with { Dash = true }, 1);
        seenDucking.AddRange(Run(ducking, Forward, Second));

        var walking = new GameWorld(Corridor(6));
        Fire(walking, new Vector3(1f, 1.4f, 9f), -Vector3.UnitZ);
        var seenWalking = Run(walking, Forward, Second);

        Assert.DoesNotContain(seenDucking, e => e is PlayerHit);
        Assert.Contains(seenWalking, e => e is PlayerHit);
    }

    [Fact]
    public void It_flies_through_enemies_and_hits_only_the_player()
    {
        // A ghoul stands between the bolt and the player; the bolt flies low (0.5 m) right through it.
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nG\n.\n."));
        Fire(world, new Vector3(1f, 0.5f, 11f), -Vector3.UnitZ);

        var seen = Run(world, PlayerInput.None, Second);

        Assert.DoesNotContain(seen, e => e is GhoulHit);
        Assert.Contains(seen, e => e is PlayerHit);
    }

    [Fact]
    public void A_very_fast_projectile_does_not_jump_through_the_player()
    {
        // 150 m/s = 2.5 m per tick, more than the player is wide (about 1 m with the ball): it moves in small steps.
        var world = new GameWorld(Corridor(6));
        world.Fire(new Vector3(1f, 1.2f, 12f), -Vector3.UnitZ, 150f, Tuning.BloodBolt.Damage, Tuning.BloodBolt.Radius, Tuning.BloodBolt.LifeTime);

        var seen = Run(world, PlayerInput.None, Second / 4);

        Assert.Contains(seen, e => e is PlayerHit);
    }

    [Fact]
    public void A_dead_player_is_not_hit_any_more()
    {
        var world = new GameWorld(Corridor(4));
        world.Player.TakeDamage(Tuning.Combat.PlayerMaxHealth, []);
        Fire(world, new Vector3(1f, 1.2f, 7f), -Vector3.UnitZ);

        var seen = Run(world, PlayerInput.None, Second);

        Assert.DoesNotContain(seen, e => e is ProjectileHitPlayer);
        Assert.Contains(seen, e => e is ProjectileImpact);            // it flies on to the wall behind
    }

    [Fact]
    public void It_disappears_after_its_lifetime()
    {
        // 140 m of corridor: farther than it can fly in its lifetime.
        var world = new GameWorld(Corridor(70));
        var bolt = Fire(world, new Vector3(1f, 1.4f, 5f), Vector3.UnitZ);

        var seen = Run(world, PlayerInput.None, SimulationClock.SecondsToTicks(Tuning.BloodBolt.LifeTime) + 1);

        Assert.True(bolt.IsGone);
        Assert.Empty(world.Projectiles);
        Assert.DoesNotContain(seen, e => e is ProjectileImpact);
    }

    [Fact]
    public void Every_projectile_gets_its_own_number()
    {
        var world = new GameWorld(Corridor(10));

        var a = Fire(world, new Vector3(1f, 1.4f, 5f), Vector3.UnitZ);
        var b = Fire(world, new Vector3(1f, 1.4f, 6f), Vector3.UnitZ);

        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(2, world.Projectiles.Count);
    }

    private static Projectile Fire(GameWorld world, Vector3 origin, Vector3 direction) =>
        world.Fire(origin, direction, Tuning.BloodBolt.Speed, Tuning.BloodBolt.Damage, Tuning.BloodBolt.Radius, Tuning.BloodBolt.LifeTime);

    /// <summary>A corridor along +Z: the player in row 0, then <paramref name="rows"/> more rows, closed by the outer wall.</summary>
    private static Level Corridor(int rows) => LevelParser.Parse(string.Join('\n', Enumerable.Repeat(".", rows).Prepend("P")));

    private static List<GameEvent> Run(GameWorld world, PlayerInput input, int ticks)
    {
        var seen = new List<GameEvent>();
        for (var i = 0; i < ticks; i++)
        {
            world.Tick(input);
            seen.AddRange(world.Events);
        }

        return seen;
    }

    private sealed class Vector3Comparer(float tolerance) : IEqualityComparer<Vector3>
    {
        public bool Equals(Vector3 a, Vector3 b) => Vector3.Distance(a, b) <= tolerance;

        public int GetHashCode(Vector3 obj) => 0;
    }
}
