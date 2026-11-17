namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// The hint under the crosshair about the ranged weapon in the hand (issue #259). The supply is unlimited
/// (decision record 0008), so there is no "no ammo" - only an empty magazine to reload.
/// </summary>
public enum AmmoHint
{
    /// <summary>Nothing to say: rounds left, reloading already, or the player is dead.</summary>
    None,

    /// <summary>The magazine in the hand is empty and not being reloaded.</summary>
    Reload,
}
