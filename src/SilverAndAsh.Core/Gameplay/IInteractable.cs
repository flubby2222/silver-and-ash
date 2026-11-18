using System.Numerics;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Something a player can use with the interaction key F: today doors, later chests and people.
/// The world finds the one the player is looking at (within <see cref="ControlSettings.InteractionReach"/>) and
/// decides what using it does.
/// </summary>
public interface IInteractable
{
    /// <summary>False if there is nothing to do with it right now (an open door, a barred one ...) - then it is not offered at all.</summary>
    bool CanInteract { get; }

    /// <summary>
    /// What F would do for <paramref name="player"/> - e.g. "Open", or "Locked - needs a key" when F does nothing
    /// for them right now.
    /// </summary>
    InteractionOffer OfferFor(Player player);

    /// <summary>Lowest corner of the box the player has to look at.</summary>
    Vector3 BoxMin { get; }

    /// <summary>Highest corner of that box.</summary>
    Vector3 BoxMax { get; }
}

/// <summary>
/// What a player is offered when looking at something usable: the <paramref name="Text"/> next to the key ("[F] Open"),
/// and whether F does something now (<paramref name="IsPossible"/>). If not, the text says why and shows without "[F]".
/// Equipment (issue #317) gives its <paramref name="Tier"/>: the prompt shows in that colour.
/// </summary>
public sealed record InteractionOffer(string Text, bool IsPossible, ItemTier? Tier = null);
