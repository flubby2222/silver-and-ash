using System.Numerics;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Being knocked back by a heavy blow (the club, decision record 0008): for a short time the body slides a set
/// distance away and does nothing else. Enemies move with it through their own collision, so walls stop the push.
/// </summary>
public sealed class Knockback
{
    private Vector3 _velocity;
    private int _ticksLeft;

    /// <summary>True while the push lasts.</summary>
    public bool IsActive => _ticksLeft > 0;

    /// <summary>The flat speed of the push right now (zero when there is none).</summary>
    public Vector3 Velocity => IsActive ? _velocity : Vector3.Zero;

    /// <summary>Starts a push of <paramref name="distance"/> metres along the flat <paramref name="direction"/>, lasting <paramref name="seconds"/>.</summary>
    public void Start(Vector3 direction, float distance, float seconds)
    {
        var flat = direction with { Y = 0f };
        if (distance <= 0f || flat.LengthSquared() < 0.0001f)
        {
            return;
        }

        _ticksLeft = Math.Max(1, SimulationClock.SecondsToTicks(seconds));
        _velocity = Vector3.Normalize(flat) * (distance / (_ticksLeft * SimulationClock.SecondsPerTick));
    }

    /// <summary>One tick has passed.</summary>
    public void Tick()
    {
        if (_ticksLeft > 0)
        {
            _ticksLeft--;
        }
    }
}
