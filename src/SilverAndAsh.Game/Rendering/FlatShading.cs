using System.Numerics;
using Raylib_cs;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Flat shading for everything in the world: each surface gets a fixed brightness depending on the direction
/// it faces (up, down, or one of the four wall directions). The level and the figures use the same values,
/// so they look like they belong together.
/// </summary>
internal static class FlatShading
{
    /// <summary>Brightness (0..1) of a surface that faces <paramref name="normal"/> (length 1).</summary>
    public static float Brightness(Vector3 normal)
    {
        var flat = MathF.Max(MathF.Abs(normal.X), MathF.Abs(normal.Z));
        if (MathF.Abs(normal.Y) > flat)
        {
            return normal.Y > 0f ? LookSettings.FloorBrightness : LookSettings.CeilingBrightness;
        }

        return WallBrightness(normal);
    }

    /// <summary>Brightness of a wall, by the direction it faces.</summary>
    public static float WallBrightness(Vector3 normal) =>
        MathF.Abs(normal.X) > MathF.Abs(normal.Z)
            ? (normal.X > 0f ? LookSettings.WallFacingPlusXBrightness : LookSettings.WallFacingMinusXBrightness)
            : (normal.Z > 0f ? LookSettings.WallFacingPlusZBrightness : LookSettings.WallFacingMinusZBrightness);

    /// <summary><paramref name="color"/> darkened by <paramref name="brightness"/>.</summary>
    public static Color Apply(Color color, float brightness) => new(
        (byte)Math.Clamp(color.R * brightness, 0f, 255f),
        (byte)Math.Clamp(color.G * brightness, 0f, 255f),
        (byte)Math.Clamp(color.B * brightness, 0f, 255f),
        color.A);
}
