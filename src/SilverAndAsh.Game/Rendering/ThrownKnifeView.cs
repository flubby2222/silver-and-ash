using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The throwing knives out of the hand (issue #821): those in flight, drawn between two ticks so they move smoothly and
/// tumbling as they fly, and those that stick in a wall or a target. Pure presentation: which knives there are, where
/// and how many is the world's state (<see cref="GameWorld.FlyingKnives"/>, <see cref="GameWorld.StuckKnives"/>); how a
/// knife lies is <see cref="ThrowingKnivesPose"/>. Without the model a small pale box stands in for the blade.
/// </summary>
internal sealed class ThrownKnifeView(TexturedModel? knife)
{
    private static readonly Color Blade = new(206, 210, 222, 255);
    private static readonly Vector3 BoxSize = new(0.004f, 0.02f, ThrowingKnivesPose.BladeLength * ThrowingKnivesPose.FileScale);

    private readonly Matrix4x4[] _turn = [Matrix4x4.Identity];
    private readonly Matrix4x4[] _frame = new Matrix4x4[1];

    /// <summary>Draws every knife. <paramref name="alpha"/> blends between the last two ticks. Call between BeginFigures and EndFigures.</summary>
    public void Draw(IReadOnlyList<ThrownKnife> flying, IReadOnlyList<StuckKnife> stuck, float alpha)
    {
        foreach (var thrown in flying)
        {
            // Between two ticks: the point where it is drawn, and the range it had left there (it turns with the way).
            var point = Vector3.Lerp(thrown.PreviousPosition, thrown.Position, alpha);
            var rangeLeft = thrown.RangeLeft + Vector3.Distance(point, thrown.Position);
            DrawKnife(ThrowingKnivesPose.Flying(point, thrown.Direction, rangeLeft));
        }

        foreach (var held in stuck)
        {
            DrawKnife(ThrowingKnivesPose.Stuck(held.Point, held.Direction, held.Id));
        }
    }

    private void DrawKnife(Matrix4x4 pose)
    {
        if (knife is null)
        {
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0f, BoxSize.Z / 2f) * pose, BoxSize, Blade);
            return;
        }

        knife.Rig.Pose(_turn, pose, _frame);
        knife.DrawPart(0, _frame[0]);
    }
}
