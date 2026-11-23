using System.Numerics;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.World;

/// <summary>
/// The free-standing columns of the boss room (issue #597): the cover a player finds from Gorm's chain swing and shock
/// wave until three blows break it (issue #590). They stand on a square grid round the room's middle -
/// <see cref="ColumnsBalance.BossRoomPerSide"/> to a side, <see cref="ColumnsBalance.BossRoomSpacing"/> apart - each moved
/// onto the nearest corner of four cells, so no figure that starts in a cell's middle starts inside one. A grid point
/// where <see cref="ColumnPlacement"/> forbids a column, or too near Gorm's place or the cell the staircase rises in after
/// his death, stays empty. Nothing is rolled: the same room always gets the same columns. Other kinds of room get none
/// here - their pieces are issue #284's rules.
/// </summary>
public static class BossRoomColumns
{
    /// <summary>A copy of <paramref name="generated"/> with columns in its boss room (none if it has no boss room).</summary>
    public static GeneratedLevel Place(GeneratedLevel generated, ColumnsBalance balance)
    {
        ArgumentNullException.ThrowIfNull(generated);
        ArgumentNullException.ThrowIfNull(balance);
        if (generated.Rooms.FirstOrDefault(room => room.Kind == RoomKind.Boss) is not { } boss)
        {
            return generated;
        }

        var doorCells = generated.Doors.SelectMany(door => door.Cells).ToList();
        var floor = boss.FloorCells.ToHashSet();
        var keptClear = KeptClear(boss);
        var required = ColumnPlacement.RequiredGap(balance);
        var level = generated.Level;
        foreach (var column in Grid(boss, balance))
        {
            if (floor.Contains(Level.ToGrid(column.X, column.Z))
                && ColumnPlacement.Check(level, column, doorCells, balance) == ColumnFit.Fits
                && keptClear.All(cell => ColumnPlacement.GapToCell(column, cell) >= required))
            {
                level = level.WithColumns([.. level.Columns, column]);
            }
        }

        return generated with { Level = level };
    }

    /// <summary>
    /// The grid points of <paramref name="room"/>, row by row: round the mean of its floor cells' middles, each on the
    /// nearest corner of four cells - whether a column may stand there is not checked.
    /// </summary>
    public static IEnumerable<Column> Grid(PlacedRoom room, ColumnsBalance balance)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(balance);
        var middle = new Vector2(
            room.FloorCells.Average(cell => (cell.X + 0.5f) * Level.CellSize),
            room.FloorCells.Average(cell => (cell.Z + 0.5f) * Level.CellSize));
        var perSide = balance.BossRoomPerSide;
        for (var row = 0; row < perSide; row++)
        {
            for (var place = 0; place < perSide; place++)
            {
                var offset = new Vector2(place - ((perSide - 1) / 2f), row - ((perSide - 1) / 2f)) * balance.BossRoomSpacing;
                yield return new Column(OnCorner(middle.X + offset.X), OnCorner(middle.Y + offset.Y));
            }
        }
    }

    /// <summary>
    /// The cells of <paramref name="room"/> no column may come near (closer than <see cref="ColumnPlacement.RequiredGap"/>):
    /// Gorm's place (as <see cref="EnemyPlacement"/> puts him) and the cell the staircase rises in
    /// (<see cref="Stairs.CellIn"/>).
    /// </summary>
    public static IReadOnlyList<(int X, int Z)> KeptClear(PlacedRoom room)
    {
        ArgumentNullException.ThrowIfNull(room);
        var gorm = room.BossSlots is { Count: > 0 } slots ? slots[0] : EnemyPlacement.Middle(room);
        return [gorm, Stairs.CellIn(room.FloorCells)];
    }

    // The nearest multiple of the cell size: a corner shared by four cells.
    private static float OnCorner(float metres) => MathF.Round(metres / Level.CellSize, MidpointRounding.AwayFromZero) * Level.CellSize;
}
