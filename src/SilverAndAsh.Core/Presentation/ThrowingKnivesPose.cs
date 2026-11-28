using System.Numerics;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The throwing knives in the right hand, in flight and stuck (issue #821; model E, pose s12 and throw B "Durchzug" from
/// the probes of issue #204). Up to three knives stand as a fan out of the cuff of the pistol's sleeve; there is no hand,
/// the cuff's lid hides where the grips end. The knife in the hand is its own file, cut off behind the lid
/// (throwing_knife_hand.glb, drawn once per knife); the whole knife (throwing_knife.glb) is what flies and sticks.
/// <para>
/// The sleeve stands unturned at <see cref="Hold"/>. The knives are placed in the sleeve's file space
/// (<see cref="InSleeve"/>: +x is the left of the picture, y up, z forward, the sleeve's hold point at 0) and a motion
/// (<see cref="KnifeMotion"/>) turns cuff and knives together around the hold point - the cut knives are never pulled
/// out of the cuff. A throw takes <see cref="ThrowTicks"/> ticks from the release; the thrown knife is gone from the
/// hand at once and flies in the world. The reload follows the reload time of balance.json (a share 0..1).
/// </para>
/// </summary>
public static class ThrowingKnivesPose
{
    /// <summary>The knife in the hand: cut off behind the cuff's lid.</summary>
    public const string HandFile = "throwing_knife_hand.glb";

    /// <summary>The whole knife, for the flight and for what it sticks in.</summary>
    public const string KnifeFile = "throwing_knife.glb";

    /// <summary>The sleeve the knives stand in: the pistol's, two parts (<see cref="WeaponPose.HandParts"/>).</summary>
    public const string SleeveFile = "pistol_hand.glb";

    /// <summary>The one part of both files; its origin is the base of the blade, the tip points to +z.</summary>
    public const string Knife = "knife";

    /// <summary>Both files are 1.8 times as large as the knife is in the world (the scale of all weapons in the hand).</summary>
    public const float FileScale = 1.8f;

    /// <summary>How many knives the hand holds at most: 0 is the right one, 1 the middle one, 2 the left one.</summary>
    public const int Count = 3;

    /// <summary>The fan at rest: the outer knives lean this far away from the middle one (degrees).</summary>
    public const float FanRestDegrees = 17f;

    /// <summary>
    /// Freshly drawn from the sleeve in a reload the fan is still this narrow (degrees) and then opens. The motion probe
    /// had 15; at 15 two grips touch each other just in front of the cuff's lid (rule 30 of the art bible, tested).
    /// </summary>
    public const float FanClosedDegrees = 16f;

    /// <summary>The tips point this far up from the line of sight (degrees).</summary>
    public const float PitchDegrees = 44f;

    /// <summary>The fan leans this far over in the picture (degrees).</summary>
    public const float RollDegrees = 6f;

    /// <summary>The base of the blade stands this far in front of the sleeve's hold point, along the knife (file units).</summary>
    public const float Depth = 0.012f * FileScale;

    /// <summary>The fan turns around a point this far behind the base of the blade (file units).</summary>
    public const float FanPivot = 0.062f * FileScale;

    /// <summary>The fan stands this far to the left of the sleeve's hold point (file units).</summary>
    public const float Offset = 0.008f * FileScale;

    /// <summary>Each knife lies this much further along its thickness than the one to its right, so the blades do not run through each other (file units).</summary>
    public const float Stagger = 0.006f * FileScale;

    /// <summary>A throw's motion takes this many ticks from the release (throw B of the motion probe).</summary>
    public const int ThrowTicks = 40;

    /// <summary>The share of a reload at which the hand is down out of the picture and comes back with three knives.</summary>
    public const float RefilledAt = 0.30f;

    /// <summary>A knife in flight turns this far around its cross axis per metre (degrees; 40 per tick at the 0.5 m a tick of balance.json).</summary>
    public const float SpinDegreesPerMetre = 80f;

    /// <summary>This much of the blade is in what the knife sticks in (metres).</summary>
    public const float StuckDepth = 0.055f;

    /// <summary>A stuck knife's handle hangs this far up from the way it flew (degrees) ...</summary>
    public const float StuckDroopDegrees = 24f;

    /// <summary>... and this far to one side (degrees).</summary>
    public const float StuckYawDegrees = 20f;

    /// <summary>From the base of the blade to the tip (metres in the world; the file's 0.2108 by <see cref="FileScale"/>, tested).</summary>
    public const float BladeLength = 0.1171f;

    /// <summary>Both files have the one part.</summary>
    public static IReadOnlyList<RigPart> Parts { get; } = [new(Knife, null)];

    /// <summary>Where the sleeve's hold point rests (camera space, before <see cref="WeaponPose.ShrinkTowardsEye"/>).</summary>
    public static Vector3 Hold { get; } = new(0.100f, -0.095f, 0.205f);

    /// <summary>The motion at rest: nothing moves, the fan is open.</summary>
    public static KnifeMotion Rest { get; } = new(Vector3.Zero, 0f, 0f, 0f, FanRestDegrees);

    // The order of the throws: the left knife first, then the right one, the middle one last.
    private static readonly int[] ThrowOrder = [2, 0, 1];

    // Throw B at its height: the hand has gone to the middle, down and forward (camera space) and has turned.
    private static readonly Vector3 ThrowShift = new(-0.085f, -0.050f, 0.060f);
    private const float ThrowPitch = -38f;
    private const float ThrowYaw = 8f;
    private const float ThrowRoll = 24f;

    // The reload: the hand goes away down to the right and back (camera space), turned.
    private static readonly Vector3 AwayShift = new(0.060f, -0.240f, -0.030f);
    private const float AwayPitch = -35f;
    private const float AwayRoll = -15f;
    private const float DownBy = 0.12f;
    private const float UpFrom = 0.50f;
    private const float FanFrom = 0.72f;
    private const float OpenBy = 0.88f;
    private const float SetFrom = 0.90f;

    /// <summary>
    /// Whether knife <paramref name="index"/> is in the hand with <paramref name="rounds"/> left in the magazine: all
    /// three with three; the left one is thrown first, then the right one, the middle one last.
    /// </summary>
    public static bool Shows(int index, int rounds) =>
        Array.IndexOf(ThrowOrder, index) >= Count - Math.Clamp(rounds, 0, Count);

    /// <summary>
    /// How many knives the hand shows: the magazine's <paramref name="rounds"/> - in a reload only until the hand is down
    /// out of the picture (<see cref="RefilledAt"/>), from there the full <paramref name="size"/>, though the magazine
    /// counts them only at the reload's end.
    /// </summary>
    public static int ShownRounds(int rounds, int size, bool reloading, float progress) =>
        reloading && progress >= RefilledAt ? size : rounds;

    /// <summary>
    /// How much of throw B is in the pose <paramref name="tick"/> ticks after the release: at the release the hand is
    /// already on its way (0.3), snaps through to the height by tick 6, swings a little past it until tick 10, comes back
    /// until tick 34 and settles with a small dip under rest. 0 from <see cref="ThrowTicks"/> on.
    /// </summary>
    public static float ThrowAmount(float tick)
    {
        if (tick < 0f || tick >= ThrowTicks)
        {
            return 0f;
        }

        if (tick <= 6f)
        {
            return 0.30f + (0.70f * EaseOut(tick / 6f));
        }

        if (tick <= 10f)
        {
            return 1f + (0.08f * Smooth((tick - 6f) / 4f));
        }

        if (tick <= 34f)
        {
            return 1.08f * (1f - Smooth((tick - 10f) / 24f));
        }

        return -0.06f * MathF.Sin(MathF.PI * (tick - 34f) / 6f);
    }

    /// <summary>
    /// The motion of the hand <paramref name="tick"/> ticks after a knife was released (throw B "Durchzug"). On the way
    /// back the hand comes sideways sooner than up: an arc, not the same line back.
    /// </summary>
    public static KnifeMotion Throw(float tick)
    {
        var amount = ThrowAmount(tick);
        var back = tick > 10f && amount > 0f;
        var sideways = back ? MathF.Pow(amount, 0.6f) : amount;
        var upwards = back ? MathF.Pow(amount, 1.5f) : amount;
        return new KnifeMotion(
            new Vector3(ThrowShift.X * sideways, ThrowShift.Y * upwards, ThrowShift.Z * amount),
            ThrowPitch * amount, ThrowYaw * amount, ThrowRoll * sideways, FanRestDegrees);
    }

    /// <summary>
    /// The motion of the hand at <paramref name="progress"/> (0..1) of a reload: down out of the picture by 12 %, away
    /// until 50 % (at <see cref="RefilledAt"/> it holds three knives again, close together), up by 72 % with a little
    /// overshoot, then the fan opens from <see cref="FanClosedDegrees"/> until 88 % and the hand sets itself with a
    /// small tug. At 0 and 1 it is <see cref="Rest"/>.
    /// </summary>
    public static KnifeMotion Reload(float progress)
    {
        var p = Math.Clamp(progress, 0f, 1f);
        if (p <= 0f)
        {
            return Rest;
        }

        var away = p < DownBy ? EaseIn(p / DownBy)
            : p < UpFrom ? 1f
            : 1f - EaseOut((p - UpFrom) / (FanFrom - UpFrom));
        var over = 0.012f * MathF.Sin(MathF.PI * Math.Clamp((p - 0.62f) / 0.20f, 0f, 1f));
        var u = Math.Clamp((p - FanFrom) / (OpenBy - FanFrom), 0f, 1f);

        // The fan stays open on the way down; the fresh knives come up close together and open in sight.
        var spread = p < RefilledAt ? 1f
            : p >= FanFrom ? 1f - ((1f - u) * (1f - u) * (1f - u)) + (0.9f * u * (1f - u) * (1f - u))
            : 0f;
        var v = Math.Clamp((p - SetFrom) / (1f - SetFrom), 0f, 1f);
        var tug = 3f * MathF.Sin(MathF.PI * v) * (1f - v);
        return new KnifeMotion(
            (AwayShift * away) + new Vector3(0f, over, 0f),
            (AwayPitch * away) + tug, 0f, AwayRoll * away,
            FanClosedDegrees + ((FanRestDegrees - FanClosedDegrees) * spread));
    }

    /// <summary>
    /// The motion as drawn: the throw <paramref name="throwTick"/> ticks after the last release and, on top of it, the
    /// reload at <paramref name="reload"/> shown with <paramref name="weight"/> (0..1, see <see cref="ReloadMotion"/>).
    /// </summary>
    public static KnifeMotion Motion(float throwTick, float reload, float weight)
    {
        var thrown = Throw(throwTick);
        if (weight <= 0f)
        {
            return thrown;
        }

        var loading = Reload(reload);
        return new KnifeMotion(
            thrown.Shift + (loading.Shift * weight),
            thrown.PitchDegrees + (loading.PitchDegrees * weight),
            thrown.YawDegrees + (loading.YawDegrees * weight),
            thrown.RollDegrees + (loading.RollDegrees * weight),
            FanRestDegrees + ((loading.FanDegrees - FanRestDegrees) * weight));
    }

    /// <summary>
    /// The sleeve from its file into camera space in <paramref name="motion"/>: turned around its hold point (the file's
    /// origin) - pitch first, then roll, then yaw -, then moved to <see cref="Hold"/> and by the motion's shift.
    /// </summary>
    public static Matrix4x4 Sleeve(KnifeMotion motion) =>
        Matrix4x4.CreateRotationX(Radians(-motion.PitchDegrees)) *
        Matrix4x4.CreateRotationZ(Radians(motion.RollDegrees)) *
        Matrix4x4.CreateRotationY(Radians(motion.YawDegrees)) *
        WeaponPose.FileToCamera *
        Matrix4x4.CreateTranslation(Hold + motion.Shift);

    /// <summary>
    /// Knife <paramref name="index"/> (0 right, 1 middle, 2 left) from its file into the sleeve's file space, the fan
    /// open by <paramref name="fanDegrees"/>: staggered along its thickness, its broad side turned to the eyes, leant in
    /// the fan around <see cref="FanPivot"/>, pushed out of the cuff by <see cref="Depth"/>, the tip raised, the fan
    /// leant over and set to the left by <see cref="Offset"/>.
    /// </summary>
    public static Matrix4x4 InSleeve(int index, float fanDegrees)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
        return Matrix4x4.CreateTranslation(Stagger * index, 0f, 0f) *
            Matrix4x4.CreateRotationZ(Radians(-90f)) *
            Matrix4x4.CreateTranslation(0f, 0f, FanPivot) *
            Matrix4x4.CreateRotationY(Radians(fanDegrees * (index - 1))) *
            Matrix4x4.CreateTranslation(0f, 0f, Depth - FanPivot) *
            Matrix4x4.CreateRotationX(Radians(-PitchDegrees)) *
            Matrix4x4.CreateRotationZ(Radians(-RollDegrees)) *
            Matrix4x4.CreateTranslation(Offset, 0f, 0f);
    }

    /// <summary>Knife <paramref name="index"/> from its file into camera space in <paramref name="motion"/>.</summary>
    public static Matrix4x4 InHand(int index, KnifeMotion motion) => InSleeve(index, motion.FanDegrees) * Sleeve(motion);

    /// <summary>The turn of the sleeve's forearm around its <paramref name="pivot"/> in <paramref name="motion"/>: it keeps pointing at the shoulder.</summary>
    public static Matrix4x4 Forearm(KnifeMotion motion, Vector3 pivot) =>
        WeaponPose.ForearmTurn(Sleeve(motion), Sleeve(Rest), pivot, WeaponPose.PistolShoulder);

    /// <summary>
    /// The whole knife from its file into the world in flight: flying along <paramref name="direction"/> (length 1), its
    /// tip at <paramref name="point"/> whenever it points ahead, and tumbling around the base of its blade, tip over
    /// handle: the less <paramref name="rangeLeft"/> (the metres it may still fly), the further
    /// (<see cref="SpinDegreesPerMetre"/>).
    /// </summary>
    public static Matrix4x4 Flying(Vector3 point, Vector3 direction, float rangeLeft) =>
        Matrix4x4.CreateScale(1f / FileScale) *
        Flat *
        Matrix4x4.CreateRotationX(Radians(-rangeLeft * SpinDegreesPerMetre)) *
        Matrix4x4.CreateTranslation(0f, 0f, -BladeLength) *
        Along(direction) *
        Matrix4x4.CreateTranslation(point);

    /// <summary>
    /// The whole knife from its file into the world where it sticks: <see cref="StuckDepth"/> of the blade beyond
    /// <paramref name="point"/> (where it hit) along <paramref name="direction"/> (the way it flew, length 1), the handle
    /// hanging up and to one side - to which side follows <paramref name="id"/>, so knives side by side differ.
    /// </summary>
    public static Matrix4x4 Stuck(Vector3 point, Vector3 direction, int id)
    {
        var side = (id & 1) == 0 ? 1f : -1f;
        return Matrix4x4.CreateTranslation(0f, 0f, -BladeLength * FileScale) *
            Matrix4x4.CreateScale(1f / FileScale) *
            Flat *
            Matrix4x4.CreateRotationX(Radians(StuckDroopDegrees)) *
            Matrix4x4.CreateRotationY(Radians(StuckYawDegrees * side)) *
            Matrix4x4.CreateTranslation(0f, 0f, StuckDepth) *
            Along(direction) *
            Matrix4x4.CreateTranslation(point);
    }

    // Out of the hand the knife lies flat: its broad side up and down (in the file it stands on edge). So the thrower,
    // who looks along the flight, sees the broad side whenever the knife tumbles or hangs - on edge it would be a line
    // 4 mm thin.
    private static readonly Matrix4x4 Flat = Matrix4x4.CreateRotationZ(Radians(90f));

    // A turn that brings +z onto `direction` and keeps +y as far up as it goes (straight up or down: any side will do).
    private static Matrix4x4 Along(Vector3 direction)
    {
        var forward = Vector3.Normalize(direction);
        var across = Vector3.Cross(Vector3.UnitY, forward);
        var right = across.LengthSquared() < 1e-6f ? Vector3.UnitX : Vector3.Normalize(across);
        var up = Vector3.Cross(forward, right);
        return new Matrix4x4(
            right.X, right.Y, right.Z, 0f,
            up.X, up.Y, up.Z, 0f,
            forward.X, forward.Y, forward.Z, 0f,
            0f, 0f, 0f, 1f);
    }

    private static float Smooth(float u)
    {
        u = Math.Clamp(u, 0f, 1f);
        return u * u * (3f - (2f * u));
    }

    private static float EaseOut(float u)
    {
        u = Math.Clamp(u, 0f, 1f);
        return 1f - ((1f - u) * (1f - u) * (1f - u));
    }

    private static float EaseIn(float u)
    {
        u = Math.Clamp(u, 0f, 1f);
        return u * u * u;
    }

    private static float Radians(float degrees) => degrees * MathF.PI / 180f;
}

/// <summary>
/// How the hand with the throwing knives is moved out of its rest (<see cref="ThrowingKnivesPose"/>).
/// </summary>
/// <param name="Shift">The hand is moved by this (camera space, metres, before <see cref="WeaponPose.ShrinkTowardsEye"/>).</param>
/// <param name="PitchDegrees">Turned around the cross axis; negative: the tips come down and forward.</param>
/// <param name="YawDegrees">Turned around the vertical axis; positive: the tips turn to the left.</param>
/// <param name="RollDegrees">Turned around the line of sight; positive: the top leans to the right.</param>
/// <param name="FanDegrees">The outer knives lean this far away from the middle one.</param>
public readonly record struct KnifeMotion(Vector3 Shift, float PitchDegrees, float YawDegrees, float RollDegrees, float FanDegrees);

/// <summary>
/// The clock of the throw's motion in the view (<see cref="ThrowingKnivesPose.Throw"/>): started by the release of a
/// knife, it counts the ticks since. It belongs to the drawn weapon like <see cref="ReloadMotion"/>: another weapon
/// drawn ends it, so a throw never shows on the weapon drawn after it.
/// </summary>
public sealed class KnifeThrowClock
{
    private WeaponKind? _drawn;

    /// <summary>Ticks since the last release; <see cref="ThrowingKnivesPose.ThrowTicks"/> when no throw is running.</summary>
    public float Tick { get; private set; } = ThrowingKnivesPose.ThrowTicks;

    /// <summary>
    /// A shot of <paramref name="weapon"/> was fired: a knife starts the throw anew. Who throws a knife has the knives
    /// in the hand - so a throw in the very tick the knives were drawn is not taken for a weapon switch by the next
    /// <see cref="Step"/>.
    /// </summary>
    public void Released(WeaponKind weapon)
    {
        if (weapon == WeaponKind.ThrowingKnives)
        {
            _drawn = weapon;
            Tick = 0f;
        }
    }

    /// <summary>Lets <paramref name="frameSeconds"/> pass with <paramref name="drawn"/> in the hand.</summary>
    public void Step(WeaponKind drawn, float frameSeconds)
    {
        if (_drawn != drawn)
        {
            _drawn = drawn;
            Tick = ThrowingKnivesPose.ThrowTicks;
        }

        Tick = MathF.Min(ThrowingKnivesPose.ThrowTicks, Tick + (frameSeconds * SimulationClock.TicksPerSecond));
    }
}
