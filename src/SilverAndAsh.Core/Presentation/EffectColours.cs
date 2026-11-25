namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The colours of the environment's effects and stand-ins, before light and fog (issue #609). Every one is a step of a
/// ramp in assets/palette/hauptpalette.json - mostly the ramp "Stein" of the stone they come from or stand in for, the
/// spark of a bullet impact a step of the warm "Messing/Gold"; never red. A test checks them against the palette file,
/// so a colour of its own cannot creep in.
/// </summary>
public static class EffectColours
{
    /// <summary>The ramp of the stone: walls, floors, ceilings and what breaks off them.</summary>
    public const string Stone = "Stein";

    /// <summary>The warm accent ramp: small things with a purpose, light among them.</summary>
    public const string Brass = "Messing/Gold";

    /// <summary>
    /// The light dressed stone of special portals; its lightest step is the most violet light colour of the palette and
    /// stands for Marked until the trial piece of the effects decides (issue #506).
    /// </summary>
    public const string Ashlar = "Werkstein";

    /// <summary>The cold blue-grey of cloth; its lightest step is the frost of Grave Chill (issue #508).</summary>
    public const string Cloth = "Stoff";

    /// <summary>The dust of a cracked wall breaking: Stein 14, the brightest step (like <see cref="ColumnDust.Colour"/>).</summary>
    public static (byte R, byte G, byte B) WallBreakDust { get; } = (151, 127, 137);

    /// <summary>The shaft of a column whose model is missing (the plain twelve-sided stand-in): Stein 10.</summary>
    public static (byte R, byte G, byte B) ColumnStandIn { get; } = (113, 97, 104);

    /// <summary>The edges of that stand-in: Stein 4.</summary>
    public static (byte R, byte G, byte B) ColumnStandInEdge { get; } = (61, 52, 56);

    /// <summary>The short flash where a bullet hits a wall, floor or ceiling: Messing/Gold 7, the brightest warm step.</summary>
    public static (byte R, byte G, byte B) ImpactSpark { get; } = (238, 228, 160);

    /// <summary>The dark mark the bullet leaves afterwards: Stein 1, the darkest step above the fog's black.</summary>
    public static (byte R, byte G, byte B) ImpactMark { get; } = (38, 34, 40);

    /// <summary>
    /// Every colour of an environment effect or stand-in, by name, with the ramp it is a step of - and the colour of
    /// every status effect on an enemy (<see cref="StatusLooks"/>, issue #506), named "status" and its kind.
    /// </summary>
    public static IReadOnlyList<(string Name, string Ramp, (byte R, byte G, byte B) Colour)> All { get; } =
    [
        ("column dust", Stone, ColumnDust.Colour),
        ("wall break dust", Stone, WallBreakDust),
        ("column stand-in", Stone, ColumnStandIn),
        ("column stand-in edge", Stone, ColumnStandInEdge),
        ("impact spark", Brass, ImpactSpark),
        ("impact mark", Stone, ImpactMark),
        .. StatusLooks.All.Select(look => ($"status {look.Kind}", look.Ramp, look.Colour)),
    ];
}
