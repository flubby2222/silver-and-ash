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
    public const string Arm = "arm";

    /// <summary>The pistol is rolled this far around its barrel, its top towards the middle of the picture (degrees).</summary>
    public const float PistolRollDegrees = 15f;

    /// <summary>Full recoil: the pistol is lifted this far around the grip (degrees, front end up).</summary>
    public const float RecoilLiftDegrees = 14f;

    /// <summary>Full recoil: the hammer falls forward (degrees for <see cref="Rig.Lift"/>).</summary>
    public const float CockFallDegrees = -38f;

    /// <summary>The blade at rest leans this far over, its top towards the middle of the picture (degrees).</summary>
    public const float BladeRestRollDegrees = 20f;

    /// <summary>At the height of a swing it leans this much further (degrees).</summary>
    public const float SwingRollDegrees = 25f;

    /// <summary>At the height of a swing the tip has turned this far to the right (degrees).</summary>
    public const float SwingYawDegrees = 95f;

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
    /// in every pose - the long sleeves run on out of the picture, and only the sleeve at the height of a blade swing
    /// reaches farther within it, out to the left edge of the picture. The player keeps 35 cm
    /// from every wall (its radius) - the rest is room for bob, sway and the landing dip.
    /// </summary>
    public const float Reach = 0.30f;

    /// <summary>
    /// Nothing nearer to the eyes than this is drawn (metres): raylib's near plane. What lies closer is cut off - the
    /// sleeve running back past the eyes to the elbow, but never the glove or the weapon in view.
    /// </summary>
    public const float NearPlane = 0.05f;

    /// <summary>
    /// The models are drawn this much smaller and that much nearer to the eyes, in camera space before the camera
    /// matrix. Shrinking around the eye leaves the picture exactly as it is (every corner stays on its line of sight),
    /// but brings the far end of the barrel, the flash and the blade within <see cref="Reach"/> - out of the walls.
    /// Not smaller: the near plane would cut away the glove (tried with 0.3).
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
    /// A hand file (glove, cuff, sleeve) is one part with its pivot at the grip: it is drawn with the frame of the stock or
    /// the sword and so hangs rigidly on the weapon.
    /// </summary>
    public static IReadOnlyList<RigPart> HandParts { get; } = [new(Arm, null)];

    /// <summary>Where the pistol's grip rests (camera space).</summary>
    public static Vector3 PistolGrip { get; } = new(0.095f, -0.08f, 0.18f);

    /// <summary>Where the blade's grip rests (camera space).</summary>
    public static Vector3 BladeGrip { get; } = new(-0.11f, -0.095f, 0.19f);

    /// <summary>Full recoil: the pistol moves this far up and back.</summary>
    public static Vector3 RecoilShift { get; } = new(0f, 0.012f, -0.035f);

    /// <summary>Fully wound up: the blade hand pulls back and to the left (a little down, the lift raises the tip).</summary>
    public static Vector3 WindUpShift { get; } = new(-0.03f, -0.01f, -0.03f);

    /// <summary>At the height of a swing the blade hand has moved this far to the right and up.</summary>
    public static Vector3 SwingShift { get; } = new(0.06f, 0.04f, 0f);

    /// <summary>From a model file (+x = the weapon's left) to camera space (+x = right): x mirrored.</summary>
    public static Matrix4x4 FileToCamera { get; } = Matrix4x4.CreateScale(-1f, 1f, 1f);

    /// <summary>
    /// The pistol's grip from the file into camera space. <paramref name="kick"/>: 1 right after a shot, easing to 0.
    /// At rest the barrel runs along the line of sight, rolled so its top leans towards the middle of the picture.
    /// </summary>
    public static Matrix4x4 Pistol(float kick) =>
        FileToCamera *
        Matrix4x4.CreateRotationZ(Radians(PistolRollDegrees)) *
        Matrix4x4.CreateRotationX(Radians(-RecoilLiftDegrees * kick)) *
        Matrix4x4.CreateTranslation(PistolGrip + (RecoilShift * kick));

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
        Matrix4x4.CreateRotationX(Radians(-WindUpLiftDegrees * windUp)) *
        Matrix4x4.CreateRotationY(Radians((SwingYawDegrees * swing) - (WindUpYawDegrees * windUp))) *
        Matrix4x4.CreateTranslation(BladeGrip + (WindUpShift * windUp) + new Vector3(tremble, 0f, 0f) + (SwingShift * swing));

    private static float Radians(float degrees) => degrees * MathF.PI / 180f;
}
