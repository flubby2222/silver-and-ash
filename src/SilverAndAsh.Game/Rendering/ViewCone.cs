using System.Numerics;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// What the camera can see at all, to leave out whole models before they cost anything (issue #245: 25 door models
/// drawn every frame cost 5.8 ms). Something counts as unseen only when its bounding sphere lies entirely beyond the end of
/// the fog (<see cref="LookSettings.FogEnd"/>, where only fog colour is left - also the end of the pattern fade of look
/// variants 3 and 4) or entirely outside a cone around the view direction that holds the whole picture, corners included.
/// So nothing that is left out could have shown a single pixel: no popping.
/// </summary>
internal readonly struct ViewCone
{
    // A little more than the picture's corners, so a rounding error never cuts off an edge.
    private const float MarginRadians = 5f * MathF.PI / 180f;

    private readonly Vector3 _eye;
    private readonly Vector3 _forward;
    private readonly float _halfAngle;

    /// <param name="eye">Camera position.</param>
    /// <param name="forward">View direction (need not be normalised).</param>
    /// <param name="fieldOfViewDegrees">Vertical field of view, as in the camera.</param>
    /// <param name="aspect">Width / height of the picture.</param>
    public ViewCone(Vector3 eye, Vector3 forward, float fieldOfViewDegrees, float aspect)
    {
        _eye = eye;
        _forward = Vector3.Normalize(forward);

        // Half the angle to a corner of the picture: tan(corner) = tan(vertical / 2) * sqrt(1 + aspect^2).
        var halfVertical = fieldOfViewDegrees * MathF.PI / 360f;
        _halfAngle = MathF.Atan(MathF.Tan(halfVertical) * MathF.Sqrt(1f + (aspect * aspect))) + MarginRadians;
    }

    /// <summary>Could any part of a sphere around <paramref name="centre"/> with <paramref name="radius"/> show in the picture?</summary>
    public bool MaySee(Vector3 centre, float radius)
    {
        var toCentre = centre - _eye;
        var distance = toCentre.Length();
        if (distance <= radius)
        {
            return true;
        }

        if (distance - radius > LookSettings.FogEnd)
        {
            return false;
        }

        // The angle between view direction and centre, less the angle the sphere itself takes up.
        var cos = Math.Clamp(Vector3.Dot(toCentre / distance, _forward), -1f, 1f);
        return MathF.Acos(cos) - MathF.Asin(radius / distance) <= _halfAngle;
    }
}
