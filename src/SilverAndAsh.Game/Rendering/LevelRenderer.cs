using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Draws a <see cref="Level"/> with plain colours: wall blocks, floor and ceiling.
/// Temporary look - the PS2 style (low resolution, shading, fog) follows in milestone v0.3.
/// </summary>
internal static class LevelRenderer
{
    private static readonly Color WallColor = new(78, 66, 72, 255);
    private static readonly Color WallEdgeColor = new(120, 104, 112, 255);
    private static readonly Color FloorColor = new(46, 40, 44, 255);
    private static readonly Color CeilingColor = new(32, 28, 34, 255);

    public static void Draw(Level level)
    {
        for (var z = 0; z < level.Depth; z++)
        {
            for (var x = 0; x < level.Width; x++)
            {
                var cell = level[x, z];
                if (cell.IsSolid)
                {
                    // Walls that touch no walkable cell can never be seen - skip them.
                    if (TouchesWalkableCell(level, x, z))
                    {
                        DrawWall(x, z);
                    }
                }
                else
                {
                    DrawFloorAndCeiling(x, z, cell);
                }
            }
        }
    }

    // Diagonal neighbours count too: otherwise walls between two door frames would leave holes.
    private static bool TouchesWalkableCell(Level level, int x, int z)
    {
        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (!level[x + dx, z + dz].IsSolid)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void DrawWall(int x, int z)
    {
        const float Height = Cell.DefaultCeilingHeight - Cell.DefaultFloorHeight;
        var centre = new Vector3(
            (x + 0.5f) * Level.CellSize,
            Cell.DefaultFloorHeight + Height / 2f,
            (z + 0.5f) * Level.CellSize);

        Raylib.DrawCube(centre, Level.CellSize, Height, Level.CellSize, WallColor);
        Raylib.DrawCubeWires(centre, Level.CellSize, Height, Level.CellSize, WallEdgeColor);
    }

    private static void DrawFloorAndCeiling(int x, int z, Cell cell)
    {
        var x0 = x * Level.CellSize;
        var x1 = x0 + Level.CellSize;
        var z0 = z * Level.CellSize;
        var z1 = z0 + Level.CellSize;

        // Triangles are only visible from the side where their corners run counter-clockwise.
        // Floor: visible from above. Ceiling: same corners in reverse order, visible from below -
        // so the ceiling does not block the view when looking at the level from above.
        DrawQuad(
            new Vector3(x0, cell.FloorHeight, z0), new Vector3(x0, cell.FloorHeight, z1),
            new Vector3(x1, cell.FloorHeight, z1), new Vector3(x1, cell.FloorHeight, z0),
            FloorColor);

        DrawQuad(
            new Vector3(x1, cell.CeilingHeight, z0), new Vector3(x1, cell.CeilingHeight, z1),
            new Vector3(x0, cell.CeilingHeight, z1), new Vector3(x0, cell.CeilingHeight, z0),
            CeilingColor);
    }

    private static void DrawQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        Raylib.DrawTriangle3D(a, b, c, color);
        Raylib.DrawTriangle3D(a, c, d, color);
    }
}
