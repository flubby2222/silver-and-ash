using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The parts of the chest models (assets/models/chest_*.glb, one per tier) and how they move. In the file the front with
/// the lock looks towards +z, the lid hangs on a hinge along the back edge, and the chest is closed.
/// </summary>
public static class ChestPose
{
    public const string Base = "base";
    public const string Lid = "lid";
    public const string Lock = "lock";

    /// <summary>An open chest: the lid lifted this far around its hinge (degrees, see <see cref="Rig.Lift"/>) - over the back edge.</summary>
    public const float OpenLidDegrees = 110f;

    /// <summary>The parts in the order of the rig: the lid and the lock hang on the base.</summary>
    public static IReadOnlyList<RigPart> Parts { get; } =
    [
        new(Base, null),
        new(Lid, Base),
        new(Lock, Base),
    ];

    /// <summary>
    /// The turn of the whole chest around the vertical axis (radians, for <c>Matrix4x4.CreateRotationY</c>) so that its
    /// front looks the way <paramref name="front"/> says (issue #209).
    /// </summary>
    public static float Yaw(ChestFront front) => front == ChestFront.MinusZ ? MathF.PI : 0f;

    /// <summary>How far the lid is lifted (degrees): closed 0, open <see cref="OpenLidDegrees"/>.</summary>
    public static float LidDegrees(bool isOpen) => isOpen ? OpenLidDegrees : 0f;
}
