using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.Showroom;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The status display on an enemy (issue #506): per effect its shape on the body (<see cref="StatusForm"/>) - a shimmer
/// of small glowing motes in the effect's colour circling the body until a trial piece decides, the frost of Grave Chill
/// (issue #775), the sparks of a stun above the head (issue #778) - and the time left of each effect as a small number above the head. What is shown comes from
/// <see cref="EnemyStatusReadout"/>; here it is only drawn. The tint of the body itself is drawn by the enemy's view
/// (<see cref="StatusLooks.TintOf(SilverAndAsh.Core.Gameplay.Ghoul)"/>, <see cref="Tint"/>).
/// </summary>
internal static class StatusView
{
    private const int MotesPerRing = 4;
    private const float MoteSize = 0.05f;
    private const float RingGap = 0.1f;          // the ring circles this far outside the body
    private const float TurnPerTick = 0.05f;     // radians: about half a turn per second
    private const int LineGap = 1;               // pixels between two timers
    private const float DiscLift = 0.015f;       // the disc lies this far above the floor, so the floor does not cover it
    private static readonly Color Shadow = new(10, 8, 12, 255);

    // What draws one effect on one enemy: its readout, the effect's line and its number among the lines (the ring).
    private delegate void FormDrawing(EnemyStatusReadout readout, StatusLine line, int ring, Scene scene);

    // Everything a drawing may need of the frame.
    private readonly record struct Scene(long Ticks, Vector3 Eye, LevelShader Shader);

    // One drawing per shape - a new shape is one more entry here (issue #775), not one more branch in the others.
    private static readonly IReadOnlyDictionary<StatusForm, FormDrawing> Drawings = new Dictionary<StatusForm, FormDrawing>
    {
        [StatusForm.Motes] = DrawMotes,
        [StatusForm.Frost] = DrawFrost,
        [StatusForm.Sparks] = DrawSparks,
    };

    /// <summary>
    /// The shape of every effect on the body: the motes per effect a ring round the body, the first effect low, the next
    /// ones higher, turning with the world's ticks (so a photo of the same tick looks the same); the frost as rime flecks
    /// on the side facing <paramref name="eye"/> and a disc on the floor. Call between BeginFigures and EndFigures, after
    /// the enemies.
    /// </summary>
    public static void DrawShimmer(IReadOnlyList<EnemyStatusReadout> readouts, long ticks, Vector3 eye, LevelShader shader)
    {
        var scene = new Scene(ticks, eye, shader);
        foreach (var readout in readouts)
        {
            for (var ring = 0; ring < readout.Lines.Count; ring++)
            {
                var line = readout.Lines[ring];
                Drawings[line.Form](readout, line, ring, scene);
            }
        }
    }

    /// <summary>
    /// The tint of a body as the flash of <see cref="LevelShader.SetFlash"/> takes it: colour and amount, 0 without a tint.
    /// </summary>
    public static (Color Colour, float Amount) Tint(StatusTint? tint) =>
        tint is { } shown ? (Colour(shown.Colour), shown.Amount) : (Color.White, 0f);

    // A ring of motes circling the body (issue #506).
    private static void DrawMotes(EnemyStatusReadout readout, StatusLine line, int ring, Scene scene)
    {
        var size = new Vector3(MoteSize);
        var radius = readout.Radius + RingGap;
        var colour = Colour(line.Colour);
        var height = readout.Height * (0.3f + (0.25f * ring));
        var turn = (scene.Ticks % 100_000) * TurnPerTick * (ring % 2 == 0 ? 1f : -1f);
        for (var mote = 0; mote < MotesPerRing; mote++)
        {
            var angle = turn + (mote * MathF.Tau / MotesPerRing) + (ring * 0.4f);
            var bob = 0.04f * MathF.Sin((turn * 2f) + mote);
            var at = readout.Feet + new Vector3(MathF.Cos(angle) * radius, height + bob, MathF.Sin(angle) * radius);
            ShadedBox.DrawGlowing(Matrix4x4.CreateTranslation(at), size, colour);
        }
    }

    // The frost of Grave Chill (issue #775, variant A of the trial piece #616): unlit like the trial piece's effect
    // colours - only the fog covers them - a disc of rime on the floor and rime flecks on the body.
    private static void DrawFrost(EnemyStatusReadout readout, StatusLine line, int ring, Scene scene)
    {
        scene.Shader.SetGlow(1f);
        if (readout.Disc is { } disc)
        {
            DrawFrostDisc(disc, scene.Shader);
        }

        var size = new Vector3(FrostLook.FleckSize);
        var rime = Colour(FrostLook.Rime);
        foreach (var fleck in FrostLook.Flecks)
        {
            ShadedBox.DrawGlowing(Matrix4x4.CreateTranslation(FrostLook.FleckAt(fleck, readout, scene.Eye)), size, rime);
        }

        scene.Shader.SetGlow(0f);
    }

    // The sparks of a stun (issue #778, variant A of the trial piece #616): unlit like the frost, three plus shapes facing
    // the eye above the head - four arms in Eisen 6 round a middle in Eisen 7 (apart, so no two boxes fight for a pixel).
    private static void DrawSparks(EnemyStatusReadout readout, StatusLine line, int ring, Scene scene)
    {
        scene.Shader.SetGlow(1f);
        var arms = Colour(StunLook.Arms);
        var middle = Colour(StunLook.Centre);
        foreach (var spark in StunLook.SparksOf(readout, scene.Ticks, scene.Eye))
        {
            var turn = Matrix4x4.CreateRotationY(MathF.Atan2(-spark.Across.Z, spark.Across.X));
            var thick = spark.Thickness;
            var reach = (thick / 2f) + (spark.ArmLength / 2f);
            var crosswise = new Vector3(spark.ArmLength, thick, thick);
            var upright = new Vector3(thick, spark.ArmLength, thick);
            ShadedBox.DrawGlowing(turn * Matrix4x4.CreateTranslation(spark.Centre + (spark.Across * reach)), crosswise, arms);
            ShadedBox.DrawGlowing(turn * Matrix4x4.CreateTranslation(spark.Centre - (spark.Across * reach)), crosswise, arms);
            ShadedBox.DrawGlowing(turn * Matrix4x4.CreateTranslation(spark.Centre + new Vector3(0f, reach, 0f)), upright, arms);
            ShadedBox.DrawGlowing(turn * Matrix4x4.CreateTranslation(spark.Centre - new Vector3(0f, reach, 0f)), upright, arms);
            ShadedBox.DrawGlowing(turn * Matrix4x4.CreateTranslation(spark.Centre), new Vector3(thick), middle);
        }

        scene.Shader.SetGlow(0f);
    }

    // The disc as Core lays it onto the floors (FrostDisc, issue #785): inside the rim the dot pattern of the cold and
    // the dark rime, the rim in light rime; lifted a little so the floor does not cover it.
    private static void DrawFrostDisc(FrostDisc disc, LevelShader shader)
    {
        shader.SetPattern(Colour(FrostLook.Cold), FrostLook.ColdShare);
        var dark = Colour(FrostLook.DarkRime);
        Triangles(disc.Inside, dark);
        shader.SetPattern(dark, 0f);
        Triangles(disc.Rim, Colour(FrostLook.Rime));
    }

    private static void Triangles(IReadOnlyList<DiscTriangle> triangles, Color colour)
    {
        Rlgl.Begin(DrawMode.Triangles);
        Rlgl.Color4ub(colour.R, colour.G, colour.B, colour.A);
        foreach (var triangle in triangles)
        {
            Vertex(triangle.A);
            Vertex(triangle.B);
            Vertex(triangle.C);
        }

        Rlgl.End();
    }

    private static void Vertex(Vector3 at) => Rlgl.Vertex3f(at.X, at.Y + DiscLift, at.Z);

    private static Color Colour((byte R, byte G, byte B) colour) => new(colour.R, colour.G, colour.B, (byte)255);

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
