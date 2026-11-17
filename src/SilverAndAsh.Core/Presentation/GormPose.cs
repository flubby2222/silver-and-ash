using System.Numerics;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// How one part of Gorm is turned (degrees), as in the designer's table: lifted forward (<see cref="Rig.Lift"/>), then
/// spread away from the body (arms and legs: outwards on either side), then turned round the upright axis of its parent.
/// </summary>
public readonly record struct GormJoint(float Lift, float Spread = 0f, float Turn = 0f)
{
    /// <summary>Part of the way from <paramref name="from"/> to <paramref name="to"/> (0: from, 1: to).</summary>
    public static GormJoint Blend(GormJoint from, GormJoint to, float amount) => new(
        from.Lift + ((to.Lift - from.Lift) * amount),
        from.Spread + ((to.Spread - from.Spread) * amount),
        from.Turn + ((to.Turn - from.Turn) * amount));
}

/// <summary>
/// What Gorm is doing, as far as his pose needs it.
/// </summary>
/// <param name="State">His state (<see cref="Gorm.State"/>).</param>
/// <param name="Attack">The attack he winds up or carries out (<see cref="Gorm.Attack"/>).</param>
/// <param name="Progress">How far the state has come, 0..1 (<see cref="Gorm.StateProgress"/>).</param>
/// <param name="Phase2">True in phase 2 (<see cref="Gorm.IsInPhase2"/>): he stands hunched, mouth open.</param>
/// <param name="StridePhase">Where in his stride he is (radians, one stride: 2 pi).</param>
/// <param name="Stride">How much he walks, 0 (stands) .. 1.</param>
/// <param name="SwingAngle">While swinging: where the chain points, in radians from his facing towards his right
/// (-pi/2: his left, the club side; +pi/2: his right, the chain side). Null otherwise.</param>
public readonly record struct GormMotion(
    GormState State, GormAttack? Attack, float Progress, bool Phase2, float StridePhase, float Stride, float? SwingAngle)
{
    /// <summary>The motion of <paramref name="gorm"/>; stride as counted by the view (it advances with his speed).</summary>
    public static GormMotion Of(Gorm gorm, float stridePhase, float stride)
    {
        ArgumentNullException.ThrowIfNull(gorm);
        float? angle = gorm.SwingDirection is { } sweep ? GormPose.AngleOf(sweep, gorm.Facing) : null;
        return new(gorm.State, gorm.Attack, gorm.StateProgress, gorm.IsInPhase2, stridePhase, stride, angle);
    }
}

/// <summary>
/// The parts of Gorm's model (assets/models/gorm.glb, issue #249) and how they are turned. The poses are the
/// grafikdesigner's (hand-over of 2026-11-17); the file holds him standing upright, every angle 0. The origin lies
/// between his feet; he faces +z. His club hand is on +x ("px"), his chain hand on -x ("nx") - Core's "his right" is -x.
/// Where the chain and the hook lie is <see cref="GormChain"/>'s business: always as far as they reach in the game.
/// </summary>
public static class GormPose
{
    public const string Torso = "torso";
    public const string Head = "head";
    public const string Jaw = "jaw";
    public const string ClubUpperArm = "upper_arm_px";
    public const string ClubForearm = "forearm_px";
    public const string Club = "club";
    public const string ChainUpperArm = "upper_arm_nx";
    public const string ChainForearm = "forearm_nx";
    public const string ChainWrap = "wrap_nx";
    public const string ThighClubSide = "thigh_px";
    public const string ShinClubSide = "shin_px";
    public const string ThighChainSide = "thigh_nx";
    public const string ShinChainSide = "shin_nx";

    /// <summary>One link of the chain, a building block at the origin (laid by <see cref="GormChain"/>).</summary>
    public const string ChainLink = "chain_link";

    /// <summary>The hook at the end of the chain, a building block with its eye at the origin.</summary>
    public const string Hook = "hook";

    /// <summary>Where the chain leaves his fist, relative to the pivot of the chain forearm (metres, unturned).</summary>
    public static readonly Vector3 Fist = new(0f, -0.88f, 0.06f);

    /// <summary>How far he bends forward while the club is stuck (torso lift, degrees): his mask goes down with his hit zone.</summary>
    public const float StuckBend = -38f;

    /// <summary>The parts, parents before children - the order of <see cref="Joints"/> and <see cref="Turns"/>.</summary>
    public static IReadOnlyList<RigPart> Parts { get; } =
    [
        new(Torso, null),
        new(Head, Torso),
        new(Jaw, Head),
        new(ClubUpperArm, Torso),
        new(ClubForearm, ClubUpperArm),
        new(Club, ClubForearm),
        new(ChainUpperArm, Torso),
        new(ChainForearm, ChainUpperArm),
        new(ChainWrap, ChainForearm),
        new(ThighClubSide, Torso),
        new(ShinClubSide, ThighClubSide),
        new(ThighChainSide, Torso),
        new(ShinChainSide, ThighChainSide),
        new(ChainLink, null),
        new(Hook, null),
    ];

    /// <summary>Number of parts that make up his body (the rest are the chain's building blocks).</summary>
    public const int BodyPartCount = 13;

    /// <summary>Standing, the club hanging forward.</summary>
    public static IReadOnlyDictionary<string, GormJoint> Rest { get; } = new Dictionary<string, GormJoint>
    {
        [Jaw] = new(-6f), [ClubForearm] = new(14f), [ChainForearm] = new(10f),
    };

    /// <summary>Phase 2: hunched forward, mouth open, arms out and bent.</summary>
    public static IReadOnlyDictionary<string, GormJoint> Phase2Rest { get; } = new Dictionary<string, GormJoint>
    {
        [Torso] = new(-8f), [Head] = new(-6f), [Jaw] = new(-22f),
        [ClubUpperArm] = new(8f, 18f), [ChainUpperArm] = new(8f, 18f),
        [ClubForearm] = new(24f), [ChainForearm] = new(24f),
    };

    /// <summary>
    /// Winding up the swing: the chain arm raised high above his head, whirling the chain there - below the ceiling of
    /// his boss room (<see cref="World.RoomTemplate.RoomCeilingHeight"/>, 6 m; he is 3.2 m tall).
    /// </summary>
    public static IReadOnlyDictionary<string, GormJoint> SwingWindUp { get; } = new Dictionary<string, GormJoint>
    {
        [ChainUpperArm] = new(160f, 25f), [ChainForearm] = new(20f), [Head] = new(8f), [Jaw] = new(-20f),
        [Torso] = new(6f), [ClubForearm] = new(14f),
    };

    /// <summary>
    /// The swing: the chain arm out to the side, low enough that the fist holds the chain at the height it hits
    /// (the designer had 85 degrees - his fist would then hang above the band the chain hits in). Turned after the chain.
    /// The club forearm is bent a little more than the designer's 14 degrees: with the arm back and the torso forward,
    /// the club would otherwise dip into the floor.
    /// </summary>
    public static IReadOnlyDictionary<string, GormJoint> Swing { get; } = new Dictionary<string, GormJoint>
    {
        [ChainUpperArm] = new(15f, 52f), [ChainForearm] = new(0f), [Torso] = new(-6f), [Jaw] = new(-25f),
        [ClubUpperArm] = new(-10f, -15f), [ClubForearm] = new(20f),
    };

    /// <summary>Winding up the throw: the chain arm drawn back, the hook hanging ready (it glows).</summary>
    public static IReadOnlyDictionary<string, GormJoint> ThrowWindUp { get; } = new Dictionary<string, GormJoint>
    {
        [ChainUpperArm] = new(-40f, 45f), [ChainForearm] = new(60f), [Torso] = new(4f), [Head] = new(-4f),
        [Jaw] = new(-12f), [ClubForearm] = new(14f),
    };

    /// <summary>The throw: the chain arm thrust forward, after the hook.</summary>
    public static IReadOnlyDictionary<string, GormJoint> Throw { get; } = new Dictionary<string, GormJoint>
    {
        [ChainUpperArm] = new(80f, 10f), [ChainForearm] = new(5f), [Torso] = new(-8f), [Jaw] = new(-20f),
        [ClubForearm] = new(14f),
    };

    /// <summary>The roar into phase 2: leaning back, head in the neck, mouth wide, both arms up.</summary>
    public static IReadOnlyDictionary<string, GormJoint> Roar { get; } = new Dictionary<string, GormJoint>
    {
        [Torso] = new(16f), [Head] = new(38f), [Jaw] = new(-45f),
        [ClubUpperArm] = new(150f, 30f), [ChainUpperArm] = new(150f, 30f),
        [ClubForearm] = new(20f), [ChainForearm] = new(20f),
    };

    /// <summary>
    /// The club raised high above his head - below the ceiling of his boss room (<see cref="World.RoomTemplate.RoomCeilingHeight"/>,
    /// 6 m) - the top of the hammer's wind-up.
    /// </summary>
    public static IReadOnlyDictionary<string, GormJoint> HammerUp { get; } = new Dictionary<string, GormJoint>
    {
        [ClubUpperArm] = new(150f, 10f), [ClubForearm] = new(30f), [Torso] = new(8f), [Jaw] = new(-15f),
        [ChainForearm] = new(10f),
    };

    /// <summary>The blow: bent forward, the club slammed down in front of him.</summary>
    public static IReadOnlyDictionary<string, GormJoint> Slam { get; } = new Dictionary<string, GormJoint>
    {
        [ClubUpperArm] = new(55f, 5f), [ClubForearm] = new(10f), [Torso] = new(-20f), [Jaw] = new(-30f),
        [ChainForearm] = new(10f),
    };

    /// <summary>The club stuck in the floor: bent far forward over it, head down with his hit zone.</summary>
    public static IReadOnlyDictionary<string, GormJoint> Stuck { get; } = new Dictionary<string, GormJoint>
    {
        [Torso] = new(StuckBend), [Head] = new(10f), [Jaw] = new(-20f),
        [ClubUpperArm] = new(60f, 5f), [ClubForearm] = new(0f), [ChainForearm] = new(10f),
    };

    /// <summary>
    /// The part of the swing wind-up in which he raises his chain arm; only then does he whirl the chain (before it
    /// still hangs).
    /// </summary>
    public const float SwingArmRaised = 0.25f;

    /// <summary>
    /// The angle of every part (order of <see cref="Parts"/>) for <paramref name="motion"/>: the designer's poses,
    /// eased in where the placeholder eased (wind-ups), the roar quickly in, held and down again.
    /// </summary>
    public static void Joints(GormMotion motion, Span<GormJoint> joints)
    {
        if (joints.Length != Parts.Count)
        {
            throw new ArgumentException($"Gorm has {Parts.Count} parts.", nameof(joints));
        }

        var rest = motion.Phase2 ? Phase2Rest : Rest;
        var p = Math.Clamp(motion.Progress, 0f, 1f);
        var (pose, amount) = (motion.State, motion.Attack) switch
        {
            (GormState.WindingUp, GormAttack.Swing) => (SwingWindUp, MathF.Min(1f, p / SwingArmRaised)),
            (GormState.WindingUp, GormAttack.Throw) => (ThrowWindUp, MathF.Min(1f, p * 3f)),
            (GormState.WindingUp, GormAttack.Hammer) => (HammerUp, p),
            (GormState.Swinging, _) => (Swing, 1f),
            (GormState.Throwing, _) => (Throw, 1f),
            (GormState.Slamming, _) => (Slam, 1f),
            (GormState.Stuck, _) => (Stuck, 1f),
            (GormState.Roaring, _) => (Roar, MathF.Min(1f, MathF.Min(p, 1f - p) * 6f)),
            _ => (rest, 0f),
        };

        for (var i = 0; i < Parts.Count; i++)
        {
            var name = Parts[i].Name;
            joints[i] = GormJoint.Blend(rest.GetValueOrDefault(name), pose.GetValueOrDefault(name), amount);
        }

        if (motion.State == GormState.Swinging && motion.SwingAngle is { } angle)
        {
            // The chain arm follows the chain round, from the front (as far as his shoulder lets it) to his right.
            var index = IndexOf(ChainUpperArm);
            joints[index] = joints[index] with { Turn = ArmTurn(angle) };
        }

        if (motion.State == GormState.Walking && motion.Stride > 0f)
        {
            // Heavy steps: the legs swing, the knee bends on the way forward, the arms swing against the legs.
            var swing = MathF.Sin(motion.StridePhase) * motion.Stride;
            Add(joints, ThighClubSide, 18f * swing);
            Add(joints, ThighChainSide, -18f * swing);
            Add(joints, ShinClubSide, -20f * MathF.Max(0f, swing));
            Add(joints, ShinChainSide, -20f * MathF.Max(0f, -swing));
            Add(joints, ClubUpperArm, -8f * swing);
            Add(joints, ChainUpperArm, 8f * swing);
        }
    }

    /// <summary>
    /// How far the chain arm is turned round (degrees) while the chain points <paramref name="swingAngle"/> from his
    /// facing (radians, towards his right): out to his right is 0, straight ahead 90; it goes no further than 120 -
    /// at the start the chain is still on his club side, out of the arm's reach.
    /// </summary>
    public static float ArmTurn(float swingAngle) => Math.Clamp(90f - (swingAngle * 180f / MathF.PI), 0f, 120f);

    /// <summary>The flat direction <paramref name="sweep"/> as an angle from <paramref name="facing"/> towards his right (radians).</summary>
    public static float AngleOf(Vector3 sweep, Vector3 facing)
    {
        var right = new Vector3(-facing.Z, 0f, facing.X);
        return MathF.Atan2(Vector3.Dot(sweep, right), Vector3.Dot(sweep, facing));
    }

    /// <summary>
    /// The turn of each part (order of <see cref="Parts"/>) for <see cref="Rig.Pose"/>: lift, then spread outwards
    /// (to +x on the club side, to -x on the chain side), then turned round the parent's upright axis.
    /// </summary>
    public static void Turns(ReadOnlySpan<GormJoint> joints, Span<Matrix4x4> turns)
    {
        if (joints.Length != Parts.Count || turns.Length != Parts.Count)
        {
            throw new ArgumentException($"Gorm has {Parts.Count} parts.", nameof(turns));
        }

        for (var i = 0; i < Parts.Count; i++)
        {
            var name = Parts[i].Name;
            var side = name.EndsWith("_px", StringComparison.Ordinal) ? 1f : name.EndsWith("_nx", StringComparison.Ordinal) ? -1f : 0f;
            var joint = joints[i];
            turns[i] = Rig.Lift(joint.Lift) *
                Matrix4x4.CreateRotationZ(Radians(joint.Spread * side)) *
                Matrix4x4.CreateRotationY(Radians(joint.Turn));
        }
    }

    /// <summary>Index of a part in <see cref="Parts"/>.</summary>
    public static int IndexOf(string name)
    {
        for (var i = 0; i < Parts.Count; i++)
        {
            if (Parts[i].Name == name)
            {
                return i;
            }
        }

        throw new ArgumentException($"Gorm has no part '{name}'.", nameof(name));
    }

    private static void Add(Span<GormJoint> joints, string name, float lift)
    {
        var index = IndexOf(name);
        joints[index] = joints[index] with { Lift = joints[index].Lift + lift };
    }

    private static float Radians(float degrees) => degrees * MathF.PI / 180f;
}

/// <summary>
/// Where the links of Gorm's chain and its hook lie (issue #249). The rule of the boss (decision record 0007): what is
/// drawn matches what reaches - the chain ends exactly where it hits in the game, never shorter, never longer.
/// The links of the model are laid one by one along a line, every second one turned a quarter round the chain, the
/// last one ending exactly at the far point; the hook (scaled <see cref="HookScale"/>) is laid so its far end lies there.
/// </summary>
public static class GormChain
{
    /// <summary>Length of one link of the model (metres, its long axis is y).</summary>
    public const float LinkLength = 0.35f;

    /// <summary>The links lie at most this far apart (metres), so they hang into each other.</summary>
    public const float Spacing = 0.19f;

    /// <summary>How much bigger the hook is drawn than in the file - big enough to read from afar.</summary>
    public const float HookScale = 1.8f;

    /// <summary>The hook's length in the file (metres, from the eye at the origin down along -y).</summary>
    public const float HookFileLength = 0.446f;

    /// <summary>How long the hook is drawn (metres): from its eye to its far end.</summary>
    public static float HookLength => HookFileLength * HookScale;

    /// <summary>
    /// The frames of the links (corner of a link -> world) from <paramref name="from"/> to <paramref name="to"/>: the first
    /// link starts at <paramref name="from"/>, the last ends at <paramref name="to"/>. <paramref name="first"/>: the turn
    /// of the first link (0 or 1), so two pieces of one chain go on alternating.
    /// </summary>
    public static void Lay(Vector3 from, Vector3 to, ICollection<Matrix4x4> links, int first = 0)
    {
        ArgumentNullException.ThrowIfNull(links);
        var line = to - from;
        var length = line.Length();
        if (length < 0.0001f)
        {
            return;
        }

        var along = line / length;
        var count = length <= LinkLength ? 1 : (int)MathF.Ceiling((length - LinkLength) / Spacing) + 1;
        var step = count > 1 ? (length - LinkLength) / (count - 1) : 0f;
        var start = count > 1 ? LinkLength / 2f : length / 2f;
        var (across, other) = Across(along);
        for (var i = 0; i < count; i++)
        {
            var (x, z) = (i + first) % 2 == 0 ? (across, other) : (other, -across);
            var centre = from + (along * (start + (i * step)));
            links.Add(Frame(x, along, z, centre, 1f));
        }
    }

    /// <summary>
    /// The frame of the hook (corner -> world) so that its far end lies at <paramref name="tip"/>, pointing along
    /// <paramref name="along"/> (from the chain to the tip), its barb towards <paramref name="up"/>.
    /// </summary>
    public static Matrix4x4 HookAt(Vector3 tip, Vector3 along, Vector3 up)
    {
        along = Vector3.Normalize(along);
        var barb = up - (along * Vector3.Dot(up, along));
        barb = barb.LengthSquared() > 0.0001f ? Vector3.Normalize(barb) : Across(along).Across;
        var y = -along;
        var x = Vector3.Cross(y, barb);
        return Frame(x, y, barb, tip - (along * HookLength), HookScale);
    }

    /// <summary>Where the hook's eye is (the chain's end) when its far end lies at <paramref name="tip"/>.</summary>
    public static Vector3 HookEye(Vector3 tip, Vector3 along) => tip - (Vector3.Normalize(along) * HookLength);

    /// <summary>How much chain hangs between his fist and the hook while he winds up the throw (metres).</summary>
    public const float ReadyChain = 0.5f;

    /// <summary>How high above the floor the hook's tip stays while it hangs ready (metres), as the hook at rest.</summary>
    public const float ReadyClearance = 0.03f;

    /// <summary>
    /// The hook hanging ready below his fist while he winds up the throw (it glows): <see cref="ReadyChain"/> of chain,
    /// then the hook, straight down - or, while his fist is still too low for that (he only draws his arm back), reaching
    /// forward along <paramref name="facing"/> to just above the floor at <paramref name="floor"/>, never into it.
    /// Lays the links into <paramref name="links"/> and gives the frame of the hook.
    /// </summary>
    public static Matrix4x4 Ready(Vector3 fist, float floor, Vector3 facing, ICollection<Matrix4x4> links)
    {
        var length = ReadyChain + HookLength;
        var drop = MathF.Max(0f, fist.Y - (floor + ReadyClearance));
        var tip = drop >= length
            ? fist - (Vector3.UnitY * length)
            : (fist with { Y = floor + ReadyClearance }) +
                (Vector3.Normalize(facing with { Y = 0f }) * MathF.Sqrt((length * length) - (drop * drop)));
        var along = Vector3.Normalize(tip - fist);
        Lay(fist, HookEye(tip, along), links);
        return HookAt(tip, along, facing);
    }

    /// <summary>
    /// The far end of the sweeping chain: as far from his upright axis as the swing reaches, at the middle of the
    /// height it hits in (<see cref="Gorm.SwingArea"/>), in the direction it sweeps (<see cref="Gorm.SwingDirection"/>).
    /// </summary>
    public static Vector3 SwingEnd(Vector3 position, Vector3 sweep, (float Reach, float Low, float High) area) =>
        position + new Vector3(0f, (area.Low + area.High) / 2f, 0f) + (Vector3.Normalize(sweep with { Y = 0f }) * area.Reach);

    /// <summary>
    /// The far end of the chain whirled above his head while he winds up the swing: as far from his upright axis as the
    /// swing will reach (so you see how far to stay away), at the height of his fist.
    /// </summary>
    public static Vector3 WhirlEnd(Vector3 position, Vector3 fist, Vector3 direction, float reach) =>
        (position with { Y = fist.Y }) + (Vector3.Normalize(direction with { Y = 0f }) * reach);

    // Two directions across the chain, at right angles to it and to each other.
    private static (Vector3 Across, Vector3 Other) Across(Vector3 along)
    {
        var reference = MathF.Abs(along.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitZ;
        var across = Vector3.Normalize(Vector3.Cross(reference, along));
        return (across, Vector3.Cross(across, along));
    }

    // Rows of a frame: where x, y and z of the part point (scaled), and where its origin lies.
    private static Matrix4x4 Frame(Vector3 x, Vector3 y, Vector3 z, Vector3 origin, float scale) => new(
        x.X * scale, x.Y * scale, x.Z * scale, 0f,
        y.X * scale, y.Y * scale, y.Z * scale, 0f,
        z.X * scale, z.Y * scale, z.Z * scale, 0f,
        origin.X, origin.Y, origin.Z, 1f);
}
