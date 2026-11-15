using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

/// <summary>The spawn budget (issue #157): every combat and boss room buys enemies with its budget and puts them on free floor.</summary>
public class EnemyPlacementTests
{
    private static readonly IReadOnlyList<RoomTemplate> Templates = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    private static readonly DifficultyBalance Difficulty = Balance.Default.Difficulty;

    private static readonly Lazy<GeneratedLevel[]> ManyLevels = new(() => Enumerable.Range(0, 60)
        .Select(seed => EnemyPlacement.Populate(LevelGenerator.Generate(Templates, seed, Balance.Default.Layout), Difficulty))
        .ToArray());

    private static List<(int X, int Z)> Ghouls(GeneratedLevel generated, PlacedRoom room) =>
        generated.Level.GhoulPositions.Select(position => Level.ToGrid(position.X, position.Z)).Where(room.FloorCells.Contains).ToList();

    private static List<(int X, int Z)> Bats(GeneratedLevel generated, PlacedRoom room) =>
        generated.Level.BatPositions.Select(position => Level.ToGrid(position.X, position.Z)).Where(room.FloorCells.Contains).ToList();

    private static int Spent(GeneratedLevel generated, PlacedRoom room, DifficultyBalance difficulty) =>
        (Ghouls(generated, room).Count * difficulty.GhoulCost) + (Bats(generated, room).Count * difficulty.BatCost);

    private static float Metres((int X, int Z) a, (int X, int Z) b) =>
        Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z)) * Level.CellSize;

    private static IEnumerable<PlacedRoom> Arenas(GeneratedLevel generated) =>
        generated.Rooms.Where(room => room.Kind is RoomKind.Combat or RoomKind.Boss);

    [Fact]
    public void The_same_seed_gives_the_same_enemies()
    {
        var first = EnemyPlacement.Populate(LevelGenerator.Generate(Templates, 42, Balance.Default.Layout), Difficulty);
        var second = EnemyPlacement.Populate(LevelGenerator.Generate(Templates, 42, Balance.Default.Layout), Difficulty);

        Assert.Equal(first.Level.GhoulPositions, second.Level.GhoulPositions);
        Assert.Equal(first.Level.BatPositions, second.Level.BatPositions);
    }

    [Fact]
    public void Enemies_stand_on_the_floor_of_combat_and_boss_rooms_only()
    {
        Assert.All(ManyLevels.Value, generated =>
        {
            var arenaFloor = Arenas(generated).SelectMany(room => room.FloorCells).ToHashSet();
            var enemies = generated.Level.GhoulPositions.Concat(generated.Level.BatPositions)
                .Select(position => Level.ToGrid(position.X, position.Z));
            Assert.All(enemies, cell => Assert.Contains(cell, arenaFloor));
        });
    }

    [Fact]
    public void No_room_spends_more_than_its_budget_and_every_combat_room_has_an_enemy()
    {
        Assert.All(ManyLevels.Value, generated => Assert.All(Arenas(generated), room =>
        {
            var budget = EnemyPlacement.BudgetOf(room, generated.Rooms, Difficulty);
            var spent = Spent(generated, room, Difficulty);
            Assert.InRange(spent, 1, budget);
        }));
    }

    [Fact]
    public void Budgets_grow_with_the_distance_and_the_boss_room_has_its_own()
    {
        Assert.All(ManyLevels.Value, generated =>
        {
            var combat = generated.Rooms.Where(room => room.Kind == RoomKind.Combat).OrderBy(room => room.DistanceFromStart).ToList();
            var budgets = combat.Select(room => EnemyPlacement.BudgetOf(room, generated.Rooms, Difficulty)).ToList();

            Assert.Equal(Difficulty.BudgetNear, budgets[0]);
            Assert.Equal(Difficulty.BudgetFar, budgets[^1]);
            Assert.Equal(budgets.Order(), budgets);
            Assert.Equal(Difficulty.BudgetBoss, EnemyPlacement.BudgetOf(generated.Rooms.Single(room => room.Kind == RoomKind.Boss), generated.Rooms, Difficulty));
        });
    }

    [Fact]
    public void Rooms_spend_their_budget_as_far_as_the_room_allows()
    {
        // Left over is at most what the dearest enemy would cost - unless the room ran out of free places.
        var rooms = ManyLevels.Value.SelectMany(generated => Arenas(generated).Select(room => (generated, room))).ToList();
        var dearest = Math.Max(Difficulty.GhoulCost, Difficulty.BatCost);
        var nearlySpent = rooms.Count(pair =>
            EnemyPlacement.BudgetOf(pair.room, pair.generated.Rooms, Difficulty) - Spent(pair.generated, pair.room, Difficulty) < dearest);

        Assert.True(nearlySpent >= rooms.Count * 0.9, $"Only {nearlySpent} of {rooms.Count} rooms spent their budget.");
    }

    [Fact]
    public void Enemies_keep_away_from_the_doors_and_from_each_other()
    {
        Assert.All(ManyLevels.Value, generated => Assert.All(Arenas(generated), room =>
        {
            var doors = generated.Doors.Where(door => door.RoomA == room.Index || door.RoomB == room.Index).Select(door => (door.X, door.Z)).ToList();
            var enemies = Ghouls(generated, room).Concat(Bats(generated, room)).ToList();

            Assert.All(enemies, enemy => Assert.All(doors, door => Assert.True(
                Metres(enemy, door) >= Difficulty.DoorDistance, $"Seed {generated.Seed}: an enemy {Metres(enemy, door)} m from a door.")));
            for (var i = 0; i < enemies.Count; i++)
            {
                for (var j = i + 1; j < enemies.Count; j++)
                {
                    Assert.True(Metres(enemies[i], enemies[j]) >= Difficulty.Spacing, $"Seed {generated.Seed}: two enemies too close.");
                }
            }
        }));
    }

    [Fact]
    public void The_same_room_gets_different_line_ups_with_other_seeds()
    {
        // One layout, ten seeds for the enemies: the line-up of a room changes.
        var layout = LevelGenerator.Generate(Templates, 5, Balance.Default.Layout);

        var lineUps = Enumerable.Range(0, 10)
            .Select(seed => EnemyPlacement.Populate(layout with { Seed = seed }, Difficulty))
            .Select(generated => string.Join(";", generated.Level.GhoulPositions.Concat(generated.Level.BatPositions)))
            .Distinct()
            .Count();

        Assert.Equal(10, lineUps);
    }

    [Fact]
    public void A_room_template_is_not_always_filled_the_same_way()
    {
        // The same template at the same distance gets a different line-up in different levels.
        var lineUps = ManyLevels.Value
            .SelectMany(generated => Arenas(generated).Where(room => room.Kind == RoomKind.Combat)
                .Select(room => (room.TemplateName, Pattern: string.Join(";", Ghouls(generated, room).Concat(Bats(generated, room))
                    .Select(cell => (cell.X - room.X, cell.Z - room.Z)).Order()))))
            .GroupBy(pair => pair.TemplateName)
            .Select(group => group.Select(pair => pair.Pattern).Distinct().Count());

        Assert.All(lineUps, count => Assert.True(count > 5, $"Only {count} different line-ups."));
    }

    [Fact]
    public void Weights_decide_the_mix()
    {
        var onlyGhouls = Difficulty with { BatWeightNear = 0f, BatWeightFar = 0f };
        var onlyBats = Difficulty with { GhoulWeightNear = 0f, GhoulWeightFar = 0f };
        var layout = LevelGenerator.Generate(Templates, 8, Balance.Default.Layout);

        var ghoulsOnly = EnemyPlacement.Populate(layout, onlyGhouls);
        var batsOnly = EnemyPlacement.Populate(layout, onlyBats);

        Assert.Empty(ghoulsOnly.Level.BatPositions);
        Assert.NotEmpty(ghoulsOnly.Level.GhoulPositions);
        Assert.Empty(batsOnly.Level.GhoulPositions);
        Assert.NotEmpty(batsOnly.Level.BatPositions);
    }

    [Fact]
    public void Far_rooms_get_more_bats_with_the_standard_weights()
    {
        var near = 0;
        var nearBats = 0;
        var far = 0;
        var farBats = 0;
        foreach (var generated in ManyLevels.Value)
        {
            var combat = generated.Rooms.Where(room => room.Kind == RoomKind.Combat).OrderBy(room => room.DistanceFromStart).ToList();
            near += Ghouls(generated, combat[0]).Count + Bats(generated, combat[0]).Count;
            nearBats += Bats(generated, combat[0]).Count;
            far += Ghouls(generated, combat[^1]).Count + Bats(generated, combat[^1]).Count;
            farBats += Bats(generated, combat[^1]).Count;
        }

        Assert.True(farBats / (float)far > nearBats / (float)near, "Far rooms should have a higher share of bats.");
    }

    [Fact]
    public void Near_weights_rule_the_nearest_room_far_weights_the_farthest_and_the_boss_room()
    {
        // Near: only ghouls. Far: only bats. So the nearest combat room has only ghouls, the farthest and the boss
        // room only bats.
        var split = Difficulty with { GhoulWeightNear = 1f, GhoulWeightFar = 0f, BatWeightNear = 0f, BatWeightFar = 1f };

        Assert.All(Enumerable.Range(0, 20), seed =>
        {
            var generated = EnemyPlacement.Populate(LevelGenerator.Generate(Templates, seed, Balance.Default.Layout), split);
            var combat = generated.Rooms.Where(room => room.Kind == RoomKind.Combat).OrderBy(room => room.DistanceFromStart).ToList();
            var boss = generated.Rooms.Single(room => room.Kind == RoomKind.Boss);

            Assert.Empty(Bats(generated, combat[0]));
            Assert.Empty(Ghouls(generated, combat[^1]));
            Assert.Empty(Ghouls(generated, boss));
            Assert.NotEmpty(Bats(generated, boss));
        });
    }

    [Fact]
    public void The_places_themselves_are_random_not_only_the_kinds()
    {
        // Only ghouls, one layout, ten seeds: the cells a room's enemies stand on still change.
        var onlyGhouls = Difficulty with { BatWeightNear = 0f, BatWeightFar = 0f };
        var layout = LevelGenerator.Generate(Templates, 5, Balance.Default.Layout);
        var room = layout.Rooms.Where(room => room.Kind == RoomKind.Combat).MaxBy(room => room.DistanceFromStart)!;

        var cellSets = Enumerable.Range(0, 10)
            .Select(seed => EnemyPlacement.Populate(layout with { Seed = seed }, onlyGhouls))
            .Select(generated => string.Join(";", Ghouls(generated, room).Order()))
            .Distinct()
            .Count();

        Assert.True(cellSets >= 8, $"Only {cellSets} different sets of places in ten seeds.");
    }

    [Fact]
    public void With_no_budget_each_combat_room_still_gets_one_enemy()
    {
        var none = Difficulty with { BudgetNear = 0, BudgetFar = 0, BudgetBoss = 0 };

        var generated = EnemyPlacement.Populate(LevelGenerator.Generate(Templates, 3, Balance.Default.Layout), none);

        Assert.All(generated.Rooms.Where(room => room.Kind == RoomKind.Combat), room =>
            Assert.Equal(1, Ghouls(generated, room).Count + Bats(generated, room).Count));
    }

    [Fact]
    public void Start_corridors_and_safe_rooms_stay_empty()
    {
        Assert.All(ManyLevels.Value, generated => Assert.All(
            generated.Rooms.Where(room => room.Kind is not (RoomKind.Combat or RoomKind.Boss)),
            room => Assert.Empty(Ghouls(generated, room).Concat(Bats(generated, room)))));
    }

    [Fact]
    public void The_rest_of_the_level_stays_as_it_was()
    {
        var plain = LevelGenerator.Generate(Templates, 9, Balance.Default.Layout);

        var populated = EnemyPlacement.Populate(plain, Difficulty);

        Assert.Equal(plain.Rooms, populated.Rooms);
        Assert.Equal(plain.Doors, populated.Doors);
        Assert.Equal((plain.Level.PlayerStartX, plain.Level.PlayerStartZ), (populated.Level.PlayerStartX, populated.Level.PlayerStartZ));
        Assert.True(Enumerable.Range(0, plain.Level.Width).All(x => Enumerable.Range(0, plain.Level.Depth)
            .All(z => plain.Level[x, z] == populated.Level[x, z])));
    }
}
