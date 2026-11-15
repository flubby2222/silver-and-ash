using System.Numerics;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Something a player can use with the interaction key F: today doors, later chests, locks and people.
/// The world finds the one the player is looking at (within <see cref="ControlSettings.InteractionReach"/>) and
/// decides what using it does.
/// </summary>
public interface IInteractable
{
    /// <summary>The word shown next to the key, e.g. "Open" for "[F] Open".</summary>
    string Prompt { get; }

    /// <summary>False if it cannot be used right now (an open door, a locked door without a key ...).</summary>
    bool CanInteract { get; }

    /// <summary>Lowest corner of the box the player has to look at.</summary>
    Vector3 BoxMin { get; }

    /// <summary>Highest corner of that box.</summary>
    Vector3 BoxMax { get; }
}
