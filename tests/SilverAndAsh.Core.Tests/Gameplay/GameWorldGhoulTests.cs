using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class GameWorldGhoulTests
{
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly PlayerInput Melee = PlayerInput.None with { Melee = true };
    private static readonly PlayerInput Shoot = PlayerInput.None with { Shoot = true };
    private static readonly int BladeTicks = SimulationClock.SecondsToTicks(Balance.Default.Blade.Cooldown);

    [Fact]
    public void The_world_puts_a_ghoul_on_every_G_cell_with_ids_after_the_dummies()
    {
        var world = new GameWorld(LevelParser.Parse("P.D\n.G.\nG.."));

        Assert.Equal(2, world.Ghouls.Count);
        Assert.Equal(world.Level.CellCenter(1, 1), world.Ghouls[0].Position);
        Assert.Equal(world.Level.CellCenter(0, 2), world.Ghouls[1].Position);

        // Ids are unique among all targets, so events never mix up a dummy and a ghoul.
        var ids = world.Targets.Select(target => target.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(3, ids.Count);
    }

    [Fact]
    public void Ghouls_start_looking_towards_the_player()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nG"));

        Assert.Equal(-Vector3.UnitZ, world.Ghouls[0].Facing);
    }

    [Fact]
    public void As_many_blade_swings_as_its_health_needs_kill_a_ghoul()
    {
        // With the standard values: 100 health, 40 per swing - the third swing kills.
        var world = InFrontOfAGhoul();
        var swings = (int)Math.Ceiling(Balance.Default.Ghoul.MaxHealth / (double)Balance.Default.Blade.Damage);

        for (var swing = 1; swing < swings; swing++)
        {
            Swing(world);
            RunTicks(world, PlayerInput.None, BladeTicks);
        }

        Assert.False(world.Ghouls[0].IsDead, "One swing short of its health the ghoul must still live.");
        var seen = new List<GameEvent>();
        world.Tick(Melee);
        world.Tick(PlayerInput.None);
        seen.AddRange(world.Events);

        Assert.True(world.Ghouls[0].IsDead);
        Assert.Contains(new GhoulDied(world.Ghouls[0].Id), seen);
    }

    [Fact]
    public void A_pistol_headshot_kills_a_ghoul_at_once()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nG\n."));
        var ghoul = world.Ghouls[0];
        var head = ghoul.Position + ghoul.Facing * (BodySizes.Ghoul.Radius * 0.8f)
            + new Vector3(0f, BodySizes.Ghoul.HeadTop - BodySizes.Ghoul.HeadHeight / 2f, 0f);
        AimAt(world.Player, head);

        world.Tick(Shoot);

        Assert.True(ghoul.IsDead);
        Assert.Contains(world.Events, e => e is GhoulHit { Critical: true });
    }

    // Issue #433: shots hit the shape of the model, not the round body the ghoul walks with. From 16 m away, so the
    // ray runs almost flat over the ghoul (it falls 5 cm per metre) and passes close to the aimed point.
    private const string LongCorridor = "P\n.\n.\n.\n.\n.\n.\n.\nG";

    [Fact]
    public void A_shot_at_the_middle_of_the_models_head_is_a_headshot()
    {
        var world = new GameWorld(LevelParser.Parse(LongCorridor));
        var ghoul = world.Ghouls[0];

        ShootAt(world,OnGhoul(ghoul, BodySizes.Ghoul.HeadForward, BodySizes.Ghoul.HeadTop - BodySizes.Ghoul.HeadHeight / 2f, 0f));

        Assert.Contains(world.Events, e => e is GhoulHit { Critical: true });
    }

    [Fact]
    public void A_shot_at_the_models_shoulder_hits_the_body()
    {
        // The upper arm: beside the head, at shoulder height (the model's shoulders are 0.14 .. 0.35 m to the side).
        var world = new GameWorld(LevelParser.Parse(LongCorridor));
        var ghoul = world.Ghouls[0];

        ShootAt(world,OnGhoul(ghoul, BodySizes.Ghoul.TorsoForward, BodySizes.Ghoul.TorsoHeight - 0.1f, BodySizes.Ghoul.TorsoRadius - 0.1f));

        Assert.Contains(world.Events, e => e is GhoulHit { Critical: false });
        Assert.True(ghoul.Health < Balance.Default.Ghoul.MaxHealth);
    }

    [Fact]
    public void A_shot_just_above_the_head_misses()
    {
        var world = new GameWorld(LevelParser.Parse(LongCorridor));
        var ghoul = world.Ghouls[0];

        ShootAt(world,OnGhoul(ghoul, BodySizes.Ghoul.HeadForward, BodySizes.Ghoul.HeadTop + 0.03f, 0f));

        AssertMissed(world, ghoul);
    }

    [Fact]
    public void A_shot_just_beside_the_head_misses()
    {
        // Above the shoulders, a hand's breadth beside the head: there is nothing of the model there.
        var world = new GameWorld(LevelParser.Parse(LongCorridor));
        var ghoul = world.Ghouls[0];

        ShootAt(world,OnGhoul(ghoul, BodySizes.Ghoul.HeadForward, BodySizes.Ghoul.HeadTop - 0.05f, BodySizes.Ghoul.HeadRadius + 0.03f));

        AssertMissed(world, ghoul);
    }

    [Fact]
    public void A_shot_just_above_the_back_misses()
    {
        var world = new GameWorld(LevelParser.Parse(LongCorridor));
        var ghoul = world.Ghouls[0];

        ShootAt(world,OnGhoul(ghoul, 0f, BodySizes.Ghoul.TorsoHeight + 0.04f, BodySizes.Ghoul.HeadRadius + 0.14f));

        AssertMissed(world, ghoul);
    }

    [Fact]
    public void A_shot_just_beside_the_shoulder_misses()
    {
        var world = new GameWorld(LevelParser.Parse(LongCorridor));
        var ghoul = world.Ghouls[0];

        ShootAt(world,OnGhoul(ghoul, BodySizes.Ghoul.TorsoForward, BodySizes.Ghoul.TorsoHeight - 0.1f, BodySizes.Ghoul.TorsoRadius + 0.03f));

        AssertMissed(world, ghoul);
    }

    [Fact]
    public void The_player_cannot_walk_through_a_ghoul_but_through_a_dead_one()
    {
        var alive = new GameWorld(LevelParser.Parse("P\n.\nG\n.\n.\n."));
        RunTicks(alive, Forward, 3 * SimulationClock.TicksPerSecond);
        Assert.True(alive.Player.Position.Z < alive.Ghouls[0].Position.Z);

        var dead = new GameWorld(LevelParser.Parse("P\n.\nG\n.\n.\n."));
        dead.Ghouls[0].TakeDamage(Balance.Default.Ghoul.MaxHealth, []);
        RunTicks(dead, Forward, SimulationClock.TicksPerSecond);
        Assert.True(dead.Player.Position.Z > dead.Ghouls[0].Position.Z);
    }

    [Fact]
    public void A_shot_flies_over_a_dead_ghoul()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nG\n.\nD"));
        world.Ghouls[0].TakeDamage(Balance.Default.Ghoul.MaxHealth, []);
        AimAt(world.Player, world.Dummies[0].Position + new Vector3(0f, 1f, 0f));

        world.Tick(Shoot);

        Assert.Contains(world.Events, e => e is DummyHit);
    }

    [Fact]
    public void The_world_lets_dead_ghouls_disappear()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nG"));
        world.Ghouls[0].TakeDamage(Balance.Default.Ghoul.MaxHealth, []);

        RunTicks(world, PlayerInput.None, SimulationClock.SecondsToTicks(Balance.Default.Ghoul.CorpseTime) + 1);

        Assert.True(world.Ghouls[0].IsGone);
    }

    private static GameWorld InFrontOfAGhoul()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nG"));
        RunTicks(world, Forward, SimulationClock.TicksPerSecond);
        RunTicks(world, PlayerInput.None, 10);

        // The ghoul is low: look down at it so it is well inside the blade's cone.
        AimAt(world.Player, world.Ghouls[0].Position + new Vector3(0f, 0.6f, 0f));
        return world;
    }

    /// <summary>A point on the ghoul: this far in front of its feet, this high, this far to its side (metres).</summary>
    private static Vector3 OnGhoul(Ghoul ghoul, float forward, float up, float side) =>
        ghoul.Position + ghoul.Facing * forward + new Vector3(0f, up, 0f)
        + new Vector3(ghoul.Facing.Z, 0f, -ghoul.Facing.X) * side;

    private static void ShootAt(GameWorld world, Vector3 point)
    {
        AimAt(world.Player, point);
        world.Tick(Shoot);
    }

    private static void AssertMissed(GameWorld world, Ghoul ghoul)
    {
        Assert.DoesNotContain(world.Events, e => e is GhoulHit);
        Assert.Equal(Balance.Default.Ghoul.MaxHealth, ghoul.Health);
    }

    private static void Swing(GameWorld world)
    {
        world.Tick(Melee);
        world.Tick(PlayerInput.None);
    }

    private static void AimAt(Player player, Vector3 point)
    {
        var direction = Vector3.Normalize(point - player.EyePosition);
        var pixels = new Vector2(
            -(MathF.Atan2(direction.X, direction.Z) - player.View.YawRadians) / ControlSettings.MouseSensitivity,
            -(MathF.Asin(direction.Y) - player.View.PitchRadians) / ControlSettings.MouseSensitivity);
        player.Look(PlayerInput.None with { Look = pixels });
    }

    private static void RunTicks(GameWorld world, PlayerInput input, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            world.Tick(input);
        }
    }
}
