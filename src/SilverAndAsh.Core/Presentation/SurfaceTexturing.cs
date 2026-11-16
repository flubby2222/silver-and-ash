using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// Texture coordinates of the level from the world position (issue #233): one tile of a texture covers
/// <c>tileSize</c> x <c>tileSize</c> metres, wherever it lies. Neighbouring faces in the same plane therefore continue
/// the texture without a seam - across cell borders, steps and cells of different heights - because their shared
/// corners get the same coordinates.
/// <para>
/// Floors and ceilings: u along x, v along z. Walls: u along the wall, v with the height above y = 0 (not with the
/// lower edge of the face), so every course of stones lies at the same height all around. Walls are never turned by
/// 90 degrees - the damp streaks of the wall texture run down.
/// </para>
/// <para>
/// Directions: u grows to the right of someone looking at the face, so no wall is mirrored. v of a wall falls with
/// the height, because raylib puts the first row of a PNG at v = 0: the picture stands upright, its bottom row just
/// above y = 0 (the designer's "v = 0 is the lower edge of a course" in Blender, whose v counts from the bottom).
/// </para>
/// </summary>
public static class SurfaceTexturing
{
    /// <summary>
    /// The texture coordinate of the corner at <paramref name="position"/> of a face of kind <paramref name="kind"/>
    /// facing <paramref name="normal"/>. One unit of the result is one tile; the texture repeats.
    /// </summary>
    public static Vector2 TexCoord(Vector3 position, Vector3 normal, SurfaceKind kind, float tileSize)
    {
        if (!(tileSize > 0f) || float.IsInfinity(tileSize))
        {
            throw new ArgumentOutOfRangeException(nameof(tileSize), tileSize, "A tile must be larger than 0 metres.");
        }

        var p = position / tileSize;
        if (kind != SurfaceKind.Wall)
        {
            return new Vector2(p.X, p.Z);
        }

        // Looking at a wall that faces +x one looks toward -x, and the right hand points to -z (and so on).
        var u = MathF.Abs(normal.X) > MathF.Abs(normal.Z)
            ? (normal.X > 0f ? -p.Z : p.Z)
            : (normal.Z > 0f ? p.X : -p.X);
        return new Vector2(u, -p.Y);
    }
}
