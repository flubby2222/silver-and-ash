using System.Numerics;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The brightness of a surface for each of the six directions it can face - the values of the flat shading
/// (LookSettings in the game). Walls facing +x are brighter than walls facing -x, so corners show without lines.
/// </summary>
public readonly record struct FacingBrightness(float PlusX, float MinusX, float Up, float Down, float PlusZ, float MinusZ);

/// <summary>
/// Soft shading of models (Gouraud, decision record 0010): the brightness is worked out at every corner from the
/// direction its normal faces in the world - after the model was turned - and the graphics card lets it run across
/// the triangle. The six values of the flat shading are blended by the squares of the normal's parts, so a normal
/// that points straight along an axis gets exactly the flat value, and one in between a smooth mix.
/// </summary>
public static class SmoothShading
{
    /// <summary>
    /// Brightness of a corner whose normal faces <paramref name="normal"/> in the world:
    /// x² · (x &gt; 0 ? PlusX : MinusX) + y² · (y &gt; 0 ? Up : Down) + z² · (z &gt; 0 ? PlusZ : MinusZ).
    /// The normal does not need length 1; a zero normal gets the mean of the six values.
    /// </summary>
    public static float Brightness(Vector3 normal, FacingBrightness facing)
    {
        var lengthSquared = normal.LengthSquared();
        if (lengthSquared <= 1e-12f)
        {
            return (facing.PlusX + facing.MinusX + facing.Up + facing.Down + facing.PlusZ + facing.MinusZ) / 6f;
        }

        // The squares of a length-1 vector add up to 1 - they are the weights of the mix.
        var x = normal.X * normal.X / lengthSquared;
        var y = normal.Y * normal.Y / lengthSquared;
        var z = normal.Z * normal.Z / lengthSquared;
        return x * (normal.X > 0f ? facing.PlusX : facing.MinusX)
            + y * (normal.Y > 0f ? facing.Up : facing.Down)
            + z * (normal.Z > 0f ? facing.PlusZ : facing.MinusZ);
    }
}
