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

    /// <summary>Base colours of the surfaces before shading.</summary>
    public static readonly Color WallColor = new(122, 104, 112, 255);
    public static readonly Color FloorColor = new(84, 72, 78, 255);
    public static readonly Color CeilingColor = new(64, 56, 66, 255);

    /// <summary>
    /// Flat shading: brightness factor per facing direction (1 = full base colour).
    /// Different values for the four wall directions make corners and edges visible without lines.
    /// </summary>
    public const float FloorBrightness = 0.9f;
    public const float CeilingBrightness = 0.55f;
    public const float WallFacingPlusXBrightness = 1.0f;
    public const float WallFacingMinusXBrightness = 0.62f;
    public const float WallFacingPlusZBrightness = 0.82f;
    public const float WallFacingMinusZBrightness = 0.72f;
}
