using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.Showroom;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The free-standing columns of a level (issue #539, <see cref="Level.Columns"/>) with their stages of breaking
/// (issue #601, <see cref="ColumnBreakLook"/>): intact, after one blow, after the next, falling apart and lying broken,
/// with a soft cloud of dust while they fall (<see cref="ColumnDust"/>). Which stage and since when tells
/// <see cref="ColumnLooks"/> from Core's events; nothing here decides anything. A stage without its model falls back to
/// the old stand-in: a plain twelve-sided stone shaft from the floor to the ceiling (standing), or a low stump (broken).
/// </summary>
internal sealed class ColumnView : IDisposable
{
    // Twelve sides, as the accepted probe.
    private const int Sides = 12;

    // The stump of the stand-in, as high as the broken model's (0.82 m).
    private const float StumpHeight = 0.82f;

    // Fallen pieces lie up to about 3.5 m from the foot; a little more, so nothing pops at the edge of the view.
    private const float BoundingRadius = 4.5f;

    // Steps of the ramp Stein (EffectColours, issue #609).
    private static readonly Color Stone = PaletteColour.Opaque(EffectColours.ColumnStandIn);
    private static readonly Color Edge = PaletteColour.Opaque(EffectColours.ColumnStandInEdge);

    // The dust of the stone, from the palette (ColumnDust.Colour).
    private static readonly Color Dust = PaletteColour.Opaque(ColumnDust.Colour);

    private readonly Dictionary<ColumnStage, TexturedModel> _models;
    private readonly Matrix4x4[] _fallTurns = new Matrix4x4[ColumnBreakLook.Parts(ColumnStage.Falling).Count];
    private readonly Matrix4x4[] _fallFrames = new Matrix4x4[ColumnBreakLook.Parts(ColumnStage.Falling).Count];
    private readonly Matrix4x4[] _turn = [Matrix4x4.Identity];
    private readonly Matrix4x4[] _frame = new Matrix4x4[1];
    private readonly List<DustPuff> _puffs = [];

    private ColumnView(Dictionary<ColumnStage, TexturedModel> models)
    {
        _models = models;
    }

    /// <summary>
    /// Loads the model of every stage from <paramref name="directory"/> (<see cref="ColumnBreakLook.FileName"/>). A
    /// missing or broken one: a warning in the log and the stand-in for that stage. Needs the window.
    /// </summary>
    public static ColumnView Load(string directory, FileLog? log)
    {
        var models = new Dictionary<ColumnStage, TexturedModel>();
        foreach (var stage in Enum.GetValues<ColumnStage>())
        {
            var path = Path.Combine(directory, ColumnBreakLook.FileName(stage));
            if (TexturedModel.TryLoad(path, ColumnBreakLook.Parts(stage), log) is { } model)
            {
                models[stage] = model;
            }
        }

        return new ColumnView(models);
    }

    /// <summary>
    /// Draws the columns of <paramref name="level"/> - the standing ones and those of <paramref name="looks"/> that broke -
    /// as they look at <paramref name="ticks"/> ticks of the world, and the showroom's <paramref name="exhibits"/>. Gathers
    /// their dust for <see cref="DrawDust"/>. Call between BeginFigures and EndFigures.
    /// </summary>
    public void Draw(Level level, ColumnLooks looks, IReadOnlyList<ExhibitColumn> exhibits, long ticks, ViewCone view)
    {
        _puffs.Clear();
        foreach (var column in level.Columns.Concat(looks.Broken).Distinct())
        {
            DrawOne(level, new Vector3(column.X, 0f, column.Z), looks.Of(column, ticks), view);
        }

        foreach (var exhibit in exhibits)
        {
            var look = exhibit.Stage == ColumnStage.Falling
                ? ShowroomColumnCycle.Look(ticks, exhibit.Yaw)
                : new ColumnLook(exhibit.Stage, exhibit.Yaw);
            DrawOne(level, exhibit.Position, look, view);
        }
    }

    /// <summary>
    /// Draws the dust gathered by the last <see cref="Draw"/>: soft, see-through balls that do not hide one another, lit,
    /// fogged and in the picture variant by the level <paramref name="shader"/> like the stone around them (issue #609).
    /// Call between BeginFigures and EndFigures, after everything solid.
    /// </summary>
    public void DrawDust(LevelShader shader) => DustCloudView.Draw(shader, _puffs, Dust);

    public void Dispose()
    {
        foreach (var model in _models.Values)
        {
            model.Dispose();
        }
    }

    private void DrawOne(Level level, Vector3 at, ColumnLook look, ViewCone view)
    {
        var cell = level.CellAt(at);
        var height = cell.CeilingHeight - cell.FloorHeight;
        var foot = new Vector3(at.X, cell.FloorHeight, at.Z);
        var figure = ColumnBreakLook.Figure(foot, height, look.Yaw);
        if (look.TicksSinceBreak is { } since && since < ColumnDust.Ticks)
        {
            ColumnDust.Puffs(since, figure, _puffs);
        }

        if (!view.MaySee(foot + new Vector3(0f, height / 2f, 0f), MathF.Max(BoundingRadius, height / 2f)))
        {
            return;
        }

        if (!_models.TryGetValue(look.Stage, out var model))
        {
            DrawStandIn(foot, look.Stage is ColumnStage.Falling or ColumnStage.Broken ? MathF.Min(StumpHeight, height) : height);
            return;
        }

        if (look.Stage == ColumnStage.Falling)
        {
            ColumnBreakLook.FallTurns(look.TicksSinceBreak ?? 0, _fallTurns);
            model.Rig.Pose(_fallTurns, figure, _fallFrames);
            model.Draw(_fallFrames);
            return;
        }

        model.Rig.Pose(_turn, figure, _frame);
        model.Draw(_frame);
    }

    private static void DrawStandIn(Vector3 foot, float height)
    {
        Raylib.DrawCylinder(foot, Column.Radius, Column.Radius, height, Sides, Stone);
        Raylib.DrawCylinderWires(foot, Column.Radius, Column.Radius, height, Sides, Edge);
    }
}
