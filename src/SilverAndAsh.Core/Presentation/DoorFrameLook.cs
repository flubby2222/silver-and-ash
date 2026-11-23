using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The stone frame around a door (issue #303): one model per door kind and wall variant,
/// assets/models/door_frame_{normal,locked,boss}_{a,b,c}.glb. Each is built on the real wall texture of its variant - the
/// arch field and the reveals continue the wall around them - plus the same tile as brighter dressed stone for the arch
/// rings and the plates at the springing and the foot.
/// <para>
/// The boss portal (issue #568, tools/models/boss_portal.py) spans both cells of the boss door and is as high as the
/// rooms it joins (<see cref="CellsWide"/>, <see cref="Height"/>); its origin lies in the middle of the two cells.
/// </para>
/// <para>
/// The frame sits in the door's file space (origin on the floor in the middle of the cell, turned by
/// <see cref="DoorPose.Yaw"/>) and never moves. Like a cracked wall (<see cref="CrackedWallLook"/>) its two faces look
/// into different rooms, so each face is drawn from the file of its own variant: <see cref="Front"/> (the stone on the
/// file's +z side) from the file of the front variant, <see cref="Back"/> from the file of the back variant.
/// </para>
/// </summary>
public static class DoorFrameLook
{
    public const string Front = "portal_front";
    public const string Back = "portal_back";

    private static readonly RigPart[] AllParts = [new(Front, null), new(Back, null)];

    /// <summary>The parts in the order of the rig; none hangs on another and none moves.</summary>
    public static IReadOnlyList<RigPart> Parts => AllParts;

    /// <summary>Whether a door of <paramref name="kind"/> stands in a stone frame: every kind but the cracked wall.</summary>
    public static bool HasFrame(DoorKind kind) => kind is DoorKind.Normal or DoorKind.Locked or DoorKind.Boss;

    /// <summary>
    /// How many cells side by side the model and frame of <paramref name="kind"/> fill: two for the boss portal (issue
    /// #568, as wide as the boss door of issue #566), one for every other kind.
    /// </summary>
    public static int CellsWide(DoorKind kind) => kind == DoorKind.Boss ? 2 : 1;

    /// <summary>
    /// Whether the model of <paramref name="kind"/> fits a door of <paramref name="cells"/> cells: only a door exactly as
    /// wide as the model. It stands once, on the floor in the middle of all its cells. A boss door of one cell (the
    /// showroom, a hand-made level) would let the 4 m wide portal stick into the walls beside it - it gets the placeholder.
    /// </summary>
    public static bool Fits(DoorKind kind, int cells) => cells == CellsWide(kind);

    /// <summary>
    /// The height of the frame of <paramref name="kind"/>: the ceiling of its doorway cells
    /// (<see cref="LevelGenerator.DoorCell"/>) - 3.5 m, the boss portal 6 m.
    /// </summary>
    public static float Height(DoorKind kind) => LevelGenerator.DoorCell(kind).CeilingHeight;

    /// <summary>The file of the frame of <paramref name="kind"/> for <paramref name="variant"/>, e.g. door_frame_boss_b.glb.</summary>
    public static string FileName(DoorKind kind, TextureVariant variant)
    {
        if (!HasFrame(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "A cracked wall has no door frame.");
        }

        if (variant is not (TextureVariant.A or TextureVariant.B or TextureVariant.C))
        {
            throw new ArgumentOutOfRangeException(nameof(variant), variant, null);
        }

        return $"door_frame_{kind.ToString().ToLowerInvariant()}_{variant.ToString().ToLowerInvariant()}.glb";
    }

    /// <summary>
    /// The wall variant each face of the frame of the door in cell (<paramref name="x"/>, <paramref name="z"/>) shows:
    /// the variant of the walls of the room it faces - the same as for a cracked wall in that cell.
    /// </summary>
    public static (TextureVariant Front, TextureVariant Back) Variants(SurfaceVariants surfaces, int x, int z, RoomSide side) =>
        CrackedWallLook.Variants(surfaces, x, z, side);

    /// <summary>
    /// The frame file to draw a face from: the one of <paramref name="wanted"/> (the variant the level really draws
    /// there) if it was loaded, else the one of variant A, else none.
    /// </summary>
    public static TextureVariant? Pick(TextureVariant wanted, bool wantedLoaded, bool variantALoaded) =>
        wantedLoaded ? wanted : variantALoaded ? TextureVariant.A : null;

    /// <summary>
    /// Whether the door model is drawn at all: only with its model and a frame for both faces. Without the frame the
    /// leaves would hang in a hole in the wall - then the whole door is the coded placeholder.
    /// </summary>
    public static bool DrawsModel(bool modelLoaded, TextureVariant? front, TextureVariant? back) =>
        modelLoaded && front is not null && back is not null;
}
