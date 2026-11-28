using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The state of the reload movement of the weapon in the hand (issue #292). It belongs to the <em>drawn</em> weapon:
/// only a weapon that has its own reload motion follows the magazine, and a change of the weapon starts from rest, so
/// the reload of one weapon (the rifle) never shows up on another (the pistol).
/// </summary>
public sealed class ReloadMotion
{
    private WeaponKind? _drawn;

    /// <summary>How far the reload has come, 0..1 (what the pose reads).</summary>
    public float Progress { get; private set; }

    /// <summary>How strongly the motion shows, 0..1; fades in and out.</summary>
    public float Weight { get; private set; }

    /// <summary>Weapons that have a reload motion of their own. Every magazine weapon that gets one is added here.</summary>
    public static bool HasMotion(WeaponKind kind) => kind is WeaponKind.Pistol or WeaponKind.StakeCrossbow or WeaponKind.ThrowingKnives;

    /// <summary>Follows the magazine of the drawn weapon; resets at once when another weapon is drawn.</summary>
    public void Step(WeaponKind drawn, bool reloading, float magazineProgress, float frameSeconds)
    {
        if (_drawn != drawn)
        {
            _drawn = drawn;
            Reset();
        }

        var active = reloading && HasMotion(drawn);
        if (active)
        {
            Progress = magazineProgress;
        }

        Weight = WeaponPose.ReloadWeightStep(Weight, active, frameSeconds);
    }

    /// <summary>The reload ran to its end: the click was the last of it, nothing to fade out.</summary>
    public void Reset()
    {
        Progress = 0f;
        Weight = 0f;
    }
}
