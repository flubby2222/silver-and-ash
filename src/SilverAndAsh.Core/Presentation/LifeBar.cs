namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// A tick mark on the life bar (issue #435): <paramref name="X"/> columns right of the bar's left end, long on every
/// fifth one, dark while it lies over the filled part (<paramref name="IsOverFill"/>), grey over the empty part.
/// </summary>
public readonly record struct LifeBarTick(int X, bool IsLong, bool IsOverFill);

/// <summary>
/// The shape of the life bar (issue #435, layout B): <paramref name="Length"/> columns with its right end cap,
/// <paramref name="Filled"/> columns of the bar before the cap filled from the left, the cap itself filled only while
/// <paramref name="IsFull"/>, and the tick marks.
/// </summary>
public sealed record LifeBarShape(int Length, int Filled, bool IsFull, IReadOnlyList<LifeBarTick> Ticks);

/// <summary>The life bar that grows with the highest health: pure arithmetic, the HUD layout places its parts.</summary>
public static class LifeBar
{
    /// <summary>Every so many tick marks one is long.</summary>
    public const int LongTickEvery = 5;

    /// <summary>
    /// The life bar of a player with the highest health <paramref name="maxLife"/> and <paramref name="life"/> of it left
    /// (0 to 1), one column per point of health, between the end caps <paramref name="capWidth"/> wide and at most
    /// <paramref name="longest"/> long. The fill runs between the two caps; a tick mark every
    /// <paramref name="lifePerTick"/> points from the left cap on - one per health boon - none without a step.
    /// </summary>
    public static LifeBarShape Shape(int maxLife, float life, int lifePerTick, int capWidth, int longest)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capWidth);
        ArgumentOutOfRangeException.ThrowIfLessThan(longest, (2 * capWidth) + 1);

        var length = Math.Clamp(maxLife, (2 * capWidth) + 1, longest);
        var inner = length - (2 * capWidth);
        var share = float.IsNaN(life) ? 0f : Math.Clamp(life, 0f, 1f);
        var reached = (int)MathF.Round(inner * share, MidpointRounding.AwayFromZero);
        var isFull = share > 0f && reached == inner;
        var edge = capWidth + reached;
        var filled = share <= 0f ? 0 : edge;

        var ticks = new List<LifeBarTick>();
        if (lifePerTick > 0)
        {
            for (var i = 1; capWidth + (lifePerTick * i) < length - capWidth; i++)
            {
                var x = capWidth + (lifePerTick * i);
                ticks.Add(new LifeBarTick(x, i % LongTickEvery == 0, x < filled));
            }
        }

        return new LifeBarShape(length, filled, isFull, ticks);
    }
}
