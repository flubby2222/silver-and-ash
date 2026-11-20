using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Presentation;

/// <summary>The two poses of the crossbow skeleton (issue #497).</summary>
public enum CrossbowSkeletonStance
{
    /// <summary>Crossbow lowered, eyes dark.</summary>
    Rest,

    /// <summary>Crossbow raised to the shoulder, eyes glowing red - the warning before a shot.</summary>
    Announce,
}

/// <summary>
/// The crossbow skeleton (issue #497, model "F flach" of issue #334): two models from tools/models/crossbow_skeleton.py,
/// assets/models/crossbow_skeleton_rest.glb and crossbow_skeleton_announce.glb, one per pose. Each has five parts that
/// do not move in the file; the figure stands with its origin on the floor, looking along +Z. Red is only in the eyes of
/// the announce pose (the generator checks it, and so do the tests).
/// </summary>
public static class CrossbowSkeletonLook
{
    public const string Skeleton = "skelett";
    public const string Crossbow = "armbrust";
    public const string Gear = "ausruestung";
    public const string Cloth = "stoff";
    public const string BowString = "sehne";

    /// <summary>About the height of the figure, in metres (top of the skull) - for the label above it.</summary>
    public const float Height = 1.86f;

    private static readonly RigPart[] All =
    [
        new(Skeleton, null), new(Crossbow, null), new(Gear, null), new(Cloth, null), new(BowString, null),
    ];

    /// <summary>The parts of both models, in the order of the rig.</summary>
    public static IReadOnlyList<RigPart> Parts => All;

    /// <summary>The file of <paramref name="stance"/>.</summary>
    public static string FileName(CrossbowSkeletonStance stance) => stance switch
    {
        CrossbowSkeletonStance.Rest => "crossbow_skeleton_rest.glb",
        CrossbowSkeletonStance.Announce => "crossbow_skeleton_announce.glb",
        _ => throw new ArgumentOutOfRangeException(nameof(stance), stance, null),
    };
}
