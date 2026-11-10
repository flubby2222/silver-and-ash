using System.Numerics;

namespace SilverAndAsh.Core.World;

/// <summary>What kind of surface a face is - the renderer chooses its look from this.</summary>
public enum SurfaceKind
{
    /// <summary>Walkable ground, facing up.</summary>
    Floor,

    /// <summary>Top of a room, facing down.</summary>
    Ceiling,

    /// <summary>Vertical wall, facing into the room.</summary>
    Wall,
}

/// <summary>
/// One flat four-cornered face of the level. Corners A, B, C, D run counter-clockwise when looked at
/// from the side the <see cref="Normal"/> points to - that is the visible side (see "backface culling").
/// </summary>
public readonly record struct Face(Vector3 A, Vector3 B, Vector3 C, Vector3 D, Vector3 Normal, SurfaceKind Kind);

/// <summary>
/// Turns a <see cref="Level"/> into the faces that can actually be seen: floor and ceiling of every walkable
/// cell, a wall face wherever a walkable cell borders a solid one, and the vertical pieces between walkable
/// cells of different heights (step edges, pit walls). Faces between two solid cells are never visible and are
/// left out. Built once when a level is loaded, not every frame.
/// </summary>
public static class LevelGeometry
{
    private static readonly (int Dx, int Dz, Vector3 Normal)[] Directions =
    [
        // Neighbour offset, and the normal of the wall face: it points from the wall back INTO the walkable cell.
        (-1, 0, Vector3.UnitX),
        (1, 0, -Vector3.UnitX),
        (0, -1, Vector3.UnitZ),
        (0, 1, -Vector3.UnitZ),
    ];

    /// <summary>All visible faces of <paramref name="level"/>.</summary>
    public static IReadOnlyList<Face> Build(Level level)
    {
        ArgumentNullException.ThrowIfNull(level);
        var faces = new List<Face>();

        for (var z = 0; z < level.Depth; z++)
        {
            for (var x = 0; x < level.Width; x++)
            {
                var cell = level[x, z];
                if (cell.IsSolid)
                {
                    continue;
                }

                AddFloorAndCeiling(faces, x, z, cell);

                foreach (var (dx, dz, normal) in Directions)
                {
                    var neighbour = level[x + dx, z + dz];
                    if (neighbour.IsSolid)
                    {
                        faces.Add(WallFace(x, z, dx, dz, normal, cell.FloorHeight, cell.CeilingHeight));
                        continue;
                    }

                    // Two walkable cells with different heights: the vertical piece in between is seen from the
                    // LOWER floor (a step edge or a pit wall) ...
                    if (neighbour.FloorHeight > cell.FloorHeight)
                    {
                        faces.Add(WallFace(x, z, dx, dz, normal, cell.FloorHeight, neighbour.FloorHeight));
                    }

                    // ... and from the HIGHER ceiling (the edge where a ceiling drops down).
                    if (neighbour.CeilingHeight < cell.CeilingHeight)
                    {
                        faces.Add(WallFace(x, z, dx, dz, normal, neighbour.CeilingHeight, cell.CeilingHeight));
                    }
                }
            }
        }

        return faces;
    }

    private static void AddFloorAndCeiling(List<Face> faces, int x, int z, Cell cell)
    {
        var x0 = x * Level.CellSize;
        var x1 = x0 + Level.CellSize;
        var z0 = z * Level.CellSize;
        var z1 = z0 + Level.CellSize;
        var floor = cell.FloorHeight;
        var ceiling = cell.CeilingHeight;

        // Same four corners, opposite order: the floor is seen from above, the ceiling from below.
        faces.Add(new Face(
            new Vector3(x0, floor, z0), new Vector3(x0, floor, z1), new Vector3(x1, floor, z1), new Vector3(x1, floor, z0),
            Vector3.UnitY, SurfaceKind.Floor));
        faces.Add(new Face(
            new Vector3(x1, ceiling, z0), new Vector3(x1, ceiling, z1), new Vector3(x0, ceiling, z1), new Vector3(x0, ceiling, z0),
            -Vector3.UnitY, SurfaceKind.Ceiling));
    }

    /// <summary>The wall face on the border between walkable cell (x, z) and its solid neighbour (x+dx, z+dz).</summary>
    private static Face WallFace(int x, int z, int dx, int dz, Vector3 normal, float bottom, float top)
    {
        // The two ends of the shared border line, on the ground plane.
        Vector2 start, end;
        var size = Level.CellSize;
        var x0 = x * size;
        var z0 = z * size;

        if (dx != 0)
        {
            var borderX = dx < 0 ? x0 : x0 + size;
            start = new Vector2(borderX, z0);
            end = new Vector2(borderX, z0 + size);
        }
        else
        {
            var borderZ = dz < 0 ? z0 : z0 + size;
            start = new Vector2(x0, borderZ);
            end = new Vector2(x0 + size, borderZ);
        }

        // Order the corners counter-clockwise as seen from the room (the side the normal points to).
        var a = new Vector3(start.X, bottom, start.Y);
        var b = new Vector3(end.X, bottom, end.Y);
        var c = new Vector3(end.X, top, end.Y);
        var d = new Vector3(start.X, top, start.Y);

        var faceNormal = Vector3.Cross(b - a, c - a);
        return Vector3.Dot(faceNormal, normal) > 0f
            ? new Face(a, b, c, d, normal, SurfaceKind.Wall)
            : new Face(b, a, d, c, normal, SurfaceKind.Wall);
    }
}
