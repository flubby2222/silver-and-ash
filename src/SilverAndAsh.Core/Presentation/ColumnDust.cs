using System.Numerics;

namespace SilverAndAsh.Core.Presentation;

/// <summary>One puff of dust: its middle in the world, its size (radius in metres) and how dense it is (0 to 1).</summary>
public readonly record struct DustPuff(Vector3 Position, float Radius, float Opacity);

/// <summary>
/// The dust of a breaking column (issue #601): at every occasion of the fall (grinding, tearing, each piece landing, the
/// swap to the broken model - <see cref="ColumnFallTable.Dust"/>) a soft cloud of puffs swells up, drifts outwards and
/// upwards, slows down and thins out. The client's ruling on the probe: softer than its edged stones, a soft cloud.
/// Same ticks since the break, same puffs: no random numbers of its own, nothing that changes the game.
/// </summary>
public static class ColumnDust
{
    /// <summary>How long one cloud lasts, in ticks (0.9 s).</summary>
    public const int LifeTicks = 54;

    /// <summary>
    /// The colour of the dust before light and fog: the brightest step of the ramp "Stein" in
    /// assets/palette/hauptpalette.json - the dust of the stone it comes from, never red.
    /// </summary>
    public static (byte R, byte G, byte B) Colour { get; } = (151, 127, 137);

    /// <summary>Ticks until a cloud is at its densest.</summary>
    public const int SwellTicks = 7;

    /// <summary>Ticks after the break until the last cloud is gone.</summary>
    public static int Ticks
    {
        get
        {
            var last = 0;
            var dust = ColumnFallTable.Dust;
            for (var i = 0; i < dust.Length; i += 6)
            {
                last = Math.Max(last, (int)dust[i]);
            }

            return last + LifeTicks;
        }
    }

    /// <summary>
    /// The puffs <paramref name="ticksSinceBreak"/> ticks after the break of a column placed by <paramref name="figure"/>
    /// (<see cref="ColumnBreakLook.Figure"/>), added to <paramref name="puffs"/>.
    /// </summary>
    public static void Puffs(int ticksSinceBreak, Matrix4x4 figure, ICollection<DustPuff> puffs)
    {
        ArgumentNullException.ThrowIfNull(puffs);
        var dust = ColumnFallTable.Dust;
        for (int i = 0, occasion = 0; i < dust.Length; i += 6, occasion++)
        {
            var age = ticksSinceBreak - (int)dust[i];
            if (age < 0 || age >= LifeTicks)
            {
                continue;
            }

            var place = new Vector3(dust[i + 1], dust[i + 2], dust[i + 3]);
            var size = dust[i + 4];
            var count = (int)(dust[i + 5] * 1.6f);
            for (var puff = 0; puff < count; puff++)
            {
                puffs.Add(Puff(occasion, puff, place, size, age, figure));
            }
        }
    }

    // One puff `age` ticks into its cloud. Outwards at a random angle, a little up, slowing down; it grows the whole time
    // and is densest after SwellTicks, then thins out with the square of the time left.
    private static DustPuff Puff(int occasion, int puff, Vector3 place, float size, int age, Matrix4x4 figure)
    {
        var angle = Unit(occasion, puff, 0) * 2f * MathF.PI;
        var speed = 1.6f + (2f * Unit(occasion, puff, 1));
        var radius = size * 1.5f * (0.6f + (0.5f * Unit(occasion, puff, 2)));
        var rise = 0.8f + (0.8f * Unit(occasion, puff, 3));
        var start = new Vector3(MathF.Cos(angle) * radius * 0.9f, 0.25f * Unit(occasion, puff, 4), MathF.Sin(angle) * radius * 0.9f);
        var drift = new Vector3(MathF.Cos(angle) * speed, rise, MathF.Sin(angle) * speed);

        var life = age / (float)LifeTicks;
        var travelled = 0.6f * (1f - ((1f - life) * (1f - life)));
        var grown = 0.45f + (0.95f * MathF.Sqrt(life));
        var density = age < SwellTicks ? (age + 1) / (float)SwellTicks : 1f;
        var thinning = (1f - life) * (1f - life);
        return new DustPuff(
            Vector3.Transform(place + start + (drift * travelled), figure),
            radius * grown,
            density * thinning);
    }

    private static float Unit(int occasion, int puff, int salt) => StableUnit.Of(occasion, puff, salt);
}
