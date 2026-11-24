using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

/// <summary>
/// Issue #583: a <see cref="LevelDoor"/> compares by value - its further cells (<see cref="LevelDoor.MoreCells"/>, the
/// second cell of the boss door) by their cells, not by the list they sit in. So two generations with the same seed
/// give equal doors, and a test on determinism may compare them.
/// </summary>
public class LevelDoorTests
{
    private static readonly IReadOnlyList<RoomTemplate> Templates = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    private static readonly LevelDoor BossDoor = new(2, 1, 0, 1, RoomSide.South, DoorKind.Boss) { MoreCells = [(3, 1)] };

    [Fact]
    public void Two_generations_with_the_same_seed_give_equal_doors_the_boss_door_too()
    {
        var first = LevelGenerator.Generate(Templates, 7, Balance.Default.Layout);
        var second = LevelGenerator.Generate(Templates, 7, Balance.Default.Layout);
        var boss = first.Doors.ToList().FindIndex(door => door.MoreCells.Count > 0);

        // Each generation makes its own list of further cells - equal cells in two different lists.
        Assert.True(boss >= 0, "The level has no door over two cells.");
        Assert.NotSame(first.Doors[boss].MoreCells, second.Doors[boss].MoreCells);

        Assert.Equal(first.Doors[boss], second.Doors[boss]);
        Assert.Equal(first.Doors[boss].GetHashCode(), second.Doors[boss].GetHashCode());
        Assert.Equal(first.Doors, second.Doors);
    }

    [Fact]
    public void Doors_with_the_same_cells_in_other_lists_are_equal()
    {
        var same = BossDoor with { MoreCells = [(3, 1)] };

        Assert.Equal(BossDoor, same);
        Assert.True(BossDoor == same);
        Assert.Equal(BossDoor.GetHashCode(), same.GetHashCode());
    }

    [Fact]
    public void Doors_that_differ_in_any_one_property_are_not_equal()
    {
        LevelDoor[] others =
        [
            BossDoor with { X = 4 },
            BossDoor with { Z = 2 },
            BossDoor with { RoomA = 2 },
            BossDoor with { RoomB = 2 },
            BossDoor with { Side = RoomSide.North },
            BossDoor with { Kind = DoorKind.Normal },
            BossDoor with { HasGrate = true },
            BossDoor with { MoreCells = [(1, 1)] },
            BossDoor with { MoreCells = [] },
            BossDoor with { MoreCells = [(3, 1), (4, 1)] },
        ];

        Assert.All(others, other => Assert.NotEqual(BossDoor, other));
        Assert.False(BossDoor.Equals(null));
    }

    [Fact]
    public void Every_property_of_a_level_door_is_one_the_comparison_knows()
    {
        // LevelDoor writes its own Equals and GetHashCode (for MoreCells). A new property would be left out of them
        // without a word - this test names every property, so a new one has to be added there and here.
        var properties = typeof(LevelDoor).GetProperties().Select(property => property.Name).Order(StringComparer.Ordinal);

        Assert.Equal(["Cells", "HasGrate", "Kind", "MoreCells", "RoomA", "RoomB", "Side", "X", "Z"], properties);
    }
}
