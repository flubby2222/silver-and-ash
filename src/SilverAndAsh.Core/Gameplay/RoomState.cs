using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// What has happened in one room of a generated level during this run: entered yet? sealed as an arena? cleared?
/// The world changes it; everybody else (HUD, map later) only reads it.
/// </summary>
public sealed class RoomState
{
    internal RoomState(PlacedRoom room, IEnumerable<int> doorIds)
    {
        Index = room.Index;
        Kind = room.Kind;
        Cells = room.FloorCells.ToHashSet();
        DoorIds = doorIds.ToList();
    }

    /// <summary>Position in <see cref="GameWorld.Rooms"/> (the same as in the generated level).</summary>
    public int Index { get; }

    public RoomKind Kind { get; }

    /// <summary>The room's walkable cells (not its doors).</summary>
    public IReadOnlySet<(int X, int Z)> Cells { get; }

    /// <summary>The doors of this room (numbers in <see cref="GameWorld.Doors"/>).</summary>
    public IReadOnlyList<int> DoorIds { get; }

    /// <summary>True once a player stood in it with the whole body.</summary>
    public bool IsEntered { get; private set; }

    /// <summary>True while it is an arena: all its doors barred until its enemies are dead.</summary>
    public bool IsSealed { get; private set; }

    /// <summary>True once all its enemies are dead (an arena that has nobody in it is cleared on entering).</summary>
    public bool IsCleared { get; private set; }

    /// <summary>Combat and boss rooms become arenas.</summary>
    public bool IsArena => Kind is RoomKind.Combat or RoomKind.Boss;

    /// <summary>The enemies that started in this room.</summary>
    internal List<ITarget> Enemies { get; } = [];

    internal void Enter() => IsEntered = true;

    internal void Seal() => IsSealed = true;

    internal void Clear()
    {
        IsSealed = false;
        IsCleared = true;
    }
}
