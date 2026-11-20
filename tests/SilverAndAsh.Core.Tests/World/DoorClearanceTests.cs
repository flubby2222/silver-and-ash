using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

/// <summary>Issue #490: the rule itself - straight into the room the door distance, beside the door the side distance.</summary>
public class DoorClearanceTests
{
    private static readonly DifficultyBalance Difficulty = Balance.Default.Difficulty;

    // A room of 7 x 6 cells (columns 0..6, rows 1..6) with its door at (3, 0) in the north wall: the way in is +Z.
    private static readonly List<(int X, int Z)> Room =
        Enumerable.Range(0, 7).SelectMany(x => Enumerable.Range(1, 6).Select(z => (x, z))).ToList();

    private static readonly RoomDoor Door = DoorClearance.Of((3, 0), Room);

    [Fact]
    public void The_way_in_leads_from_the_door_to_the_floor_cell_next_to_it()
    {
        Assert.Equal((3, 0), Door.Cell);
        Assert.Equal(new Vector2(0f, 1f), Door.Inward);
        Assert.Equal(new Vector2(-1f, 0f), DoorClearance.Of((7, 3), Room).Inward);
    }

    [Fact]
    public void Straight_ahead_the_door_distance_counts_beside_the_door_the_side_distance()
    {
        // (3, 1) lies straight ahead, (2, 1) diagonally beside the door frame (45 degrees), (1, 1) along the wall.
        Assert.Equal(0f, DoorClearance.AngleDegrees((3, 1), Door), 0.01f);
        Assert.Equal(45f, DoorClearance.AngleDegrees((2, 1), Door), 0.01f);
        Assert.Equal(Difficulty.DoorDistance, DoorClearance.Needed((3, 1), Door, Difficulty));
        Assert.Equal(Difficulty.SideDoorDistance, DoorClearance.Needed((2, 1), Door, Difficulty));
        Assert.Equal(Difficulty.SideDoorDistance, DoorClearance.Needed((1, 1), Door, Difficulty));
    }

    [Fact]
    public void A_cell_keeps_clear_if_it_is_far_enough_for_its_direction()
    {
        // One cell straight ahead is enough in front; beside the frame the same distance is not.
        Assert.True(DoorClearance.Keeps((3, 1), [Door], Difficulty with { DoorDistance = Level.CellSize }));
        Assert.False(DoorClearance.Keeps((2, 1), [Door], Difficulty with { DoorDistance = Level.CellSize, SideDoorDistance = 2 * Level.CellSize }));
        Assert.True(DoorClearance.Keeps((1, 1), [Door], Difficulty with { DoorDistance = Level.CellSize, SideDoorDistance = 2 * Level.CellSize }));
        Assert.True(DoorClearance.Keeps((0, 1), [], Difficulty));
    }

    [Fact]
    public void The_least_bad_cell_lies_ahead_even_if_a_cell_beside_the_door_is_farther()
    {
        // A wide, shallow room (11 x 2) with its door in the middle of the north wall. No cell keeps clear: ahead the
        // farthest cells are (4, 2) and (6, 2), about 4.5 m away; the corners beside the door lie almost 11 m away.
        var room = Enumerable.Range(0, 11).SelectMany(x => Enumerable.Range(1, 2).Select(z => (x, z))).ToList();
        var door = DoorClearance.Of((5, 0), room);
        var tooFar = Difficulty with { DoorDistance = 100f, SideDoorDistance = 200f };

        Assert.Equal((4, 2), DoorClearance.LeastBad(room, [door], tooFar));

        // With the same distance everywhere the least bad cell is simply the farthest one.
        Assert.Equal((0, 2), DoorClearance.LeastBad(room, [door], tooFar with { SideDoorDistance = 100f }));
    }

    [Fact]
    public void A_door_needs_floor_to_look_into()
    {
        Assert.Throws<ArgumentException>(() => DoorClearance.Of((0, 0), []));
    }
}
