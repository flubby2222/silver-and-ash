namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// How far the player is ducked: 0 = standing, 1 = fully ducked. Goes up and down smoothly within
/// <see cref="PlayerBalance.CrouchTransitionTime"/>, so the camera does not jump. Only the eyes follow this
/// smooth value; the hitbox switches at once (see <see cref="Player.CollisionHeight"/>).
/// </summary>
public sealed class Crouch
{
    private readonly float _stepPerTick;

    /// <summary>Standing; ducks as fast as <paramref name="balance"/> says (the standard values if none is given).</summary>
    public Crouch(PlayerBalance? balance = null)
    {
        _stepPerTick = SimulationClock.SecondsPerTick / (balance ?? Balance.Default.Player).CrouchTransitionTime;
    }

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
            ? MathF.Min(Amount + _stepPerTick, target)
            : MathF.Max(Amount - _stepPerTick, target);
    }

    /// <summary>The amount blended between the previous and the latest tick (for drawing).</summary>
    public float Interpolated(float alpha) => PreviousAmount + (Amount - PreviousAmount) * Math.Clamp(alpha, 0f, 1f);

    /// <summary>Eye height above the feet for a given amount: from standing to ducked eye height.</summary>
    public static float EyeHeight(float amount) =>
        BodySizes.Player.EyeHeight + (BodySizes.Player.DodgeEyeHeight - BodySizes.Player.EyeHeight) * amount;
}
