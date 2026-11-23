using System.Numerics;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.Showroom;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Showroom;

/// <summary>
/// Issue #601: the breaking column in the showroom - its four stages side by side and one breaking in a loop. Only
/// pictures: the level's columns stay what they were.
/// </summary>
public class ShowroomBreakingColumnTests
{
    private static readonly ShowroomLayout Layout = ShowroomLayout.Build();

    [Fact]
    public void Every_stage_stands_once_and_one_column_breaks_in_a_loop()
    {
        var stages = Layout.Exhibits.Columns.Select(column => column.Stage).Order().ToList();

        Assert.Equal([ColumnStage.Intact, ColumnStage.Hit1, ColumnStage.Hit2, ColumnStage.Falling, ColumnStage.Broken], stages);
    }

    [Fact]
    public void Every_exhibit_stands_on_open_floor_under_a_room_s_ceiling_apart_from_the_level_s_columns()
    {
        var cells = new HashSet<(int X, int Z)>(Layout.Level.Columns.Select(column => Level.ToGrid(column.X, column.Z)));
        foreach (var column in Layout.Exhibits.Columns)
        {
            var cell = Layout.Level.CellAt(column.Position);
            Assert.False(cell.IsSolid, $"{column.Stage} stands in a wall.");
            Assert.Equal(0f, cell.FloorHeight);
            Assert.Equal(ColumnBreakLook.ModelHeight, cell.CeilingHeight - cell.FloorHeight);
            Assert.True(cells.Add(Level.ToGrid(column.Position.X, column.Position.Z)), $"{column.Stage} shares its cell.");
        }
    }

    [Fact]
    public void The_four_stages_show_the_side_of_the_blows_to_the_passage_and_the_looping_one_falls_into_free_cells()
    {
        foreach (var column in Layout.Exhibits.Columns.Where(column => column.Stage != ColumnStage.Falling))
        {
            var side = Vector3.TransformNormal(ColumnBreakLook.HitSide, Matrix4x4.CreateRotationY(column.Yaw));
            Assert.True(Vector3.Dot(side, -Vector3.UnitZ) > 0.99f, $"{column.Stage} shows its blows towards {side}.");
        }

        var looping = Assert.Single(Layout.Exhibits.Columns, column => column.Stage == ColumnStage.Falling);
        var figure = ColumnBreakLook.Figure(looping.Position, ColumnBreakLook.ModelHeight, looping.Yaw);
        for (var piece = 0; piece < ColumnBreakLook.FallPieces.Count; piece++)
        {
            var lies = Vector3.Transform(ColumnBreakLook.PieceMiddle(piece, ColumnBreakLook.SwapTick - 1), figure);
            Assert.False(Layout.Level.CellAt(lies).IsSolid, $"{ColumnBreakLook.FallPieces[piece]} lies in a wall at {lies}.");
            Assert.DoesNotContain(Layout.Exhibits.Columns, other => other != looping
                && Vector2.Distance(new(other.Position.X, other.Position.Z), new(lies.X, lies.Z)) < Column.Radius);
        }
    }

    [Fact]
    public void Every_stage_has_its_label()
    {
        foreach (var column in Layout.Exhibits.Columns)
        {
            Assert.Contains(Layout.Labels, label => label.Text == ShowroomLayout.BreakingColumnLabel(column.Stage));
        }

        Assert.Equal(5, Layout.Exhibits.Columns.Select(column => ShowroomLayout.BreakingColumnLabel(column.Stage)).Distinct().Count());
    }

    [Fact]
    public void The_exhibits_are_only_pictures_the_level_keeps_its_two_columns()
    {
        Assert.Equal(2, Layout.Level.Columns.Count);
        foreach (var column in Layout.Exhibits.Columns)
        {
            Assert.Null(Layout.Level.ColumnAt(column.Position + new Vector3(0f, 1f, 0f)));
        }
    }

    [Fact]
    public void The_loop_stands_at_Z2_breaks_falls_lies_and_starts_again()
    {
        const float yaw = 0.7f;
        var stand = ShowroomColumnCycle.StandTicks;

        Assert.Equal(new ColumnLook(ColumnStage.Hit2, yaw), ShowroomColumnCycle.Look(0, yaw));
        Assert.Equal(new ColumnLook(ColumnStage.Hit2, yaw), ShowroomColumnCycle.Look(stand - 1, yaw));
        Assert.Equal(new ColumnLook(ColumnStage.Falling, yaw, 0), ShowroomColumnCycle.Look(stand, yaw));
        Assert.Equal(new ColumnLook(ColumnStage.Falling, yaw, ColumnBreakLook.SwapTick - 1), ShowroomColumnCycle.Look(stand + ColumnBreakLook.SwapTick - 1, yaw));
        Assert.Equal(new ColumnLook(ColumnStage.Broken, yaw, ColumnBreakLook.SwapTick), ShowroomColumnCycle.Look(stand + ColumnBreakLook.SwapTick, yaw));
        Assert.Equal(ColumnStage.Broken, ShowroomColumnCycle.Look(ShowroomColumnCycle.Length - 1, yaw).Stage);
        Assert.True(ShowroomColumnCycle.Look(ShowroomColumnCycle.Length - 1, yaw).TicksSinceBreak >= ColumnDust.Ticks, "The loop starts again before the dust has settled.");

        for (var tick = 0L; tick < ShowroomColumnCycle.Length; tick += 5)
        {
            Assert.Equal(ShowroomColumnCycle.Look(tick, yaw), ShowroomColumnCycle.Look(tick + ShowroomColumnCycle.Length, yaw));
        }
    }

    [Fact]
    public void There_are_photos_of_every_stage_at_6_and_10_m_and_a_clip_of_the_break()
    {
        var names = PhotoScenes.All.Select(scene => scene.Name).ToList();
        foreach (var stage in new[] { "intact", "hit1", "hit2", "broken" })
        {
            Assert.Contains($"column-{stage}-6m", names);
            Assert.Contains($"column-{stage}-10m", names);
        }

        Assert.NotNull(MotionScenes.Find("column-breaking"));
    }
}
