using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.Showroom;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The status display on an enemy (issue #506), a placeholder until the trial piece of the effects: a shimmer of small
/// glowing motes in the effect's colour circling the body, one ring per effect, and the time left of each effect as a
/// small number above the head. What is shown comes from <see cref="EnemyStatusReadout"/>; here it is only drawn.
/// </summary>
internal static class StatusView
{
    private const int MotesPerRing = 4;
    private const float MoteSize = 0.05f;
    private const float RingGap = 0.1f;          // the ring circles this far outside the body
    private const float TurnPerTick = 0.05f;     // radians: about half a turn per second
    private const int LineGap = 1;               // pixels between two timers
    private static readonly Color Shadow = new(10, 8, 12, 255);

    /// <summary>
    /// The shimmer: per effect a ring of motes round the body, the first effect low, the next ones higher. It turns with
    /// the world's ticks, so a photo of the same tick looks the same. Call between BeginFigures and EndFigures, after the
    /// enemies.
    /// </summary>
    public static void DrawShimmer(IReadOnlyList<EnemyStatusReadout> readouts, long ticks)
    {
        var size = new Vector3(MoteSize);
        foreach (var readout in readouts)
        {
            var radius = readout.Radius + RingGap;
            for (var ring = 0; ring < readout.Lines.Count; ring++)
            {
                var line = readout.Lines[ring];
                var colour = new Color(line.Colour.R, line.Colour.G, line.Colour.B, (byte)255);
                var height = readout.Height * (0.3f + (0.25f * ring));
                var turn = (ticks % 100_000) * TurnPerTick * (ring % 2 == 0 ? 1f : -1f);
                for (var mote = 0; mote < MotesPerRing; mote++)
                {
                    var angle = turn + (mote * MathF.Tau / MotesPerRing) + (ring * 0.4f);
                    var bob = 0.04f * MathF.Sin((turn * 2f) + mote);
                    var at = readout.Feet + new Vector3(MathF.Cos(angle) * radius, height + bob, MathF.Sin(angle) * radius);
                    ShadedBox.DrawGlowing(Matrix4x4.CreateTranslation(at), size, colour);
                }
            }
        }
    }

    /// <summary>
    /// The time left of every effect that shows one, one small number per line above the head, the first effect on
    /// top; drawn into the small picture (its big pixels), so call after EndMode3D, before picture.EndDrawing. Hidden
    /// behind a wall or behind the camera: nothing.
    /// </summary>
    /// <param name="camera">The camera the 3D picture was drawn with.</param>
    /// <param name="pictureWidth">Width of the low-resolution picture.</param>
    /// <param name="pictureHeight">Height of the low-resolution picture.</param>
    public static void DrawTimers(
        IReadOnlyList<EnemyStatusReadout> readouts, Level level, Camera3D camera, int pictureWidth, int pictureHeight)
    {
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        var lineHeight = PixelText.CapHeight(1) + 2 + LineGap;
        foreach (var readout in readouts)
        {
            var timers = readout.Lines.Where(line => line.Timer is not null).ToList();
            var anchor = readout.Anchor;
            if (timers.Count == 0 || Vector3.Dot(anchor - camera.Position, forward) < 0.5f
                || !ShowroomLabelSight.Sees(level, camera.Position, anchor))
            {
                continue;
            }

            var point = Raylib.GetWorldToScreenEx(anchor, camera, pictureWidth, pictureHeight);
            var top = (int)point.Y - (timers.Count * lineHeight);
            foreach (var line in timers)
            {
                var text = line.Timer!;
                var left = (int)point.X - (PixelText.MeasureScaled(text, 1) / 2);
                var colour = new Color(line.Colour.R, line.Colour.G, line.Colour.B, (byte)255);
                PixelText.DrawScaled(text, left + 1, top + 1, 1, Shadow);
                PixelText.DrawScaled(text, left, top, 1, colour);
                top += lineHeight;
            }
        }
    }
}
