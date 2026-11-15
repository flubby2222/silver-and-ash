using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

public class EnemyPlacementTests
{
    private static readonly IReadOnlyList<RoomTemplate> Templates = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    private static readonly DifficultyBalance Difficulty = Balance.Default.Difficulty;

    private static readonly Lazy<GeneratedLevel[]> ManyLevels = new(() => Enumerable.Range(0, 50)
        .Select(seed => EnemyPlacement.Populate(LevelGenerator.Generate(Templates, seed, Balance.Default.Layout), Difficulty))
        .ToArray());

    private static int Ghouls(GeneratedLevel generated, PlacedRoom room) =>
        generated.Level.GhoulPositions.Count(position => room.FloorCells.Contains(Level.ToGrid(position.X, position.Z)));

    private static int Bats(GeneratedLevel generated, PlacedRoom room) =>
        generated.Level.BatPositions.Count(position => room.FloorCells.Contains(Level.ToGrid(position.X, position.Z)));

    [Fact]
    public void The_same_seed_gives_the_same_enemies()
    {
        var first = EnemyPlacement.Populate(LevelGenerator.Generate(Templates, 42, Balance.Default.Layout), Difficulty);
        var second = EnemyPlacement.Populate(LevelGenerator.Generate(Templates, 42, Balance.Default.Layout), Difficulty);

        Assert.Equal(first.Level.GhoulPositions, second.Level.GhoulPositions);
        Assert.Equal(first.Level.BatPositions, second.Level.BatPositions);
    }

    [Fact]
    public void Enemies_stand_only_on_enemy_places_of_combat_and_boss_rooms()
    {
        Assert.All(ManyLevels.Value, generated =>
        {
            var ghoulSlots = generated.Rooms.Where(room => room.Kind is RoomKind.Combat or RoomKind.Boss).SelectMany(room => room.GhoulSlots).ToHashSet();
            var batSlots = generated.Rooms.Where(room => room.Kind is RoomKind.Combat or RoomKind.Boss).SelectMany(room => room.BatSlots).ToHashSet();

            Assert.All(generated.Level.GhoulPositions, position => Assert.Contains(Level.ToGrid(position.X, position.Z), ghoulSlots));
            Assert.All(generated.Level.BatPositions, position => Assert.Contains(Level.ToGrid(position.X, position.Z), batSlots));
            Assert.Empty(generated.Level.DummyPositions);
        });
    }

    [Fact]
    public void Every_combat_room_has_at_least_one_enemy_and_the_boss_room_is_full()
    {
        Assert.All(ManyLevels.Value, generated =>
        {
            Assert.All(generated.Rooms.Where(room => room.Kind == RoomKind.Combat), room =>
                Assert.True(Ghouls(generated, room) + Bats(generated, room) >= 1, $"Seed {generated.Seed}: {room.TemplateName} is empty."));

            var boss = generated.Rooms.Single(room => room.Kind == RoomKind.Boss);
            Assert.Equal(boss.GhoulSlots.Count, Ghouls(generated, boss));
            Assert.Equal(boss.BatSlots.Count, Bats(generated, boss));
        });
    }

    [Fact]
    public void The_farther_a_combat_room_the_more_of_its_places_are_taken()
    {
        Assert.All(ManyLevels.Value, generated =>
        {
            var combat = generated.Rooms.Where(room => room.Kind == RoomKind.Combat).OrderBy(room => room.DistanceFromStart).ToList();
            var nearest = combat[0];
            var farthest = combat[^1];

            // With the standard values: the nearest room gets its "near" shares, the farthest all of them.
            Assert.Equal((int)MathF.Round(nearest.GhoulSlots.Count * Difficulty.GhoulShareNear, MidpointRounding.AwayFromZero), Ghouls(generated, nearest));
            Assert.Equal((int)MathF.Round(nearest.BatSlots.Count * Difficulty.BatShareNear, MidpointRounding.AwayFromZero), Bats(generated, nearest));
            Assert.Equal(farthest.GhoulSlots.Count, Ghouls(generated, farthest));
            Assert.Equal(farthest.BatSlots.Count, Bats(generated, farthest));

            // In between it never goes down with the distance (all combat templates have the same number of places).
            var counts = combat.Select(room => Ghouls(generated, room) + Bats(generated, room)).ToList();
            Assert.Equal(counts.Order(), counts);
        });
    }

    [Fact]
    public void Another_seed_takes_other_places_in_the_same_layout()
    {
        var layout = LevelGenerator.Generate(Templates, 5, Balance.Default.Layout);

        var choices = Enumerable.Range(0, 10)
            .Select(seed => EnemyPlacement.Populate(layout with { Seed = seed }, Difficulty))
            .Select(generated => string.Join(";", generated.Level.GhoulPositions))
            .Distinct()
            .Count();

        Assert.True(choices > 1, "Every seed took the same ghoul places.");
    }

    [Fact]
    public void Start_and_corridors_stay_empty()
    {
        Assert.All(ManyLevels.Value, generated => Assert.All(
            generated.Rooms.Where(room => room.Kind is RoomKind.Start or RoomKind.Corridor),
            room => Assert.Equal(0, Ghouls(generated, room) + Bats(generated, room))));
    }

    [Fact]
    public void With_no_share_at_all_each_combat_room_still_gets_one_enemy()
    {
        var none = new DifficultyBalance { GhoulShareNear = 0f, GhoulShareFar = 0f, BatShareNear = 0f, BatShareFar = 0f };

        var generated = EnemyPlacement.Populate(LevelGenerator.Generate(Templates, 3, Balance.Default.Layout), none);

        Assert.All(generated.Rooms.Where(room => room.Kind == RoomKind.Combat), room =>
            Assert.Equal(1, Ghouls(generated, room) + Bats(generated, room)));
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
