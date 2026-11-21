using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Presentation;

/// <summary>The two sizes of wall niche (issue #538): one per ceiling height of the game.</summary>
public enum WallNicheSize
{
    /// <summary>A flat niche for a corridor (3.5 m clear height), standing out 0.125 m.</summary>
    Corridor,

    /// <summary>A deeper niche for a room (6 m clear height), standing out 0.25 m.</summary>
    Room,
}

/// <summary>
/// The wall niche (issue #538, docs/design/formenlehre-gotik.md): a pointed niche set flat on a wall - a stepped frame of
/// stone in front of a dark back plate, with a sill. Pure decoration - no body, no collision, nothing it does to a step or
/// a shot. One model per size and wall variant, assets/models/wall_niche_{corridor,room}_{a,b,c}.glb, built by
/// tools/models/wall_niche.py on the real wall texture of its variant.
/// <para>
/// File space: origin in the middle of the niche on the floor, the wall's face at z = 0, the niche standing out toward
/// +z. The frame is <see cref="Width"/> wide, so it reaches a quarter metre into the cells on both sides along the wall.
/// The back plate shows the wall's texture darkened in the grid of the wall (one tile per metre), so it only lines up
/// where the niche's middle sits on a whole metre along the wall - <see cref="Place"/> puts it in the middle of a cell's
/// wall, which always is.
/// </para>
/// </summary>
public static class WallNicheLook
{
    /// <summary>The stepped frame of stone around the opening.</summary>
    public const string Frame = "frame";

    /// <summary>The dark plate in the opening, just in front of the wall.</summary>
    public const string BackPlate = "back_plate";

    /// <summary>The sill under the opening.</summary>
    public const string Sill = "sill";

    /// <summary>The width of the frame and the sill, in metres.</summary>
    public const float Width = 1.5f;

    /// <summary>The height of the frame's apex above the floor, in metres.</summary>
    public const float Height = 2.2655f;

    /// <summary>How far apart two heights may lie and still count as the same, in metres.</summary>
    public const float HeightTolerance = 1e-3f;

    // Clear height -> size. A new size is one more line here.
    private static readonly (float ClearHeight, WallNicheSize Size, float Depth)[] Sizes =
    [
        (Cell.DefaultCeilingHeight, WallNicheSize.Corridor, 0.125f),
        (RoomTemplate.RoomCeilingHeight, WallNicheSize.Room, 0.25f),
    ];

    private static readonly RigPart[] AllParts = [new(Frame, null), new(BackPlate, null), new(Sill, null)];

    /// <summary>The parts in the order of the rig: frame, back plate and sill, fixed.</summary>
    public static IReadOnlyList<RigPart> Parts => AllParts;

    /// <summary>
    /// The size of niche for a cell of <paramref name="clearHeight"/> metres between floor and ceiling, or null if none
    /// is built for it - then no niche stands there.
    /// </summary>
    public static WallNicheSize? SizeFor(float clearHeight)
    {
        foreach (var (height, size, _) in Sizes)
        {
            if (MathF.Abs(height - clearHeight) <= HeightTolerance)
            {
                return size;
            }
        }

        return null;
    }

    /// <summary>
    /// How far the model of a niche of <paramref name="size"/> stands out from the wall, in metres. Only the model has
    /// it: <see cref="Place"/> puts every niche on the wall's face whatever its depth, so a model set into the wall can
    /// replace it without changing the rule.
    /// </summary>
    public static float Depth(WallNicheSize size)
    {
        foreach (var (_, each, depth) in Sizes)
        {
            if (each == size)
            {
                return depth;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(size), size, null);
    }

    /// <summary>The file of the niche of <paramref name="size"/> for <paramref name="variant"/>, e.g. wall_niche_room_b.glb.</summary>
    public static string FileName(WallNicheSize size, TextureVariant variant)
    {
        if (variant is not (TextureVariant.A or TextureVariant.B or TextureVariant.C))
        {
            throw new ArgumentOutOfRangeException(nameof(variant), variant, null);
        }

        _ = Depth(size);
        return $"wall_niche_{size.ToString().ToLowerInvariant()}_{variant.ToString().ToLowerInvariant()}.glb";
    }

    /// <summary>
    /// Where a niche stands against the <paramref name="wall"/> side of the cell (<paramref name="x"/>,
    /// <paramref name="z"/>) of <paramref name="level"/>, and which size it takes: on the cell's floor in the middle of
    /// that wall, looking into the cell.
    /// <para>
    /// A niche may stand there only if the cell is open floor with a clear height (ceiling minus floor) a niche is built
    /// for, the cell behind that side is wall, and the cells on both sides along the wall are open floor at the same
    /// height with wall behind them too - the frame reaches into them, so it must not hang into a corner, an opening or a
    /// door, nor stand over a step. Wall pillars are no cells; keeping niches away from them is up to the layout.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentException">If no niche may stand there.</exception>
    public static (Vector3 Position, Vector3 Facing, WallNicheSize Size) Place(Level level, int x, int z, RoomSide wall)
    {
        ArgumentNullException.ThrowIfNull(level);
        var (facing, along) = Axes(wall);
        var cell = level[x, z];
        if (cell.IsSolid)
        {
            throw new ArgumentException($"Cell ({x}, {z}) is no open floor.", nameof(level));
        }

        var size = SizeFor(cell.CeilingHeight - cell.FloorHeight)
            ?? throw new ArgumentException($"Cell ({x}, {z}) has a clear height no niche is built for.", nameof(level));
        if (!WallBehind(level, x, z, facing))
        {
            throw new ArgumentException($"Cell ({x}, {z}) has no wall on its {wall} side.", nameof(level));
        }

        foreach (var side in (ReadOnlySpan<int>)[-1, 1])
        {
            var (nx, nz) = (x + (side * (int)along.X), z + (side * (int)along.Z));
            var next = level[nx, nz];
            if (next.IsSolid || MathF.Abs(next.FloorHeight - cell.FloorHeight) > HeightTolerance
                || !WallBehind(level, nx, nz, facing))
            {
                throw new ArgumentException(
                    $"The niche at cell ({x}, {z}) would reach into cell ({nx}, {nz}), which has no plain wall on its {wall} side.",
                    nameof(level));
            }
        }

        return (level.CellCenter(x, z) - (facing * (Level.CellSize / 2f)), facing, size);
    }

    /// <summary>
    /// The file to draw a niche from: the one of <paramref name="wanted"/> (the variant of the wall behind it) if it was
    /// loaded, else the one of variant A, else none - then the niche is not drawn, the wall stays bare.
    /// </summary>
    public static TextureVariant? Pick(TextureVariant wanted, bool wantedLoaded, bool variantALoaded) =>
        WallPillarLook.Pick(wanted, wantedLoaded, variantALoaded);

    // The direction the niche looks (away from the wall into the cell) and the direction along the wall.
    private static (Vector3 Facing, Vector3 Along) Axes(RoomSide wall) => wall switch
    {
        RoomSide.North => (Vector3.UnitZ, Vector3.UnitX),
        RoomSide.South => (-Vector3.UnitZ, Vector3.UnitX),
        RoomSide.West => (Vector3.UnitX, Vector3.UnitZ),
        RoomSide.East => (-Vector3.UnitX, Vector3.UnitZ),
        _ => throw new ArgumentOutOfRangeException(nameof(wall), wall, null),
    };

    private static bool WallBehind(Level level, int x, int z, Vector3 facing) =>
        level[x - (int)facing.X, z - (int)facing.Z].IsSolid;
}