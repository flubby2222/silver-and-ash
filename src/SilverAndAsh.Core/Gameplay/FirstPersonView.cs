using System.Numerics;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Where the player is looking, as two angles:
/// <list type="bullet">
///   <item><b>Yaw</b> – turning left/right around the vertical axis. 0 = looking along +Z.</item>
///   <item><b>Pitch</b> – looking up (+) or down (-). Limited to just under ±90°.</item>
/// </list>
/// </summary>
public sealed class FirstPersonView
{
    /// <summary>Creates a view looking horizontally in the direction given by <paramref name="yawRadians"/>.</summary>
    public FirstPersonView(float yawRadians = 0f)
    {
        YawRadians = yawRadians;
    }

    /// <summary>Left/right angle in radians. Positive turns towards +X.</summary>
    public float YawRadians { get; private set; }

    /// <summary>Up/down angle in radians. Positive looks up.</summary>
    public float PitchRadians { get; private set; }

    /// <summary>Unit vector in the direction the player is looking (including up/down).</summary>
    public Vector3 Forward
    {
        get
        {
            var horizontal = MathF.Cos(PitchRadians);
            return new Vector3(
                MathF.Sin(YawRadians) * horizontal,
                MathF.Sin(PitchRadians),
                MathF.Cos(YawRadians) * horizontal);
        }
    }

    /// <summary>
    /// Like <see cref="Forward"/>, but tilted down by an extra <paramref name="tiltDownRadians"/> - for camera effects
    /// such as the landing nod. The stored angles do not change.
    /// </summary>
    public Vector3 ForwardTiltedDown(float tiltDownRadians)
    {
        var pitch = Math.Clamp(
            PitchRadians - tiltDownRadians, -Tuning.Controls.MaxPitchRadians, Tuning.Controls.MaxPitchRadians);
        var horizontal = MathF.Cos(pitch);
        return new Vector3(MathF.Sin(YawRadians) * horizontal, MathF.Sin(pitch), MathF.Cos(YawRadians) * horizontal);
    }

    /// <summary>
    /// Unit vector of the view direction flattened onto the ground. Walking uses this,
    /// so looking up or down does not make the player slower.
    /// </summary>
    public Vector3 FlatForward => new(MathF.Sin(YawRadians), 0f, MathF.Cos(YawRadians));

    /// <summary>Unit vector pointing to the player's right, on the ground.</summary>
    public Vector3 FlatRight => new(-MathF.Cos(YawRadians), 0f, MathF.Sin(YawRadians));

    /// <summary>
    /// Turns the view by a mouse movement in pixels. Mouse right turns right, mouse up looks up (not inverted).
    /// </summary>
    public void ApplyMouse(Vector2 deltaPixels, float sensitivity = Tuning.Controls.MouseSensitivity)
    {
        // Turning right means rotating from +Z towards -X, i.e. a smaller yaw.
        // IEEERemainder keeps the angle between -π and π, so it never grows without limit.
        YawRadians = MathF.IEEERemainder(YawRadians - deltaPixels.X * sensitivity, MathF.Tau);

        // Screen Y grows downwards, so moving the mouse up (negative Y) must increase the pitch.
        PitchRadians = Math.Clamp(
            PitchRadians - deltaPixels.Y * sensitivity,
            -Tuning.Controls.MaxPitchRadians,
            Tuning.Controls.MaxPitchRadians);
    }
}
