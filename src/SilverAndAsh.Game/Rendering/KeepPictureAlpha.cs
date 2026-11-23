using Raylib_cs;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Blending for see-through things drawn into the small picture of the 3D world (dark edge of a hit, dust): they blend
/// their colour over what is there, but leave the picture's own alpha at 1. The default blending would lower it too, and
/// when the picture is put on the screen the black behind it would show through - see-through dust became a dark,
/// smoky stain that way (issue #609).
/// </summary>
internal static class KeepPictureAlpha
{
    // OpenGL blending values; Raylib-cs takes them as plain numbers.
    private const int GlZero = 0;
    private const int GlOne = 1;
    private const int GlSrcAlpha = 0x0302;
    private const int GlOneMinusSrcAlpha = 0x0303;
    private const int GlFuncAdd = 0x8006;

    /// <summary>From now on: colour blended by its alpha, the picture's alpha kept.</summary>
    public static void Begin()
    {
        Rlgl.SetBlendFactorsSeparate(GlSrcAlpha, GlOneMinusSrcAlpha, GlZero, GlOne, GlFuncAdd, GlFuncAdd);
        Raylib.BeginBlendMode(BlendMode.CustomSeparate);
    }

    /// <summary>Back to the default blending.</summary>
    public static void End() => Raylib.EndBlendMode();
}
