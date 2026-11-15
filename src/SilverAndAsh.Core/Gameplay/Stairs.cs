using System.Numerics;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// The spiral staircase in the middle of the boss room (decision record 0007). It rises out of the floor once the boss
/// is dead; when it stands, "[F] Climb the stairs" wins the run. With F, so nobody wins by accident before picking up
/// the boss's loot.
/// </summary>
public sealed class Stairs : IInteractable
{
    /// <summary>Radius of the staircase (its steps around the pole), in metres.</summary>
    public const float Radius = 1.2f;

    /// <summary>Height of the staircase, in metres.</summary>
    public const float Height = 4f;

    private readonly int _riseTicks;
    private int _risenTicks;

    internal Stairs(Vector3 position, float riseSeconds)
    {
        Position = position;
        _riseTicks = Math.Max(1, SimulationClock.SecondsToTicks(riseSeconds));
    }

    /// <summary>Bottom centre, on the floor in the middle of the boss room.</summary>
    public Vector3 Position { get; }

    /// <summary>How far it has risen: 0 = still in the floor, 1 = standing.</summary>
    public float RiseFraction => (float)_risenTicks / _riseTicks;

    /// <summary>Offered once it stands.</summary>
    public bool CanInteract => RiseFraction >= 1f;

    public Vector3 BoxMin => Position - new Vector3(Radius, 0f, Radius);

    public Vector3 BoxMax => Position + new Vector3(Radius, Height, Radius);

    public InteractionOffer OfferFor(Player player) => new("Climb the stairs", true);

    internal void Tick()
    {
        if (_risenTicks < _riseTicks)
        {
            _risenTicks++;
        }
    }
}
