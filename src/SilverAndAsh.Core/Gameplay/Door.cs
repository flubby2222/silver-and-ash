using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// A door between two rooms. It fills one cell of the level: while closed or barred, the world treats that cell as
/// wall (so it blocks walking, sight, shots, blood bolts and the enemies' way-finding). Once open it stays open -
/// only an arena's grate can block it for a while. A locked door needs a key (<see cref="Player.Keys"/>) to open.
/// </summary>
public sealed class Door : IInteractable
{
    internal Door(int id, LevelDoor plan, Level level)
    {
        var cell = level[plan.X, plan.Z];
        if (cell.IsSolid)
        {
            throw new ArgumentException($"Door {id} at cell {plan.X}/{plan.Z} must lie on a walkable cell of the level.", nameof(plan));
        }

        Id = id;
        X = plan.X;
        Z = plan.Z;
        Kind = plan.Kind;
        Side = plan.Side;
        RoomA = plan.RoomA;
        RoomB = plan.RoomB;
        BoxMin = new Vector3(plan.X * Level.CellSize, cell.FloorHeight, plan.Z * Level.CellSize);
        BoxMax = new Vector3((plan.X + 1) * Level.CellSize, cell.CeilingHeight, (plan.Z + 1) * Level.CellSize);
    }

    /// <summary>Number of the door in its world (position in <see cref="GameWorld.Doors"/>).</summary>
    public int Id { get; }

    /// <summary>Column of the door's cell.</summary>
    public int X { get; }

    /// <summary>Row of the door's cell.</summary>
    public int Z { get; }

    public DoorKind Kind { get; }

    /// <summary>The room on one side (number in <see cref="GameWorld.Rooms"/>).</summary>
    public int RoomA { get; }

    /// <summary>The room on the other side.</summary>
    public int RoomB { get; }

    /// <summary>Which way the passage runs: North/South = along Z, East/West = along X.</summary>
    public RoomSide Side { get; }

    public bool IsOpen { get; private set; }

    /// <summary>True while the grate of an arena is down: the door blocks, open or not, and F does nothing.</summary>
    public bool IsBarred { get; private set; }

    /// <summary>True if nothing can pass: closed, or barred.</summary>
    public bool Blocks => !IsOpen || IsBarred;

    /// <summary>
    /// Every closed door that is not barred is offered - a locked one too, to say that it needs a key. A cracked wall
    /// never is: it is a secret, and F does not open it.
    /// </summary>
    public bool CanInteract => !IsOpen && !IsBarred && Kind != DoorKind.Cracked;

    /// <summary>Wooden and boss doors open with F. A locked door opens with F only for a player with a key, and uses it up.</summary>
    public InteractionOffer OfferFor(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (Kind != DoorKind.Locked)
        {
            return new("Open", true);
        }

        return player.Keys > 0 ? new("Unlock (1 key)", true) : new("Locked - needs a key", false);
    }

    public Vector3 BoxMin { get; }

    public Vector3 BoxMax { get; }

    internal void Open() => IsOpen = true;

    /// <summary>A cracked wall breaks open (see <see cref="DoorKind.Cracked"/>).</summary>
    internal void Break() => IsOpen = true;

    /// <summary>An arena's grate falls - never at an unbroken cracked wall, which would give the secret away.</summary>
    internal void Bar() => IsBarred = Kind != DoorKind.Cracked || IsOpen;

    internal void Lift() => IsBarred = false;
}
