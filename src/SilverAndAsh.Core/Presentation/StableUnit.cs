namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// A number from 0 to 1 that is the same for the same three integers: an integer hash, no state, nothing random. For
/// effects that must look alike every time they are drawn again (<see cref="ColumnDust"/>, <see cref="WallBreakDust"/>).
/// </summary>
internal static class StableUnit
{
    public static float Of(int a, int b, int c)
    {
        var hash = unchecked((uint)((a * 73856093) ^ (b * 19349663) ^ (c * 83492791)) + 0x9E3779B9u);
        hash ^= hash >> 16;
        hash *= 0x7FEB352Du;
        hash ^= hash >> 15;
        hash *= 0x846CA68Bu;
        hash ^= hash >> 16;
        return (hash & 0xFFFFFF) / (float)0x1000000;
    }
}
