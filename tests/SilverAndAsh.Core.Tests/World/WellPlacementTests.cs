using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

/// <summary>Where the Well of Sirius stands (issue #438): at most one per level, in a quiet room, from the seed.</summary>
public class WellPlacementTests
{
    private static readonly IReadOnlyList<RoomTemplate> Templates = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    private static readonly WellBalance Standard = Balance.Default.Well;

    // As the game builds its levels (LevelZero.Build), the well last. Built once: generating is the slow part.
    private static readonly Lazy<GeneratedLevel[]> Levels = new(() => Enumerable.Range(0, 150)
        .Select(seed => new LevelZero(Templates).Build(seed, Balance.Default))
        .ToArray());

    [Fact]
    public void With_the_standard_values_nearly_every_level_has_its_well()
    {
        // levelChance is 1 for now: a level goes without only if no room has a free block of 3 x 3 cells.
        var withWell = Levels.Value.Count(level => level.Well is not null);

        Assert.True(withWell >= Levels.Value.Length * 0.9, $"Only {withWell} of {Levels.Value.Length} levels have a well.");
    }

    [Fact]
    public void The_well_stands_in_a_quiet_room_amid_eight_floor_cells_of_it_away_from_chests_and_the_start()
    {
        foreach (var level in Levels.Value.Where(level => level.Well is not null))
        {
            var well = level.Well!;
            var room = level.Rooms[well.Room];
            Assert.DoesNotContain(room.Kind, new[] { RoomKind.Combat, RoomKind.Boss });

            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var cell = (well.X + dx, well.Z + dz);
                    Assert.Contains(cell, room.FloorCells);
                    Assert.False(level.Level[cell.Item1, cell.Item2].IsSolid);
                    Assert.DoesNotContain(level.Chests!, chest => (chest.X, chest.Z) == cell);
                    Assert.NotEqual((level.Level.PlayerStartX, level.Level.PlayerStartZ), cell);
                    Assert.DoesNotContain(cell, room.ChestSlots ?? []);
                }
            }
        }
    }

    [Fact]
    public void The_well_keeps_its_block_of_three_by_three_cells_clear_of_every_teleporter()
    {
        // Teleporters (issue #436) are placed before the well; the well must not stand on one or reach into it.
        var sharingARoom = 0;
        foreach (var level in Levels.Value.Where(level => level.Well is not null))
        {
            var well = level.Well!;
            Assert.NotEmpty(level.Teleporters!);
            foreach (var teleporter in level.Teleporters!)
            {
                Assert.True(
                    Math.Max(Math.Abs(teleporter.X - well.X), Math.Abs(teleporter.Z - well.Z)) >= 2,
                    $"Seed {level.Seed}: well at ({well.X}, {well.Z}), teleporter at ({teleporter.X}, {teleporter.Z}).");
                sharingARoom += teleporter.Room == well.Room ? 1 : 0;
            }
        }

        // Not empty: in many levels well and teleporter stand in the same room (the start room is quiet).
        Assert.True(sharingARoom >= 10, $"Only {sharingARoom} levels have well and teleporter in one room.");
    }

    [Fact]
    public void Placing_the_well_shifts_no_teleporter()
    {
        foreach (var level in Levels.Value.Take(30))
        {
            var without = level with { Well = null };

            Assert.Same(without.Teleporters, WellPlacement.Place(without, Standard).Teleporters);
        }
    }

    [Fact]
    public void The_same_seed_gives_the_same_well_and_different_seeds_different_places()
    {
        var again = Enumerable.Range(0, 20).Select(seed => new LevelZero(Templates).Build(seed, Balance.Default).Well);

        Assert.Equal(Levels.Value.Take(20).Select(level => level.Well), again);
        Assert.True(Levels.Value.Select(level => level.Well).Distinct().Count() > 10);
    }

    [Fact]
    public void Placing_the_well_shifts_no_room_enemy_or_chest()
    {
        foreach (var level in Levels.Value.Take(30))
        {
            var without = level with { Well = null };

            var placed = WellPlacement.Place(without, Standard);

            Assert.Same(without.Chests, placed.Chests);
            Assert.Same(without.Rooms, placed.Rooms);
            Assert.Same(without.Level, placed.Level);
            Assert.Same(without.Doors, placed.Doors);
        }
    }

    [Fact]
    public void With_level_chance_0_no_level_has_a_well()
    {
        var never = Standard with { LevelChance = 0f };

        Assert.All(Levels.Value.Take(50), level => Assert.Null(WellPlacement.Place(level, never).Well));
    }

    [Fact]
    public void With_a_level_chance_of_one_half_about_half_the_levels_have_one()
    {
        var half = Standard with { LevelChance = 0.5f };
        var fitting = Levels.Value.Where(level => level.Well is not null).ToList();

        var count = fitting.Count(level => WellPlacement.Place(level, half).Well is not null);

        Assert.InRange(count, fitting.Count * 0.3, fitting.Count * 0.7);
    }

    [Fact]
    public void Without_a_quiet_room_there_is_no_well()
    {
        foreach (var level in Levels.Value.Take(20))
        {
            var allFights = level with { Rooms = level.Rooms.Select(room => room with { Kind = RoomKind.Combat }).ToList() };

            Assert.Empty(WellPlacement.Candidates(allFights));
            Assert.Null(WellPlacement.Place(allFights, Standard).Well);
        }
    }

    [Fact]
    public void A_new_run_puts_the_well_of_its_level_into_the_world()
    {
        var seed = Enumerable.Range(0, 150).First(seed => Levels.Value[seed].Well is not null);

        var world = new LevelZero(Templates).NewRun(seed, Balance.Default);

        Assert.Equal(Levels.Value[seed].Well, world.Well!.Plan);
        Assert.False(world.Well.IsSpent);
        Assert.Equal(world.Level.CellCenter(world.Well.Plan.X, world.Well.Plan.Z), world.Well.Position);
    }
}
