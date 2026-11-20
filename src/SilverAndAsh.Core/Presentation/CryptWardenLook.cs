using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Presentation;

/// <summary>The two poses of the crypt warden (issue #530).</summary>
public enum CryptWardenStance
{
    /// <summary>Pole propped up, eye dull.</summary>
    Rest,

    /// <summary>Upper body twisted, pole raised for the blow, eye blood red - the warning before the attack.</summary>
    Announce,
}

/// <summary>
/// The crypt warden (issue #530, model "K1" of issue #335, round 7): two models from tools/models/crypt_warden.py,
/// assets/models/crypt_warden_rest.glb and crypt_warden_announce.glb, one per pose. Each has nine parts that do not move
/// in the file; the figure stands with its origin on the floor, looking along +Z. Red is only in the eye of the announce
/// pose (the generator checks it, and so do the tests). Only the look: the enemy's rules belong to issue #335.
/// </summary>
public static class CryptWardenLook
{
    public const string LegLeft = "leg_l";
    public const string LegRight = "leg_r";
    public const string Torso = "torso";
    public const string Head = "head";
    public const string Eyes = "eyes";
    public const string ArmLeft = "arm_l";
    public const string ArmRight = "arm_r";
    public const string Gear = "gear";
    public const string Pole = "pole";

    /// <summary>
    /// Height of the highest point at rest, in metres: the tip of the propped-up pole. The head ends lower, at 2.20 m at
    /// rest and 2.28 m while announcing. For the label above the figure.
    /// </summary>
    public const float Height = 2.52f;

    /// <summary>
    /// Radius of a sphere around the middle of the figure at half height (<see cref="Height"/> / 2 above its origin) that
    /// holds every point of both poses - the raised pole reaches farthest. For the view cone.
    /// </summary>
    public const float BoundingRadius = 2.6f;

    private static readonly RigPart[] All =
    [
        new(LegLeft, null), new(LegRight, null), new(Torso, null), new(Head, null), new(Eyes, null),
        new(ArmLeft, null), new(ArmRight, null), new(Gear, null), new(Pole, null),
    ];

    /// <summary>The parts of both models, in the order of the rig.</summary>
    public static IReadOnlyList<RigPart> Parts => All;

    /// <summary>The file of <paramref name="stance"/>.</summary>
    public static string FileName(CryptWardenStance stance) => stance switch
    {
        CryptWardenStance.Rest => "crypt_warden_rest.glb",
        CryptWardenStance.Announce => "crypt_warden_announce.glb",
        _ => throw new ArgumentOutOfRangeException(nameof(stance), stance, null),
    };
}
