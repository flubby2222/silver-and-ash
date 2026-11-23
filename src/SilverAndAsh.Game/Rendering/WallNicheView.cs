using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The wall niches of the showroom (issues #538 and #553, see <see cref="WallNicheLook"/>): one model per form, size and
/// wall variant, drawn in the variant the level really draws on the wall behind it. Pure decoration and pure presentation -
/// where a niche stands, which size and form it takes and where the wall face leaves a hole for a deep one decides Core.
/// Without a model of the variant and of variant A the niche is left out and the wall stays bare: for a deep one the
/// level is drawn without its hole (<see cref="AsDrawn"/>). A deep niche shows its filling (issue #553) if that model loaded.
/// </summary>
internal sealed class WallNicheView : IDisposable
{
    private const int MostParts = 8;

    // By file name: both sizes of a deep niche share one file.
    private readonly Dictionary<string, TexturedModel> _models;
    private readonly Matrix4x4[] _turns = new Matrix4x4[MostParts];
    private readonly Matrix4x4[] _frames = new Matrix4x4[MostParts];

    private WallNicheView(Dictionary<string, TexturedModel> models)
    {
        _models = models;
    }

    /// <summary>
    /// Loads the niche of every form, size and wall variant from <paramref name="directory"/>
    /// (<see cref="WallNicheLook.FileName(WallNicheForm, WallNicheSize, TextureVariant)"/>). A missing or broken one: a
    /// warning in the log and the fallback for it. Needs the window.
    /// </summary>
    public static WallNicheView Load(string directory, FileLog? log)
    {
        var models = new Dictionary<string, TexturedModel>();
        foreach (var form in Enum.GetValues<WallNicheForm>())
        {
            foreach (var size in Enum.GetValues<WallNicheSize>())
            {
                foreach (var variant in new[] { TextureVariant.A, TextureVariant.B, TextureVariant.C })
                {
                    var file = WallNicheLook.FileName(form, size, variant);
                    if (!models.ContainsKey(file)
                        && TexturedModel.TryLoad(Path.Combine(directory, file), WallNicheLook.PartsOf(form), log) is { } model)
                    {
                        models[file] = model;
                    }
                }
            }
        }

        // The fillings of the deep niches (issue #553); a missing one leaves its pocket empty.
        foreach (var filling in Enum.GetValues<WallNicheFilling>())
        {
            if (WallNicheFillingLook.FileName(filling) is { } file
                && TexturedModel.TryLoad(Path.Combine(directory, file), WallNicheFillingLook.PartsOf(filling), log) is { } model)
            {
                models[file] = model;
            }
        }

        return new WallNicheView(models);
    }

    /// <summary>
    /// <paramref name="level"/> as it is drawn with these <paramref name="niches"/>: a deep niche this view cannot draw
    /// (no model of its variant nor of variant A) leaves no hole in the wall face, so the wall stays closed instead of
    /// open into the rock (<see cref="WallNicheLook.WithoutHolesOfUndrawn"/>). The same level if every niche is drawn.
    /// </summary>
    public Level AsDrawn(Level level, IReadOnlyList<ExhibitWallNiche> niches, LevelTextures textures) =>
        WallNicheLook.WithoutHolesOfUndrawn(
            level, niches, niche => Model(niche, textures.Resolve(SurfaceKind.Wall, niche.Variant)) is not null);

    /// <summary>
    /// Draws every niche of <paramref name="niches"/> that <paramref name="view"/> may see. <paramref name="textures"/>:
    /// the wall textures really loaded (a missing B or C falls back to A on the wall, and so does its niche). Call between
    /// BeginFigures and EndFigures.
    /// </summary>
    public void Draw(IReadOnlyList<ExhibitWallNiche> niches, ViewCone view, LevelTextures textures)
    {
        foreach (var niche in niches)
        {
            var (width, height) = WallNicheLook.Extent(niche.Form);
            if (!view.MaySee(niche.Position + new Vector3(0f, height / 2f, 0f), (height / 2f) + width)
                || Model(niche, textures.Resolve(SurfaceKind.Wall, niche.Variant)) is not { } model)
            {
                continue;
            }

            // Nothing of the niche moves in its file: it is turned to look into the cell and set on the wall's face.
            DrawFixed(model, niche.Position, niche.Facing);

            // Its filling only with the niche: without the pocket around it, it would stand in the wall.
            if (WallNicheFillingLook.FileName(niche.Filling) is { } file && _models.TryGetValue(file, out var filling))
            {
                var (position, facing) = WallNicheFillingLook.Place(niche.Form, niche.Filling, niche.Position, niche.Facing);
                DrawFixed(filling, position, facing);
            }
        }
    }

    private void DrawFixed(TexturedModel model, Vector3 position, Vector3 facing)
    {
        var count = model.Rig.Count;
        var turns = _turns.AsSpan(0, count);
        var frames = _frames.AsSpan(0, count);
        turns.Fill(Matrix4x4.Identity);
        model.Rig.Pose(turns, ModelPlacement.Place(position, facing), frames);
        for (var part = 0; part < count; part++)
        {
            model.DrawPart(part, frames[part]);
        }
    }

    public void Dispose()
    {
        foreach (var model in _models.Values)
        {
            model.Dispose();
        }
    }

    private TexturedModel? Model(ExhibitWallNiche niche, TextureVariant wanted)
    {
        var file = WallNicheLook.FileName(niche.Form, niche.Size, wanted);
        var fallback = WallNicheLook.FileName(niche.Form, niche.Size, TextureVariant.A);
        var pick = WallNicheLook.Pick(wanted, _models.ContainsKey(file), _models.ContainsKey(fallback));
        return pick is { } variant ? _models[WallNicheLook.FileName(niche.Form, niche.Size, variant)] : null;
    }
}
