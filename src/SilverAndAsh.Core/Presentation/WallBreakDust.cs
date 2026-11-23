using System.Numerics;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The dust of a cracked wall breaking (issues #185, #614): puffs start in the lower part of the wall, one after the
/// other in the first ticks, drift away from whoever struck and a little up, slow down, swell and thin out - a soft
/// cloud drawn like the column's (<see cref="DustCloud"/>), thinner, so the stone chunks flying through it stay in sight.
/// Same ticks since the break and same wall, same puffs: no random numbers of its own, nothing that changes the game.
/// </summary>
public static class WallBreakDust
{
    /// <summary>How many puffs one breaking wall gives.</summary>
    public const int PuffCount = 12;

    /// <summary>The latest a puff starts after the break, in ticks (0.25 s).</summary>
    public const int MaxDelayTicks = 15;

    /// <summary>How long one puff lasts from its start, in ticks (1.2 s).</summary>
    public const int LifeTicks = 72;

    /// <summary>Ticks from its start until a puff is at its densest.</summary>
    public const int SwellTicks = 8;

    /// <summary>
    /// How dense a puff gets at most (0 to 1; the column's dust goes up to 1). Thin enough that a chunk behind the
    /// thickest part of the cloud still shows through (provisional until the play test, issue #614).
    /// </summary>
    public const float Density = 0.8f;

    /// <summary>Ticks after the break until the last puff is gone.</summary>
    public const int Ticks = MaxDelayTicks + LifeTicks;

    /// <summary>
    /// The puffs <paramref name="ticksSinceBreak"/> ticks after wall number <paramref name="wall"/> broke, added to
    /// <paramref name="puffs"/>. The wall fills the box from <paramref name="boxMin"/> to <paramref name="boxMax"/>;
    /// <paramref name="away"/> is the flat direction away from the one who struck it.
    /// </summary>
    public static void Puffs(int ticksSinceBreak, int wall, Vector3 boxMin, Vector3 boxMax, Vector3 away, ICollection<DustPuff> puffs)
    {
        ArgumentNullException.ThrowIfNull(puffs);
        var size = boxMax - boxMin;
        var side = new Vector3(-away.Z, 0f, away.X);
        for (var puff = 0; puff < PuffCount; puff++)
        {
            var age = ticksSinceBreak - (int)(MaxDelayTicks * Unit(wall, puff, 0));
            if (age < 0 || age >= LifeTicks)
            {
                continue;
            }

            var start = boxMin + new Vector3(Unit(wall, puff, 1) * size.X, Unit(wall, puff, 2) * size.Y * 0.7f, Unit(wall, puff, 3) * size.Z);
            var drift = (away * (0.3f + (1f * Unit(wall, puff, 4)))) + (side * (-0.5f + Unit(wall, puff, 5))) + new Vector3(0f, 0.2f + (0.5f * Unit(wall, puff, 6)), 0f);
            var radius = 0.35f + (0.25f * Unit(wall, puff, 7));
            puffs.Add(Puff(start, drift, radius, age));
        }
    }

    // One puff `age` ticks after its start: it travels its drift, slowing down; it grows the whole time and is densest
    // after SwellTicks, then thins out with the square of the time left.
    private static DustPuff Puff(Vector3 start, Vector3 drift, float radius, int age)
    {
        var life = age / (float)LifeTicks;
        var travelled = 1f - ((1f - life) * (1f - life));
        var grown = 0.6f + (1.2f * MathF.Sqrt(life));
        var density = age < SwellTicks ? (age + 1) / (float)SwellTicks : 1f;
        var thinning = (1f - life) * (1f - life);
        return new DustPuff(start + (drift * travelled), radius * grown, Density * density * thinning);
    }

    private static float Unit(int wall, int puff, int salt) => StableUnit.Of(wall, puff, salt);
}
