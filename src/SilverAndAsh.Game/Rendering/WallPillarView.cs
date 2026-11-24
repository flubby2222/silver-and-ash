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
    private readonly WallVariantModels<WallPillarSize> _models;
    private readonly Matrix4x4[] _turns = new Matrix4x4[1];
    private readonly Matrix4x4[] _frames = new Matrix4x4[1];

    private WallPillarView(WallVariantModels<WallPillarSize> models)
    {
        _models = models;
    }

    /// <summary>
    /// Loads the pillar of every size and wall variant from <paramref name="directory"/> (<see cref="WallPillarLook.FileName(WallPillarSize, TextureVariant)"/>).
    /// A missing or broken one: a warning in the log and the fallback for it. Needs the window.
    /// </summary>
    public static WallPillarView Load(string directory, FileLog? log) =>
        new(WallVariantModels<WallPillarSize>.Load(
            directory, Enum.GetValues<WallPillarSize>(), WallPillarLook.FileName, _ => WallPillarLook.Parts, log));

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
                || _models.Pick(size, textures.Resolve(SurfaceKind.Wall, pillar.Variant)) is not { } model)
            {
                continue;
            }

            // Nothing of the pillar moves in its file: it is turned to look into the cell and stood on the wall's face.
            _turns.AsSpan().Fill(Matrix4x4.Identity);
            model.Rig.Pose(_turns, ModelPlacement.Place(pillar.Position, pillar.Facing), _frames);
            model.DrawPart(0, _frames[0]);
        }
    }

    public void Dispose() => _models.Dispose();
}