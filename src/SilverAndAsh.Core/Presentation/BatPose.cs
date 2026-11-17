using System.Numerics;

namespace SilverAndAsh.Core.Presentation;

/// <summary>What a bat is doing, as far as the pose of its parts goes.</summary>
public enum BatStance
{
    /// <summary>Hovering, circling, rising: the wings beat.</summary>
    Hovering,

    /// <summary>Screeching, the warning before a dive: reared up, wings high, mouth wide open.</summary>
    Screeching,

    /// <summary>Diving head first, the wings half swept back - also the folded wings of a dead bat.</summary>
    Diving,
}

/// <summary>One set of angles for the parts of the bat (degrees), as in the designer's table.</summary>
/// <param name="Body">Lift of the body (front end up), see <see cref="Rig.Lift"/>.</param>
/// <param name="Head">Lift of the head.</param>
/// <param name="Jaw">Lift of the jaw (negative: open).</param>
/// <param name="InnerUp">Inner wing lifted around its long axis (tip up), the same on both sides.</param>
/// <param name="OuterUp">Outer wing lifted, relative to the inner wing.</param>
/// <param name="InnerBack">Inner wing swept around the vertical axis (tip back).</param>
/// <param name="OuterBack">Outer wing swept back, relative to the inner wing.</param>
public readonly record struct BatAngles(
    float Body, float Head, float Jaw, float InnerUp, float OuterUp, float InnerBack, float OuterBack);

/// <summary>
/// The parts of the bat model (assets/models/bat.glb, issue #249) and how they are turned. The poses are the
/// grafikdesigner's (design hand-over of 2026-11-16): the file holds the wings spread flat, every angle 0. The origin is
/// the middle of the body; it faces +z.
/// </summary>
public static class BatPose
{
    public const string Body = "body";
    public const string Head = "head";
    public const string Jaw = "jaw";
    public const string InnerWingLeft = "wing_in_nx";
    public const string InnerWingRight = "wing_in_px";
    public const string OuterWingLeft = "wing_out_nx";
    public const string OuterWingRight = "wing_out_px";

    /// <summary>Hovering, in the middle of a wing beat.</summary>
    public static BatAngles Hover { get; } = new(15f, 0f, -12f, 8f, 4f, 0f, 0f);

    /// <summary>The top of a wing beat.</summary>
    public static BatAngles BeatUp { get; } = new(15f, 0f, -10f, 40f, 20f, 0f, 0f);

    /// <summary>The bottom of a wing beat.</summary>
    public static BatAngles BeatDown { get; } = new(15f, 0f, -10f, -35f, -15f, 0f, 0f);

    /// <summary>Screeching: tall instead of wide, the wings almost upright and a little forward, the fangs bared.</summary>
    public static BatAngles Screech { get; } = new(12f, 30f, -55f, 65f, 25f, -15f, -20f);

    /// <summary>Diving: nose down, head first, the wings half swept back like an arrow head.</summary>
    public static BatAngles Dive { get; } = new(-30f, -5f, -45f, -12f, -5f, 32f, 38f);

    /// <summary>The outer wing follows the inner one this much later in the beat (radians).</summary>
    public const float OuterWingLag = 0.6f;

    /// <summary>The parts, parents before children - the order of <see cref="Turns"/>.</summary>
    public static IReadOnlyList<RigPart> Parts { get; } =
    [
        new(Body, null),
        new(Head, Body),
        new(Jaw, Head),
        new(InnerWingLeft, Body),
        new(InnerWingRight, Body),
        new(OuterWingLeft, InnerWingLeft),
        new(OuterWingRight, InnerWingRight),
    ];

    /// <summary>
    /// The angles for a stance. <paramref name="beatPhase"/>: where in the wing beat it is (radians, one beat: 2 pi);
    /// only hovering beats - the inner wing swings from the middle up to <see cref="BeatUp"/> and down to
    /// <see cref="BeatDown"/>, the outer wing <see cref="OuterWingLag"/> later.
    /// </summary>
    public static BatAngles Angles(BatStance stance, float beatPhase) => stance switch
    {
        BatStance.Screeching => Screech,
        BatStance.Diving => Dive,
        _ => Hover with
        {
            InnerUp = Beat(Hover.InnerUp, BeatUp.InnerUp, BeatDown.InnerUp, MathF.Sin(beatPhase)),
            OuterUp = Beat(Hover.OuterUp, BeatUp.OuterUp, BeatDown.OuterUp, MathF.Sin(beatPhase - OuterWingLag)),
        },
    };

    /// <summary>Writes the turn of each part around its pivot, in the order of <see cref="Parts"/>.</summary>
    public static void Turns(BatAngles angles, Span<Matrix4x4> turns)
    {
        if (turns.Length != Parts.Count)
        {
            throw new ArgumentException($"The bat has {Parts.Count} parts.", nameof(turns));
        }

        turns[0] = Rig.Lift(angles.Body);
        turns[1] = Rig.Lift(angles.Head);
        turns[2] = Rig.Lift(angles.Jaw);
        turns[3] = Wing(-1f, angles.InnerUp, angles.InnerBack);
        turns[4] = Wing(1f, angles.InnerUp, angles.InnerBack);
        turns[5] = Wing(-1f, angles.OuterUp, angles.OuterBack);
        turns[6] = Wing(1f, angles.OuterUp, angles.OuterBack);
    }

    // A wing: first lifted around the long axis z, then swept around the vertical axis y; side -1 is the left wing,
    // turned the other way so both tips go up and back alike.
    private static Matrix4x4 Wing(float side, float up, float back) =>
        Matrix4x4.CreateRotationZ(Radians(up * side)) * Matrix4x4.CreateRotationY(Radians(back * side));

    // From the middle towards the top (beat > 0) or the bottom (beat < 0) of the wing beat.
    private static float Beat(float middle, float top, float bottom, float beat) =>
        beat >= 0f ? middle + (top - middle) * beat : middle + (middle - bottom) * beat;

    private static float Radians(float degrees) => degrees * MathF.PI / 180f;
}
