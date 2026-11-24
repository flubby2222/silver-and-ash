using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Issue #583: where an enemy of a combat room comes out when it has to give way to a player (issue #445), every cell
/// of a door counts - also the second one of a door over two cells, not only the first. Driven tick by tick.
/// </summary>
public class EmergenceDoorCellsTests
{
    private const int Arena = 1;

    private static readonly PlayerInput Forward = new(new Vector2(0f, 1f), Vector2.Zero, false);
    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };

    // Room 0 (start) in row 0, a door over two cells (3, 1) and (4, 1), room 1 (combat, one ghoul just inside, beside
    // the way the player walks in) in rows 2-13. The player walks straight in through the first door cell.
    private const string Map =
        "#..P...#\n###..###\n#.G....#\n#......#\n#......#\n#......#\n#......#\n#......#\n#......#\n#......#\n#......#\n#......#\n#......#\n#......#";

    private static List<(int X, int Z)> Rows(int from, int to) =>
        Enumerable.Range(from, to - from + 1).SelectMany(z => Enumerable.Range(1, 6).Select(x => (x, z))).ToList();

    private static GameWorld World()
    {
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 8, 1, 0, Rows(0, 0), [], []),
            new(Arena, RoomKind.Combat, "arena", 0, 2, 8, 12, 1, Rows(2, 13), [(2, 2)], []),
        ];
        LevelDoor[] doors = [new(3, 1, 0, Arena, RoomSide.South) { MoreCells = [(4, 1)] }];
        return new GameWorld(LevelParser.Parse(Map), 1, Balance.Default, doors, rooms);
    }

    private static (int X, int Z) CellOf(Vector3 at) => Level.ToGrid(at.X, at.Z);

    [Fact]
    public void An_enemy_that_gives_way_to_a_player_keeps_clear_of_both_cells_of_a_two_cell_door()
    {
        var difficulty = Balance.Default.Difficulty;
        var world = World();
        var ghoul = world.Ghouls.Single();
        var start = CellOf(ghoul.Position);

        RunTicks(world, PressF, 1);
        RunTicks(world, Forward, 180, () => world.Rooms[Arena].IsSealed);

        // The room sealed with the player so near the ghoul's start cell that it had to give way.
        Assert.True(world.Rooms[Arena].IsSealed);
        var player = new Vector2(world.Player.Position.X, world.Player.Position.Z);
        var startMiddle = new Vector2(world.Level.CellCenter(start.X, start.Z).X, world.Level.CellCenter(start.X, start.Z).Z);
        Assert.True(Vector2.Distance(player, startMiddle) < difficulty.PlayerDistance, $"The player at {player} is not near the ghoul's start {start}.");

        var cell = CellOf(ghoul.Position);
        Assert.NotEqual(start, cell);
        var door = world.Doors.Single();
        Assert.Equal(2, door.Cells.Count);
        Assert.All(door.Cells, doorCell => Assert.True(
            DoorClearance.Keeps(cell, [DoorClearance.Of(doorCell, world.Rooms[Arena].Cells)], difficulty),
            $"The ghoul comes out on {cell}, too near the door cell {doorCell}."));
    }
}
