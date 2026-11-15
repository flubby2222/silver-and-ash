using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// A door between two rooms. It fills one cell of the level: while closed, the world treats that cell as wall
/// (so it blocks walking, sight, shots, blood bolts and the enemies' way-finding); once open it stays open.
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

    /// <summary>Which way the passage runs: North/South = along Z, East/West = along X.</summary>
    public RoomSide Side { get; }

    public bool IsOpen { get; private set; }

    public string Prompt => "Open";

    /// <summary>Wooden and boss doors open with F. Locked doors need keys and arena grates open by themselves (later).</summary>
    public bool CanInteract => !IsOpen && Kind is DoorKind.Normal or DoorKind.Boss;

    public Vector3 BoxMin { get; }

    public Vector3 BoxMax { get; }

    internal void Open() => IsOpen = true;
}
