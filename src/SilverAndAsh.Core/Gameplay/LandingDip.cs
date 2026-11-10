namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// The "nod" when landing: the eyes sink a little and the view tilts down - the harder the landing,
/// the stronger (up to a limit) - then spring back smoothly. Purely a feel effect: it changes the camera,
/// not where the player stands.
/// </summary>
public sealed class LandingDip
{
    // Each tick the dip shrinks to this fraction of itself: exponential "spring back".
    private static readonly float DecayPerTick =
        MathF.Exp(-SimulationClock.SecondsPerTick / Tuning.Player.LandingDipRecoveryTime);

    /// <summary>How far the eyes are currently lowered, in metres (0 = not at all).</summary>
    public float Depth { get; private set; }

    /// <summary>The depth one tick earlier, for smooth drawing between ticks.</summary>
    public float PreviousDepth { get; private set; }

    /// <summary>Advances the effect by one tick: the dip springs back a bit.</summary>
    public void Tick()
    {
        PreviousDepth = Depth;
        Depth *= DecayPerTick;

        // Below 2 mm the dip is invisible (one pixel is more than a centimetre at our resolution):
        // round to zero, so the camera really comes to rest.
        if (Depth < 0.002f)
        {
            Depth = 0f;
        }
    }

    /// <summary>Starts a dip for a landing at <paramref name="fallSpeed"/> metres per second (positive = falling).</summary>
    public void Land(float fallSpeed)
    {
        var depth = Math.Min(Math.Max(0f, fallSpeed) * Tuning.Player.LandingDipPerSpeed, Tuning.Player.LandingDipMax);

        // A new landing never makes a running dip smaller.
        Depth = Math.Max(Depth, depth);
    }

    /// <summary>Depth for drawing, blended between the previous and the latest tick.</summary>
    public float Interpolated(float alpha) => PreviousDepth + (Depth - PreviousDepth) * Math.Clamp(alpha, 0f, 1f);
}
