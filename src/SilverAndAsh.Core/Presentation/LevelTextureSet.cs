using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Presentation;

/// <summary>The three layers of the castle (ADR 0005); each may get its own stones.</summary>
public enum LevelLayer
{
    /// <summary>Level 0.</summary>
    Catacombs,

    /// <summary>Level 1.</summary>
    Dungeon,

    /// <summary>Level 2.</summary>
    CastleHall,
}

/// <summary>
/// The textures of one layer: one file per kind of surface in assets/textures/, and the size of one tile in metres
/// (issue #233). Only the catacombs have their own set so far; the other layers use it as well until theirs exist.
/// </summary>
public sealed record LevelTextureSet(string Wall, string Floor, string Ceiling, float TileSize)
{
    /// <summary>Name of the checksum list next to the textures (same format as <see cref="ModelChecksums"/>).</summary>
    public const string ChecksumFileName = "textures.sha256";

    /// <summary>The catacombs: rubble walls, worn slabs, sooty vault - one tile is 1 m (tools/textures/catacomb.py).</summary>
    public static readonly LevelTextureSet Catacombs = new("catacomb_wall.png", "catacomb_floor.png", "catacomb_ceiling.png", 1f);

    /// <summary>The set of <paramref name="layer"/>.</summary>
    public static LevelTextureSet For(LevelLayer layer) => layer switch
    {
        LevelLayer.Catacombs or LevelLayer.Dungeon or LevelLayer.CastleHall => Catacombs,
        _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, "Unknown layer."),
    };

    /// <summary>The file for surfaces of kind <paramref name="kind"/>.</summary>
    public string FileFor(SurfaceKind kind) => kind switch
    {
        SurfaceKind.Floor => Floor,
        SurfaceKind.Ceiling => Ceiling,
        SurfaceKind.Wall => Wall,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown kind of surface."),
    };

    /// <summary>
    /// The file of variant <paramref name="variant"/> for surfaces of kind <paramref name="kind"/> (issue #244): A is
    /// <see cref="FileFor(SurfaceKind)"/> itself, B and C carry "_b" or "_c" before the ending (catacomb_wall_b.png).
    /// </summary>
    public string FileFor(SurfaceKind kind, TextureVariant variant)
    {
        var file = FileFor(kind);
        return variant switch
        {
            TextureVariant.A => file,
            TextureVariant.B or TextureVariant.C =>
                Path.GetFileNameWithoutExtension(file) + "_" + variant.ToString().ToLowerInvariant() + Path.GetExtension(file),
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Unknown variant."),
        };
    }
}
