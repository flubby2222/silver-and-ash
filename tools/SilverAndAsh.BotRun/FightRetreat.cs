using System.Numerics;

namespace SilverAndAsh.BotRun;

/// <summary>
/// The ground a blow that is coming will hit: a circle of <paramref name="Reach"/> round <paramref name="Centre"/>,
/// when it has a direction (<paramref name="Facing"/>) only the part within <paramref name="CosHalfAngle"/> of it.
/// A full circle has the cosine -1. (Plain flat numbers, so a decision can be tested without a world.)
/// </summary>
internal readonly record struct StrikeZone(Vector2 Centre, float Reach, Vector2 Facing, float CosHalfAngle)
{
    public bool Contains(Vector2 point)
    {
        var offset = point - Centre;
        var length = offset.Length();
        return length <= Reach && (length < 0.0001f || Vector2.Dot(offset / length, Facing) >= CosHalfAngle);
    }
}

/// <summary>One enemy near the bot: where it stands, how wide its body is, and the ground of its blow when one is coming.</summary>
internal readonly record struct Foe(Vector2 Position, float Radius, StrikeZone? Strike);

/// <summary>
/// Where the bot steps when it leaves the reach of an enemy (issue #805). It looks at sixteen directions: a direction
/// counts only when the ground is free for <see cref="Step"/> metres, it is better the fewer of the blows that are
/// coming still hit its end point (<see cref="Margin"/> to spare), then the farther the way keeps from every enemy's
/// body, then the nearer it is to straight away from the nearest enemy.
/// </summary>
internal static class FightRetreat
{
    /// <summary>How far one retreat looks ahead, metres (about half a second of walking).</summary>
    public const float Step = 3f;

    /// <summary>How far outside a strike zone the end point must lie, metres.</summary>
    public const float Margin = 0.3f;

    /// <summary>A way that stays free this far (metres) is better than one that ends at a wall after <see cref="Step"/>.</summary>
    public const float FarStep = 6f;

    /// <summary>What a free way far ahead is worth in the score.</summary>
    public const float OpenBonus = 3f;

    /// <summary>What it is worth to go on the way of the tick before (no wavering between two ways).</summary>
    public const float KeepHeading = 1f;

    /// <summary>The distance to an enemy's body that counts as safe, metres: more clearance is no better.</summary>
    public const float EnoughClearance = 2f;

    private const int Directions = 16;

    /// <summary>The unit direction to step in, or null when no direction is free.</summary>
    public static Vector2? Choose(Vector2 from, IReadOnlyList<Foe> foes, Func<Vector2, bool> isFree, Vector2? previous = null)
    {
        var away = AwayFrom(from, foes);
        Vector2? best = null;
        var bestScore = float.NegativeInfinity;
        for (var i = 0; i < Directions; i++)
        {
            var angle = i * MathF.Tau / Directions;
            var direction = new Vector2(MathF.Sin(angle), MathF.Cos(angle));
            var end = from + (direction * Step);
            if (!isFree(end))
            {
                continue;
            }

            var score = Score(from, end, Vector2.Dot(direction, away), foes);
            score += isFree(from + (direction * FarStep)) ? OpenBonus : 0f;
            score += previous is { } before ? KeepHeading * Vector2.Dot(direction, before) : 0f;
            if (score > bestScore)
            {
                bestScore = score;
                best = direction;
            }
        }

        return best;
    }

    /// <summary>Higher is better: the blows that miss first, then the room kept from the bodies, then the heading.</summary>
    public static float Score(Vector2 from, Vector2 end, float alignment, IReadOnlyList<Foe> foes)
    {
        var hit = foes.Count(foe => foe.Strike is { } zone && Hits(zone, end));
        var clearance = foes.Count == 0 ? EnoughClearance : foes.Min(foe => SegmentDistance(from, end, foe.Position) - foe.Radius);
        return (-100f * hit) + (10f * MathF.Min(clearance, EnoughClearance)) + alignment;
    }

    // Inside the zone, or just outside it (the margin in length and in angle).
    private static bool Hits(StrikeZone zone, Vector2 point) =>
        zone.Contains(point) || new StrikeZone(zone.Centre, zone.Reach + Margin, zone.Facing, zone.CosHalfAngle - 0.1f).Contains(point);

    private static Vector2 AwayFrom(Vector2 from, IReadOnlyList<Foe> foes)
    {
        if (foes.Count == 0)
        {
            return Vector2.Zero;
        }

        var nearest = foes.MinBy(foe => Vector2.Distance(from, foe.Position));
        var offset = from - nearest.Position;
        return offset.LengthSquared() > 0.0001f ? Vector2.Normalize(offset) : Vector2.Zero;
    }

    private static float SegmentDistance(Vector2 a, Vector2 b, Vector2 point)
    {
        var line = b - a;
        var t = Math.Clamp(Vector2.Dot(point - a, line) / line.LengthSquared(), 0f, 1f);
        return Vector2.Distance(point, a + (line * t));
    }
}
