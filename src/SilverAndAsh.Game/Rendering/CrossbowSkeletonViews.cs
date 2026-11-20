using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// How a crossbow skeleton is drawn (issue #497, see <see cref="CrossbowSkeletonLook"/>) - swappable like the other
/// figures: the model of its pose, or boxes without the files. Pure presentation; where it stands and in which pose
/// decides Core.
/// </summary>
internal interface ICrossbowSkeletonView : IDisposable
{
    /// <summary>Draws a skeleton in <paramref name="stance"/> standing at <paramref name="position"/>, looking along <paramref name="facing"/>.</summary>
    void Draw(CrossbowSkeletonStance stance, Vector3 position, Vector3 facing);
}

/// <summary>Loading the crossbow skeleton's view and drawing the showroom's skeletons.</summary>
internal static class CrossbowSkeletonViews
{
    // The figure is 1.86 m high and about 1 m across with the crossbow; from the middle at half height a little more than 1.2 m.
    private const float BoundingRadius = 1.3f;

    /// <summary>
    /// Both models from <paramref name="directory"/>, or the placeholder if one of them is missing or broken (a warning
    /// in the log). Needs the window.
    /// </summary>
    public static ICrossbowSkeletonView Load(string directory, FileLog? log)
    {
        var rest = TexturedModel.TryLoad(Path.Combine(directory, CrossbowSkeletonLook.FileName(CrossbowSkeletonStance.Rest)), CrossbowSkeletonLook.Parts, log);
        var announce = TexturedModel.TryLoad(Path.Combine(directory, CrossbowSkeletonLook.FileName(CrossbowSkeletonStance.Announce)), CrossbowSkeletonLook.Parts, log);
        if (rest is not null && announce is not null)
        {
            return new ModelCrossbowSkeletonView(rest, announce);
        }

        rest?.Dispose();
        announce?.Dispose();
        return new PlaceholderCrossbowSkeletonView();
    }

    /// <summary>Draws every skeleton of <paramref name="skeletons"/> that <paramref name="view"/> may see. Call between BeginFigures and EndFigures.</summary>
    public static void Draw(this ICrossbowSkeletonView figure, IReadOnlyList<ExhibitCrossbowSkeleton> skeletons, ViewCone view)
    {
        foreach (var skeleton in skeletons)
        {
            if (view.MaySee(skeleton.Position + new Vector3(0f, CrossbowSkeletonLook.Height / 2f, 0f), BoundingRadius))
            {
                figure.Draw(skeleton.Stance, skeleton.Position, skeleton.Facing);
            }
        }
    }
}

/// <summary>The models: one per pose, nothing moves in the files. The red eyes are in the announce model's picture.</summary>
internal sealed class ModelCrossbowSkeletonView(TexturedModel rest, TexturedModel announce) : ICrossbowSkeletonView
{
    private readonly Matrix4x4[] _turns = new Matrix4x4[CrossbowSkeletonLook.Parts.Count];
    private readonly Matrix4x4[] _frames = new Matrix4x4[CrossbowSkeletonLook.Parts.Count];

    public void Draw(CrossbowSkeletonStance stance, Vector3 position, Vector3 facing)
    {
        var model = stance == CrossbowSkeletonStance.Announce ? announce : rest;
        _turns.AsSpan().Fill(Matrix4x4.Identity);
        model.Rig.Pose(_turns, ModelPlacement.Place(position, facing), _frames);
        model.Draw(_frames);
    }

    public void Dispose()
    {
        rest.Dispose();
        announce.Dispose();
    }
}

/// <summary>Boxes in place of the models: bones, a crossbow lowered or raised, red eyes only while announcing.</summary>
internal sealed class PlaceholderCrossbowSkeletonView : ICrossbowSkeletonView
{
    private static readonly Color Bone = new(196, 186, 160, 255);
    private static readonly Color Wood = new(92, 64, 44, 255);
    private static readonly Color Socket = new(24, 20, 20, 255);
    private static readonly Color Eye = new(170, 24, 24, 255);
    private static readonly Vector3 EyeSize = new(0.04f, 0.03f, 0.02f);

    public void Draw(CrossbowSkeletonStance stance, Vector3 position, Vector3 facing)
    {
        var body = ModelPlacement.Place(position, facing);
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.45f, 0f) * body, new Vector3(0.3f, 0.9f, 0.16f), Bone);
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 1.25f, 0f) * body, new Vector3(0.36f, 0.7f, 0.18f), Bone);
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 1.73f, 0.02f) * body, new Vector3(0.18f, 0.24f, 0.2f), Bone);

        var announce = stance == CrossbowSkeletonStance.Announce;
        var bow = announce
            ? Matrix4x4.CreateTranslation(0.12f, 1.45f, 0.35f)
            : Matrix4x4.CreateRotationX(0.6f) * Matrix4x4.CreateTranslation(0.2f, 0.95f, 0.25f);
        ShadedBox.Draw(bow * body, new Vector3(0.08f, 0.08f, 0.7f), Wood);
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0f, 0.3f) * bow * body, new Vector3(0.6f, 0.04f, 0.05f), Wood);

        foreach (var side in new[] { -1f, 1f })
        {
            var eye = Matrix4x4.CreateTranslation(0.045f * side, 1.76f, 0.125f) * body;
            if (announce)
            {
                ShadedBox.DrawGlowing(eye, EyeSize, Eye);
            }
            else
            {
                ShadedBox.Draw(eye, EyeSize, Socket);
            }
        }
    }

    public void Dispose()
    {
    }
}
