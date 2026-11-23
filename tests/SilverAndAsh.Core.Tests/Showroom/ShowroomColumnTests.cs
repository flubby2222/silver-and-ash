using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.Showroom;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Showroom;

/// <summary>Issue #539: the column hall of the showroom.</summary>
public class ShowroomColumnTests
{
    private static readonly ShowroomLayout Layout = ShowroomLayout.Build();

    [Fact]
    public void Two_columns_stand_one_under_each_ceiling_height()
    {
        var columns = Layout.Level.Columns.OrderBy(column => column.X).ToList();

        Assert.Equal(2, columns.Count);
        Assert.Equal(ShowroomSection.CeilingOf(ShowroomPart.Corridor), Layout.Level.CellAt(new(columns[0].X, 0f, columns[0].Z)).CeilingHeight);
        Assert.Equal(ShowroomSection.CeilingOf(ShowroomPart.Room), Layout.Level.CellAt(new(columns[1].X, 0f, columns[1].Z)).CeilingHeight);
    }

    [Fact]
    public void Both_columns_keep_to_the_placement_rule()
    {
        var doors = Layout.Doors.Select(door => (door.X, door.Z)).ToList();
        var others = Layout.Level.Columns;

        foreach (var column in others)
        {
            // Checked against the other column only, as the generator will place one after the other.
            var without = Layout.Level.WithColumns(others.Where(other => other != column));
            Assert.Equal(ColumnFit.Fits, ColumnPlacement.Check(without, column, doors, Balance.Default.Columns));
        }
    }

    [Fact]
    public void Each_column_has_its_label()
    {
        Assert.Contains(Layout.Labels, label => label.Text == ShowroomLayout.ColumnLabel(ShowroomPart.Corridor));
        Assert.Contains(Layout.Labels, label => label.Text == ShowroomLayout.ColumnLabel(ShowroomPart.Room));
        Assert.NotEqual(ShowroomLayout.ColumnLabel(ShowroomPart.Corridor), ShowroomLayout.ColumnLabel(ShowroomPart.Room));
    }

    [Fact]
    public void The_hall_can_be_walked_into_from_the_start()
    {
        // Over open cells of the same floor from the player's start (the doors stay shut - they are not needed).
        var level = Layout.NewWorld().Level;
        var start = (level.PlayerStartX, level.PlayerStartZ);
        var seen = new HashSet<(int X, int Z)> { start };
        var open = new Queue<(int X, int Z)>([start]);
        while (open.TryDequeue(out var cell))
        {
            foreach (var next in new[] { (cell.X + 1, cell.Z), (cell.X - 1, cell.Z), (cell.X, cell.Z + 1), (cell.X, cell.Z - 1) })
            {
                if (level.IsInside(next.Item1, next.Item2) && !level[next.Item1, next.Item2].IsSolid && seen.Add(next))
                {
                    open.Enqueue(next);
                }
            }
        }

        foreach (var column in Layout.Level.Columns)
        {
            var (x, z) = Level.ToGrid(column.X, column.Z);
            Assert.Contains((x + 2, z), seen);                         // the cells round the column, too
            Assert.Contains((x - 2, z), seen);
            Assert.Contains((x, z - 2), seen);
            Assert.Contains((x, z + 2), seen);
        }
    }

    [Fact]
    public void There_is_a_photo_of_each_column()
    {
        var names = PhotoScenes.All.Select(scene => scene.Name).ToList();

        Assert.Contains("column-corridor", names);
        Assert.Contains("column-room", names);
    }
}
