using Raylib_cs;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// All values that shape how the game LOOKS, in one place (the counterpart to Core's Tuning,
/// which shapes how it FEELS). Decided with comparison pictures in milestone v0.3.
/// </summary>
internal static class LookSettings
{
    /// <summary>Width of the picture the 3D world is drawn into (then scaled up with hard pixel edges).</summary>
    public const int RenderWidth = 854;

    /// <summary>Height of the picture the 3D world is drawn into.</summary>
    public const int RenderHeight = 480;

    /// <summary>Background colour of the 3D world (empty space).</summary>
    public static readonly Color BackgroundColor = new(20, 16, 24, 255);

    /// <summary>Colour of the bars when the window shape does not match the picture.</summary>
    public static readonly Color BarColor = Color.Black;

    /// <summary>Vertical field of view of the camera in degrees.</summary>
    public const float FieldOfViewDegrees = 70f;
}
