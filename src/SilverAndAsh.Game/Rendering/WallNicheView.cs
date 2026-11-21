using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The wall niches of the showroom (issue #538, see <see cref="WallNicheLook"/>): one model per size and wall variant,
/// drawn in the variant the level really draws on the wall behind it. Pure decoration and pure presentation - where a
/// niche stands and which size it takes decides Core. Without a model of the variant and of variant A the niche is left
/// out, the wall stays bare.
/// </summary>
internal sealed class WallNicheView : IDisposable
{
    private readonly Dictionary<(WallNicheSize Size, TextureVariant Variant), TexturedModel> _models;
    private readonly Matrix4x4[] _turns = new Matrix4x4[3];
    private readonly Matrix4x4[] _frames = new Matrix4x4[3];

    private WallNicheView(Dictionary<(WallNicheSize, TextureVariant), TexturedModel> models)
    {
        _models = models;
    }

    /// <summary>
    /// Loads the niche of every size and wall variant from <paramref name="directory"/> (<see cref="WallNicheLook.FileName(WallNicheSize, TextureVariant)"/>).
    /// A missing or broken one: a warning in the log and the fallback for it. Needs the window.
    /// </summary>
    public static WallNicheView Load(string directory, FileLog? log)
    {
        var models = new Dictionary<(WallNicheSize, TextureVariant), TexturedModel>();
        foreach (var size in Enum.GetValues<WallNicheSize>())
        {
            foreach (var variant in new[] { TextureVariant.A, TextureVariant.B, TextureVariant.C })
            {
                var path = Path.Combine(directory, WallNicheLook.FileName(size, variant));
                if (TexturedModel.TryLoad(path, WallNicheLook.Parts, log) is { } model)
                {
                    models[(size, variant)] = model;
                }
            }
        }

        return new WallNicheView(models);
    }

    /// <summary>
    /// Draws every niche of <paramref name="niches"/> that <paramref name="view"/> may see. <paramref name="textures"/>:
    /// the wall textures really loaded (a missing B or C falls back to A on the wall, and so does its niche). Call between
    /// BeginFigures and EndFigures.
    /// </summary>
    public void Draw(IReadOnlyList<ExhibitWallNiche> niches, ViewCone view, LevelTextures textures)
    {
        foreach (var niche in niches)
        {
            const float half = WallNicheLook.Height / 2f;
            if (!view.MaySee(niche.Position + new Vector3(0f, half, 0f), half + WallNicheLook.Width)
                || Model(niche.Size, textures.Resolve(SurfaceKind.Wall, niche.Variant)) is not { } model)
            {
                continue;
            }

            // Nothing of the niche moves in its file: it is turned to look into the cell and set on the wall's face.
            _turns.AsSpan().Fill(Matrix4x4.Identity);
            model.Rig.Pose(_turns, ModelPlacement.Place(niche.Position, niche.Facing), _frames);
            for (var part = 0; part < _frames.Length; part++)
            {
                model.DrawPart(part, _frames[part]);
            }
        }
    }

    public void Dispose()
    {
        foreach (var model in _models.Values)
        {
            model.Dispose();
        }
    }

    private TexturedModel? Model(WallNicheSize size, TextureVariant wanted)
    {
        var pick = WallNicheLook.Pick(wanted, _models.ContainsKey((size, wanted)), _models.ContainsKey((size, TextureVariant.A)));
        return pick is { } variant ? _models[(size, variant)] : null;
    }
}
