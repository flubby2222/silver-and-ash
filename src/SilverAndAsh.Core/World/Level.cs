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
    private readonly List<(int X, int Z)> _ghoulCells;
    private readonly List<(int X, int Z)> _batCells;
    private readonly List<(int X, int Z)> _bossCells;

    /// <summary>
    /// Creates a level. <paramref name="cells"/> is indexed as [x, z]. <paramref name="dummyCells"/> are the grid
    /// positions of training dummies (optional).
    /// </summary>
    public Level(
        Cell[,] cells, int playerStartX, int playerStartZ,
        IEnumerable<(int X, int Z)>? dummyCells = null, IEnumerable<(int X, int Z)>? ghoulCells = null,
        IEnumerable<(int X, int Z)>? batCells = null, IEnumerable<(int X, int Z)>? bossCells = null)
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

        _ghoulCells = ghoulCells?.ToList() ?? [];
        foreach (var (x, z) in _ghoulCells)
        {
            if (!IsInside(x, z) || this[x, z].IsSolid)
            {
                throw new ArgumentException("Ghouls must start on walkable cells inside the level.", nameof(ghoulCells));
            }
        }

        _batCells = batCells?.ToList() ?? [];
        foreach (var (x, z) in _batCells)
        {
            if (!IsInside(x, z) || this[x, z].IsSolid)
            {
                throw new ArgumentException("Bats must start above walkable cells inside the level.", nameof(batCells));
            }
        }

        _bossCells = bossCells?.ToList() ?? [];
        foreach (var (x, z) in _bossCells)
        {
            if (!IsInside(x, z) || this[x, z].IsSolid)
            {
                throw new ArgumentException("A boss must start on a walkable cell inside the level.", nameof(bossCells));
            }
        }
    }

    /// <summary>World positions where bosses start (Gorm, 'T'): centres of their cells, on the floor.</summary>
    public IReadOnlyList<System.Numerics.Vector3> BossPositions => _bossCells.Select(cell => CellCenter(cell.X, cell.Z)).ToList();

    /// <summary>World positions where the ghouls start: centres of their cells, on the floor.</summary>
    /// <summary>Floor positions of the cells where bats start (they hover above them).</summary>
    public IReadOnlyList<System.Numerics.Vector3> BatPositions => _batCells.Select(cell => CellCenter(cell.X, cell.Z)).ToList();

    public IReadOnlyList<System.Numerics.Vector3> GhoulPositions => _ghoulCells.Select(cell => CellCenter(cell.X, cell.Z)).ToList();

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

    /// <summary>
    /// A copy of this level with some cells replaced - start and figures stay. Levels never change themselves,
    /// so a world that opens a door gets a new level instead.
    /// </summary>
    public Level WithCells(IEnumerable<(int X, int Z, Cell Cell)> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var cells = (Cell[,])_cells.Clone();
        foreach (var (x, z, cell) in changes)
        {
            if (!IsInside(x, z))
            {
                throw new ArgumentException($"Cell {x}/{z} lies outside the level.", nameof(changes));
            }

            cells[x, z] = cell;
        }

        return new Level(cells, PlayerStartX, PlayerStartZ, _dummyCells, _ghoulCells, _batCells, _bossCells)
        {
            WallCutouts = WallCutouts,
            Columns = Columns,
            Portals = Portals,
        };
    }

    /// <summary>
    /// The holes left in wall faces for models set into the wall (issue #553, <see cref="LevelGeometry"/>). Only the
    /// picture: cells, walking, collision, sight and shots do not know them. A hole whose wall a later copy no longer has
    /// (the cell closed, the cell behind opened) or that no longer fits under its cell's ceiling is simply not drawn.
    /// </summary>
    public IReadOnlyList<WallCutout> WallCutouts { get; private init; } = [];

    /// <summary>A copy of this level with <paramref name="cutouts"/> as its holes in wall faces (cells, start and figures stay).</summary>
    /// <exception cref="ArgumentException">
    /// If a hole is not in front of a wall of an open cell, does not lie within the cell's side and clear height, or a
    /// side has two holes.
    /// </exception>
    public Level WithWallCutouts(IEnumerable<WallCutout> cutouts)
    {
        ArgumentNullException.ThrowIfNull(cutouts);
        var list = cutouts.ToList();
        foreach (var cutout in list)
        {
            CheckCutout(cutout);
        }

        if (list.Select(cutout => (cutout.X, cutout.Z, cutout.Side)).Distinct().Count() != list.Count)
        {
            throw new ArgumentException("A wall side can have at most one hole.", nameof(cutouts));
        }

        return new Level(_cells, PlayerStartX, PlayerStartZ, _dummyCells, _ghoulCells, _batCells, _bossCells)
        {
            WallCutouts = list,
            Columns = Columns,
            Portals = Portals,
        };
    }

    /// <summary>
    /// The free-standing columns (issue #539). Unlike <see cref="WallCutouts"/> they are solid: walking
    /// (<see cref="Physics.CharacterCollision"/>), sight and shots (<see cref="Physics.LineOfSight.IsInsideGeometry"/>)
    /// stop at them. The cells they stand on stay open - way-finding goes round them (<see cref="Physics.ColumnDetour"/>).
    /// </summary>
    public IReadOnlyList<Column> Columns { get; private init; } = [];

    /// <summary>
    /// A copy of this level with <paramref name="columns"/> as its columns (cells, start, figures and holes stay). Only
    /// checks that each stands on an open cell; whether it leaves enough room is <see cref="ColumnPlacement"/>'s rule.
    /// </summary>
    /// <exception cref="ArgumentException">If a column's middle is not a finite point on an open cell of the level.</exception>
    public Level WithColumns(IEnumerable<Column> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        var list = columns.ToList();
        foreach (var column in list)
        {
            var (x, z) = ToGrid(column.X, column.Z);
            if (!float.IsFinite(column.X) || !float.IsFinite(column.Z) || !IsInside(x, z) || this[x, z].IsSolid)
            {
                throw new ArgumentException($"The column at {column.X}/{column.Z} m does not stand on an open cell.", nameof(columns));
            }
        }

        return new Level(_cells, PlayerStartX, PlayerStartZ, _dummyCells, _ghoulCells, _batCells, _bossCells)
        {
            WallCutouts = WallCutouts,
            Columns = list,
            Portals = Portals,
        };
    }

    /// <summary>The column whose circle holds <paramref name="point"/> (any height), or null.</summary>
    public Column? ColumnAt(Vector3 point)
    {
        foreach (var column in Columns)
        {
            if (column.Contains(point))
            {
                return column;
            }
        }

        return null;
    }

    /// <summary>
    /// The stone portals of the boss doors (issue #598). Like <see cref="Columns"/> they are solid: walking, sight and
    /// shots stop at their stone; the door cells they frame stay open, and way-finding leads through the middle of their
    /// opening (<see cref="Pathfinder"/>).
    /// </summary>
    public IReadOnlyList<BossPortal> Portals { get; private init; } = [];

    /// <summary>A copy of this level with <paramref name="portals"/> as its boss portals (cells, start, figures, holes and columns stay).</summary>
    /// <exception cref="ArgumentException">If a portal's middle is not a finite point inside the level.</exception>
    public Level WithPortals(IEnumerable<BossPortal> portals)
    {
        ArgumentNullException.ThrowIfNull(portals);
        var list = portals.ToList();
        foreach (var portal in list)
        {
            var (x, z) = ToGrid(portal.X, portal.Z);
            if (!float.IsFinite(portal.X) || !float.IsFinite(portal.Z) || !float.IsFinite(portal.Floor) || !IsInside(x, z))
            {
                throw new ArgumentException($"The portal at {portal.X}/{portal.Z} m does not lie inside the level.", nameof(portals));
            }
        }

        return new Level(_cells, PlayerStartX, PlayerStartZ, _dummyCells, _ghoulCells, _batCells, _bossCells)
        {
            WallCutouts = WallCutouts,
            Columns = Columns,
            Portals = list,
        };
    }

    /// <summary>The portal whose stone holds <paramref name="point"/>, or null.</summary>
    public BossPortal? PortalAt(Vector3 point)
    {
        foreach (var portal in Portals)
        {
            if (portal.Contains(point))
            {
                return portal;
            }
        }

        return null;
    }

    /// <summary>
    /// Where a way through cell (<paramref name="x"/>, <paramref name="z"/>) goes: its middle, but in a doorway framed by
    /// a boss portal the middle of the portal's opening (<see cref="BossPortal.PassagePoint"/>).
    /// </summary>
    public Vector3 WayPoint(int x, int z)
    {
        foreach (var portal in Portals)
        {
            if (portal.Frames(x, z))
            {
                return portal.PassagePoint(x, z) with { Y = this[x, z].FloorHeight };
            }
        }

        return CellCenter(x, z);
    }

    private void CheckCutout(WallCutout cutout)
    {
        var (x, z) = (cutout.X, cutout.Z);
        var (dx, dz) = cutout.Behind;
        if (!IsInside(x, z) || this[x, z].IsSolid || !this[x + dx, z + dz].IsSolid)
        {
            throw new ArgumentException($"Cell {x}/{z} is no open cell with a wall on its {cutout.Side} side.", nameof(cutout));
        }

        var clear = this[x, z].CeilingHeight - this[x, z].FloorHeight;
        if (!(cutout.From >= 0f && cutout.From < cutout.To && cutout.To <= CellSize
            && cutout.Bottom >= 0f && cutout.Bottom < cutout.Top && cutout.Top <= clear))
        {
            throw new ArgumentException($"The hole at cell {x}/{z} does not lie within its wall face.", nameof(cutout));
        }
    }

    /// <summary>
    /// A copy of this level with these ghouls, bats and bosses instead of its own (cells, start and dummies stay).
    /// Without <paramref name="bossCells"/> it has no boss.
    /// </summary>
    public Level WithEnemies(
        IEnumerable<(int X, int Z)> ghoulCells, IEnumerable<(int X, int Z)> batCells, IEnumerable<(int X, int Z)>? bossCells = null) =>
        new(_cells, PlayerStartX, PlayerStartZ, _dummyCells, ghoulCells, batCells, bossCells) { WallCutouts = WallCutouts, Columns = Columns, Portals = Portals };

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
