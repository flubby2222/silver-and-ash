namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// How far the player is ducked: 0 = standing, 1 = fully ducked. Goes up and down smoothly within
/// <see cref="Tuning.Player.CrouchTransitionTime"/>, so the camera does not jump. Only the eyes follow this
/// smooth value; the hitbox switches at once (see <see cref="Player.CollisionHeight"/>).
/// </summary>
public sealed class Crouch
{
    private const float StepPerTick = SimulationClock.SecondsPerTick / Tuning.Player.CrouchTransitionTime;

    /// <summary>0 = standing, 1 = fully ducked, after the latest tick.</summary>
    public float Amount { get; private set; }

    /// <summary>The amount one tick earlier, for smooth drawing between ticks.</summary>
    public float PreviousAmount { get; private set; }

    /// <summary>Moves one tick towards ducked (<paramref name="ducking"/> = true) or standing.</summary>
    public void Tick(bool ducking)
    {
        PreviousAmount = Amount;
        var target = ducking ? 1f : 0f;
        Amount = Amount < target
            ? MathF.Min(Amount + StepPerTick, target)
            : MathF.Max(Amount - StepPerTick, target);
    }

    /// <summary>The amount blended between the previous and the latest tick (for drawing).</summary>
    public float Interpolated(float alpha) => PreviousAmount + (Amount - PreviousAmount) * Math.Clamp(alpha, 0f, 1f);

    /// <summary>Eye height above the feet for a given amount: from standing to ducked eye height.</summary>
    public static float EyeHeight(float amount) =>
        Tuning.Player.EyeHeight + (Tuning.Player.DodgeEyeHeight - Tuning.Player.EyeHeight) * amount;
}
