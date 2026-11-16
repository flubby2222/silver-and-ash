using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

public class SecretRoomTests
{
    private static readonly IReadOnlyList<RoomTemplate> Rooms = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    private static readonly LayoutBalance Layout = Balance.Default.Layout;

    // As in the game: level, then enemies, then chests.
    private static readonly Lazy<GeneratedLevel[]> ManyLevels = new(() => Enumerable.Range(0, 100)
        .Select(seed => ChestPlacement.Place(
            EnemyPlacement.Populate(LevelGenerator.Generate(Rooms, seed, Layout), Balance.Default.Difficulty), Balance.Default.Chests))
        .ToArray());

    private static PlacedRoom Secret(GeneratedLevel level) => Assert.Single(level.Rooms, room => room.Kind == RoomKind.Secret);

    [Fact]
    public void Secret_templates_have_one_entrance_one_chest_place_and_no_enemies()
    {
        var secrets = Rooms.Where(room => room.Kind == RoomKind.Secret).ToList();

        Assert.True(secrets.Count >= 2, $"Only {secrets.Count} secret templates.");
        Assert.All(secrets, room =>
        {
            Assert.Single(room.DoorSlots);
            Assert.Single(room.ChestSlots);
            Assert.Empty(room.GhoulSlots);
            Assert.Empty(room.BatSlots);
        });
    }

    [Fact]
    public void Every_level_has_one_secret_room_directly_behind_a_cracked_wall_of_a_combat_room()
    {
        Assert.All(ManyLevels.Value, level =>
        {
            var secret = Secret(level);
            var wall = Assert.Single(level.Doors, door => door.RoomA == secret.Index || door.RoomB == secret.Index);

            Assert.Equal(DoorKind.Cracked, wall.Kind);
            Assert.Equal(secret.Index, wall.RoomB);
            Assert.Equal(RoomKind.Combat, level.Rooms[wall.RoomA].Kind);
            Assert.Equal(level.Rooms[wall.RoomA].DistanceFromStart + 1, secret.DistanceFromStart);
        });
    }

    [Fact]
    public void The_secret_room_holds_one_standing_chest_never_a_beggars()
    {
        var tiers = new List<ChestTier>();
        Assert.All(ManyLevels.Value, level =>
        {
            var secret = Secret(level);
            var chest = Assert.Single(level.Chests!, chest => chest.Room == secret.Index);
            Assert.Contains((chest.X, chest.Z), secret.ChestSlots!);
            Assert.False(chest.RisesWhenCleared);
            tiers.Add(chest.Tier);
        });

        // Standard: Guard's 35 %, King's 50 %, Vampire's 15 %.
        Assert.DoesNotContain(ChestTier.Beggar, tiers);
        Assert.InRange(tiers.Count(tier => tier == ChestTier.Guard) / (float)tiers.Count, 0.22f, 0.48f);
        Assert.InRange(tiers.Count(tier => tier == ChestTier.King) / (float)tiers.Count, 0.37f, 0.63f);
        Assert.InRange(tiers.Count(tier => tier == ChestTier.Vampire) / (float)tiers.Count, 0.06f, 0.26f);
    }

    [Fact]
    public void The_number_of_secret_rooms_comes_from_the_layout()
    {
        foreach (var seed in Enumerable.Range(0, 20))
        {
            var level = LevelGenerator.Generate(Rooms, seed, Layout with { SecretRooms = 0 });
            Assert.DoesNotContain(level.Rooms, room => room.Kind == RoomKind.Secret);
            Assert.DoesNotContain(level.Doors, door => door.Kind == DoorKind.Cracked);
        }
    }

    [Fact]
    public void Without_secret_templates_a_level_is_built_without_a_secret_room()
    {
        var level = LevelGenerator.Generate(Rooms.Where(room => room.Kind != RoomKind.Secret).ToList(), 5, Layout);

        Assert.DoesNotContain(level.Rooms, room => room.Kind == RoomKind.Secret);
    }
}
