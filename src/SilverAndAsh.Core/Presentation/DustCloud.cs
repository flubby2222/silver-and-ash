using System.Numerics;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// How a cloud of dust is drawn (issues #601, #614): every <see cref="DustPuff"/> as a few faint, see-through balls inside
/// one another - dense in the middle, fading out to the edge, a soft cloud and no edged blocks. The same for the dust of a
/// breaking column (<see cref="ColumnDust"/>) and of a breaking wall (<see cref="WallBreakDust"/>); the game draws them
/// with one routine. <see cref="Cover"/> tells how much of what lies behind a cloud it hides.
/// </summary>
public static class DustCloud
{
    /// <summary>
    /// The shells of one puff: size (of its radius) and opacity (0 to 255 at full density). Strong enough that the cloud
    /// of a column's landing pieces is plainly seen from 6 m (provisional until the play test, issue #601).
    /// </summary>
    public static IReadOnlyList<(float Scale, float Alpha)> Shells { get; } = [(1f, 40f), (0.72f, 55f), (0.45f, 70f)];

    /// <summary>
    /// The opacity of one shell of <paramref name="puff"/> as drawn (0 to 255), from its <see cref="Shells"/> entry.
    /// </summary>
    public static float Alpha(DustPuff puff, float shellAlpha) => Math.Clamp(shellAlpha * puff.Opacity, 0f, 255f);

    /// <summary>
    /// How much of <paramref name="target"/>, seen from <paramref name="eye"/>, the <paramref name="puffs"/> hide: 0 not at
    /// all, 1 wholly. Every shell whose near side lies between the two lays its opacity over what is behind it, as the
    /// see-through drawing mixes it; a shell the eye is inside shows nothing (only its far side faces the eye).
    /// </summary>
    public static float Cover(Vector3 eye, Vector3 target, IEnumerable<DustPuff> puffs)
    {
        ArgumentNullException.ThrowIfNull(puffs);
        var toTarget = target - eye;
        var distance = toTarget.Length();
        if (distance <= 0f)
        {
            return 0f;
        }

        var direction = toTarget / distance;
        var through = 1f;
        foreach (var puff in puffs)
        {
            foreach (var (scale, alpha) in Shells)
            {
                if (NearSide(eye, direction, puff.Position, puff.Radius * scale) is { } near && near < distance)
                {
                    through *= 1f - (Alpha(puff, alpha) / 255f);
                }
            }
        }

        return 1f - through;
    }

    // Where the ray from `eye` along `direction` first enters the ball, or null if it misses it, the ball lies behind
    // the eye or the eye is inside.
    private static float? NearSide(Vector3 eye, Vector3 direction, Vector3 centre, float radius)
    {
        var toCentre = centre - eye;
        if (toCentre.LengthSquared() <= radius * radius)
        {
            return null;
        }

        var along = Vector3.Dot(toCentre, direction);
        var offSquared = toCentre.LengthSquared() - (along * along);
        if (along <= 0f || offSquared > radius * radius)
        {
            return null;
        }

        return along - MathF.Sqrt((radius * radius) - offSquared);
    }
}
