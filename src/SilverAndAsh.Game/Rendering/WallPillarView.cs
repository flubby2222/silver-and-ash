using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The wall pillars of the showroom (issue #536, see <see cref="WallPillarLook"/>): one model per size and wall variant,
/// drawn in the variant the level really draws on the wall behind it. Pure decoration and pure presentation - where a
/// pillar stands decides Core. Without a model of the variant and of variant A the pillar is left out, the wall stays bare.
/// </summary>
internal sealed class WallPillarView : IDisposable
{
    private readonly Dictionary<(WallPillarSize Size, TextureVariant Variant), TexturedModel> _models;
    private readonly Matrix4x4[] _turns = new Matrix4x4[1];
    private readonly Matrix4x4[] _frames = new Matrix4x4[1];

    private WallPillarView(Dictionary<(WallPillarSize, TextureVariant), TexturedModel> models)
    {
        _models = models;
    }

    /// <summary>
    /// Loads the pillar of every size and wall variant from <paramref name="directory"/> (<see cref="WallPillarLook.FileName(WallPillarSize, TextureVariant)"/>).
    /// A missing or broken one: a warning in the log and the fallback for it. Needs the window.
    /// </summary>
    public static WallPillarView Load(string directory, FileLog? log)
    {
        var models = new Dictionary<(WallPillarSize, TextureVariant), TexturedModel>();
        foreach (var size in Enum.GetValues<WallPillarSize>())
        {
            foreach (var variant in new[] { TextureVariant.A, TextureVariant.B, TextureVariant.C })
            {
                var path = Path.Combine(directory, WallPillarLook.FileName(size, variant));
                if (TexturedModel.TryLoad(path, WallPillarLook.Parts, log) is { } model)
                {
                    models[(size, variant)] = model;
                }
            }
        }

        return new WallPillarView(models);
    }

    /// <summary>
    /// Draws every pillar of <paramref name="pillars"/> that <paramref name="view"/> may see. <paramref name="textures"/>:
    /// the wall textures really loaded (a missing B or C falls back to A on the wall, and so does its pillar). Call between
    /// BeginFigures and EndFigures.
    /// </summary>
    public void Draw(IReadOnlyList<ExhibitWallPillar> pillars, ViewCone view, LevelTextures textures)
    {
        foreach (var pillar in pillars)
        {
            if (WallPillarLook.SizeFor(pillar.CeilingHeight) is not { } size)
            {
                continue;
            }

            var height = WallPillarLook.Height(size);
            if (!view.MaySee(pillar.Position + new Vector3(0f, height / 2f, 0f), (height / 2f) + WallPillarLook.Width)
                || Model(size, textures.Resolve(SurfaceKind.Wall, pillar.Variant)) is not { } model)
            {
                continue;
            }

            // Nothing of the pillar moves in its file: it is turned to look into the cell and stood on the wall's face.
            _turns.AsSpan().Fill(Matrix4x4.Identity);
            model.Rig.Pose(_turns, CryptWardenViews.Place(pillar.Position, pillar.Facing), _frames);
            model.DrawPart(0, _frames[0]);
        }
    }

    public void Dispose()
    {
        foreach (var model in _models.Values)
        {
            model.Dispose();
        }
    }

    private TexturedModel? Model(WallPillarSize size, TextureVariant wanted)
    {
        var pick = WallPillarLook.Pick(wanted, _models.ContainsKey((size, wanted)), _models.ContainsKey((size, TextureVariant.A)));
        return pick is { } variant ? _models[(size, variant)] : null;
    }
}