using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class GameWorldPistolTests
{
    private static readonly PlayerInput Shoot = PlayerInput.None with { Shoot = true };
    private static readonly int PistolTicks = SimulationClock.SecondsToTicks(Tuning.Combat.PistolCooldown);

    // The eyes (1.6 m) are at the height of a dummy's head: a straight shot is a headshot. Tests about normal
    // damage therefore aim a little lower, at the body (since #70, hit zones).
    private const float BodyHeight = 1f;

    [Fact]
    public void A_shot_at_a_dummy_ahead_does_the_pistol_damage()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nD\n."));   // 6 m ahead: far beyond the blade
        AimAt(world, world.Dummies[0], BodyHeight);

        world.Tick(Shoot);

        Assert.Equal(Tuning.Combat.DummyMaxHealth - Tuning.Combat.PistolDamage, world.Dummies[0].Health);
        Assert.Contains(new DummyHit(0, Tuning.Combat.PistolDamage), world.Events);
        Assert.DoesNotContain(world.Events, e => e is BulletImpact);
    }

    [Fact]
    public void Enough_shots_knock_a_dummy_down_one_less_does_not()
    {
        // Counted from the values (35 damage: 3 shots), so the test stays right when the damage is tuned.
        var shotsNeeded = (int)Math.Ceiling((double)Tuning.Combat.DummyMaxHealth / Tuning.Combat.PistolDamage);
        var world = new GameWorld(LevelParser.Parse("P\n.\nD"));
        AimAt(world, world.Dummies[0], BodyHeight);

        for (var shot = 1; shot <= shotsNeeded; shot++)
        {
            world.Tick(Shoot);
            RunTicks(world, PlayerInput.None, PistolTicks);
            Assert.Equal(shot == shotsNeeded, world.Dummies[0].IsDown);
        }

        Assert.Equal(3, shotsNeeded);
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
        AimAt(world, world.Dummies[0], BodyHeight);

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

    [Fact]
    public void A_headshot_does_triple_damage_and_knocks_a_dummy_down_at_once()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nD\n."));
        AimAt(world, world.Dummies[0], Tuning.Combat.DummyHeight - Tuning.Combat.DummyHeadHeight / 2f);

        world.Tick(Shoot);

        var damage = (int)MathF.Round(Tuning.Combat.PistolDamage * Tuning.Combat.PistolHeadshotMultiplier, MidpointRounding.AwayFromZero);
        Assert.Contains(new DummyHit(0, damage, Critical: true), world.Events);
        Assert.True(world.Dummies[0].IsDown);
    }

    [Fact]
    public void A_body_shot_is_not_critical()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nD\n."));
        AimAt(world, world.Dummies[0], BodyHeight);

        world.Tick(Shoot);

        Assert.DoesNotContain(world.Events, e => e is DummyHit { Critical: true });
    }

    [Fact]
    public void Rules_added_to_the_world_change_the_damage()
    {
        // The modular part: an effect (later an item) only adds a rule to the world.
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nD\n."));
        world.DamageRules.Add(new DoubleForPistol());
        AimAt(world, world.Dummies[0], BodyHeight);

        world.Tick(Shoot);

        Assert.Contains(new DummyHit(0, 2 * Tuning.Combat.PistolDamage), world.Events);
    }

    /// <summary>Turns the view (like the mouse would) so it looks at <paramref name="height"/> above the dummy's feet.</summary>
    private static void AimAt(GameWorld world, TrainingDummy dummy, float height)
    {
        var player = world.Player;
        var direction = Vector3.Normalize(dummy.Position with { Y = dummy.Position.Y + height } - player.EyePosition);
        var yaw = MathF.Atan2(direction.X, direction.Z);
        var pitch = MathF.Asin(direction.Y);

        // ApplyMouse: yaw -= x * sensitivity, pitch -= y * sensitivity.
        var pixels = new Vector2(
            -(yaw - player.View.YawRadians) / Tuning.Controls.MouseSensitivity,
            -(pitch - player.View.PitchRadians) / Tuning.Controls.MouseSensitivity);
        player.Look(PlayerInput.None with { Look = pixels });

        Assert.Equal(1f, Vector3.Dot(player.View.Forward, direction), 0.0001f);
    }

    private sealed class DoubleForPistol : IDamageRule
    {
        public bool IsCritical => false;

        public float Multiplier(Hit hit) => hit.Weapon == Weapon.Pistol ? 2f : 1f;
    }

    private static void RunTicks(GameWorld world, PlayerInput input, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            world.Tick(input);
        }
    }
}
