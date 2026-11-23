using System.Numerics;

namespace SilverAndAsh.Core.World;

/// <summary>What <see cref="ColumnPlacement.Check"/> says about a place for a column.</summary>
public enum ColumnFit
{
    /// <summary>The column may stand there.</summary>
    Fits,

    /// <summary>Its middle is not on an open cell of the level.</summary>
    NotOnOpenFloor,

    /// <summary>It touches a wall, or leaves a gap to one that the widest figure cannot pass.</summary>
    TooCloseToWall,

    /// <summary>The same against the edge of a floor more than a step higher or lower than its own.</summary>
    TooCloseToEdge,

    /// <summary>The same against a door's cell.</summary>
    TooCloseToDoor,

    /// <summary>The same against another column of the level.</summary>
    TooCloseToColumn,
}

/// <summary>
/// Where a free-standing column may stand (issue #539) - the rule the level generator (issue #284) asks. A gap between
/// a column and a wall, a door or another column is either nothing at all or wide enough for every figure. Nothing leans
/// on a column, so "nothing at all" never happens: every gap must be at least <see cref="RequiredGap"/>.
/// </summary>
public static class ColumnPlacement
{
    /// <summary>
    /// The narrowest gap in metres a column may leave: the widest figure's diameter
    /// (<see cref="BodySizes.WidestFigureDiameter"/>) plus <see cref="ColumnsBalance.Play"/>.
    /// </summary>
    public static float RequiredGap(ColumnsBalance? balance = null) =>
        BodySizes.WidestFigureDiameter + (balance ?? new ColumnsBalance()).Play;

    /// <summary>
    /// Whether <paramref name="column"/> may stand in <paramref name="level"/>, beside the columns it already has and the
    /// doors on <paramref name="doorCells"/>. The gaps are measured on the ground plane from the column's circle to the
    /// nearest point of a wall cell, a cell whose floor lies more than a step (<see cref="BodySizes.Player.StepHeight"/>)
    /// above or below the column's, a door cell, or another column's circle. The first rule broken is named, in the order
    /// of <see cref="ColumnFit"/>.
    /// </summary>
    public static ColumnFit Check(Level level, Column column, IEnumerable<(int X, int Z)> doorCells, ColumnsBalance? balance = null)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(doorCells);
        var (cellX, cellZ) = Level.ToGrid(column.X, column.Z);
        if (!float.IsFinite(column.X) || !float.IsFinite(column.Z) || !level.IsInside(cellX, cellZ) || level[cellX, cellZ].IsSolid)
        {
            return ColumnFit.NotOnOpenFloor;
        }

        var required = RequiredGap(balance);
        var floor = level[cellX, cellZ].FloorHeight;
        var near = CellsWithin(column, Column.Radius + required).ToList();
        if (near.Any(cell => level[cell.X, cell.Z].IsSolid && GapToCell(column, cell) < required))
        {
            return ColumnFit.TooCloseToWall;
        }

        if (near.Any(cell => MathF.Abs(level[cell.X, cell.Z].FloorHeight - floor) > BodySizes.Player.StepHeight
            && GapToCell(column, cell) < required))
        {
            return ColumnFit.TooCloseToEdge;
        }

        if (doorCells.Any(cell => GapToCell(column, cell) < required))
        {
            return ColumnFit.TooCloseToDoor;
        }

        return level.Columns.Any(other => Vector2.Distance(other.Centre, column.Centre) - (2f * Column.Radius) < required)
            ? ColumnFit.TooCloseToColumn
            : ColumnFit.Fits;
    }

    /// <summary>From the column's circle to the nearest point of the cell's square, in metres (negative if they overlap).</summary>
    public static float GapToCell(Column column, (int X, int Z) cell)
    {
        var min = new Vector2(cell.X, cell.Z) * Level.CellSize;
        var nearest = Vector2.Clamp(column.Centre, min, min + new Vector2(Level.CellSize));
        return Vector2.Distance(column.Centre, nearest) - Column.Radius;
    }

    // Every cell a circle of reach round the column's middle could touch, inside the level or not (outside is wall).
    private static IEnumerable<(int X, int Z)> CellsWithin(Column column, float reach)
    {
        var (minX, minZ) = Level.ToGrid(column.X - reach, column.Z - reach);
        var (maxX, maxZ) = Level.ToGrid(column.X + reach, column.Z + reach);
        for (var z = minZ; z <= maxZ; z++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                yield return (x, z);
            }
        }
    }
}
