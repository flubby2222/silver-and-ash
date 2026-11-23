namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The colours of the environment's effects and stand-ins, before light and fog (issue #609). Every one is a step of the
/// ramp "Stein" in assets/palette/hauptpalette.json - the stone they come from or stand in for, never red. A test checks
/// them against the palette file, so a colour of its own cannot creep in.
/// </summary>
public static class EffectColours
{
    /// <summary>The dust of a cracked wall breaking: Stein 14, the brightest step (like <see cref="ColumnDust.Colour"/>).</summary>
    public static (byte R, byte G, byte B) WallBreakDust { get; } = (151, 127, 137);

    /// <summary>The shaft of a column whose model is missing (the plain twelve-sided stand-in): Stein 10.</summary>
    public static (byte R, byte G, byte B) ColumnStandIn { get; } = (113, 97, 104);

    /// <summary>The edges of that stand-in: Stein 4.</summary>
    public static (byte R, byte G, byte B) ColumnStandInEdge { get; } = (61, 52, 56);

    /// <summary>Every colour of an environment effect or stand-in, by name - the column's dust included.</summary>
    public static IReadOnlyList<(string Name, (byte R, byte G, byte B) Colour)> All { get; } =
    [
        ("column dust", ColumnDust.Colour),
        ("wall break dust", WallBreakDust),
        ("column stand-in", ColumnStandIn),
        ("column stand-in edge", ColumnStandInEdge),
    ];
}
