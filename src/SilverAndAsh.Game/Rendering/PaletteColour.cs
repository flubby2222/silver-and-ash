using Raylib_cs;

namespace SilverAndAsh.Game.Rendering;

/// <summary>Turns a palette colour from Core (for example <c>EffectColours</c>) into a Raylib colour.</summary>
internal static class PaletteColour
{
    /// <summary><paramref name="colour"/>, fully opaque.</summary>
    public static Color Opaque((byte R, byte G, byte B) colour) => new(colour.R, colour.G, colour.B, (byte)255);
}
