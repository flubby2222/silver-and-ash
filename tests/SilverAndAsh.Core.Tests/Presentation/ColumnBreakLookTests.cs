using System.Numerics;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Issue #601: the breaking column - one model per stage, the stage chosen from the blows a column took, the fall of
/// 81 ticks (1.35 s, the client's ruling) baked from the probe, and the dust of the fall.
/// </summary>
public class ColumnBreakLookTests
{
    private const float Tolerance = 0.0001f;

    private static readonly int HitsToBreak = Balance.Default.Columns.HitsToBreak;

    [Theory]
    [InlineData(ColumnStage.Intact, "column_intact.glb")]
    [InlineData(ColumnStage.Hit1, "column_hit1.glb")]
    [InlineData(ColumnStage.Hit2, "column_hit2.glb")]
    [InlineData(ColumnStage.Falling, "column_falling.glb")]
    [InlineData(ColumnStage.Broken, "column_broken.glb")]
    public void Every_stage_has_its_own_file(ColumnStage stage, string file)
    {
        Assert.Equal(file, ColumnBreakLook.FileName(stage));
    }

    [Fact]
    public void Only_the_five_stages_have_files()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ColumnBreakLook.FileName((ColumnStage)9));
    }

    [Fact]
    public void An_unstruck_column_is_intact_the_blow_before_the_break_gives_Z2_and_every_earlier_one_Z1()
    {
        Assert.True(HitsToBreak >= 2, "With fewer than two blows there would be no stage between intact and broken.");
        Assert.Equal(ColumnStage.Intact, ColumnBreakLook.StageOf(0, HitsToBreak));
        for (var hits = 1; hits < HitsToBreak - 1; hits++)
        {
            Assert.Equal(ColumnStage.Hit1, ColumnBreakLook.StageOf(hits, HitsToBreak));
        }

        Assert.Equal(ColumnStage.Hit2, ColumnBreakLook.StageOf(HitsToBreak - 1, HitsToBreak));
    }

    [Theory]
    [InlineData(1, 3, ColumnStage.Hit1)]
    [InlineData(2, 3, ColumnStage.Hit2)]
    [InlineData(1, 2, ColumnStage.Hit2)]
    [InlineData(1, 5, ColumnStage.Hit1)]
    [InlineData(3, 5, ColumnStage.Hit1)]
    [InlineData(4, 5, ColumnStage.Hit2)]
    [InlineData(-1, 3, ColumnStage.Intact)]
    public void The_stage_follows_the_blows_left_whatever_the_balance(int hits, int hitsToBreak, ColumnStage expected)
    {
        Assert.Equal(expected, ColumnBreakLook.StageOf(hits, hitsToBreak));
    }

    [Fact]
    public void The_fall_lasts_81_ticks_then_the_broken_model_lies()
    {
        Assert.Equal(81, ColumnBreakLook.SwapTick);
        Assert.Equal(1.35f, ColumnBreakLook.SwapTick / (float)SimulationClock.TicksPerSecond, 3);
        Assert.Equal(ColumnStage.Falling, ColumnBreakLook.StageAfterBreak(0));
        Assert.Equal(ColumnStage.Falling, ColumnBreakLook.StageAfterBreak(ColumnBreakLook.SwapTick - 1));
        Assert.Equal(ColumnStage.Broken, ColumnBreakLook.StageAfterBreak(ColumnBreakLook.SwapTick));
        Assert.Equal(ColumnStage.Broken, ColumnBreakLook.StageAfterBreak(10_000));
    }

    [Fact]
    public void The_standing_and_lying_files_are_one_fixed_part_named_as_the_file()
    {
        foreach (var stage in new[] { ColumnStage.Intact, ColumnStage.Hit1, ColumnStage.Hit2, ColumnStage.Broken })
        {
            var parts = Read(stage);
            var name = Path.GetFileNameWithoutExtension(ColumnBreakLook.FileName(stage));
            Assert.Equal([new RigPart(name, null)], ColumnBreakLook.Parts(stage));
            Assert.Equal([name], parts.Keys);
            Assert.Equal(Vector3.Zero, parts[name].Pivot);
        }
    }

    [Fact]
    public void The_falling_file_has_the_base_and_the_pieces_of_the_table_turning_about_its_pivots()
    {
        var parts = Read(ColumnStage.Falling);
        Assert.Equal(ColumnBreakLook.Parts(ColumnStage.Falling).Select(part => part.Name).Order(), parts.Keys.Order());
        Assert.Equal(ColumnBreakLook.FallBase, ColumnBreakLook.Parts(ColumnStage.Falling)[0].Name);
        Assert.Equal(Vector3.Zero, parts[ColumnBreakLook.FallBase].Pivot);

        for (var i = 0; i < ColumnBreakLook.FallPieces.Count; i++)
        {
            var expected = ColumnBreakLook.PiecePivot(i);
            var pivot = parts[ColumnBreakLook.FallPieces[i]].Pivot;
            Assert.True(Vector3.Distance(expected, pivot) < 0.001f, $"{ColumnBreakLook.FallPieces[i]}: pivot {pivot}, table {expected}.");
        }
    }

    [Fact]
    public void The_models_stand_a_room_high_and_the_stump_stays_below_a_metre()
    {
        var intact = Read(ColumnStage.Intact)["column_intact"];
        Assert.Equal(ColumnBreakLook.ModelHeight, intact.PositionMax.Y, 1);
        Assert.Equal(0f, intact.PositionMin.Y, 2);

        var broken = Read(ColumnStage.Broken)["column_broken"];
        Assert.InRange(broken.PositionMax.Y, 0.5f, 1f);
    }

    [Fact]
    public void Before_the_swap_every_piece_lies_low_and_at_the_start_every_piece_stands_where_the_file_has_it()
    {
        Span<Matrix4x4> turns = stackalloc Matrix4x4[ColumnBreakLook.Parts(ColumnStage.Falling).Count];
        ColumnBreakLook.FallTurns(0, turns);
        Assert.Equal(Matrix4x4.Identity, turns[0]);
        for (var i = 0; i < ColumnBreakLook.FallPieces.Count; i++)
        {
            // At tick 0 the pieces have not moved: their turn is (nearly) none.
            Assert.True(IsNear(Matrix4x4.Identity, turns[i + 1], 0.02f), $"{ColumnBreakLook.FallPieces[i]} has moved at tick 0.");
            Assert.True(ColumnBreakLook.PieceMiddle(i, ColumnBreakLook.SwapTick - 1).Y < 1f, $"{ColumnBreakLook.FallPieces[i]} still stands at the swap.");
        }

        // The upper part falls towards the side of the blows: its middle ends up on that side of the stump.
        var capital = ColumnBreakLook.FallPieces.ToList().IndexOf("capital");
        Assert.True(Vector3.Dot(ColumnBreakLook.PieceMiddle(capital, ColumnBreakLook.SwapTick - 1), ColumnBreakLook.HitSide) > 1f);
    }

    [Fact]
    public void After_the_swap_the_fall_keeps_its_last_pose_and_before_the_break_its_first()
    {
        var count = ColumnBreakLook.Parts(ColumnStage.Falling).Count;
        var last = new Matrix4x4[count];
        var later = new Matrix4x4[count];
        var first = new Matrix4x4[count];
        var before = new Matrix4x4[count];
        ColumnBreakLook.FallTurns(ColumnBreakLook.SwapTick - 1, last);
        ColumnBreakLook.FallTurns(ColumnBreakLook.SwapTick + 50, later);
        ColumnBreakLook.FallTurns(0, first);
        ColumnBreakLook.FallTurns(-5, before);
        Assert.Equal(last, later);
        Assert.Equal(first, before);
        Assert.Throws<ArgumentException>(() => ColumnBreakLook.FallTurns(0, new Matrix4x4[count - 1]));
    }

    [Theory]
    [InlineData(5f, 0f)]
    [InlineData(0f, 5f)]
    [InlineData(-3f, -4f)]
    [InlineData(2f, -7f)]
    public void The_side_of_the_blows_is_turned_towards_the_striker(float dx, float dz)
    {
        var column = new Vector2(10f, 20f);
        var striker = new Vector3(column.X + dx, 1.5f, column.Y + dz);

        var yaw = ColumnBreakLook.YawToward(column, striker);

        var side = Vector3.TransformNormal(ColumnBreakLook.HitSide, Matrix4x4.CreateRotationY(yaw));
        var towards = Vector3.Normalize(new Vector3(dx, 0f, dz));
        Assert.True(Vector3.Distance(towards, side) < Tolerance, $"Side {side}, striker towards {towards}.");
    }

    [Fact]
    public void A_striker_straight_above_turns_nothing()
    {
        Assert.Equal(0f, ColumnBreakLook.YawToward(new Vector2(3f, 4f), new Vector3(3f, 5f, 4f)));
    }

    [Fact]
    public void The_figure_puts_the_foot_on_the_floor_and_stretches_only_the_height()
    {
        var foot = new Vector3(4f, 0.5f, 7f);
        var figure = ColumnBreakLook.Figure(foot, 3.5f, 1f);

        Assert.True(Vector3.Distance(foot, Vector3.Transform(Vector3.Zero, figure)) < Tolerance);
        Assert.Equal(foot.Y + 3.5f, Vector3.Transform(new Vector3(0f, ColumnBreakLook.ModelHeight, 0f), figure).Y, 4);
        Assert.Equal(1f, Vector3.TransformNormal(Vector3.UnitX, figure).Length(), 4);

        // Under a room's ceiling the model keeps its height.
        var room = ColumnBreakLook.Figure(Vector3.Zero, RoomTemplate.RoomCeilingHeight, 0f);
        Assert.Equal(ColumnBreakLook.ModelHeight, Vector3.Transform(new Vector3(0f, ColumnBreakLook.ModelHeight, 0f), room).Y, 4);
    }

    // ---- Dust --------------------------------------------------------------------------------------------------------

    [Fact]
    public void The_dust_starts_with_the_break_and_is_gone_after_its_ticks()
    {
        Assert.Equal(ColumnBreakLook.SwapTick + ColumnDust.LifeTicks, ColumnDust.Ticks);
        // The first cloud rises with the grinding of the first ticks (the probe: tick 2), the last one with the swap.
        Assert.Empty(Puffs(0));
        Assert.Contains(Enumerable.Range(1, 4), tick => Puffs(tick).Count > 0);
        Assert.NotEmpty(Puffs(ColumnBreakLook.SwapTick));
        Assert.NotEmpty(Puffs(ColumnDust.Ticks - 1));
        Assert.Empty(Puffs(ColumnDust.Ticks));
        Assert.Empty(Puffs(-1));
    }

    [Fact]
    public void Every_tick_of_the_dust_brings_the_same_puffs_again()
    {
        for (var tick = 0; tick < ColumnDust.Ticks; tick += 7)
        {
            Assert.Equal(Puffs(tick), Puffs(tick));
        }
    }

    [Fact]
    public void A_puff_swells_then_thins_out_and_stays_soft_and_near_the_column()
    {
        for (var tick = 0; tick < ColumnDust.Ticks; tick++)
        {
            foreach (var puff in Puffs(tick))
            {
                Assert.InRange(puff.Opacity, 0f, 1f);
                Assert.InRange(puff.Radius, 0.01f, 3f);
                Assert.InRange(new Vector2(puff.Position.X, puff.Position.Z).Length(), 0f, 8f);
                Assert.InRange(puff.Position.Y, -0.5f, ColumnBreakLook.ModelHeight + 1f);
            }
        }

        // The first cloud: densest after its swell, faint at the end of its life.
        var dense = Puffs(ColumnDust.SwellTicks + 2).Max(puff => puff.Opacity);
        var faint = Puffs(ColumnDust.LifeTicks - 1).Where(puff => puff.Opacity > 0f).DefaultIfEmpty().Min(puff => puff.Opacity);
        Assert.True(dense > 0.5f);
        Assert.True(faint < 0.05f);
    }

    [Fact]
    public void The_dust_moves_with_the_column()
    {
        var at = Matrix4x4.CreateTranslation(10f, 0f, 20f);
        var here = Puffs(20);
        var there = new List<DustPuff>();
        ColumnDust.Puffs(20, at, there);

        Assert.Equal(here.Count, there.Count);
        for (var i = 0; i < here.Count; i++)
        {
            Assert.True(Vector3.Distance(here[i].Position + new Vector3(10f, 0f, 20f), there[i].Position) < 0.001f);
        }
    }

    private static List<DustPuff> Puffs(int ticksSinceBreak)
    {
        var puffs = new List<DustPuff>();
        ColumnDust.Puffs(ticksSinceBreak, Matrix4x4.Identity, puffs);
        return puffs;
    }

    private static Dictionary<string, GlbPart> Read(ColumnStage stage)
    {
        var path = RepositoryFiles.Find($"assets/models/{ColumnBreakLook.FileName(stage)}");
        Assert.True(File.Exists(path), $"{path} is missing.");
        return GlbScene.Read(File.ReadAllBytes(path)).ToDictionary(part => part.Name);
    }

    private static bool IsNear(Matrix4x4 a, Matrix4x4 b, float tolerance) =>
        MathF.Abs(a.M11 - b.M11) < tolerance && MathF.Abs(a.M12 - b.M12) < tolerance && MathF.Abs(a.M13 - b.M13) < tolerance
        && MathF.Abs(a.M21 - b.M21) < tolerance && MathF.Abs(a.M22 - b.M22) < tolerance && MathF.Abs(a.M23 - b.M23) < tolerance
        && MathF.Abs(a.M31 - b.M31) < tolerance && MathF.Abs(a.M32 - b.M32) < tolerance && MathF.Abs(a.M33 - b.M33) < tolerance
        && MathF.Abs(a.M41 - b.M41) < tolerance && MathF.Abs(a.M42 - b.M42) < tolerance && MathF.Abs(a.M43 - b.M43) < tolerance;
}
