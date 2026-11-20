using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// How a crypt warden is drawn (issue #530, see <see cref="CryptWardenLook"/>) - swappable like the other figures: the
/// model of its pose, or boxes without the files. Pure presentation; where it stands and in which pose decides Core.
/// </summary>
internal interface ICryptWardenView : IDisposable
{
    /// <summary>Draws a warden in <paramref name="stance"/> standing at <paramref name="position"/>, looking along <paramref name="facing"/>.</summary>
    void Draw(CryptWardenStance stance, Vector3 position, Vector3 facing);
}

/// <summary>Loading the crypt warden's view and drawing the showroom's wardens.</summary>
internal static class CryptWardenViews
{
    /// <summary>
    /// Both models from <paramref name="directory"/>, or the placeholder if one of them is missing or broken (a warning
    /// in the log). Needs the window.
    /// </summary>
    public static ICryptWardenView Load(string directory, FileLog? log)
    {
        var rest = TexturedModel.TryLoad(Path.Combine(directory, CryptWardenLook.FileName(CryptWardenStance.Rest)), CryptWardenLook.Parts, log);
        var announce = TexturedModel.TryLoad(Path.Combine(directory, CryptWardenLook.FileName(CryptWardenStance.Announce)), CryptWardenLook.Parts, log);
        if (rest is not null && announce is not null)
        {
            return new ModelCryptWardenView(rest, announce);
        }

        rest?.Dispose();
        announce?.Dispose();
        return new PlaceholderCryptWardenView();
    }

    /// <summary>Draws every warden of <paramref name="wardens"/> that <paramref name="view"/> may see. Call between BeginFigures and EndFigures.</summary>
    public static void Draw(this ICryptWardenView figure, IReadOnlyList<ExhibitCryptWarden> wardens, ViewCone view)
    {
        foreach (var warden in wardens)
        {
            if (view.MaySee(warden.Position + new Vector3(0f, CryptWardenLook.Height / 2f, 0f), CryptWardenLook.BoundingRadius))
            {
                figure.Draw(warden.Stance, warden.Position, warden.Facing);
            }
        }
    }

    /// <summary>Turns a figure looking along +Z to <paramref name="facing"/> and stands it at <paramref name="position"/>.</summary>
    public static Matrix4x4 Place(Vector3 position, Vector3 facing) =>
        Matrix4x4.CreateRotationY(MathF.Atan2(facing.X, facing.Z)) * Matrix4x4.CreateTranslation(position);
}

/// <summary>The models: one per pose, nothing moves in the files. The red eye is in the announce model's picture.</summary>
internal sealed class ModelCryptWardenView(TexturedModel rest, TexturedModel announce) : ICryptWardenView
{
    private readonly Matrix4x4[] _turns = new Matrix4x4[CryptWardenLook.Parts.Count];
    private readonly Matrix4x4[] _frames = new Matrix4x4[CryptWardenLook.Parts.Count];

    public void Draw(CryptWardenStance stance, Vector3 position, Vector3 facing)
    {
        var model = stance == CryptWardenStance.Announce ? announce : rest;
        _turns.AsSpan().Fill(Matrix4x4.Identity);
        model.Rig.Pose(_turns, CryptWardenViews.Place(position, facing), _frames);
        model.Draw(_frames);
    }

    public void Dispose()
    {
        rest.Dispose();
        announce.Dispose();
    }
}

/// <summary>Boxes in place of the models: legs, body, head, a pole at rest or raised, a red eye only while announcing.</summary>
internal sealed class PlaceholderCryptWardenView : ICryptWardenView
{
    private static readonly Color Cloth = new(60, 56, 64, 255);
    private static readonly Color Wood = new(92, 64, 44, 255);
    private static readonly Color Socket = new(24, 20, 20, 255);
    private static readonly Color Eye = new(170, 24, 24, 255);
    private static readonly Vector3 EyeSize = new(0.07f, 0.05f, 0.03f);

    public void Draw(CryptWardenStance stance, Vector3 position, Vector3 facing)
    {
        var body = CryptWardenViews.Place(position, facing);
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.6f, 0f) * body, new Vector3(0.6f, 1.2f, 0.4f), Cloth);
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 1.6f, 0f) * body, new Vector3(0.9f, 0.9f, 0.55f), Cloth);
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 2.25f, 0.05f) * body, new Vector3(0.4f, 0.5f, 0.4f), Socket);

        var announce = stance == CryptWardenStance.Announce;
        var pole = announce
            ? Matrix4x4.CreateRotationX(-0.6f) * Matrix4x4.CreateTranslation(0.7f, 2.0f, 0.1f)
            : Matrix4x4.CreateTranslation(0.6f, 1.1f, 0.2f);
        ShadedBox.Draw(pole * body, new Vector3(0.1f, 2.2f, 0.1f), Wood);

        var eye = Matrix4x4.CreateTranslation(-0.05f, 2.28f, 0.21f) * body;
        if (announce)
        {
            ShadedBox.DrawGlowing(eye, EyeSize, Eye);
        }
        else
        {
            ShadedBox.Draw(eye, EyeSize, Socket);
        }
    }

    public void Dispose()
    {
    }
}
