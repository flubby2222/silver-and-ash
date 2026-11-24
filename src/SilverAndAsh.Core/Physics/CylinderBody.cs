using System.Numerics;

namespace SilverAndAsh.Core.Physics;

/// <summary>
/// A standing body that others cannot walk through, as an upright cylinder:
/// <paramref name="Position"/> = centre of its feet, plus radius and height. Used for training dummies now,
/// for enemies later.
/// </summary>
public readonly record struct CylinderBody(Vector3 Position, float Radius, float Height) : ISolid
{
    /// <summary>
    /// Pushes a body out of this one on the ground plane - the same sliding as along walls. A body above or below
    /// (no height overlap) is not pushed.
    /// </summary>
    public bool PushOut(ref Vector3 position, ref Vector3 velocity, float radius, float height, float stepHeight)
    {
        var heightsOverlap = position.Y < Position.Y + Height && Position.Y < position.Y + height;
        return heightsOverlap
            && CharacterCollision.PushOutOfCircle(ref position, ref velocity, radius, new Vector2(Position.X, Position.Z), Radius);
    }
}
