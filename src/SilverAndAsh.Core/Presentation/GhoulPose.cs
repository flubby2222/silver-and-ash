namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// What a ghoul is doing at the moment, as far as its drawing needs it (all 0: resting).
/// </summary>
/// <param name="WindUp">How far it has wound up for a blow, 0..1 (Ghoul.WindUpProgress).</param>
/// <param name="Lunge">The blow itself, 1 right when it strikes, falling back to 0.</param>
/// <param name="CrawlPhase">Where in the crawling cycle it is, in radians (one cycle: 2 pi).</param>
/// <param name="Crawl">How much it crawls, 0 (stands still) .. 1 (full speed).</param>
public readonly record struct GhoulMotion(float WindUp, float Lunge, float CrawlPhase, float Crawl);

/// <summary>
/// The parts of the ghoul model (assets/models/ghoul.glb) and how far each is lifted (degrees, see
/// <see cref="Rig.Lift"/>) for a <see cref="GhoulMotion"/>. The angles are the grafikdesigner's poses
/// (design hand-over of 2026-11-16): the model in the file is the resting pose with the jaw closed.
/// </summary>
public static class GhoulPose
{
    public const string Torso = "torso";
    public const string Head = "head";
    public const string Jaw = "jaw";
    public const string UpperArmLeft = "upper_arm_nx";
    public const string ForearmLeft = "forearm_nx";
    public const string UpperArmRight = "upper_arm_px";
    public const string ForearmRight = "forearm_px";
    public const string ThighLeft = "thigh_nx";
    public const string ShinLeft = "shin_nx";
    public const string ThighRight = "thigh_px";
    public const string ShinRight = "shin_px";

    /// <summary>Resting: the jaw hangs open a little.</summary>
    public const float JawResting = -12f;

    /// <summary>Fully wound up (WindUp = 1): rears up around the hip, the head keeps looking at the player, mouth wide open.</summary>
    public const float TorsoWoundUp = 34f;
    public const float HeadWoundUp = -24f;
    public const float JawWoundUp = -38f;
    public const float UpperArmRightWoundUp = 120f;
    public const float UpperArmLeftWoundUp = 128f;
    public const float ForearmRightWoundUp = -25f;
    public const float ForearmLeftWoundUp = -20f;

    /// <summary>The blow (Lunge = 1): the torso slams forward and down, the arms with it.</summary>
    public const float TorsoLunge = -14f;
    public const float UpperArmLunge = 55f;

    /// <summary>Crawling at full speed: the upper arms swing in turn by this much around the rest, the forearms half as much back.</summary>
    public const float UpperArmCrawlSwing = 20f;
    public const float ForearmCrawlSwing = -10f;

    /// <summary>Crawling: the thighs swing in turn, against the arm of the same side.</summary>
    public const float ThighCrawlSwing = 12f;

    /// <summary>The parts, parents before children - the order of <see cref="Angles"/>.</summary>
    public static IReadOnlyList<RigPart> Parts { get; } =
    [
        new(Torso, null),
        new(Head, Torso),
        new(Jaw, Head),
        new(UpperArmLeft, Torso),
        new(ForearmLeft, UpperArmLeft),
        new(UpperArmRight, Torso),
        new(ForearmRight, UpperArmRight),
        new(ThighLeft, null),
        new(ShinLeft, ThighLeft),
        new(ThighRight, null),
        new(ShinRight, ThighRight),
    ];

    /// <summary>
    /// Writes how far each part is lifted, in degrees, in the order of <see cref="Parts"/>. Winding up blends linearly
    /// from the rest to the wound-up pose; crawling and the blow add on top.
    /// </summary>
    public static void Angles(GhoulMotion motion, Span<float> degrees)
    {
        if (degrees.Length != Parts.Count)
        {
            throw new ArgumentException($"The ghoul has {Parts.Count} parts.", nameof(degrees));
        }

        var windUp = Math.Clamp(motion.WindUp, 0f, 1f);
        var lunge = Math.Clamp(motion.Lunge, 0f, 1f);
        var crawl = Math.Clamp(motion.Crawl, 0f, 1f);

        // Crawling: right arm forward while the left one goes back, and the other way round.
        var right = MathF.Sin(motion.CrawlPhase) * crawl;
        var left = -right;

        degrees[0] = TorsoWoundUp * windUp + TorsoLunge * lunge;
        degrees[1] = HeadWoundUp * windUp;
        degrees[2] = JawResting + (JawWoundUp - JawResting) * windUp;
        degrees[3] = UpperArmLeftWoundUp * windUp + UpperArmLunge * lunge + UpperArmCrawlSwing * left;
        degrees[4] = ForearmLeftWoundUp * windUp + ForearmCrawlSwing * left;
        degrees[5] = UpperArmRightWoundUp * windUp + UpperArmLunge * lunge + UpperArmCrawlSwing * right;
        degrees[6] = ForearmRightWoundUp * windUp + ForearmCrawlSwing * right;
        degrees[7] = ThighCrawlSwing * right;
        degrees[8] = 0f;
        degrees[9] = ThighCrawlSwing * left;
        degrees[10] = 0f;
    }
}
