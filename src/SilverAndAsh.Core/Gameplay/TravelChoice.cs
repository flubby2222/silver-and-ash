namespace SilverAndAsh.Core.Gameplay;

/// <summary>How a destination looks from the teleporter a player stands on (issue #480).</summary>
public enum TravelState
{
    /// <summary>The teleporter the player stands on: shown, but not a destination.</summary>
    Here,

    /// <summary>Free to travel to.</summary>
    Ready,

    /// <summary>Its room is sealed by a fight: shown, but it cannot be chosen until the fight is over.</summary>
    Sealed,
}

/// <summary>One entry of the travel choice: an unlocked teleporter and whether a player can travel there now.</summary>
public sealed record TravelChoice(Teleporter Teleporter, TravelState State)
{
    /// <summary>True if choosing it sends the player there.</summary>
    public bool CanTravel => State == TravelState.Ready;
}
