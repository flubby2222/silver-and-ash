using System.Numerics;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The stun on an enemy (issue #778), as variant A of the effects trial piece draws it (issue #616,
/// design/proben/grafik/statuseffekte-616/effekte.py, <c>stun_a</c>): three plus-shaped sparks circling above the head,
/// the middle of each in Eisen 7, its arms in Eisen 6 - no tint of the body. Told apart from the frost of Grave Chill
/// (flecks on the body, a disc on the floor) by shape and place: the sparks stand above the head, never on the body or
/// the floor. Like the frost they are unlit, only covered by the fog. Sizes are those of the trial piece for a ghoul;
/// other enemies get them in proportion to their body (<see cref="ScaleFor"/>), as the disc of the frost does.
/// </summary>
public static class StunLook
{
    /// <summary>Eisen 7, "Funke Mitte": the middle of each spark.</summary>
    public static (byte R, byte G, byte B) Centre { get; } = (198, 205, 210);

    /// <summary>Eisen 6, "Funke Arme": the arms of each spark (also the colour of the timer).</summary>
    public static (byte R, byte G, byte B) Arms { get; } = (163, 175, 187);

    /// <summary>How many sparks circle the head, as in the trial piece.</summary>
    public const int SparkCount = 3;

    /// <summary>Radius of the ring the sparks circle on above a ghoul's head, in metres (trial piece: 0.30 m).</summary>
    public const float RingRadius = 0.30f;

    /// <summary>How far above the top of a ghoul's head the ring lies, in metres (trial piece: 0.10 m).</summary>
    public const float Lift = 0.10f;

    /// <summary>
    /// Half the length of one arm of a spark on a ghoul, in metres (trial piece: 0.075 m, at least 3 pixels) - a spark is
    /// 15 cm across: about two pixels at 10 m, six at 4 m.
    /// </summary>
    public const float ArmLength = 0.075f;

    /// <summary>Thickness of an arm and edge of the middle of a spark on a ghoul, in metres (trial piece: the arm / 2.5).</summary>
    public const float Thickness = 0.03f;

    /// <summary>
    /// Ticks for one turn of the ring (1 s; provisional - the trial piece shows a still picture and gives no speed). A
    /// ghoul's stun of 0.5 s sees half a turn.
    /// </summary>
    public const int TicksPerTurn = 60;

    /// <summary>
    /// How much larger than on a ghoul the ring and its lift are on an enemy whose body (for walking and bumping) has
    /// <paramref name="bodyRadius"/>: the body's share of a ghoul's, as for the disc of the frost (decision on PR #783).
    /// A ghoul keeps exactly the sizes of the trial piece.
    /// </summary>
    public static float ScaleFor(float bodyRadius) => bodyRadius / BodySizes.Ghoul.Radius;

    /// <summary>
    /// How much larger than on a ghoul one spark is: grows with a larger body, but never shrinks below the trial piece's
    /// size, so a spark stays readable at 10 m on a smaller enemy (the bat).
    /// </summary>
    public static float SparkScaleFor(float bodyRadius) => MathF.Max(1f, ScaleFor(bodyRadius));

    /// <summary>
    /// Where the sparks of the enemy of <paramref name="readout"/> stand at <paramref name="ticks"/> of the world, seen
    /// from <paramref name="eye"/>: on a level ring above its head (<see cref="EnemyStatusReadout.Crown"/>), evenly
    /// apart and turning with the ticks (so a photo of the same tick looks the same). Each spark is a plus facing the eye:
    /// one arm upright, one along <see cref="StunSpark.Across"/>.
    /// </summary>
    public static IReadOnlyList<StunSpark> SparksOf(EnemyStatusReadout readout, long ticks, Vector3 eye)
    {
        ArgumentNullException.ThrowIfNull(readout);
        var scale = ScaleFor(readout.Radius);
        var sparkScale = SparkScaleFor(readout.Radius);
        var middle = readout.Crown + new Vector3(0f, Lift * scale, 0f);
        var radius = RingRadius * scale;
        var toEye = (eye - middle) with { Y = 0f };
        toEye = toEye.LengthSquared() > 1e-6f ? Vector3.Normalize(toEye) : Vector3.UnitZ;
        var across = new Vector3(-toEye.Z, 0f, toEye.X);
        var turn = (float)(ticks % TicksPerTurn) / TicksPerTurn;

        var sparks = new StunSpark[SparkCount];
        for (var k = 0; k < SparkCount; k++)
        {
            var angle = MathF.Tau * (turn + ((float)k / SparkCount));
            var at = middle + new Vector3(MathF.Cos(angle) * radius, 0f, MathF.Sin(angle) * radius);
            sparks[k] = new StunSpark(at, across, ArmLength * sparkScale, Thickness * sparkScale);
        }

        return sparks;
    }

    /// <summary>
    /// How far above the top of the head the highest spark of an enemy whose body has <paramref name="bodyRadius"/>
    /// reaches, in metres: the ring's lift and the upright arm above it. The timers stand above this (issue #778).
    /// </summary>
    public static float ReachFor(float bodyRadius) =>
        (Lift * ScaleFor(bodyRadius)) + ((ArmLength + (Thickness / 2f)) * SparkScaleFor(bodyRadius));

    /// <summary>
    /// How much higher the timers of an enemy with <paramref name="lines"/> stand (issue #778): above the reach of the
    /// sparks (<see cref="ReachFor"/>) if one of its effects shows them, else not at all - the trial piece leaves the
    /// sparks and the timer to share the room above the head, and in the game the timer covered a spark.
    /// </summary>
    public static float TimerLiftFor(IEnumerable<StatusLine> lines, float bodyRadius)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return lines.Any(line => line.Form == StatusForm.Sparks) ? ReachFor(bodyRadius) : 0f;
    }
}

/// <summary>
/// One spark of the stun (issue #778): its middle, the level direction its crosswise arm points along (at right angles
/// to the eye), half the length of an arm and the thickness of an arm, in metres.
/// </summary>
public readonly record struct StunSpark(Vector3 Centre, Vector3 Across, float ArmLength, float Thickness);
