using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The models of a piece that comes in one file per wall variant (issue #587): ceiling arch, wall pillar, wall niche,
/// door frame, guide ring of the portcullis, spiral staircase. <typeparamref name="TKey"/> tells apart the files of one
/// variant (a size, a form, a kind of door); <see cref="WallVariantModels"/> loads a piece without one. Which file is
/// drawn decides <see cref="WallVariantChoice.Pick"/> in Core.
/// </summary>
internal sealed class WallVariantModels<TKey> : IDisposable
    where TKey : notnull
{
    private readonly Dictionary<(TKey Key, TextureVariant Variant), TexturedModel> _models;

    private WallVariantModels(Dictionary<(TKey Key, TextureVariant Variant), TexturedModel> models)
    {
        _models = models;
    }

    /// <summary>
    /// Loads the file of every key of <paramref name="keys"/> and every wall variant from <paramref name="directory"/>,
    /// named by <paramref name="fileName"/>, with the rig <paramref name="parts"/> gives. Keys that name the same file
    /// share one model, loaded once. A missing or broken file: a warning in the log, and <see cref="Pick"/> falls back.
    /// Needs the window.
    /// </summary>
    public static WallVariantModels<TKey> Load(
        string directory,
        IEnumerable<TKey> keys,
        Func<TKey, TextureVariant, string> fileName,
        Func<TKey, IReadOnlyList<RigPart>> parts,
        FileLog? log)
    {
        var byFile = new Dictionary<string, TexturedModel>();
        var models = new Dictionary<(TKey Key, TextureVariant Variant), TexturedModel>();
        foreach (var key in keys)
        {
            foreach (var variant in Enum.GetValues<TextureVariant>())
            {
                var file = fileName(key, variant);
                if (byFile.TryGetValue(file, out var loaded))
                {
                    models[(key, variant)] = loaded;
                }
                else if (TexturedModel.TryLoad(Path.Combine(directory, file), parts(key), log) is { } model)
                {
                    byFile[file] = model;
                    models[(key, variant)] = model;
                }
            }
        }

        return new WallVariantModels<TKey>(models);
    }

    /// <summary>
    /// The model to draw the piece of <paramref name="key"/> with where the level draws its walls in
    /// <paramref name="wanted"/>: that variant's, else variant A's, else none (<see cref="WallVariantChoice.Pick"/>).
    /// </summary>
    public TexturedModel? Pick(TKey key, TextureVariant wanted)
    {
        var pick = WallVariantChoice.Pick(wanted, _models.ContainsKey((key, wanted)), _models.ContainsKey((key, TextureVariant.A)));
        return pick is { } variant ? _models[(key, variant)] : null;
    }

    public void Dispose()
    {
        foreach (var model in _models.Values.Distinct())
        {
            model.Dispose();
        }
    }
}

/// <summary>
/// <see cref="WallVariantModels{TKey}"/> for a piece with one file per wall variant and nothing else to tell its files
/// apart (ceiling arch, guide ring, staircase).
/// </summary>
internal static class WallVariantModels
{
    /// <summary>Loads the file of every wall variant, named by <paramref name="fileName"/>. Needs the window.</summary>
    public static WallVariantModels<ValueTuple> Load(
        string directory, Func<TextureVariant, string> fileName, IReadOnlyList<RigPart> parts, FileLog? log) =>
        WallVariantModels<ValueTuple>.Load(directory, [default], (_, variant) => fileName(variant), _ => parts, log);

    /// <summary>The model to draw where the level draws its walls in <paramref name="wanted"/>.</summary>
    public static TexturedModel? Pick(this WallVariantModels<ValueTuple> models, TextureVariant wanted) =>
        models.Pick(default, wanted);
}
