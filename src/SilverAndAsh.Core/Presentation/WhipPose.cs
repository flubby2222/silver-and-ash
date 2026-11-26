using System.Numerics;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The whip in the hand (issue #786, model B "Leder" from the probe of issue #202): a handle and a lash of
/// <see cref="SegmentCount"/> rigid links, each hanging on the one before. It comes in two files with the same parts:
/// whip_rest.glb, the lash laid to a short loop that hangs from the hand, and whip_strike.glb, the lash straight along
/// +z. Held and bent at the grip like the club (<see cref="WeaponPose.ClubBend"/>), in the club's sleeve.
/// <para>
/// At rest and while winding up the rest file is drawn; charging whirls its loop around the handle
/// (<see cref="WhirlStep"/>). A swing shows the strike file from the moment it is out (<see cref="ShowsStrike"/>): the
/// lash rolls out from the hand to the tip (<see cref="LashTurns"/>), lies straight at the hit, then sags towards the
/// floor on the way back, until the rest file takes over again. The swing's time follows the whip's cooldown
/// (<see cref="SwingShareOfCooldown"/>). The rest file is drawn whole with the other weapons, within
/// <see cref="WeaponPose.Reach"/> (its loop a little nearer still, <see cref="LoopNearer"/>); of the strike file only
/// the handle - its lash reaches metres into the world and is
/// drawn in the world's depth, unshrunk, so walls in front of it hide it.
/// </para>
/// </summary>
public static class WhipPose
{
    /// <summary>The handle: the root of the whip, its pivot at the grip.</summary>
    public const string Handle = "handle";

    /// <summary>How many links the lash has (seg01 ... seg14).</summary>
    public const int SegmentCount = 14;

    /// <summary>
    /// Which share of the cooldown a swing takes on screen (<see cref="MeleeModelFiles.SwingShareOfCooldown"/>):
    /// most of it - a whip is slower to roll out than a blade is to cut, and is ready again soon after it lies still.
    /// Provisional (issue #786).
    /// </summary>
    public const float SwingShareOfCooldown = 0.9f;

    /// <summary>
    /// The whole whip is drawn this much smaller than its files, around the grip (<see cref="MeleeModelFiles.Scale"/>, as
    /// the axe): its handle, longer than the club's, then ends inside the cuff (tested). Provisional (issue #786).
    /// </summary>
    public const float Scale = 0.75f;

    /// <summary>
    /// The loop of the rest file (every part but the handle) is shrunk towards the eyes this much more than the other
    /// weapons (<see cref="WeaponPose.ShrinkTowardsEye"/>): the picture stays the same, but the loop, which reaches
    /// farther forward than a club's head, stays within <see cref="WeaponPose.Reach"/> (tested). Only the loop - the
    /// handle stays as near as its cuff, which hides its end.
    /// </summary>
    public const float LoopNearer = 0.8f;

    /// <summary>
    /// The straight lash of the strike file is drawn this much shorter (around the first link's pivot): its tip then
    /// lies about as far from the eyes as the whip reaches in balance.json (6 m), not at the 6.8 m of the file at
    /// <see cref="Scale"/> (tested against the balance value). Provisional (issue #786).
    /// </summary>
    public const float LashScale = 0.88f;

    /// <summary>
    /// The straight lash is turned at the handle this far to the side (degrees, around the first link's up axis) ...
    /// The swing holds the handle across the body, pointing right and forward like a club's; turned by this and
    /// <see cref="AimLiftDegrees"/>, the lash leaves it towards the crosshair and lies on the view's axis at the hit
    /// (tested), as the whip hits what it is aimed at. Provisional (issue #786).
    /// </summary>
    public const float AimYawDegrees = 50f;

    /// <summary>... and this far up (degrees; negative: down), see <see cref="AimYawDegrees"/>.</summary>
    public const float AimLiftDegrees = -21f;

    /// <summary>
    /// During a swing the strike file is shown while the swing value (<see cref="WeaponPose.SwingCurve"/>) is at least
    /// this: from half-way out (early in the strike) until the way back is half done.
    /// </summary>
    public const float StrikeFrom = 0.5f;

    /// <summary>Rolled up at the start of the strike, every joint of the lash is bent this far up (degrees): a loop over the hand.</summary>
    public const float CurlDegrees = 24f;

    /// <summary>... and this far to the side the swing comes from (degrees): the lash trails the sweep.</summary>
    public const float TrailDegrees = 4f;

    /// <summary>On the way back every joint sags this far towards the floor (degrees) before the rest file takes over.</summary>
    public const float SagDegrees = 5f;

    /// <summary>
    /// How far the roll runs behind from the first joint to the last, as a share of the strike: the tip straightens
    /// last.
    /// </summary>
    public const float RollLag = 0.8f;

    /// <summary>Charging fully, the loop whirls around the handle this often per second.</summary>
    public const float WhirlTurnsPerSecond = 2.5f;

    /// <summary>The turn of the first link towards the crosshair: <see cref="AimYawDegrees"/>, then <see cref="AimLiftDegrees"/>.</summary>
    public static Matrix4x4 Aim { get; } = Matrix4x4.CreateRotationY(Radians(AimYawDegrees)) * Rig.Lift(AimLiftDegrees);

    /// <summary>The parts in the order of the rig: the handle, then every link hanging on the one before.</summary>
    public static IReadOnlyList<RigPart> Parts { get; } = BuildParts();

    /// <summary>The name of link <paramref name="index"/> (1 ... <see cref="SegmentCount"/>) in the files.</summary>
    public static string Segment(int index) => $"seg{index:00}";

    /// <summary>
    /// Whether the strike file is shown: while a swing runs (<paramref name="progress"/> below 1, the share of the swing's
    /// time gone) and its value <paramref name="swing"/> is at least <see cref="StrikeFrom"/>.
    /// </summary>
    public static bool ShowsStrike(float progress, float swing) => progress < 1f && swing >= StrikeFrom;

    /// <summary>
    /// The turns of the strike file's parts (in the order of <see cref="Parts"/>, the handle unturned) at
    /// <paramref name="progress"/> (0..1 of the swing's time). Rolling out until the hold ends
    /// (<see cref="WeaponPose.HoldEnd"/>): each joint straightens from <see cref="CurlDegrees"/> up and
    /// <see cref="TrailDegrees"/> to the side, the ones nearer the tip later (<see cref="RollLag"/>); straight from the end
    /// of the hold, then sagging by up to <see cref="SagDegrees"/> per joint. The first link also carries
    /// <see cref="LashScale"/>, which shortens the whole lash, and the aim (<see cref="AimYawDegrees"/>,
    /// <see cref="AimLiftDegrees"/>), which turns it towards the crosshair.
    /// </summary>
    public static void LashTurns(float progress, Span<Matrix4x4> turns)
    {
        if (turns.Length != Parts.Count)
        {
            throw new ArgumentException($"The whip has {Parts.Count} parts.", nameof(turns));
        }

        progress = Math.Clamp(progress, 0f, 1f);
        var rolled = progress / WeaponPose.HoldEnd;
        var sag = Smooth((progress - WeaponPose.HoldEnd) / (1f - WeaponPose.HoldEnd)) * SagDegrees;
        turns[0] = Matrix4x4.Identity;
        for (var link = 1; link <= SegmentCount; link++)
        {
            var along = (link - 1f) / (SegmentCount - 1f);
            var left = 1f - Smooth((rolled * (1f + RollLag)) - (RollLag * along));
            var turn = Matrix4x4.CreateRotationY(Radians(TrailDegrees * left)) * Rig.Lift((CurlDegrees * left) - sag);
            turns[link] = link == 1 ? Matrix4x4.CreateScale(LashScale) * turn * Aim : turn;
        }
    }

    /// <summary>
    /// The turns of the rest file's parts: unturned, but the whole loop (the first link and all hanging on it) turned
    /// around the handle by <paramref name="whirl"/> (turns, 0..1, <see cref="WhirlStep"/>).
    /// </summary>
    public static void RestTurns(float whirl, Span<Matrix4x4> turns)
    {
        if (turns.Length != Parts.Count)
        {
            throw new ArgumentException($"The whip has {Parts.Count} parts.", nameof(turns));
        }

        turns.Fill(Matrix4x4.Identity);
        turns[1] = Matrix4x4.CreateRotationZ(whirl * 2f * MathF.PI);
    }

    /// <summary>
    /// The whirl one frame later (turns, 0..1): while charging it runs at <see cref="WhirlTurnsPerSecond"/> times the
    /// wind-up (at least a quarter of it at once); after that it runs on at full speed until the turn is complete and
    /// stops at 0 - the loop hangs down again as it does at rest, it never stops half-way round.
    /// </summary>
    public static float WhirlStep(float whirl, float windUp, bool charging, float frameSeconds)
    {
        if (!charging && whirl <= 0f)
        {
            return 0f;
        }

        var speed = charging ? WhirlTurnsPerSecond * Math.Max(windUp, 0.25f) : WhirlTurnsPerSecond;
        var next = whirl + (speed * frameSeconds);
        return charging ? next - MathF.Floor(next) : next >= 1f ? 0f : next;
    }

    private static List<RigPart> BuildParts()
    {
        var parts = new List<RigPart> { new(Handle, null) };
        for (var link = 1; link <= SegmentCount; link++)
        {
            parts.Add(new(Segment(link), link == 1 ? Handle : Segment(link - 1)));
        }

        return parts;
    }

    private static float Smooth(float u)
    {
        u = Math.Clamp(u, 0f, 1f);
        return u * u * (3f - (2f * u));
    }

    private static float Radians(float degrees) => degrees * MathF.PI / 180f;
}
