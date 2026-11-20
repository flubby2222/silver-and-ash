using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The teleporters of the level (issue #480, see <see cref="TeleporterLook"/>): the locked or the unlocked model, the
/// crystal of an unlocked one shining by itself. Pure presentation - which one is unlocked decides Core. Without its model
/// a placeholder stands in (a flat dais and a small crystal).
/// </summary>
internal sealed class TeleporterView : IDisposable
{
    // The model is 1.76 m wide and 2.65 m high; from the middle at half height a corner lies about 1.6 m away. A little more.
    private const float BoundingRadius = 1.8f;
    private const float HalfHeight = 1.35f;

    private static readonly Color Stone = new(110, 106, 104, 255);
    private static readonly Color CrystalDark = new(70, 74, 90, 255);
    private static readonly Color CrystalBright = new(170, 220, 240, 255);

    private readonly TexturedModel? _locked;
    private readonly TexturedModel? _unlocked;
    private readonly Matrix4x4[] _turns = new Matrix4x4[TeleporterLook.Parts.Count];
    private readonly Matrix4x4[] _frames = new Matrix4x4[TeleporterLook.Parts.Count];

    private TeleporterView(TexturedModel? locked, TexturedModel? unlocked)
    {
        _locked = locked;
        _unlocked = unlocked;
    }

    /// <summary>Loads both models from <paramref name="directory"/>. A missing or broken one: a warning in the log and the placeholder. Needs the window.</summary>
    public static TeleporterView Load(string directory, FileLog? log) => new(
        TexturedModel.TryLoad(Path.Combine(directory, TeleporterLook.FileName(unlocked: false)), TeleporterLook.Parts, log),
        TexturedModel.TryLoad(Path.Combine(directory, TeleporterLook.FileName(unlocked: true)), TeleporterLook.Parts, log));

    /// <summary>Draws every teleporter <paramref name="view"/> may see. Call between BeginFigures and EndFigures.</summary>
    public void Draw(IReadOnlyList<Teleporter> teleporters, ViewCone view, LevelShader shader)
    {
        foreach (var teleporter in teleporters)
        {
            if (view.MaySee(teleporter.Position + new Vector3(0f, HalfHeight, 0f), BoundingRadius))
            {
                Draw(teleporter, shader);
            }
        }
    }

    public void Dispose()
    {
        _locked?.Dispose();
        _unlocked?.Dispose();
    }

    private void Draw(Teleporter teleporter, LevelShader shader)
    {
        var place = Matrix4x4.CreateTranslation(teleporter.Position);
        var unlocked = teleporter.IsUnlocked;
        if ((unlocked ? _unlocked : _locked) is not { } model)
        {
            DrawPlaceholder(place, unlocked);
            return;
        }

        // Nothing moves in the file: every part stands where it is.
        var parts = TeleporterLook.Parts;
        _turns.AsSpan().Fill(Matrix4x4.Identity);
        model.Rig.Pose(_turns, place, _frames);
        for (var i = 0; i < parts.Count; i++)
        {
            var glows = TeleporterLook.Glows(parts[i].Name, unlocked);
            if (glows)
            {
                shader.SetGlow(TeleporterLook.Glow);
            }

            model.DrawPart(i, _frames[i], unlit: glows);
            if (glows)
            {
                shader.SetGlow(0f);
            }
        }
    }

    // The dais and a crystal at its height in the model.
    private static void DrawPlaceholder(Matrix4x4 place, bool unlocked)
    {
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.1f, 0f) * place, new Vector3(1.6f, 0.2f, 1.6f), Stone);
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 2.1f, 0f) * place, new Vector3(0.3f, 0.6f, 0.3f), unlocked ? CrystalBright : CrystalDark);
    }
}
