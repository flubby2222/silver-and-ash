using System.Numerics;

namespace SilverAndAsh.Core.World;

/// <summary>
/// A level: a grid of <see cref="Cell"/>s on the ground plane plus the player's start.
/// Grid coordinates: <c>x</c> = column (world X axis), <c>z</c> = row (world Z axis).
/// World positions are in metres; the grid starts at the world origin.
/// </summary>
public sealed class Level
{
    /// <summary>Edge length of one cell in metres.</summary>
    public const float CellSize = 2f;

    private readonly Cell[,] _cells;

    private readonly List<(int X, int Z)> _dummyCells;

    /// <summary>
    /// Creates a level. <paramref name="cells"/> is indexed as [x, z]. <paramref name="dummyCells"/> are the grid
    /// positions of training dummies (optional).
    /// </summary>
    public Level(Cell[,] cells, int playerStartX, int playerStartZ, IEnumerable<(int X, int Z)>? dummyCells = null)
    {
        ArgumentNullException.ThrowIfNull(cells);
        _cells = (Cell[,])cells.Clone();
        Width = cells.GetLength(0);
        Depth = cells.GetLength(1);

        if (Width == 0 || Depth == 0)
        {
            throw new ArgumentException("A level needs at least one cell.", nameof(cells));
        }

        foreach (var cell in _cells)
        {
            // Also catches cells that were never filled in (default value: floor = ceiling = 0).
            if (!cell.IsSolid && cell.CeilingHeight <= cell.FloorHeight)
            {
                throw new ArgumentException("Every walkable cell needs a ceiling above its floor.", nameof(cells));
            }
        }

        if (!IsInside(playerStartX, playerStartZ) || this[playerStartX, playerStartZ].IsSolid)
        {
            throw new ArgumentException("The player must start on a walkable cell inside the level.");
        }

        PlayerStartX = playerStartX;
        PlayerStartZ = playerStartZ;

        _dummyCells = dummyCells?.ToList() ?? [];
        foreach (var (x, z) in _dummyCells)
        {
            if (!IsInside(x, z) || this[x, z].IsSolid)
            {
                throw new ArgumentException("Training dummies must stand on walkable cells inside the level.", nameof(dummyCells));
            }
        }
    }

    /// <summary>World positions of the training dummies' feet: centres of their cells, on the floor.</summary>
    public IReadOnlyList<System.Numerics.Vector3> DummyPositions => _dummyCells.Select(cell => CellCenter(cell.X, cell.Z)).ToList();

    /// <summary>Number of cells along the X axis.</summary>
    public int Width { get; }

    /// <summary>Number of cells along the Z axis.</summary>
    public int Depth { get; }

    /// <summary>Grid column of the player's start cell.</summary>
    public int PlayerStartX { get; }

    /// <summary>Grid row of the player's start cell.</summary>
    public int PlayerStartZ { get; }

    /// <summary>World position of the player's feet at the start: centre of the start cell, on its floor.</summary>
    public Vector3 PlayerStartPosition => CellCenter(PlayerStartX, PlayerStartZ);

    /// <summary>
    /// The cell at grid position (x, z). Everything outside the level counts as wall,
    /// so nobody can ever leave the map.
    /// </summary>
    public Cell this[int x, int z] => IsInside(x, z) ? _cells[x, z] : Cell.Wall;

    /// <summary>True if (x, z) is a grid position inside the level.</summary>
    public bool IsInside(int x, int z) => x >= 0 && x < Width && z >= 0 && z < Depth;

    /// <summary>
    /// Grid position of the cell that contains the world position (worldX, worldZ).
    /// Uses Floor instead of a plain cast, so that e.g. -0.5 m lands in cell -1 (outside), not in cell 0.
    /// </summary>
    public static (int X, int Z) ToGrid(float worldX, float worldZ) =>
        ((int)MathF.Floor(worldX / CellSize), (int)MathF.Floor(worldZ / CellSize));

    /// <summary>The cell that contains a world position (height is ignored).</summary>
    public Cell CellAt(Vector3 worldPosition)
    {
        var (x, z) = ToGrid(worldPosition.X, worldPosition.Z);
        return this[x, z];
    }

    /// <summary>World position of the centre of a cell, at its floor height.</summary>
    public Vector3 CellCenter(int x, int z) =>
        new((x + 0.5f) * CellSize, this[x, z].FloorHeight, (z + 0.5f) * CellSize);
}
