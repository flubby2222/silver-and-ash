using System.Numerics;
using Raylib_cs;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Draws a box (cuboid) with flat shading - the building block of all placeholder figures.
/// The corners are transformed into world positions here in C#, so each face can get the brightness of the
/// direction it faces AFTER rotating (a dummy that falls over shows its top as a wall, not as a floor).
/// </summary>
internal static class ShadedBox
{
    // The six faces: outward normal, plus two edge directions u, v with u x v = normal.
    // That order makes the corners run counter-clockwise seen from outside, which is the "front" side
    // the graphics card draws (the back side is skipped: backface culling).
    private static readonly (Vector3 Normal, Vector3 U, Vector3 V)[] Faces =
    [
        (Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ),
        (-Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY),
        (Vector3.UnitY, Vector3.UnitZ, Vector3.UnitX),
        (-Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ),
        (Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY),
        (-Vector3.UnitZ, Vector3.UnitY, Vector3.UnitX),
    ];

    /// <summary>
    /// Draws a box of <paramref name="size"/> centred at the origin of <paramref name="transform"/>
    /// (local box space -> world). Call between BeginMode3D and EndMode3D.
    /// </summary>
    public static void Draw(Matrix4x4 transform, Vector3 size, Color color)
    {
        var half = size / 2f;
        Rlgl.Begin(DrawMode.Quads);
        foreach (var (normal, u, v) in Faces)
        {
            var worldNormal = Vector3.Normalize(Vector3.TransformNormal(normal, transform));
            var shaded = FlatShading.Apply(color, FlatShading.Brightness(worldNormal));
            Rlgl.Color4ub(shaded.R, shaded.G, shaded.B, shaded.A);

            var centre = normal * half;
            var edgeU = u * half;
            var edgeV = v * half;
            Corner(centre - edgeU - edgeV, transform);
            Corner(centre + edgeU - edgeV, transform);
            Corner(centre + edgeU + edgeV, transform);
            Corner(centre - edgeU + edgeV, transform);
        }

        Rlgl.End();
    }

    private static void Corner(Vector3 local, Matrix4x4 transform)
    {
        var world = Vector3.Transform(local, transform);
        Rlgl.Vertex3f(world.X, world.Y, world.Z);
    }
}
