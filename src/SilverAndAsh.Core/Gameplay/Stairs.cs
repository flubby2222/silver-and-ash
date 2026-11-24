using System.Numerics;
using SilverAndAsh.Core.World;

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

    /// <summary>
    /// Height of the staircase, in metres: the ceiling of the boss room it stands in (issue #723), so it leads up to the
    /// next level and does not stand in the middle of the room. The model (tools/models/stairs.py) is built to it.
    /// </summary>
    public const float Height = RoomTemplate.RoomCeilingHeight;

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

    /// <summary>
    /// The cell the staircase rises in: of the room's floor cells the one nearest their mean, on a tie the one with the
    /// smaller Z, then the smaller X - the middle of the room, the same for every order of the cells.
    /// </summary>
    public static (int X, int Z) CellIn(IEnumerable<(int X, int Z)> floorCells)
    {
        ArgumentNullException.ThrowIfNull(floorCells);
        var cells = floorCells.ToList();
        var x = cells.Average(cell => cell.X);
        var z = cells.Average(cell => cell.Z);
        return cells
            .OrderBy(cell => ((cell.X - x) * (cell.X - x)) + ((cell.Z - z) * (cell.Z - z)))
            .ThenBy(cell => cell.Z)
            .ThenBy(cell => cell.X)
            .First();
    }

    public InteractionOffer OfferFor(Player player) => new("Climb the stairs", true);

    internal void Tick()
    {
        if (_risenTicks < _riseTicks)
        {
            _risenTicks++;
        }
    }
}
