using System.Numerics;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The stake crossbow in the right hand (issue #793, model A "Eisenwinde" from the probe of issue #203): a crossbow with
/// the winch's crank and the stake as parts of their own. It comes in two files with the same parts:
/// stake_crossbow_loaded.glb (the bow bent back, the string drawn into the stake's end) and stake_crossbow_relaxed.glb
/// (the bow nearly straight, the string straight across its tips). Held like the pistol, in the pistol's sleeve.
/// <para>
/// A shot leaves the weapon relaxed and without a stake until it is reloaded; the reload follows the reload time from
/// balance.json (<c>progress</c> is the share of it that has run, so the motion needs no time of its own): the
/// crossbow dips its muzzle and turns the crank (<see cref="CrankDegrees"/>), at <see cref="CockedAt"/> the string
/// catches (the loaded file takes over, with a small nudge) and at <see cref="StakeAt"/> the stake is laid in
/// (<see cref="StakeLift"/>). The times are shares of the reload and match the sound (tools/audio/armbrustklang.py: the
/// catch 0.6 s and the stake 0.4 s before its end, at the 3 s of balance.json).
/// </para>
/// </summary>
public static class StakeCrossbowPose
{
    /// <summary>The crossbow without the crank and the stake: the root of the rig, its pivot at the grip.</summary>
    public const string Crossbow = "crossbow";

    /// <summary>The winch's crank; its pivot lies on the axis of the drum.</summary>
    public const string Crank = "crank";

    /// <summary>The stake, a part of its own: it is not drawn after the shot until it is laid in again.</summary>
    public const string Stake = "stake";

    /// <summary>Index of the crank in <see cref="Parts"/>.</summary>
    public const int CrankIndex = 1;

    /// <summary>Index of the stake in <see cref="Parts"/>.</summary>
    public const int StakeIndex = 2;

    /// <summary>The loaded file (the string drawn, the stake in the groove).</summary>
    public const string LoadedFile = "stake_crossbow_loaded.glb";

    /// <summary>The relaxed file (the string straight across the bow's tips).</summary>
    public const string RelaxedFile = "stake_crossbow_relaxed.glb";

    /// <summary>The parts of both files: the crank and the stake hang on nothing, they are separate and move on their own.</summary>
    public static IReadOnlyList<RigPart> Parts { get; } = [new(Crossbow, null), new(Crank, null), new(Stake, null)];

    /// <summary>Where the grip rests (camera space, before <see cref="WeaponPose.ShrinkTowardsEye"/>): further right than the pistol's, the bow is wide.</summary>
    public static Vector3 Grip { get; } = new(0.125f, -0.085f, 0.17f);

    /// <summary>The crossbow leans this far to the right at rest (degrees, around the line of sight), its top towards the middle: as the pistol, less.</summary>
    public const float RollDegrees = 8f;

    /// <summary>A shot lifts the muzzle this far (degrees) ...</summary>
    public const float RecoilLiftDegrees = 9f;

    /// <summary>... and kicks the hand back: a heavy stake leaves the wood with a push.</summary>
    public static Vector3 RecoilShift { get; } = new(0f, 0.01f, -0.05f);

    /// <summary>Reloading: the muzzle dips this far (degrees), so the winch and its crank come into view.</summary>
    public const float ReloadDipDegrees = 22f;

    /// <summary>Reloading: the crossbow rolls this far further to the right (degrees).</summary>
    public const float ReloadRollDegrees = 12f;

    /// <summary>Reloading: the hand moves a little to the left and back (camera space, metres).</summary>
    public static Vector3 ReloadShift { get; } = new(-0.02f, 0f, -0.03f);

    /// <summary>The share of the reload at which the string catches: the loaded file takes over (the sound's catch, 0.6 s before the end of 3 s).</summary>
    public const float CockedAt = 0.8f;

    /// <summary>The share of the reload at which the stake is laid in (the sound's laying in, 0.4 s before the end of 3 s).</summary>
    public const float StakeAt = 0.8667f;

    /// <summary>The crank turns this many times while the rope is drawn.</summary>
    public const int CrankTurns = 3;

    /// <summary>The stake is laid in from this far above its groove (file units, metres).</summary>
    public const float StakeDropHeight = 0.04f;

    /// <summary>The crossbow's grip from the file into camera space; <paramref name="kick"/>: 1 right after a shot, easing to 0.</summary>
    public static Matrix4x4 Pose(float kick) => Pose(kick, 0f, 0f);

    /// <summary>The same in a reload (<paramref name="reload"/>: how far it has come, <paramref name="weight"/>: how much of its motion is shown, see <see cref="ReloadMotion"/>).</summary>
    public static Matrix4x4 Pose(float kick, float reload, float weight)
    {
        var (dip, roll, shift) = ReloadOffsets(reload);
        return WeaponPose.FileToCamera *
            Matrix4x4.CreateRotationZ(Radians(RollDegrees - (roll * weight))) *
            Matrix4x4.CreateRotationX(Radians((-RecoilLiftDegrees * kick) + (dip * weight))) *
            Matrix4x4.CreateTranslation(Grip + (RecoilShift * kick) + (shift * weight));
    }

    /// <summary>
    /// The reload motion at <paramref name="p"/> (0..1): the muzzle's dip (degrees), the extra roll (degrees) and the shift
    /// of the hand (camera space). In by 8 %, held while the rope is drawn, a small nudge where the string catches
    /// (<see cref="CockedAt"/>), back by 93 %. At 0 and 1 everything is zero.
    /// </summary>
    public static (float Dip, float Roll, Vector3 Shift) ReloadOffsets(float p)
    {
        p = Math.Clamp(p, 0f, 1f);
        var env = p < 0.08f ? Smooth(p / 0.08f)
            : p < 0.85f ? 1f
            : p < 0.93f ? 1f - Smooth((p - 0.85f) / 0.08f)
            : 0f;
        var nudge = p is >= CockedAt and < CockedAt + 0.06f ? MathF.Sin(MathF.PI * (p - CockedAt) / 0.06f) * 0.01f : 0f;
        return (ReloadDipDegrees * env, ReloadRollDegrees * env, (ReloadShift * env) + new Vector3(0f, -nudge, -nudge));
    }

    /// <summary>
    /// How far the crank has turned (degrees) at reload progress <paramref name="p"/>: <see cref="CrankTurns"/> whole turns
    /// between 5 % and the catch, slow at first and then faster (the sound's rope rises in pitch); a whole number of turns,
    /// so it ends where it began.
    /// </summary>
    public static float CrankDegrees(float p)
    {
        var u = Math.Clamp((p - 0.05f) / (CockedAt - 0.05f), 0f, 1f);
        return 360f * CrankTurns * (u * u);
    }

    /// <summary>How high above its groove the stake hangs while it is laid in: 0 once it lies (and before, when it is not drawn).</summary>
    public static float StakeLift(float p) =>
        p < StakeAt ? 0f : StakeDropHeight * (1f - Smooth(Math.Clamp((p - StakeAt) / 0.06f, 0f, 1f)));

    /// <summary>Whether the loaded file is drawn: a stake in the magazine, or the reload is past the catch.</summary>
    public static bool ShowsLoaded(int rounds, bool reloading, float progress) =>
        rounds > 0 || (reloading && progress >= CockedAt);

    /// <summary>Whether the stake is drawn: loaded, and (in a reload) laid in.</summary>
    public static bool ShowsStake(int rounds, bool reloading, float progress) =>
        rounds > 0 || (reloading && progress >= StakeAt);

    /// <summary>The turns of the three parts for <see cref="Rig.Pose"/>: the crank around the drum's axis (x), the stake lifted in a reload.</summary>
    public static void Turns(float reload, float weight, Span<Matrix4x4> turns)
    {
        if (turns.Length != Parts.Count)
        {
            throw new ArgumentException($"The rig has {Parts.Count} parts.");
        }

        turns[0] = Matrix4x4.Identity;
        turns[CrankIndex] = weight > 0f ? Matrix4x4.CreateRotationX(Radians(CrankDegrees(reload) * weight)) : Matrix4x4.Identity;
        turns[StakeIndex] = weight > 0f ? Matrix4x4.CreateTranslation(0f, StakeLift(reload) * weight, 0f) : Matrix4x4.Identity;
    }

    /// <summary>The turn of the sleeve's forearm around its pivot in the pose <see cref="Pose(float, float, float)"/> gives.</summary>
    public static Matrix4x4 Forearm(float kick, float reload, float weight, Vector3 pivot) =>
        WeaponPose.ForearmTurn(Pose(kick, reload, weight), Pose(0f), pivot, WeaponPose.PistolShoulder);

    private static float Smooth(float u) => u * u * (3f - (2f * u));

    private static float Radians(float degrees) => degrees * MathF.PI / 180f;
}
