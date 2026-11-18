using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.Showroom;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The small text labels of the showroom (issue #283), drawn sharp at full window resolution over the scaled-up picture.
/// L turns them off and on.
/// </summary>
internal static class ShowroomLabelView
{
    private static readonly Color Text = new(232, 222, 196, 255);
    private static readonly Color Shadow = new(10, 8, 12, 220);

    /// <param name="camera">The camera the 3D picture was drawn with.</param>
    /// <param name="renderWidth">Width of the low-resolution picture.</param>
    /// <param name="renderHeight">Height of the low-resolution picture.</param>
    public static void Draw(IReadOnlyList<ShowroomLabel> labels, Level level, Camera3D camera, int renderWidth, int renderHeight)
    {
        var target = Letterbox.Fit(renderWidth, renderHeight, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        var forward = Vector3.Normalize(camera.Target - camera.Position);
        foreach (var label in labels)
        {
            var toLabel = label.Position - camera.Position;
            if (Vector3.Dot(toLabel, forward) < 0.5f || !ShowroomLabelSight.Sees(level, camera.Position, label.Position))
            {
                continue;
            }

            // Where the label lands in the small picture, then on the screen; nearer labels a little bigger.
            var point = Raylib.GetWorldToScreenEx(label.Position, camera, renderWidth, renderHeight);
            var x = target.X + (point.X * target.Width / renderWidth);
            var y = target.Y + (point.Y * target.Height / renderHeight);
            var fontSize = toLabel.Length() < 6f ? 22 : 18;
            var width = PixelText.Measure(label.Text, fontSize);
            var left = (int)x - (width / 2);
            var top = (int)y - (fontSize / 2);

            // A label that does not fit whole into the picture is left out rather than cut off at its edge.
            if (left - 4 < target.X || left + width + 4 > target.X + target.Width
                || top - 2 < target.Y || top + fontSize + 2 > target.Y + target.Height)
            {
                continue;
            }

            Raylib.DrawRectangle(left - 4, top - 2, width + 8, fontSize + 4, Shadow);
            PixelText.Draw(label.Text, left, top, fontSize, Text);
        }
    }
}
