using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Issue #775: the frost of Grave Chill on an enemy, as variant A of the trial piece #616. The chill goes on through the
/// world (<see cref="GameWorld.ApplyStatus"/>) and the world runs tick by tick with <see cref="Balance.Default"/>; the
/// tint of the body comes from <see cref="StatusLooks.TintOf(Ghoul)"/>, the frost's shape from the readout's lines.
/// </summary>
public class FrostLookTests
{
    private static readonly int ChillTicks = GraveChill.DurationTicks(Balance.Default.StatusEffects.GraveChill);

    private const string Corridor = "P\n.\n.\n.\nG";

    private static (GameWorld World, Ghoul Ghoul) ChilledGhoul()
    {
        var world = new GameWorld(LevelParser.Parse(Corridor));
        var ghoul = world.Ghouls[0];
        Assert.True(world.ApplyStatus(ghoul, StatusKind.GraveChill, world.Player));
        return (world, ghoul);
    }

    [Fact]
    public void A_chilled_ghoul_is_tinted_and_shows_the_frost_until_the_last_tick_of_the_chill_and_nothing_after()
    {
        var (world, ghoul) = ChilledGhoul();
        Assert.True(ChillTicks > 1, "The chill of the balance file must last more than one tick for this test.");

        RunTicks(world, PlayerInput.None, ChillTicks - 1);
        Assert.True(ghoul.Status.Has(StatusKind.GraveChill));
        Assert.Equal(FrostLook.Tint, StatusLooks.TintOf(ghoul));
        var line = Assert.Single(Assert.Single(EnemyStatusReadout.Of(world)).Lines);
        Assert.Equal(StatusForm.Frost, line.Form);
        Assert.Equal("0.1s", line.Timer);

        world.Tick(PlayerInput.None);
        Assert.False(ghoul.Status.Has(StatusKind.GraveChill));
        Assert.Null(StatusLooks.TintOf(ghoul));
        Assert.Empty(EnemyStatusReadout.Of(world));
    }

    [Fact]
    public void The_tint_is_the_same_on_the_first_and_the_last_tick_the_trial_piece_has_no_course_in_time()
    {
        var (world, ghoul) = ChilledGhoul();
        var first = StatusLooks.TintOf(ghoul);

        RunTicks(world, PlayerInput.None, ChillTicks - 1);

        Assert.Equal(first, StatusLooks.TintOf(ghoul));
    }

    [Fact]
    public void A_ghoul_without_a_chill_has_no_tint()
    {
        var world = new GameWorld(LevelParser.Parse(Corridor));
        world.Tick(PlayerInput.None);

        Assert.Null(StatusLooks.TintOf(world.Ghouls[0]));
    }

    [Fact]
    public void A_marked_ghoul_has_no_tint_and_keeps_its_motes()
    {
        var world = new GameWorld(LevelParser.Parse(Corridor));
        var ghoul = world.Ghouls[0];
        world.ApplyStatus(ghoul, StatusKind.Marked, world.Player);
        world.Tick(PlayerInput.None);

        Assert.Null(StatusLooks.TintOf(ghoul));
        Assert.Equal(StatusForm.Motes, Assert.Single(Assert.Single(EnemyStatusReadout.Of(world)).Lines).Form);
    }

    [Fact]
    public void A_ghoul_both_marked_and_chilled_is_tinted_and_shows_both_shapes()
    {
        var (world, ghoul) = ChilledGhoul();
        world.ApplyStatus(ghoul, StatusKind.Marked, world.Player);
        world.Tick(PlayerInput.None);

        Assert.Equal(FrostLook.Tint, StatusLooks.TintOf(ghoul));
        var forms = Assert.Single(EnemyStatusReadout.Of(world)).Lines.Select(line => line.Form).ToList();
        Assert.Contains(StatusForm.Frost, forms);
        Assert.Contains(StatusForm.Motes, forms);
    }

    [Fact]
    public void A_dead_ghoul_has_no_tint_although_its_chill_still_runs()
    {
        var (world, ghoul) = ChilledGhoul();
        world.Tick(PlayerInput.None);
        Assert.NotNull(StatusLooks.TintOf(ghoul));

        ghoul.TakeDamage(Balance.Default.Ghoul.MaxHealth, []);
        world.Tick(PlayerInput.None);

        Assert.True(ghoul.IsDead);
        Assert.True(ghoul.Status.Has(StatusKind.GraveChill), "The chill must still run, or the test would prove nothing.");
        Assert.Null(StatusLooks.TintOf(ghoul));
    }

    [Fact]
    public void A_chilled_bat_is_tinted_and_its_disc_lies_on_the_floor_below_it()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nB"));
        var bat = world.Bats[0];
        Assert.True(world.ApplyStatus(bat, StatusKind.GraveChill, world.Player));
        world.Tick(PlayerInput.None);

        Assert.Equal(FrostLook.Tint, StatusLooks.TintOf(bat));
        var readout = Assert.Single(EnemyStatusReadout.Of(world));
        Assert.Equal(StatusForm.Frost, Assert.Single(readout.Lines).Form);
        Assert.Equal(world.Level.CellAt(bat.Position).FloorHeight, readout.Floor);
        Assert.True(readout.Floor < readout.Feet.Y - 0.5f, "A bat flies: its disc lies on the floor, not at its feet.");
    }

    [Fact]
    public void A_chilled_gorm_is_tinted_and_his_disc_lies_at_his_feet()
    {
        var world = GormHallWorld();
        var gorm = world.Gorm!;
        Assert.True(world.ApplyStatus(gorm, StatusKind.GraveChill, world.Player));
        world.Tick(PlayerInput.None);

        Assert.Equal(FrostLook.Tint, StatusLooks.TintOf(gorm));
        var readout = Assert.Single(EnemyStatusReadout.Of(world));
        Assert.Equal(gorm.Position.Y, readout.Floor, 3);
    }

    [Fact]
    public void The_disc_grows_with_the_body_a_ghoul_keeps_the_disc_of_the_trial_piece_gorm_gets_a_larger_one()
    {
        var (ghoulWorld, _) = ChilledGhoul();
        ghoulWorld.Tick(PlayerInput.None);
        var ghoul = Assert.Single(EnemyStatusReadout.Of(ghoulWorld));

        var gormWorld = GormHallWorld();
        Assert.True(gormWorld.ApplyStatus(gormWorld.Gorm!, StatusKind.GraveChill, gormWorld.Player));
        gormWorld.Tick(PlayerInput.None);
        var gorm = Assert.Single(EnemyStatusReadout.Of(gormWorld));

        Assert.Equal(FrostLook.DiscRadius, ghoul.DiscRadius);                  // the ghoul's picture stays as it was
        Assert.True(gorm.Radius > ghoul.Radius, "Gorm's body must be wider than the ghoul's, or the test proves nothing.");
        Assert.True(gorm.DiscRadius > ghoul.DiscRadius);
        Assert.Equal(ghoul.DiscRadius / ghoul.Radius, gorm.DiscRadius / gorm.Radius, 4);  // the same proportion to the body
        Assert.True(gorm.DiscRadius > gorm.Radius, "Gorm's disc must reach beyond his body, or it hides between his legs.");
    }

    [Fact]
    public void The_look_of_grave_chill_is_the_frost_of_the_trial_piece()
    {
        var look = StatusLooks.Of(StatusKind.GraveChill, StatusLooks.All);

        Assert.Equal(StatusForm.Frost, look.Form);
        Assert.Equal(FrostLook.Tint, look.Tint);
        Assert.Equal(FrostLook.Rime, look.Colour);

        // Variant A of the trial piece #616 (effekte.py, grave_chill_a): Stoff 7, 6 and 5; 40 % toward Stoff 6; a disc
        // of 0.62 m whose rim begins at 0.72 of the squared radius; 14 flecks of 3 cm.
        Assert.Equal(((byte)168, (byte)181, (byte)199), FrostLook.Rime);
        Assert.Equal(((byte)126, (byte)145, (byte)178), FrostLook.Cold);
        Assert.Equal(((byte)86, (byte)107, (byte)154), FrostLook.DarkRime);
        Assert.Equal(new StatusTint(FrostLook.Cold, 0.4f), FrostLook.Tint);
        Assert.Equal(0.62f, FrostLook.DiscRadius);
        Assert.Equal(0.72f, FrostLook.RimStart * FrostLook.RimStart, 4);
        Assert.Equal(14, FrostLook.Flecks.Count);
        Assert.Equal(0.03f, FrostLook.FleckSize);
    }

    [Fact]
    public void Every_other_look_keeps_the_motes_and_no_tint()
    {
        Assert.All(
            StatusLooks.All.Where(look => look.Kind != StatusKind.GraveChill),
            look =>
            {
                Assert.Equal(StatusForm.Motes, look.Form);
                Assert.Null(look.Tint);
            });
    }

    [Fact]
    public void The_flecks_sit_on_the_side_facing_the_eye_within_the_body()
    {
        var (world, _) = ChilledGhoul();
        world.Tick(PlayerInput.None);
        var readout = Assert.Single(EnemyStatusReadout.Of(world));

        foreach (var eye in new[] { world.Player.Position, readout.Feet + new Vector3(5f, 1.7f, 0f) })
        {
            var toEye = Vector3.Normalize((eye - readout.Feet) with { Y = 0f });
            foreach (var fleck in FrostLook.Flecks)
            {
                var at = FrostLook.FleckAt(fleck, readout, eye);
                var flat = (at - readout.Feet) with { Y = 0f };

                Assert.Equal(readout.Radius, Vector3.Dot(flat, toEye), 4);              // in front of the middle
                Assert.True(MathF.Abs(fleck.Across) <= 1f);                              // not beside the body
                Assert.InRange(at.Y, readout.Feet.Y, readout.Feet.Y + readout.Height);  // between feet and head
            }
        }
    }

    [Fact]
    public void The_flecks_lie_apart_and_between_feet_and_head()
    {
        Assert.Equal(FrostLook.Flecks.Count, FrostLook.Flecks.Distinct().Count());
        Assert.All(FrostLook.Flecks, fleck => Assert.InRange(fleck.Up, 0f, 1f));
    }
}
