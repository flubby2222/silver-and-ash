using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Presentation;

/// <summary>The two sizes of wall pillar (issue #536): one per ceiling height of the game.</summary>
public enum WallPillarSize
{
    /// <summary>A flat pillar for a corridor, 3.5 m high.</summary>
    Corridor,

    /// <summary>A deeper pillar for a room, 6 m high.</summary>
    Room,
}

/// <summary>
/// The wall pillar (issue #536, docs/design/formenlehre-gotik.md): a pillar of stone standing in front of a wall, from
/// the floor to the ceiling. Pure decoration - no body, no collision, nothing it does to a step or a shot. One model per
/// size and wall variant, assets/models/wall_pillar_{corridor,room}_{a,b,c}.glb, built by tools/models/wall_pillar.py on
/// the real wall texture of its variant, one level of the palette brighter.
/// <para>
/// File space: origin in the middle of the pillar on the floor, the wall's face at z = 0, the pillar standing out toward
/// +z. Its texture runs in the grid of the wall (one tile per metre), so it only lines up with the wall where its middle
/// sits on a whole metre along the wall - <see cref="Place"/> puts it in the middle of a cell's wall, which always is.
/// </para>
/// </summary>
public static class WallPillarLook
{
    /// <summary>The one part of every file.</summary>
    public const string Part = "pillar";

    /// <summary>How far a pillar stands out from the wall at most: the room pillar's base and capital, in metres.</summary>
    public const float Depth = 0.3125f;

    /// <summary>The width of the base and the capital, in metres.</summary>
    public const float Width = 0.625f;

    // Ceiling height -> size. A new size is one more line here.
    private static readonly (float Ceiling, WallPillarSize Size)[] Sizes =
    [
        (Cell.DefaultCeilingHeight, WallPillarSize.Corridor),
        (RoomTemplate.RoomCeilingHeight, WallPillarSize.Room),
    ];

    private static readonly RigPart[] AllParts = [new(Part, null)];

    /// <summary>The parts in the order of the rig: one, fixed.</summary>
    public static IReadOnlyList<RigPart> Parts => AllParts;

    /// <summary>
    /// The size of pillar that reaches from the floor to a ceiling of <paramref name="ceilingHeight"/> metres, or null if
    /// none does - then no pillar stands there.
    /// </summary>
    public static WallPillarSize? SizeFor(float ceilingHeight)
    {
        foreach (var (ceiling, size) in Sizes)
        {
            if (ceiling == ceilingHeight)
            {
                return size;
            }
        }

        return null;
    }

    /// <summary>The height of a pillar of <paramref name="size"/>: the ceiling it reaches.</summary>
    public static float Height(WallPillarSize size)
    {
        foreach (var (ceiling, each) in Sizes)
        {
            if (each == size)
            {
                return ceiling;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(size), size, null);
    }

    /// <summary>The file of the pillar of <paramref name="size"/> for <paramref name="variant"/>, e.g. wall_pillar_room_b.glb.</summary>
    public static string FileName(WallPillarSize size, TextureVariant variant)
    {
        if (variant is not (TextureVariant.A or TextureVariant.B or TextureVariant.C))
        {
            throw new ArgumentOutOfRangeException(nameof(variant), variant, null);
        }

        _ = Height(size);
        return $"wall_pillar_{size.ToString().ToLowerInvariant()}_{variant.ToString().ToLowerInvariant()}.glb";
    }

    /// <summary>
    /// The file of the pillar that fits a wall of <paramref name="variant"/> under a ceiling of
    /// <paramref name="ceilingHeight"/> metres, or null if no pillar reaches that ceiling.
    /// </summary>
    public static string? FileName(TextureVariant variant, float ceilingHeight) =>
        SizeFor(ceilingHeight) is { } size ? FileName(size, variant) : null;

    /// <summary>
    /// Where a pillar stands against the <paramref name="wall"/> side of the walkable cell (<paramref name="x"/>,
    /// <paramref name="z"/>) of <paramref name="level"/>: on its floor in the middle of that wall, looking into the cell.
    /// </summary>
    public static (Vector3 Position, Vector3 Facing) Place(Level level, int x, int z, RoomSide wall)
    {
        ArgumentNullException.ThrowIfNull(level);
        var facing = wall switch
        {
            RoomSide.North => Vector3.UnitZ,
            RoomSide.South => -Vector3.UnitZ,
            RoomSide.West => Vector3.UnitX,
            RoomSide.East => -Vector3.UnitX,
            _ => throw new ArgumentOutOfRangeException(nameof(wall), wall, null),
        };
        return (level.CellCenter(x, z) - (facing * (Level.CellSize / 2f)), facing);
    }

    /// <summary>
    /// The file to draw a pillar from: the one of <paramref name="wanted"/> (the variant of the wall behind it) if it was
    /// loaded, else the one of variant A, else none - then the pillar is not drawn, the wall stays bare.
    /// </summary>
    public static TextureVariant? Pick(TextureVariant wanted, bool wantedLoaded, bool variantALoaded) =>
        wantedLoaded ? wanted : variantALoaded ? TextureVariant.A : null;
}