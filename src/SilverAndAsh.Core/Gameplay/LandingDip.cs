namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// The "nod" when landing: the eyes sink a little and the view tilts down - the harder the landing,
/// the stronger (up to a limit) - then come back up. Purely a feel effect: it changes the camera,
/// not where the player stands.
/// <para>
/// The movement follows a smooth wave: depth = A · sin²(π · t / duration). It starts and ends with
/// speed zero, so there is no twitch at the moment of landing (the first two versions jumped instantly
/// or started at full speed - both felt jerky). The lowest point is reached halfway through.
/// </para>
/// </summary>
public sealed class LandingDip
{
    private const float Duration = Tuning.Player.LandingDipDuration;

    private float _amplitude;
    private float _elapsed = Duration;

    /// <summary>How far the eyes are currently lowered, in metres (0 = not at all).</summary>
    public float Depth { get; private set; }

    /// <summary>The depth one tick earlier, for smooth drawing between ticks.</summary>
    public float PreviousDepth { get; private set; }

    /// <summary>True while a dip is playing.</summary>
    public bool IsActive => _elapsed < Duration;

    /// <summary>Advances the wave by one tick.</summary>
    public void Tick()
    {
        PreviousDepth = Depth;
        if (!IsActive)
        {
            Depth = 0f;
            return;
        }

        _elapsed = MathF.Min(_elapsed + SimulationClock.SecondsPerTick, Duration);
        var wave = MathF.Sin(MathF.PI * _elapsed / Duration);
        Depth = IsActive ? _amplitude * wave * wave : 0f;
    }

    /// <summary>Starts a dip for a landing at <paramref name="fallSpeed"/> m/s (positive = falling).</summary>
    public void Land(float fallSpeed)
    {
        // A dip that is already playing is not restarted - restarting would make the camera jump.
        if (IsActive)
        {
            return;
        }

        _amplitude = Math.Min(Math.Max(0f, fallSpeed) * Tuning.Player.LandingDipPerSpeed, Tuning.Player.LandingDipMax);
        _elapsed = 0f;
    }

    /// <summary>
    /// Makes the dip that is playing weaker by <paramref name="factor"/> (e.g. 0.4 = 40 % of the depth) -
    /// after a perfect jump the landing feels "cleanly absorbed". Scaling the whole wave keeps it smooth.
    /// </summary>
    public void Soften(float factor)
    {
        var f = Math.Clamp(factor, 0f, 1f);
        _amplitude *= f;
        Depth *= f;
        PreviousDepth *= f;
    }

    /// <summary>Depth for drawing, blended between the previous and the latest tick.</summary>
    public float Interpolated(float alpha) => PreviousDepth + (Depth - PreviousDepth) * Math.Clamp(alpha, 0f, 1f);
}
