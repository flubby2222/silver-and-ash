using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The parts of the door models (assets/models/door_*.glb, one per <see cref="DoorKind"/>, issue #245) and how they move.
/// In the file the origin is the middle of the passage on the floor, the passage runs along z and the leaves stand
/// across it, closed. The hinges stand at the outer edges: <see cref="LeafA"/> (and the single <see cref="Leaf"/>) on
/// the +x side, <see cref="LeafB"/> on the -x side. The <see cref="Portal"/> (posts, head panel, arch field) never moves.
/// </summary>
public static class DoorPose
{
    public const string Portal = "portal";
    public const string LeafA = "leaf_a";
    public const string LeafB = "leaf_b";
    public const string Leaf = "leaf";
    public const string Lock = "lock";
    public const string Cracks = "cracks";

    /// <summary>The leaves of a wooden or boss door, fully open (degrees).</summary>
    public const float DoubleDoorOpenDegrees = 90f;

    /// <summary>The leaf of a locked door, fully open (degrees): a little less, the heavy leaf stops short of the wall.</summary>
    public const float LockedOpenDegrees = 80f;

    private static readonly RigPart[] DoubleDoor = [new(Portal, null), new(LeafA, Portal), new(LeafB, Portal)];
    private static readonly RigPart[] LockedDoor = [new(Portal, null), new(Leaf, Portal), new(Lock, Leaf)];
    private static readonly RigPart[] CrackedWall = [new(Cracks, null)];

    /// <summary>The file of <paramref name="kind"/>: door_normal.glb, door_locked.glb, door_boss.glb, door_cracked.glb.</summary>
    public static string FileName(DoorKind kind) => $"door_{kind.ToString().ToLowerInvariant()}.glb";

    /// <summary>
    /// The parts in the order of the rig. Wooden and boss doors: portal, leaf_a, leaf_b. Locked door: portal, leaf, and
    /// the lock hanging on the leaf (it swings with it). Cracked wall: only its cracks - the wall itself is the level's.
    /// </summary>
    public static IReadOnlyList<RigPart> Parts(DoorKind kind) => kind switch
    {
        DoorKind.Normal or DoorKind.Boss => DoubleDoor,
        DoorKind.Locked => LockedDoor,
        DoorKind.Cracked => CrackedWall,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>How far the leaves of <paramref name="kind"/> turn when fully open (degrees); 0 for a cracked wall.</summary>
    public static float OpenDegrees(DoorKind kind) => kind switch
    {
        DoorKind.Normal or DoorKind.Boss => DoubleDoorOpenDegrees,
        DoorKind.Locked => LockedOpenDegrees,
        DoorKind.Cracked => 0f,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>
    /// The turn of the whole door around the vertical axis (radians, for <c>Matrix4x4.CreateRotationY</c>): a door whose
    /// passage runs East/West is turned a quarter, so the file's +z becomes the world's +x.
    /// </summary>
    public static float Yaw(RoomSide side) => side is RoomSide.East or RoomSide.West ? MathF.PI / 2f : 0f;

    /// <summary>
    /// Slow start, slow stop (smoothstep) - only the picture eases; the door's own progress grows evenly per tick.
    /// </summary>
    public static float Ease(float fraction)
    {
        var t = Math.Clamp(fraction, 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    /// <summary>
    /// Fills <paramref name="turns"/> (order of <see cref="Parts"/>) for a door of <paramref name="kind"/> that is
    /// <paramref name="openFraction"/> open (0..1, eased here). <paramref name="towardsPlusZ"/>: the leaves swing
    /// towards the file's +z - right for a player who opened it from the lower side (see
    /// <see cref="Gameplay.Door.OpenedFromLowSide"/>; after <see cref="Yaw"/> the file's +z is the world's +z or +x).
    /// </summary>
    public static void Turns(DoorKind kind, float openFraction, bool towardsPlusZ, Span<Matrix4x4> turns)
    {
        var parts = Parts(kind);
        if (turns.Length != parts.Count)
        {
            throw new ArgumentException($"A {kind} door has {parts.Count} parts.", nameof(turns));
        }

        // A leaf hanging on the +x side reaches towards -x; turned by +angle (CreateRotationY) it swings to +z.
        // The leaf on the -x side reaches towards +x and needs the opposite angle.
        var radians = OpenDegrees(kind) * MathF.PI / 180f * Ease(openFraction) * (towardsPlusZ ? 1f : -1f);
        for (var i = 0; i < parts.Count; i++)
        {
            turns[i] = parts[i].Name switch
            {
                LeafA or Leaf => Matrix4x4.CreateRotationY(radians),
                LeafB => Matrix4x4.CreateRotationY(-radians),
                _ => Matrix4x4.Identity,
            };
        }
    }

    /// <summary>The lock of a locked door shows only while the door is closed - unlocking uses it up.</summary>
    public static bool ShowsLock(bool isOpen) => !isOpen;
}
