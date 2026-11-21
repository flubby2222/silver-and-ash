using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The ceiling arches of the showroom (issue #537, see <see cref="CeilingArchLook"/>): one model per wall variant, drawn
/// in the variant the level really draws on the walls it stands on. Pure decoration and pure presentation - where an arch
/// stands decides Core. Without a model of the variant and of variant A the arch is left out.
/// </summary>
internal sealed class CeilingArchView : IDisposable
{
    // The arch is 2.25 m across and 3.64 m high; from its middle at half height a little more than 2 m.
    private const float BoundingRadius = 2.2f;

    private readonly Dictionary<TextureVariant, TexturedModel> _models;
    private readonly Matrix4x4[] _turns = new Matrix4x4[CeilingArchLook.Parts.Count];
    private readonly Matrix4x4[] _frames = new Matrix4x4[CeilingArchLook.Parts.Count];

    private CeilingArchView(Dictionary<TextureVariant, TexturedModel> models)
    {
        _models = models;
    }

    /// <summary>
    /// Loads the arch of every wall variant from <paramref name="directory"/> (<see cref="CeilingArchLook.FileName"/>).
    /// A missing or broken one: a warning in the log and the fallback for it. Needs the window.
    /// </summary>
    public static CeilingArchView Load(string directory, FileLog? log)
    {
        var models = new Dictionary<TextureVariant, TexturedModel>();
        foreach (var variant in new[] { TextureVariant.A, TextureVariant.B, TextureVariant.C })
        {
            var path = Path.Combine(directory, CeilingArchLook.FileName(variant));
            if (TexturedModel.TryLoad(path, CeilingArchLook.Parts, log) is { } model)
            {
                models[variant] = model;
            }
        }

        return new CeilingArchView(models);
    }

    /// <summary>
    /// Draws every arch of <paramref name="arches"/> that <paramref name="view"/> may see. <paramref name="textures"/>:
    /// the wall textures really loaded (a missing B or C falls back to A on the wall, and so does its arch). Call between
    /// BeginFigures and EndFigures.
    /// </summary>
    public void Draw(IReadOnlyList<ExhibitCeilingArch> arches, ViewCone view, LevelTextures textures)
    {
        foreach (var arch in arches)
        {
            var middle = arch.Position + new Vector3(0f, CeilingArchLook.CeilingHeight / 2f, 0f);
            if (!view.MaySee(middle, BoundingRadius) || Model(textures.Resolve(SurfaceKind.Wall, arch.Variant)) is not { } model)
            {
                continue;
            }

            // Nothing of the arch moves in its file: it is turned to span the corridor and stood in the cell's middle.
            _turns.AsSpan().Fill(Matrix4x4.Identity);
            model.Rig.Pose(_turns, ModelPlacement.Place(arch.Position, arch.Facing), _frames);
            model.Draw(_frames);
        }
    }

    public void Dispose()
    {
        foreach (var model in _models.Values)
        {
            model.Dispose();
        }
    }

    private TexturedModel? Model(TextureVariant wanted)
    {
        var pick = CeilingArchLook.Pick(wanted, _models.ContainsKey(wanted), _models.ContainsKey(TextureVariant.A));
        return pick is { } variant ? _models[variant] : null;
    }
}
