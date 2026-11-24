using System.Numerics;

namespace SilverAndAsh.Core.Physics;

/// <summary>
/// Something solid that a moving body cannot pass through (issue #694): a column, the stone of a boss portal, a standing
/// body, a box. <see cref="CharacterCollision.Move"/> pushes a body out of every kind of solid through this one method,
/// so a new kind of solid is one more implementation, not one more resolve method.
/// </summary>
public interface ISolid
{
    /// <summary>
    /// Pushes a body out of this solid on the ground plane and removes the part of <paramref name="velocity"/> that points
    /// into it, so the body slides along. The body is an upright capsule: <paramref name="position"/> is the middle of its
    /// feet, with <paramref name="radius"/> and <paramref name="height"/>, and it climbs up to
    /// <paramref name="stepHeight"/>. The solid decides itself at which heights it stands in the way. Height (Y) is not
    /// touched.
    /// </summary>
    /// <returns>True if it pushed the body.</returns>
    bool PushOut(ref Vector3 position, ref Vector3 velocity, float radius, float height, float stepHeight);
}
