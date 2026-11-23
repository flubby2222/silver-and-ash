namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The depth of the weapons in the hands (issue #624). The world is drawn with raylib's near plane of 5 cm. That plane cut
/// the back end of a cuff open in the lower left corner of the picture: the cuff runs back from the grip to within 1 cm
/// of the eyes, and through the cut one looked into the tube, whose inside is not drawn - a hole, with the handle of the
/// blade showing through. So the weapons get a depth of their own, with the near plane <see cref="WeaponPose.NearPlane"/>.
/// <para>
/// A projection maps the distance z in front of the eyes to a depth d = A - B / z (-1 at the near plane, growing
/// towards the far plane, after the division by z). The weapons share the depth buffer with the world, so their depth
/// must stay in front of every wall: <see cref="Row"/> maps the weapon near plane to -1 and lets the depth rise towards
/// the world's depth at <see cref="WeaponPose.Reach"/> without ever reaching it. Every point of a weapon lies in front
/// of every point of the world beyond the reach - the player keeps 35 cm from every wall, the weapons stay within 30 cm
/// (tested in WeaponDepthTests); within the weapons the order of depth is kept. A fixed object of the world nearer than
/// the reach (about 0.09 to 0.30 m) may still hide parts of a weapon in front of it.
/// </para>
/// </summary>
public static class WeaponDepth
{
    /// <summary>
    /// The near plane of the world's projection (metres). The game sets it at start (rlSetClipPlanes); it equals raylib's
    /// default (rlGetCullDistanceNear).
    /// </summary>
    public const float WorldNearPlane = 0.05f;

    /// <summary>
    /// The far plane of the world's projection (metres). The game sets it at start (rlSetClipPlanes); it equals raylib's
    /// default (rlGetCullDistanceFar, 4000 in Raylib-cs 8.1).
    /// </summary>
    public const float WorldFarPlane = 4000f;

    /// <summary>
    /// The depth row (A, B) for the weapons from the world's (<paramref name="worldA"/>, <paramref name="worldB"/>):
    /// d = A - B / z, -1 at <see cref="WeaponPose.NearPlane"/>, below the world's depth at <see cref="WeaponPose.Reach"/>
    /// for every z.
    /// </summary>
    public static (float A, float B) Row(float worldA, float worldB)
    {
        var a = worldA - (worldB / WeaponPose.Reach);
        return (a, (a + 1f) * WeaponPose.NearPlane);
    }

    /// <summary>The depth (-1 near .. 1 far) of a point <paramref name="z"/> metres in front of the eyes under the row (<paramref name="a"/>, <paramref name="b"/>).</summary>
    public static float At(float a, float b, float z) => a - (b / z);

    /// <summary>
    /// The depth row of a standard perspective projection with these near and far planes (as raylib's BeginMode3D
    /// builds it): -1 at the near plane, 1 at the far plane.
    /// </summary>
    public static (float A, float B) Perspective(float near, float far) =>
        ((far + near) / (far - near), 2f * far * near / (far - near));
}
