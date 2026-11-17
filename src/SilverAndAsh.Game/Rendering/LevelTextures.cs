using Raylib_cs;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The textures of the walls, floors and ceilings of one layer (assets/textures/, issue #233), on the graphics card -
/// each kind of surface in up to three variants (issue #244). Like the models, every file is first compared with the
/// checked-in checksum list; a missing or broken file costs a warning in the log. A missing variant B or C falls back
/// to A for just the surfaces that chose it; a missing A means that kind of surface is drawn plain as before.
/// </summary>
internal sealed class LevelTextures : IDisposable
{
    private readonly Dictionary<(SurfaceKind Kind, TextureVariant Variant), Texture2D> _textures;

    private LevelTextures(LevelTextureSet set, Dictionary<(SurfaceKind Kind, TextureVariant Variant), Texture2D> textures)
    {
        Set = set;
        _textures = textures;
    }

    /// <summary>The set the textures come from (for the size of a tile).</summary>
    public LevelTextureSet Set { get; }

    /// <summary>
    /// Loads the files of <paramref name="set"/> from <paramref name="directory"/>. Never fails: what cannot be loaded is
    /// left out. Needs the window (the textures go to the graphics card).
    /// </summary>
    public static LevelTextures Load(string directory, LevelTextureSet set, FileLog? log)
    {
        ArgumentNullException.ThrowIfNull(set);
        var textures = new Dictionary<(SurfaceKind Kind, TextureVariant Variant), Texture2D>();
        ModelChecksums? list = null;
        try
        {
            list = ModelChecksums.Parse(File.ReadAllText(Path.Combine(directory, LevelTextureSet.ChecksumFileName)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            log?.Warning($"Level textures in '{directory}': the checksum list could not be read ({exception.Message}) - walls, floors and ceilings are drawn plain.");
        }

        if (list is not null)
        {
            foreach (var kind in Enum.GetValues<SurfaceKind>())
            {
                foreach (var variant in Enum.GetValues<TextureVariant>())
                {
                    var texture = TryLoad(Path.Combine(directory, set.FileFor(kind, variant)), list, variant, log);
                    if (texture is { } loaded)
                    {
                        textures[(kind, variant)] = loaded;
                    }
                }
            }
        }

        return new LevelTextures(set, textures);
    }

    /// <summary>
    /// The variant that surfaces of kind <paramref name="kind"/> which chose <paramref name="variant"/> are really drawn
    /// with: the variant itself if it was loaded, else A.
    /// </summary>
    public TextureVariant Resolve(SurfaceKind kind, TextureVariant variant) =>
        _textures.ContainsKey((kind, variant)) ? variant : TextureVariant.A;

    /// <summary>The texture of <paramref name="variant"/> for surfaces of kind <paramref name="kind"/> (or of A in its place), or null: draw them plain.</summary>
    public Texture2D? For(SurfaceKind kind, TextureVariant variant) =>
        _textures.TryGetValue((kind, Resolve(kind, variant)), out var texture) ? texture : null;

    public void Dispose()
    {
        foreach (var texture in _textures.Values)
        {
            Raylib.UnloadTexture(texture);
        }

        _textures.Clear();
    }

    private static Texture2D? TryLoad(string path, ModelChecksums list, TextureVariant variant, FileLog? log)
    {
        var fallback = variant == TextureVariant.A ? "this surface is drawn plain" : "its surfaces show variant A instead";

        // Byte for byte the file in the repository - a broken picture never reaches raylib.
        var mismatch = list.Check(path);
        if (mismatch is not null)
        {
            log?.Warning($"Level texture '{path}': {mismatch} - {fallback}.");
            return null;
        }

        var texture = Raylib.LoadTexture(path);
        if (!Raylib.IsTextureValid(texture))
        {
            log?.Warning($"Level texture '{path}' could not be loaded - {fallback}.");
            return null;
        }

        // Hard pixels as in the PlayStation era (decision record 0010): LoadTexture makes no mipmaps, point filtering
        // never blends neighbouring texels, and the tile repeats over the whole level.
        Raylib.SetTextureFilter(texture, TextureFilter.Point);
        Raylib.SetTextureWrap(texture, TextureWrap.Repeat);
        log?.Info($"Level texture '{path}' loaded: {texture.Width} x {texture.Height}, {texture.Mipmaps} mipmap level.");
        return texture;
    }
}
