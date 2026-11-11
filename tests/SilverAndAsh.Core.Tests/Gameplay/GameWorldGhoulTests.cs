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
    private static readonly int BladeTicks = SimulationClock.SecondsToTicks(Tuning.Combat.BladeCooldown);

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
    public void Two_blade_swings_kill_a_ghoul()
    {
        var world = InFrontOfAGhoul();

        Swing(world);
        RunTicks(world, PlayerInput.None, BladeTicks);
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
        var head = ghoul.Position + ghoul.Facing * (Tuning.Ghoul.Radius * 0.8f)
            + new Vector3(0f, Tuning.Ghoul.Height - Tuning.Ghoul.HeadHeight / 2f, 0f);
        AimAt(world.Player, head);

        world.Tick(Shoot);

        Assert.True(ghoul.IsDead);
        Assert.Contains(world.Events, e => e is GhoulHit { Critical: true });
    }

    [Fact]
    public void The_player_cannot_walk_through_a_ghoul_but_through_a_dead_one()
    {
        var alive = new GameWorld(LevelParser.Parse("P\n.\nG\n.\n.\n."));
        RunTicks(alive, Forward, 3 * SimulationClock.TicksPerSecond);
        Assert.True(alive.Player.Position.Z < alive.Ghouls[0].Position.Z);

        var dead = new GameWorld(LevelParser.Parse("P\n.\nG\n.\n.\n."));
        dead.Ghouls[0].TakeDamage(Tuning.Ghoul.MaxHealth, []);
        RunTicks(dead, Forward, SimulationClock.TicksPerSecond);
        Assert.True(dead.Player.Position.Z > dead.Ghouls[0].Position.Z);
    }

    [Fact]
    public void A_shot_flies_over_a_dead_ghoul()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nG\n.\nD"));
        world.Ghouls[0].TakeDamage(Tuning.Ghoul.MaxHealth, []);
        AimAt(world.Player, world.Dummies[0].Position + new Vector3(0f, 1f, 0f));

        world.Tick(Shoot);

        Assert.Contains(world.Events, e => e is DummyHit);
    }

    [Fact]
    public void The_world_lets_dead_ghouls_disappear()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\nG"));
        world.Ghouls[0].TakeDamage(Tuning.Ghoul.MaxHealth, []);

        RunTicks(world, PlayerInput.None, SimulationClock.SecondsToTicks(Tuning.Ghoul.CorpseTime) + 1);

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

    private static void Swing(GameWorld world)
    {
        world.Tick(Melee);
        world.Tick(PlayerInput.None);
    }

    private static void AimAt(Player player, Vector3 point)
    {
        var direction = Vector3.Normalize(point - player.EyePosition);
        var pixels = new Vector2(
            -(MathF.Atan2(direction.X, direction.Z) - player.View.YawRadians) / Tuning.Controls.MouseSensitivity,
            -(MathF.Asin(direction.Y) - player.View.PitchRadians) / Tuning.Controls.MouseSensitivity);
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
