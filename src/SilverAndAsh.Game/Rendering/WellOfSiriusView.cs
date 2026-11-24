using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Sirius' Relief (issue #438, see <see cref="WellOfSiriusLook"/>): the ready model with flowing water, or the dry
/// spent one. Pure presentation. Without its model a stone placeholder stands in (a basin and a column).
/// </summary>
internal sealed class WellOfSiriusView : IDisposable
{
    // Farthest point of the model from its middle at half height: about 1.3 m (1.70 x 2.30 x 1.96 m). A little more.
    private const float BoundingRadius = 1.6f;

    private static readonly Color Stone = new(120, 114, 108, 255);

    private readonly TexturedModel? _ready;
    private readonly TexturedModel? _spent;
    private readonly Matrix4x4[] _turns = new Matrix4x4[WellOfSiriusLook.Parts(spent: false).Count];
    private readonly Matrix4x4[] _frames = new Matrix4x4[WellOfSiriusLook.Parts(spent: false).Count];

    private WellOfSiriusView(TexturedModel? ready, TexturedModel? spent)
    {
        _ready = ready;
        _spent = spent;
    }

    /// <summary>Loads both models from <paramref name="directory"/>. A missing or broken one: a warning in the log and the placeholder. Needs the window.</summary>
    public static WellOfSiriusView Load(string directory, FileLog? log) => new(
        TexturedModel.TryLoad(Path.Combine(directory, WellOfSiriusLook.FileName(spent: false)), WellOfSiriusLook.Parts(spent: false), log),
        TexturedModel.TryLoad(Path.Combine(directory, WellOfSiriusLook.FileName(spent: true)), WellOfSiriusLook.Parts(spent: true), log));

    /// <summary>Draws the well if there is one and <paramref name="view"/> may see it. Call between BeginFigures and EndFigures.</summary>
    public void Draw(WellOfSirius? well, float seconds, ViewCone view)
    {
        if (well is null || !view.MaySee(well.Position + new Vector3(0f, WellOfSirius.Height / 2f, 0f), BoundingRadius))
        {
            return;
        }

        var place = Matrix4x4.CreateTranslation(well.Position);
        if ((well.IsSpent ? _spent : _ready) is not { } model)
        {
            DrawPlaceholder(place);
            return;
        }

        // Nothing moves in the file: every part stands where it is; only the water's texture flows.
        var parts = WellOfSiriusLook.Parts(well.IsSpent);
        var turns = _turns.AsSpan(0, parts.Count);
        var frames = _frames.AsSpan(0, parts.Count);
        turns.Fill(Matrix4x4.Identity);
        model.Rig.Pose(turns, place, frames);
        var frame = WellOfSiriusLook.Frame(seconds);
        for (var i = 0; i < parts.Count; i++)
        {
            model.DrawPart(i, frames[i], uvOffset: WellOfSiriusLook.UvOffset(parts[i].Name, frame));
        }
    }

    public void Dispose()
    {
        _ready?.Dispose();
        _spent?.Dispose();
    }

    // A basin and a column of stone, as wide as the well's solid circle.
    private static void DrawPlaceholder(Matrix4x4 place)
    {
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.3f, 0f) * place, new Vector3(1.6f, 0.6f, 1.6f), Stone);
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, WellOfSirius.Height / 2f, 0f) * place, new Vector3(0.3f, WellOfSirius.Height, 0.3f), Stone);
    }
}
