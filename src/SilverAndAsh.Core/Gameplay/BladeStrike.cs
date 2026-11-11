using System.Numerics;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Which bodies a swing of the silver blade hits: everything inside a cone in front of the eyes -
/// <see cref="Tuning.Combat.BladeRange"/> long, <see cref="Tuning.Combat.BladeAngle"/> wide, up and down as well
/// as left and right - and not behind a wall.
/// </summary>
public static class BladeStrike
{
    /// <summary>
    /// True if a swing from <paramref name="eye"/> in <paramref name="direction"/> (length 1) hits <paramref name="body"/>.
    /// </summary>
    public static bool Hits(Level level, Vector3 eye, Vector3 direction, CylinderBody body)
    {
        ArgumentNullException.ThrowIfNull(level);

        // The point of the body's middle line (its axis) that is closest to the eye: at eye height if the body
        // is that tall, otherwise its top or bottom end.
        var axisPoint = body.Position with { Y = Math.Clamp(eye.Y, body.Position.Y, body.Position.Y + body.Height) };
        var toBody = axisPoint - eye;
        var distance = toBody.Length();

        // Range counts to the body's surface, not its middle.
        if (distance - body.Radius > Tuning.Combat.BladeRange)
        {
            return false;
        }

        // Inside the cone: the angle between the looking direction and the direction to the body is at most
        // half the blade angle - in any direction, so up and down count as well as left and right.
        var forward = Vector3.Normalize(direction);
        if (distance > 0.0001f)
        {
            var cosine = Vector3.Dot(forward, toBody / distance);
            if (cosine < MathF.Cos(Tuning.Combat.BladeAngle / 2f * MathF.PI / 180f))
            {
                return false;
            }
        }

        // No hits through walls: check the line from the eye to the body's surface.
        var surface = distance > body.Radius ? eye + toBody / distance * (distance - body.Radius) : eye;
        return LineOfSight.IsClear(level, eye, surface);
    }
}
