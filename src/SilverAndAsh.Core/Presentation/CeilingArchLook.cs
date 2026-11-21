using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The ceiling arch (issue #537, docs/design/formenlehre-gotik.md): a pointed arch of stone across a corridor one cell
/// wide, standing on a flat pillar at each wall. Pure decoration - no body, no collision, nothing it does to a step or a
/// shot. One model per wall variant, assets/models/ceiling_arch_{a,b,c}.glb, built by tools/models/ceiling_arch.py on the
/// real wall texture of its variant, one level of the palette brighter.
/// <para>
/// File space: origin in the middle of the corridor on the floor, in the plane of the arch; the corridor runs along z, its
/// walls stand at x = -1 and x = +1, its ceiling lies at y = 3.5. The pillars' texture runs in the grid of the wall (one
/// tile per metre), so it only lines up with the wall where the arch's plane sits on a whole metre along the corridor -
/// <see cref="Place"/> puts it through the middle of a cell, which always is.
/// </para>
/// </summary>
public static class CeilingArchLook
{
    /// <summary>The arch itself, from wall to wall.</summary>
    public const string Arch = "arch";

    /// <summary>The pillar at the wall on the file's -x side.</summary>
    public const string PillarLeft = "pillar_left";

    /// <summary>The pillar at the wall on the file's +x side.</summary>
    public const string PillarRight = "pillar_right";

    /// <summary>The ceiling the arch is built for: the corridor's, in metres.</summary>
    public const float CeilingHeight = Cell.DefaultCeilingHeight;

    /// <summary>The width of the corridor the arch spans: one cell, in metres.</summary>
    public const float Width = Level.CellSize;

    private static readonly RigPart[] AllParts = [new(Arch, null), new(PillarLeft, null), new(PillarRight, null)];

    /// <summary>The parts in the order of the rig: the arch and its two pillars, fixed.</summary>
    public static IReadOnlyList<RigPart> Parts => AllParts;

    /// <summary>The file of the arch for a wall of <paramref name="variant"/>, e.g. ceiling_arch_b.glb.</summary>
    public static string FileName(TextureVariant variant)
    {
        if (variant is not (TextureVariant.A or TextureVariant.B or TextureVariant.C))
        {
            throw new ArgumentOutOfRangeException(nameof(variant), variant, null);
        }

        return $"ceiling_arch_{variant.ToString().ToLowerInvariant()}.glb";
    }

    /// <summary>
    /// Where an arch stands across the walkable cell (<paramref name="x"/>, <paramref name="z"/>) of
    /// <paramref name="level"/>: on its floor in its middle, looking along the corridor. The cell must lie in a corridor
    /// one cell wide under the corridor ceiling - walls on two opposite sides, open floor on the other two.
    /// </summary>
    /// <exception cref="ArgumentException">If the cell is no such corridor cell.</exception>
    public static (Vector3 Position, Vector3 Facing) Place(Level level, int x, int z)
    {
        ArgumentNullException.ThrowIfNull(level);
        var cell = level[x, z];
        if (cell.IsSolid || cell.CeilingHeight != CeilingHeight)
        {
            throw new ArgumentException($"Cell ({x}, {z}) is no open floor under a ceiling of {CeilingHeight} m.", nameof(level));
        }

        var facing = Along(level, x, z)
            ?? throw new ArgumentException($"Cell ({x}, {z}) lies in no corridor one cell wide.", nameof(level));
        return (level.CellCenter(x, z), facing);
    }

    /// <summary>
    /// The file to draw an arch from: the one of <paramref name="wanted"/> (the variant of the walls it stands on) if it
    /// was loaded, else the one of variant A, else none - then the arch is not drawn.
    /// </summary>
    public static TextureVariant? Pick(TextureVariant wanted, bool wantedLoaded, bool variantALoaded) =>
        WallPillarLook.Pick(wanted, wantedLoaded, variantALoaded);

    // The direction of the corridor through (x, z): walls west and east -> along z, walls north and south -> along x.
    private static Vector3? Along(Level level, int x, int z)
    {
        bool Solid(int dx, int dz) => level[x + dx, z + dz].IsSolid;
        if (Solid(-1, 0) && Solid(1, 0) && !Solid(0, -1) && !Solid(0, 1))
        {
            return Vector3.UnitZ;
        }

        if (Solid(0, -1) && Solid(0, 1) && !Solid(-1, 0) && !Solid(1, 0))
        {
            return Vector3.UnitX;
        }

        return null;
    }
}
