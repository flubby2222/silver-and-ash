using System.Numerics;

namespace SilverAndAsh.Core.Physics;

/// <summary>
/// A solid box standing on the floor (axis-aligned, from <paramref name="Min"/> to <paramref name="Max"/>), e.g. a
/// chest. A character cannot walk through it, but can stand on its top - once it has jumped high enough.
/// </summary>
public readonly record struct BoxBody(Vector3 Min, Vector3 Max) : ISolid
{
    /// <summary>
    /// Pushes a body out of the box on the ground plane - like a wall, with sliding. A box whose top is at most a step
    /// above the feet does not stop it (it stands on it or steps up), nor does one above its head.
    /// </summary>
    public bool PushOut(ref Vector3 position, ref Vector3 velocity, float radius, float height, float stepHeight)
    {
        if (Max.Y <= position.Y + stepHeight || Min.Y >= position.Y + height)
        {
            return false;
        }

        return CharacterCollision.PushOutOfRectangle(ref position, ref velocity, radius, new Vector2(Min.X, Min.Z), new Vector2(Max.X, Max.Z));
    }
}
