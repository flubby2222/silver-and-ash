using System.Numerics;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The parts of the weapon models in the hands (assets/models/pistol.glb, blade.glb and the hands pistol_hand.glb,
/// blade_hand.glb, issue #249) and where they are held: in camera space, x = right, y = up, z = forward (metres), the
/// same space and the same movements as the placeholder boxes.
/// <para>
/// In the files the origin is the grip, the barrel or blade points to +z, and +x is the weapon's <em>left</em> side (a
/// normal right-handed model). Camera space has +x on the right, so every pose starts with <see cref="FileToCamera"/>,
/// a mirror of x. The placeholder's camera matrix mirrors as well - together the two give a real turn again, and
/// the faces of the model keep their front sides.
/// </para>
/// </summary>
public static class WeaponPose
{
    public const string Stock = "stock";
    public const string Barrel = "barrel";
    public const string Cock = "cock";
    public const string Fittings = "fittings";
    public const string Flash = "flash";
    public const string Sword = "sword";

    /// <summary>The cuff of a sleeve with the short piece of sleeve behind it: hangs rigidly on the weapon.</summary>
    public const string Cuff = "arm_cuff";

    /// <summary>The forearm of a sleeve: a long straight tube, its pivot at the back end of the cuff (issue #265).</summary>
    public const string Forearm = "arm_forearm";

    /// <summary>The pistol is rolled this far around its barrel, its top towards the middle of the picture (degrees).</summary>
    public const float PistolRollDegrees = 15f;

    /// <summary>Full recoil: the pistol is lifted this far around the grip (degrees, front end up).</summary>
    public const float RecoilLiftDegrees = 14f;

    /// <summary>Full recoil: the hammer falls forward (degrees for <see cref="Rig.Lift"/>).</summary>
    public const float CockFallDegrees = -38f;

    /// <summary>The blade at rest leans this far over, its top towards the middle of the picture (degrees).</summary>
    public const float BladeRestRollDegrees = 20f;

    /// <summary>At the height of a swing it leans this much further (degrees).</summary>
    public const float SwingRollDegrees = 8f;

    /// <summary>At the height of a swing the tip has turned this far to the right (degrees).</summary>
    public const float SwingYawDegrees = 50f;

    /// <summary>At the height of a swing the tip has dipped this far (degrees, as <see cref="Rig.Lift"/> negated): a chop, not a sweep.</summary>
    public const float SwingDipDegrees = 12f;

    /// <summary>Fully wound up for a charged swing, the tip has turned this far to the left (degrees).</summary>
    public const float WindUpYawDegrees = 35f;

    /// <summary>
    /// Fully wound up, the blade is also lifted this far, its tip up (degrees, as <see cref="Rig.Lift"/>). Without the
    /// lift the sleeve behind the grip swung so close to the eyes that the near plane cut it open in the lower left of
    /// the picture (issue #256); lifted, it runs down out of the picture before it gets that close.
    /// </summary>
    public const float WindUpLiftDegrees = 40f;

    /// <summary>
    /// Every point of a weapon model and its sleeve that lies within the picture stays this close to the eyes (metres),
    /// in every pose, with the forearm turned towards the shoulder as drawn (tested) - the long sleeves run on out of
    /// the picture. The player keeps 35 cm
    /// from every wall (its radius) - the rest is room for bob, sway and the landing dip.
    /// </summary>
    public const float Reach = 0.30f;

    /// <summary>
    /// Nothing nearer to the eyes than this is drawn (metres): raylib's near plane. What lies closer is cut off - the
    /// sleeve running back past the eyes to the elbow, but never the cuff or the weapon in view.
    /// </summary>
    public const float NearPlane = 0.05f;

    /// <summary>
    /// The models are drawn this much smaller and that much nearer to the eyes, in camera space before the camera
    /// matrix. Shrinking around the eye leaves the picture exactly as it is (every corner stays on its line of sight),
    /// but brings the far end of the barrel, the flash and the blade within <see cref="Reach"/> - out of the walls.
    /// Not smaller: the near plane would cut away the hand (tried with 0.3, then still a glove).
    /// </summary>
    public const float ShrinkTowardsEye = 0.5f;

    /// <summary>The pistol's parts in the order of the rig: barrel, hammer, fittings and flash hang on the stock.</summary>
    public static IReadOnlyList<RigPart> PistolParts { get; } =
    [
        new(Stock, null),
        new(Barrel, Stock),
        new(Cock, Stock),
        new(Fittings, Stock),
        new(Flash, Stock),
    ];

    /// <summary>The silver blade is one part.</summary>
    public static IReadOnlyList<RigPart> BladeParts { get; } = [new(Sword, null)];

    /// <summary>
    /// A hand file (pistol_hand.glb, blade_hand.glb) is a sleeve in two parts, both drawn on the frame of the stock or
    /// the sword: the cuff hangs rigidly on the weapon, the forearm turns around its pivot so it always points at the
    /// shoulder (<see cref="ForearmTurn"/>). In the file the forearm already points at the shoulder of the resting pose.
    /// </summary>
    public static IReadOnlyList<RigPart> HandParts { get; } = [new(Cuff, null), new(Forearm, null)];

    /// <summary>
    /// Where the shoulder of the blade's arm is (camera space, before <see cref="ShrinkTowardsEye"/>): below the eyes,
    /// to the left and a little behind them. The blade's forearm always points here.
    /// </summary>
    public static Vector3 BladeShoulder { get; } = new(-0.40f, -0.65f, -0.20f);

    /// <summary>Where the shoulder of the pistol's arm is: the mirror image of <see cref="BladeShoulder"/>.</summary>
    public static Vector3 PistolShoulder { get; } = new(0.40f, -0.65f, -0.20f);

    /// <summary>Where the pistol's grip rests (camera space).</summary>
    public static Vector3 PistolGrip { get; } = new(0.095f, -0.08f, 0.18f);

    /// <summary>Where the blade's grip rests (camera space).</summary>
    public static Vector3 BladeGrip { get; } = new(-0.11f, -0.095f, 0.19f);

    /// <summary>Full recoil: the pistol moves this far up and back.</summary>
    public static Vector3 RecoilShift { get; } = new(0f, 0.012f, -0.035f);

    /// <summary>Fully wound up: the blade hand pulls back and to the left (a little down, the lift raises the tip).</summary>
    public static Vector3 WindUpShift { get; } = new(-0.03f, -0.01f, -0.03f);

    /// <summary>At the height of a swing the blade hand has moved this far to the right and up.</summary>
    public static Vector3 SwingShift { get; } = new(0.09f, 0f, 0.03f);

    /// <summary>
    /// How far a swing has come (0..1 of its time) -> the swing value for <see cref="Blade"/> (0 rest, 1 the height).
    /// Weight comes from the timing: a fast strike (the first quarter), a short hold with a little overshoot where
    /// the blade would meet something, then a slow way back that dips a touch past rest and settles. Values above 1 and
    /// below 0 are the overshoot - small (<see cref="SwingOvershoot"/>).
    /// </summary>
    public static float SwingCurve(float s)
    {
        s = Math.Clamp(s, 0f, 1f);
        if (s < StrikeEnd)
        {
            var u = s / StrikeEnd;
            return 1f - ((1f - u) * (1f - u) * (1f - u));
        }

        if (s < HoldEnd)
        {
            var u = (s - StrikeEnd) / (HoldEnd - StrikeEnd);
            return 1f + (SwingOvershoot * MathF.Sin(MathF.PI * u));
        }

        var back = (s - HoldEnd) / (1f - HoldEnd);
        var eased = back * back * (3f - (2f * back));
        var settle = back > 0.55f ? 0.04f * MathF.Sin(MathF.PI * (back - 0.55f) / 0.45f) : 0f;
        return 1f - eased - settle;
    }

    /// <summary>The strike takes this part of a swing (a quarter: 4 of 17 ticks) ...</summary>
    public const float StrikeEnd = 0.25f;

    /// <summary>... the hold at the height until here ...</summary>
    public const float HoldEnd = 0.40f;

    /// <summary>... overshooting by this much of the swing.</summary>
    public const float SwingOvershoot = 0.06f;

    /// <summary>From a model file (+x = the weapon's left) to camera space (+x = right): x mirrored.</summary>
    public static Matrix4x4 FileToCamera { get; } = Matrix4x4.CreateScale(-1f, 1f, 1f);

    /// <summary>
    /// The pistol's grip from the file into camera space. <paramref name="kick"/>: 1 right after a shot, easing to 0.
    /// At rest the barrel runs along the line of sight, rolled so its top leans towards the middle of the picture.
    /// </summary>
    public static Matrix4x4 Pistol(float kick) => Pistol(kick, 0f, 0f);

    /// <summary>
    /// The same with a reload: <paramref name="reload"/> is how far the reload has come (0..1 of its time),
    /// <paramref name="weight"/> how much of the reload motion is shown (0..1: fades in and, if the reload is cancelled,
    /// out again).
    /// </summary>
    public static Matrix4x4 Pistol(float kick, float reload, float weight)
    {
        var (roll, lift, shift) = ReloadOffsets(reload);
        return FileToCamera *
            Matrix4x4.CreateRotationZ(Radians(PistolRollDegrees - (roll * weight))) *
            Matrix4x4.CreateRotationX(Radians((-RecoilLiftDegrees * kick) - (lift * weight))) *
            Matrix4x4.CreateTranslation(PistolGrip + (RecoilShift * kick) + (shift * weight));
    }

    /// <summary>
    /// The reload motion at <paramref name="p"/> (0..1): extra roll (degrees, the top turning away to the right), extra
    /// lift of the muzzle (degrees) and the shift of the hand (camera space). Away in the first 20 %, held (with two
    /// small taps where the rounds go in) until 80 %, back by 92 %, then the hammer click and a small nudge. At 0 and 1
    /// everything is zero. The pistol tilts to the right and the muzzle rises steeply; the hand itself hardly moves (see <see cref="ReloadShift"/>).
    /// </summary>
    public static (float Roll, float Lift, Vector3 Shift) ReloadOffsets(float p)
    {
        p = Math.Clamp(p, 0f, 1f);
        float env;
        if (p < 0.2f)
        {
            var u = p / 0.2f;
            env = u * u * (3f - (2f * u));
        }
        else if (p < 0.8f)
        {
            env = 1f;
        }
        else if (p < 0.92f)
        {
            var u = (p - 0.8f) / 0.12f;
            env = 1f - (u * u * (3f - (2f * u)));
        }
        else
        {
            env = 0f;
        }

        // Two taps while the rounds go in: the hand bobs.
        var tap = (p is > 0.3f and < 0.7f) ? MathF.Abs(MathF.Sin((p - 0.3f) / 0.4f * 2f * MathF.PI)) * 0.012f : 0f;
        // The nudge of the hammer click.
        var nudge = p > 0.92f ? MathF.Sin(MathF.PI * (p - 0.92f) / 0.08f) * 0.012f : 0f;
        var click = new Vector3(0f, nudge, -nudge);
        return (ReloadRollDegrees * env, ReloadLiftDegrees * env,
            (ReloadShift * env) + new Vector3(0f, -tap, 0f) + click);
    }

    /// <summary>Reloading: the pistol rolls this far (degrees, its top turning away to the right) ...</summary>
    public const float ReloadRollDegrees = 25f;

    /// <summary>... the muzzle rises this far (degrees) ...</summary>
    public const float ReloadLiftDegrees = 40f;

    /// <summary>... and the hand moves a little to the right and back, not down (camera space, metres): more would cover the reload display of the HUD.</summary>
    public static Vector3 ReloadShift { get; } = new(0.01f, 0.0f, -0.02f);

    /// <summary>How fast the reload motion fades in and out (per second): a tenth of a second from nothing to all.</summary>
    public const float ReloadFadePerSecond = 10f;

    /// <summary>
    /// The next value of the reload motion's weight (0..1): rises while a reload runs, falls when it is gone (cancelled by a
    /// weapon switch), so the pistol eases back instead of jumping.
    /// </summary>
    public static float ReloadWeightStep(float weight, bool reloading, float frameSeconds) =>
        Math.Clamp(weight + ((reloading ? 1f : -1f) * frameSeconds * ReloadFadePerSecond), 0f, 1f);

    /// <summary>The hammer while reloading: fallen forward until the click (92 % of the reload), then cocked.</summary>
    public static float ReloadCockDegrees(float reload, float weight, float kick) =>
        reload >= 0.92f || weight <= 0f ? CockDegrees(kick) : CockFallDegrees * Math.Max(kick, weight);

    /// <summary>How far the hammer is turned (degrees for <see cref="Rig.Lift"/>): fallen forward after a shot.</summary>
    public static float CockDegrees(float kick) => CockFallDegrees * kick;

    /// <summary>
    /// The blade's grip from the file into camera space. <paramref name="swing"/>: 0 at rest, 1 at the height of a
    /// swing; <paramref name="windUp"/>: 0 at rest, 1 fully drawn back; <paramref name="tremble"/>: a small sideways
    /// shake when fully charged (metres).
    /// </summary>
    public static Matrix4x4 Blade(float swing, float windUp, float tremble) =>
        FileToCamera *
        Matrix4x4.CreateRotationZ(Radians(-(BladeRestRollDegrees + (SwingRollDegrees * swing)))) *
        Matrix4x4.CreateRotationX(Radians((-WindUpLiftDegrees * windUp) + (SwingDipDegrees * swing))) *
        Matrix4x4.CreateRotationY(Radians((SwingYawDegrees * swing) - (WindUpYawDegrees * windUp))) *
        Matrix4x4.CreateTranslation(BladeGrip + (WindUpShift * windUp) + new Vector3(tremble, 0f, 0f) + (SwingShift * swing));

    /// <summary>
    /// The turn of the blade's forearm around its pivot (file space, for <see cref="Rig.Pose"/>) in the pose
    /// <see cref="Blade"/> gives for the same values. <paramref name="pivot"/>: the forearm's pivot from the file.
    /// </summary>
    public static Matrix4x4 BladeForearm(float swing, float windUp, float tremble, Vector3 pivot) =>
        ForearmTurn(Blade(swing, windUp, tremble), Blade(0f, 0f, 0f), pivot, BladeShoulder);

    /// <summary>The turn of the pistol's forearm around its pivot in the pose <see cref="Pistol"/> gives.</summary>
    public static Matrix4x4 PistolForearm(float kick, Vector3 pivot) =>
        ForearmTurn(Pistol(kick), Pistol(0f), pivot, PistolShoulder);

    /// <summary>The same in a reload (see <see cref="Pistol(float, float, float)"/>).</summary>
    public static Matrix4x4 PistolForearm(float kick, float reload, float weight, Vector3 pivot) =>
        ForearmTurn(Pistol(kick, reload, weight), Pistol(0f), pivot, PistolShoulder);

    /// <summary>
    /// The forearm of a sleeve points from its pivot to the shoulder in every pose: the weapon turns, the cuff turns with
    /// it, the forearm only as far as it must to keep pointing at the shoulder. Returns its turn around the pivot (file
    /// space): the shortest one from where it points at rest to where it points in <paramref name="weapon"/>. The
    /// shortest turn changes smoothly with the pose and never rolls the tube over, as long as the forearm does not
    /// turn right round (it turns far less, tested).
    /// </summary>
    /// <param name="weapon">The weapon's pose, file -> camera space, without bob, sway and shrinking.</param>
    /// <param name="rest">The weapon's resting pose: in it the forearm points as it stands in the file.</param>
    /// <param name="pivot">The forearm's pivot (file space).</param>
    /// <param name="shoulder">The shoulder (camera space).</param>
    public static Matrix4x4 ForearmTurn(Matrix4x4 weapon, Matrix4x4 rest, Vector3 pivot, Vector3 shoulder) =>
        ShortestTurn(ShoulderDirection(rest, pivot, shoulder), ShoulderDirection(weapon, pivot, shoulder));

    /// <summary>
    /// The direction from the forearm's <paramref name="pivot"/> to the <paramref name="shoulder"/> (camera space), in
    /// the file's space of a weapon held in <paramref name="weapon"/> (file -> camera space). The poses mirror x
    /// (<see cref="FileToCamera"/>); inverting the whole pose takes the mirror back with it.
    /// </summary>
    public static Vector3 ShoulderDirection(Matrix4x4 weapon, Vector3 pivot, Vector3 shoulder)
    {
        if (!Matrix4x4.Invert(weapon, out var cameraToFile))
        {
            throw new ArgumentException("A weapon pose must be invertible.", nameof(weapon));
        }

        return Vector3.Normalize(Vector3.Transform(shoulder, cameraToFile) - pivot);
    }

    // The shortest turn that brings the unit vector `from` onto the unit vector `to` (row vectors: from * turn = to).
    // Half-way quaternion: axis from x to, its length the sine; 1 + cosine as the real part, normalized, is the turn
    // by the angle between them. Straight opposite there is no shortest turn - half round about any axis across.
    private static Matrix4x4 ShortestTurn(Vector3 from, Vector3 to)
    {
        var cosine = Vector3.Dot(from, to);
        if (cosine < -0.9999f)
        {
            var across = Vector3.Cross(from, MathF.Abs(from.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY);
            return Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(across), MathF.PI);
        }

        return Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(new Quaternion(Vector3.Cross(from, to), 1f + cosine)));
    }

    private static float Radians(float degrees) => degrees * MathF.PI / 180f;
}
