namespace SilverAndAsh.Core;

/// <summary>
/// Mouse settings - not balancing, but the player's own taste (later adjustable in the pause menu, v0.8).
/// </summary>
public static class ControlSettings
{
    /// <summary>
    /// How far the view turns per pixel of mouse movement, in radians.
    /// 0.0025 rad = about 0.14 degrees - a medium sensitivity.
    /// </summary>
    public const float MouseSensitivity = 0.0025f;

    /// <summary>Maximum look up/down angle: just under 90 degrees, so the view never flips over.</summary>
    public const float MaxPitchRadians = 89f * MathF.PI / 180f;
}
