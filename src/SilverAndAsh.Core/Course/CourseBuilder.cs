using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Course;

/// <summary>
/// Collects what the stations of the test course put down - floor and door cells, rooms, doors, enemies, columns,
/// finds, teleporters, the well - and makes a level of it. Every cell is claimed once: a station that runs into another
/// one is refused at once.
/// </summary>
internal sealed class CourseBuilder
{
    private readonly Dictionary<(int X, int Z), (Cell Cell, string Owner)> _cells = [];

    public List<PlacedRoom> Rooms { get; } = [];

    public List<LevelDoor> Doors { get; } = [];

    public List<(int X, int Z)> Ghouls { get; } = [];

    public List<(int X, int Z)> Bats { get; } = [];

    public List<(int X, int Z)> Bosses { get; } = [];

    public List<(Column Column, string Owner)> Columns { get; } = [];

    public List<ExhibitLoot> Loot { get; } = [];

    public List<ExhibitConsumable> Consumables { get; } = [];

    public List<ExhibitWeapon> Weapons { get; } = [];

    public List<ExhibitItem> Items { get; } = [];

    public List<TeleporterPlan> Teleporters { get; } = [];

    public WellPlan? Well { get; set; }

    /// <summary>
    /// Adds a room of <paramref name="kind"/> with floor from (<paramref name="x0"/>, <paramref name="z0"/>) to
    /// (<paramref name="x1"/>, <paramref name="z1"/>), both included, under the room ceiling of its kind. Its number in
    /// <see cref="Rooms"/> is returned.
    /// </summary>
    public int AddRoom(
        RoomKind kind, string name, int x0, int z0, int x1, int z1, int distanceFromStart,
        IReadOnlyList<(int X, int Z)>? chestSlots = null, IReadOnlyList<(int X, int Z)>? bossSlots = null)
    {
        var floor = Cell.Floor with { CeilingHeight = RoomTemplate.CeilingFor(kind) };
        var cells = new List<(int X, int Z)>();
        for (var z = z0; z <= z1; z++)
        {
            for (var x = x0; x <= x1; x++)
            {
                Claim((x, z), floor, name);
                cells.Add((x, z));
            }
        }

        // Like a placed template, the room's box includes its ring of wall.
        Rooms.Add(new PlacedRoom(
            Rooms.Count, kind, name, x0 - 1, z0 - 1, x1 - x0 + 3, z1 - z0 + 3, distanceFromStart, cells, [], [], chestSlots, bossSlots));
        return Rooms.Count - 1;
    }

    /// <summary>Adds <paramref name="door"/>; its cells become doorways of its kind.</summary>
    public void AddDoor(LevelDoor door, string owner)
    {
        foreach (var cell in door.Cells)
        {
            Claim(cell, LevelGenerator.DoorCell(door.Kind), owner);
        }

        Doors.Add(door);
    }

    /// <summary>
    /// The level of all claimed cells, with a ring of wall round them, a player starting at <paramref name="start"/>, and
    /// the enemies - columns are put in afterwards, each one checked.
    /// </summary>
    public Level ToLevel((int X, int Z) start)
    {
        var width = _cells.Keys.Max(cell => cell.X) + 2;
        var depth = _cells.Keys.Max(cell => cell.Z) + 2;
        var cells = new Cell[width, depth];
        for (var x = 0; x < width; x++)
        {
            for (var z = 0; z < depth; z++)
            {
                cells[x, z] = _cells.TryGetValue((x, z), out var claimed) ? claimed.Cell : Cell.Wall;
            }
        }

        return new Level(cells, start.X, start.Z, ghoulCells: Ghouls, batCells: Bats, bossCells: Bosses);
    }

    private void Claim((int X, int Z) cell, Cell content, string owner)
    {
        if (cell.X < 1 || cell.Z < 1)
        {
            throw new InvalidOperationException($"The test course: '{owner}' reaches cell {cell}, outside the ring of wall.");
        }

        if (!_cells.TryAdd(cell, (content, owner)))
        {
            throw new InvalidOperationException($"The test course: '{owner}' runs into '{_cells[cell].Owner}' at cell {cell}.");
        }
    }
}
