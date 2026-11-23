using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The free-standing columns of a level (issue #539, <see cref="Level.Columns"/>), for now as a stand-in: a plain
/// twelve-sided stone shaft from the floor to the ceiling of its cell, its corners on the circle that blocks walking
/// and shots, with darker edges. The model follows in a later step.
/// </summary>
internal static class ColumnView
{
    // Twelve sides, as the accepted probe.
    private const int Sides = 12;

    private static readonly Color Stone = new(112, 106, 98, 255);
    private static readonly Color Edge = new(58, 54, 50, 255);

    /// <summary>Draws every column of <paramref name="level"/> that <paramref name="view"/> may see. Call in 3D mode.</summary>
    public static void Draw(Level level, ViewCone view)
    {
        foreach (var column in level.Columns)
        {
            var cell = level.CellAt(new Vector3(column.X, 0f, column.Z));
            var height = cell.CeilingHeight - cell.FloorHeight;
            var foot = new Vector3(column.X, cell.FloorHeight, column.Z);
            if (!view.MaySee(foot + new Vector3(0f, height / 2f, 0f), MathF.Max(Column.Radius, height / 2f)))
            {
                continue;
            }

            Raylib.DrawCylinder(foot, Column.Radius, Column.Radius, height, Sides, Stone);
            Raylib.DrawCylinderWires(foot, Column.Radius, Column.Radius, height, Sides, Edge);
        }
    }
}
