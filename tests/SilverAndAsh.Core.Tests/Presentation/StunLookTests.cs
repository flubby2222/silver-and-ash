using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Issue #778: the stun on an enemy as variant A of the trial piece #616 - three plus-shaped sparks circling above the
/// head. The stun goes on through the world (<see cref="GameWorld.ApplyStatus"/>) and the world runs tick by tick with
/// <see cref="Balance.Default"/>; where the sparks stand comes from <see cref="StunLook.SparksOf"/> on the readout.
/// </summary>
public class StunLookTests
{
    private static readonly StunBalance Values = Balance.Default.StatusEffects.Stun;

    private const string Corridor = "P\n.\n.\n.\nG";

    private static (GameWorld World, IEnemy Enemy, int Ticks) StunnedGhoul()
    {
        var world = new GameWorld(LevelParser.Parse(Corridor));
        Assert.True(world.ApplyStatus(world.Ghouls[0], StatusKind.Stun, world.Player));
        return (world, world.Ghouls[0], Stun.DurationTicks(Values));
    }

    private static (GameWorld World, IEnemy Enemy, int Ticks) StunnedBat()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nB"));
        Assert.True(world.ApplyStatus(world.Bats[0], StatusKind.Stun, world.Player));
        return (world, world.Bats[0], Stun.DurationTicks(Values));
    }

    private static (GameWorld World, IEnemy Enemy, int Ticks) StunnedGorm()
    {
        var world = GormHallWorld();
        Assert.True(world.ApplyStatus(world.Gorm!, StatusKind.Stun, world.Player));
        return (world, world.Gorm!, Stun.GormStaggerTicks(Values));
    }

    public static TheoryData<string> Kinds => ["ghoul", "bat", "gorm"];

    private static (GameWorld World, IEnemy Enemy, int Ticks) Stunned(string kind) => kind switch
    {
        "ghoul" => StunnedGhoul(),
        "bat" => StunnedBat(),
        _ => StunnedGorm(),
    };

    [Theory]
    [MemberData(nameof(Kinds))]
    public void Every_kind_shows_the_sparks_on_every_tick_of_its_stun_and_nothing_the_tick_it_ends(string kind)
    {
        var (world, enemy, ticks) = Stunned(kind);
        Assert.True(ticks > 1, "The stun of the balance file must last more than one tick for this test.");

        var shown = 0;
        while (enemy.Status.Has(StatusKind.Stun))
        {
            var readout = Assert.Single(EnemyStatusReadout.Of(world));
            var line = Assert.Single(readout.Lines);
            Assert.Equal(StatusForm.Sparks, line.Form);
            Assert.Equal(StunLook.SparkCount, StunLook.SparksOf(readout, world.Ticks, world.Player.Position).Count);
            Assert.True(readout.TimerLift > 0f);
            shown++;
            world.Tick(PlayerInput.None);
        }

        Assert.Equal(ticks, shown);                       // from the tick it lands to its last tick
        Assert.Empty(EnemyStatusReadout.Of(world));       // gone with the effect
        Assert.Null(StatusLooks.TintOf(enemy, StatusLooks.All));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void The_sparks_circle_above_the_head_on_a_ring_no_wider_than_the_body(string kind)
    {
        var (world, _, _) = Stunned(kind);
        world.Tick(PlayerInput.None);
        var readout = Assert.Single(EnemyStatusReadout.Of(world));
        var ring = StunLook.RingRadius * StunLook.ScaleFor(readout.Radius);
        Assert.True(ring <= readout.Radius, "The ring must not reach beyond the body, or the sparks stand beside it.");

        foreach (var ticks in new long[] { world.Ticks, world.Ticks + 7, world.Ticks + 31 })
        {
            foreach (var spark in StunLook.SparksOf(readout, ticks, world.Player.Position))
            {
                var flat = (spark.Centre - readout.Crown) with { Y = 0f };
                Assert.Equal(ring, flat.Length(), 4);                                        // on the ring round the head
                Assert.True(spark.Centre.Y - spark.ArmLength > readout.Crown.Y,              // the whole spark above the head
                    $"A spark reaches down to {spark.Centre.Y - spark.ArmLength}, the head ends at {readout.Crown.Y}.");
                Assert.True(spark.Centre.Y + spark.ArmLength < readout.Anchor.Y);            // and below the timers
            }
        }
    }

    [Fact]
    public void Over_a_ghoul_the_ring_lies_over_its_head_which_hangs_forward_of_its_feet()
    {
        var (world, enemy, _) = StunnedGhoul();
        world.Tick(PlayerInput.None);
        var readout = Assert.Single(EnemyStatusReadout.Of(world));
        var ghoul = (Ghoul)enemy;

        var forward = Vector3.Dot(readout.HeadOffset, ghoul.Facing);
        Assert.Equal(BodySizes.Ghoul.HeadForward, forward, 4);
        Assert.Equal(BodySizes.Ghoul.HeadTop, readout.Crown.Y - readout.Feet.Y, 4);
    }

    [Fact]
    public void A_ghoul_keeps_the_sizes_of_the_trial_piece_gorm_gets_them_in_proportion_to_his_body()
    {
        var ghoul = StunnedGhoul();
        ghoul.World.Tick(PlayerInput.None);
        var gorm = StunnedGorm();
        gorm.World.Tick(PlayerInput.None);
        var ghoulSpark = StunLook.SparksOf(Assert.Single(EnemyStatusReadout.Of(ghoul.World)), 0, Vector3.Zero)[0];
        var gormReadout = Assert.Single(EnemyStatusReadout.Of(gorm.World));
        var gormSpark = StunLook.SparksOf(gormReadout, 0, Vector3.Zero)[0];

        Assert.Equal(StunLook.ArmLength, ghoulSpark.ArmLength);
        Assert.Equal(StunLook.Thickness, ghoulSpark.Thickness);
        Assert.Equal(StunLook.ArmLength * gormReadout.Radius / BodySizes.Ghoul.Radius, gormSpark.ArmLength, 4);
    }

    [Fact]
    public void On_a_bat_the_sparks_stay_as_large_as_on_a_ghoul_and_circle_in_the_air()
    {
        var (world, _, _) = StunnedBat();
        world.Tick(PlayerInput.None);
        var readout = Assert.Single(EnemyStatusReadout.Of(world));
        Assert.True(readout.Radius < BodySizes.Ghoul.Radius, "The bat must be smaller than a ghoul, or the test proves nothing.");

        var spark = StunLook.SparksOf(readout, world.Ticks, world.Player.Position)[0];

        Assert.Equal(StunLook.ArmLength, spark.ArmLength);
        Assert.True(spark.Centre.Y > readout.Floor + BodySizes.Bat.HoverHeight, "The sparks circle above the bat in the air.");
    }

    [Fact]
    public void A_ghoul_both_stunned_and_chilled_shows_the_frost_and_the_sparks_and_the_cold_tint()
    {
        var (world, enemy, _) = StunnedGhoul();
        Assert.True(world.ApplyStatus(enemy, StatusKind.GraveChill, world.Player));
        world.Tick(PlayerInput.None);

        var readout = Assert.Single(EnemyStatusReadout.Of(world));
        var forms = readout.Lines.Select(line => line.Form).ToList();
        Assert.Contains(StatusForm.Frost, forms);
        Assert.Contains(StatusForm.Sparks, forms);
        Assert.Equal(FrostLook.Tint, StatusLooks.TintOf((Ghoul)enemy));
        Assert.Equal(StunLook.ReachFor(readout.Radius), readout.TimerLift);

        // The stun ends first: the frost stays, the sparks go, the timers come back down.
        RunTicks(world, PlayerInput.None, Stun.DurationTicks(Values));
        Assert.False(enemy.Status.Has(StatusKind.Stun));
        var after = Assert.Single(EnemyStatusReadout.Of(world));
        Assert.Equal(StatusForm.Frost, Assert.Single(after.Lines).Form);
        Assert.Equal(0f, after.TimerLift);
        Assert.Equal(FrostLook.Tint, StatusLooks.TintOf((Ghoul)enemy));
    }

    [Fact]
    public void Without_a_stun_the_timers_stand_where_they_always_stood()
    {
        var world = new GameWorld(LevelParser.Parse(Corridor));
        world.ApplyStatus(world.Ghouls[0], StatusKind.GraveChill, world.Player);
        world.Tick(PlayerInput.None);
        var readout = Assert.Single(EnemyStatusReadout.Of(world));

        Assert.Equal(0f, readout.TimerLift);
        Assert.Equal(readout.Feet.Y + readout.Height + 0.05f, readout.Anchor.Y, 5);
    }

    [Fact]
    public void The_sparks_turn_with_the_ticks_and_are_back_after_one_turn()
    {
        var (world, _, _) = StunnedGhoul();
        world.Tick(PlayerInput.None);
        var readout = Assert.Single(EnemyStatusReadout.Of(world));
        var eye = world.Player.Position;

        var now = StunLook.SparksOf(readout, 100, eye);
        var later = StunLook.SparksOf(readout, 100 + (StunLook.TicksPerTurn / 4), eye);
        var turned = StunLook.SparksOf(readout, 100 + StunLook.TicksPerTurn, eye);

        Assert.NotEqual(now[0].Centre, later[0].Centre);
        for (var k = 0; k < StunLook.SparkCount; k++)
        {
            Assert.Equal(now[k].Centre.X, turned[k].Centre.X, 4);
            Assert.Equal(now[k].Centre.Z, turned[k].Centre.Z, 4);
        }

        // Evenly apart on the ring: a third of a turn between two sparks.
        var middle = readout.Crown + new Vector3(0f, StunLook.Lift, 0f);
        var a = Vector3.Normalize((now[0].Centre - middle) with { Y = 0f });
        var b = Vector3.Normalize((now[1].Centre - middle) with { Y = 0f });
        Assert.Equal(MathF.Cos(MathF.Tau / 3f), Vector3.Dot(a, b), 4);
    }

    [Fact]
    public void Every_spark_faces_the_eye()
    {
        var (world, _, _) = StunnedGhoul();
        world.Tick(PlayerInput.None);
        var readout = Assert.Single(EnemyStatusReadout.Of(world));

        foreach (var eye in new[] { world.Player.Position, readout.Feet + new Vector3(5f, 1.7f, 0f) })
        {
            var toEye = Vector3.Normalize((eye - (readout.Crown + new Vector3(0f, StunLook.Lift, 0f))) with { Y = 0f });
            foreach (var spark in StunLook.SparksOf(readout, world.Ticks, eye))
            {
                Assert.Equal(0f, Vector3.Dot(spark.Across, toEye), 4);
                Assert.Equal(0f, spark.Across.Y);
                Assert.Equal(1f, spark.Across.Length(), 4);
            }
        }
    }

    [Fact]
    public void The_look_of_stun_is_the_sparks_of_the_trial_piece()
    {
        var look = StatusLooks.Of(StatusKind.Stun, StatusLooks.All);

        Assert.Equal(StatusForm.Sparks, look.Form);
        Assert.Null(look.Tint);
        Assert.Equal(StunLook.Arms, look.Colour);

        // Variant A of the trial piece #616 (effekte.py, stun_a): middle Eisen 7, arms Eisen 6; three sparks on a ring of
        // 0.30 m, 0.10 m above the head; arms of 0.075 m, as thick as an arm / 2.5.
        Assert.Equal(((byte)198, (byte)205, (byte)210), StunLook.Centre);
        Assert.Equal(((byte)163, (byte)175, (byte)187), StunLook.Arms);
        Assert.Equal(3, StunLook.SparkCount);
        Assert.Equal(0.30f, StunLook.RingRadius);
        Assert.Equal(0.10f, StunLook.Lift);
        Assert.Equal(0.075f, StunLook.ArmLength);
        Assert.Equal(StunLook.ArmLength / 2.5f, StunLook.Thickness, 4);
    }

    [Fact]
    public void The_sparks_and_the_frost_take_different_forms()
    {
        Assert.NotEqual(
            StatusLooks.Of(StatusKind.GraveChill, StatusLooks.All).Form,
            StatusLooks.Of(StatusKind.Stun, StatusLooks.All).Form);
    }
}
