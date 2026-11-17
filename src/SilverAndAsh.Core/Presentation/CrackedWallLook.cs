using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Presentation;

/// <summary>How a standing cracked wall is drawn - from the best of what could be loaded (see <see cref="CrackedWallLook.Choose"/>).</summary>
public enum CrackedWallDrawing
{
    /// <summary>On each face the plate of that face's wall variant, with its cracks and the debris at its foot (issue #246).</summary>
    Panels,

    /// <summary>The fine dark lines of door_cracked.glb in front of both faces (issue #245).</summary>
    Lines,

    /// <summary>A few coded dark boxes in front of both faces: nothing could be loaded.</summary>
    Placeholder,
}

/// <summary>
/// The cracked wall to a secret room (issue #246): one model per wall variant, assets/models/cracked_wall_a.glb,
/// cracked_wall_b.glb, cracked_wall_c.glb. Each paints its cracks on the real wall texture of that variant, so the plate
/// continues the wall around it without a seam.
/// <para>
/// In the file the origin is the middle of the cell on its floor, the wall's faces lie across z like a door's leaves, and
/// <see cref="DoorPose.Yaw"/> turns it like a door. The two faces of the cell look into different rooms - and every wall
/// face shows the variant of the room it faces (<see cref="LevelGeometry.CellOf"/>) - so each face is drawn from the
/// file of its own variant: <see cref="PanelFront"/> and <see cref="DebrisFront"/> for the face on the file's +z side,
/// <see cref="PanelBack"/> and <see cref="DebrisBack"/> for the -z side. <see cref="Chunk"/> is one stone of about
/// <see cref="ChunkSize"/> around the origin, for the chunks of the breaking wall (issue #185).
/// </para>
/// </summary>
public static class CrackedWallLook
{
    public const string PanelFront = "panel_front";
    public const string PanelBack = "panel_back";
    public const string DebrisFront = "debris_front";
    public const string DebrisBack = "debris_back";
    public const string Chunk = "chunk";

    /// <summary>The edge of the chunk model in metres (it lies within +-ChunkSize/2 around its origin, roughly).</summary>
    public const float ChunkSize = 0.25f;

    private static readonly RigPart[] AllParts =
        [new(PanelFront, null), new(PanelBack, null), new(DebrisFront, null), new(DebrisBack, null), new(Chunk, null)];

    /// <summary>The parts in the order of the rig; none hangs on another and none moves.</summary>
    public static IReadOnlyList<RigPart> Parts => AllParts;

    /// <summary>The file of <paramref name="variant"/>: cracked_wall_a.glb, cracked_wall_b.glb, cracked_wall_c.glb.</summary>
    public static string FileName(TextureVariant variant) => variant switch
    {
        TextureVariant.A or TextureVariant.B or TextureVariant.C => $"cracked_wall_{variant.ToString().ToLowerInvariant()}.glb",
        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null),
    };

    /// <summary>
    /// The walkable cells the two faces of the cracked wall in cell (<paramref name="x"/>, <paramref name="z"/>) look
    /// into: Front on the file's +z side, Back on its -z side. After <see cref="DoorPose.Yaw"/> the file's +z is the
    /// world's +z for a wall of a North/South door and +x for an East/West one.
    /// </summary>
    public static ((int X, int Z) Front, (int X, int Z) Back) FaceCells(int x, int z, RoomSide side) =>
        side is RoomSide.East or RoomSide.West ? ((x + 1, z), (x - 1, z)) : ((x, z + 1), (x, z - 1));

    /// <summary>
    /// The wall variant each face of the cracked wall in cell (<paramref name="x"/>, <paramref name="z"/>) shows: the
    /// variant of the walls of the room it faces, as the level's own wall faces there.
    /// </summary>
    public static (TextureVariant Front, TextureVariant Back) Variants(SurfaceVariants surfaces, int x, int z, RoomSide side)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        var (front, back) = FaceCells(x, z, side);
        return (surfaces.At(front.X, front.Z, SurfaceKind.Wall), surfaces.At(back.X, back.Z, SurfaceKind.Wall));
    }

    /// <summary>
    /// How to draw a standing cracked wall. The plates only if both faces have theirs - the model of the variant the level
    /// really draws there was loaded - and the walls are textured at all (<paramref name="wallsTextured"/>; a plate of
    /// stone in front of a plain wall would stick out). Else the fine lines of the door model, if that was loaded, else
    /// the coded placeholder. One choice for the whole wall: a plate on one face and lines on the other never mix.
    /// </summary>
    public static CrackedWallDrawing Choose(bool frontLoaded, bool backLoaded, bool wallsTextured, bool linesLoaded) =>
        frontLoaded && backLoaded && wallsTextured ? CrackedWallDrawing.Panels
        : linesLoaded ? CrackedWallDrawing.Lines
        : CrackedWallDrawing.Placeholder;
}
