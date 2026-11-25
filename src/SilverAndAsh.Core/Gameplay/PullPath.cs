using System.Numerics;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// How far a whip hit pulls a body in (issue #202). It goes straight towards the one who pulls, flat, and is meant to
/// stop <see cref="MeleeBalance.PullTo"/> in front of them. It ends early where the body would run into something that
/// stops it (a wall, a column, a ledge too high to step up), and a walker (a ghoul) also where its feet would leave the
/// floor it stands on - it is never dragged over the edge of a pit. A flier (a bat) keeps its height and minds no floor.
/// The body then slides through its own collision as with a knockback, which would stop it at a wall anyway; walking the
/// path here first makes the pull end where it really ends, so <see cref="MeleePulled.Distance"/> is true.
/// </summary>
internal static class PullPath
{
    /// <summary>The path is walked in steps this long (metres).</summary>
    public const float Step = 0.05f;

    /// <summary>A floor lower than this (metres) counts as a drop; small differences from rounding do not.</summary>
    private const float DropTolerance = 0.01f;

    /// <summary>
    /// A walker stops this far (metres) before the point where its feet would leave the floor, so the slide - added up
    /// tick by tick - cannot end a rounding error over the pit.
    /// </summary>
    private const float EdgeMargin = 0.02f;

    /// <summary>
    /// How many metres <paramref name="body"/> is pulled towards <paramref name="towards"/>, to end
    /// <paramref name="stopAt"/> away from there (flat) - 0 if it is that near already, or if something stops it at
    /// once. <paramref name="stepHeight"/>: how high it can step up; <paramref name="walker"/>: it stands on the floor.
    /// </summary>
    public static float Distance(Level level, CylinderBody body, float stepHeight, bool walker, Vector3 towards, float stopAt)
    {
        ArgumentNullException.ThrowIfNull(level);
        var flat = (towards - body.Position) with { Y = 0f };
        var wanted = flat.Length() - stopAt;
        if (wanted <= 0f)
        {
            return 0f;
        }

        var direction = Vector3.Normalize(flat);
        var floor = Floor(level, body, body.Position);
        var gone = 0f;
        while (gone < wanted)
        {
            var next = MathF.Min(gone + Step, wanted);
            var there = body.Position + (direction * next);
            if (CharacterCollision.Overlaps(level, there, body.Radius, body.Height, stepHeight)
                || (walker && Floor(level, body, there + (direction * EdgeMargin)) < floor - DropTolerance))
            {
                break;
            }

            gone = next;
        }

        return gone;
    }

    private static float Floor(Level level, CylinderBody body, Vector3 at) =>
        CharacterCollision.FloorAndCeiling(level, at, body.Radius, body.Height).Floor;
}
