using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>Chests block the way - for players and enemies - but can be jumped onto (issue #184).</summary>
public class ChestBlockingTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };

    private static ChestPlan Plan(int x, int z, bool rises = false, int room = 0) =>
        new(x, z, room, ChestTier.Beggar, IsLocked: false, Silver: 5, HasBloodVial: false, RisesWhenCleared: rises);

    // A corridor along +Z, one cell wide; the player in row 0, the chest in row 3.
    private static GameWorld Corridor(bool rises = false) =>
        new(LevelParser.Parse("#P#\n#.#\n#.#\n#.#\n#.#\n#.#"), 1, Balance.Default, chests: [Plan(1, 3, rises)]);

    [Fact]
    public void A_standing_chest_stops_the_player()
    {
        var world = Corridor();
        var chest = world.Chests[0];

        RunTicks(world, Forward, 3 * Second);

        Assert.Equal(chest.BoxMin.Z - BodySizes.Player.Radius, world.Player.Position.Z, 0.02f);
    }

    [Fact]
    public void Jumping_the_player_gets_onto_or_over_the_chest()
    {
        var world = Corridor();
        var chest = world.Chests[0];
        RunTicks(world, Forward, 3 * Second, () => world.Player.Position.Z >= chest.BoxMin.Z - 1.2f);

        world.Tick(Forward with { Jump = true });
        RunTicks(world, Forward, Second / 2);

        Assert.True(world.Player.Position.Z > chest.BoxMin.Z, $"Stuck in front of the chest at z {world.Player.Position.Z}.");
    }

    [Fact]
    public void Landing_on_a_chest_the_player_stands_on_its_top()
    {
        // Jump straight up from beside its middle, drift onto it, stop.
        var world = Corridor();
        var chest = world.Chests[0];
        RunTicks(world, Forward, 3 * Second, () => world.Player.Position.Z >= chest.BoxMin.Z - 1.2f);
        world.Tick(Forward with { Jump = true });
        RunTicks(world, Forward, Second, () => world.Player.Position.Z >= chest.Position.Z - 0.1f);
        RunTicks(world, PlayerInput.None, Second, () => world.Player.IsGrounded);
        RunTicks(world, PlayerInput.None, Second / 2);

        Assert.True(world.Player.IsGrounded);
        Assert.Equal(chest.BoxMax.Y, world.Player.Position.Y, 0.01f);
    }

    [Fact]
    public void A_chest_still_under_the_floor_does_not_block()
    {
        // Without rooms it never rises: the player walks over the spot.
        var world = Corridor(rises: true);
        var chest = world.Chests[0];

        RunTicks(world, Forward, 3 * Second);

        Assert.Equal(0f, chest.RiseFraction);
        Assert.True(world.Player.Position.Z > chest.BoxMax.Z, $"Stopped at z {world.Player.Position.Z}.");
    }

    [Fact]
    public void A_chest_rising_under_the_player_lifts_them_up()
    {
        // Room 0 (start) in row 0, door in row 1, room 1 (combat, a ghoul) in rows 2-5 with a chest that rises once
        // the room is cleared - right where the player stands.
        const string Map = "#.P.#\n##.##\n#...#\n#...#\n#.G.#\n#...#";
        static List<(int X, int Z)> Rows(int from, int to) =>
            Enumerable.Range(from, to - from + 1).SelectMany(z => new[] { (1, z), (2, z), (3, z) }).ToList();
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 5, 1, 0, Rows(0, 0), [], []),
            new(1, RoomKind.Combat, "arena", 0, 2, 5, 4, 1, Rows(2, 5), [(2, 4)], []),
        ];
        LevelDoor[] doors = [new(2, 1, 0, 1, RoomSide.South)];
        var world = new GameWorld(LevelParser.Parse(Map), 1, Balance.Default, doors, rooms, [Plan(2, 3, rises: true, room: 1)]);
        var chest = world.Chests[0];
        RunTicks(world, PlayerInput.None with { Interact = true }, 1);
        RunTicks(world, Forward, 3 * Second, () => world.Player.Position.Z >= chest.Position.Z - 0.15f);
        RunTicks(world, PlayerInput.None, Second / 4);
        Assert.True(world.Rooms[1].IsSealed, "The room never sealed - the test would prove nothing.");
        Assert.Equal(0f, chest.RiseFraction);

        world.Ghouls[0].TakeDamage(10_000, []);
        RunTicks(world, PlayerInput.None, 2 * Second);

        Assert.Equal(1f, chest.RiseFraction);
        Assert.Equal(chest.BoxMax.Y, world.Player.Position.Y, 0.01f);
        Assert.True(MathF.Abs(world.Player.Position.Z - chest.Position.Z) < 0.4f, "The chest pushed the player away.");
    }

    [Fact]
    public void A_ghoul_walks_round_a_chest_not_through_it()
    {
        // The ghoul straight behind the chest; it must go round to reach the player.
        var level = LevelParser.Parse("#######\n#..P..#\n#.....#\n#.....#\n#.....#\n#..G..#\n#######");
        var world = new GameWorld(level, 1, Balance.Default, chests: [Plan(3, 3)]);
        var chest = world.Chests[0];
        var ghoul = world.Ghouls[0];
        var closest = float.MaxValue;

        RunTicks(world, PlayerInput.None, 6 * Second, () =>
        {
            closest = MathF.Min(closest, Flat(ghoul.Position - chest.Position));
            return world.Player.Health < world.Player.MaxHealth;
        });

        Assert.True(world.Player.Health < world.Player.MaxHealth, "The ghoul never reached the player.");
        Assert.True(closest >= chest.RoundBody.Radius + BodySizes.Ghoul.Radius - 0.02f, $"The ghoul came {closest} m close to the chest's middle.");
    }

    [Fact]
    public void Gorm_walks_round_a_chest_not_through_it()
    {
        var noAttacks = Balance.Default with { Gorm = Balance.Default.Gorm with { NearRange = 0f, FarRange = 0f } };
        var level = LevelParser.Parse("#########\n#...P...#\n#.......#\n#.......#\n#.......#\n#.......#\n#...T...#\n#########");
        var world = new GameWorld(level, 1, noAttacks, chests: [Plan(4, 3)]);
        var chest = world.Chests[0];
        var gorm = world.Gorm!;
        var closest = float.MaxValue;

        RunTicks(world, PlayerInput.None, 8 * Second, () =>
        {
            closest = MathF.Min(closest, Flat(gorm.Position - chest.Position));
            return false;
        });

        Assert.True(Flat(gorm.Position - world.Player.Position) < 4f, "Gorm never came close - the test would prove nothing.");
        Assert.True(closest >= chest.RoundBody.Radius + BodySizes.Gorm.Radius - 0.02f, $"Gorm came {closest} m close to the chest's middle.");
    }

    [Fact]
    public void A_ghoul_still_finds_its_way_to_a_player_standing_on_a_chest()
    {
        // The player climbs onto the chest in the corridor; the ghoul has to go round corners to get there.
        var level = LevelParser.Parse("#P###\n#.###\n#.###\n#.###\n#...#\n###.#\n#G..#");
        var world = new GameWorld(level, 1, Balance.Default, chests: [Plan(1, 3)]);
        var chest = world.Chests[0];
        RunTicks(world, Forward, 3 * Second, () => world.Player.Position.Z >= chest.BoxMin.Z - 1.2f);
        world.Tick(Forward with { Jump = true });
        RunTicks(world, Forward, Second, () => world.Player.Position.Z >= chest.Position.Z - 0.1f);
        RunTicks(world, PlayerInput.None, Second, () => world.Player.IsGrounded);
        Assert.Equal(chest.BoxMax.Y, world.Player.Position.Y, 0.01f);

        world.Ghouls[0].TakeDamage(1, []);
        RunTicks(world, PlayerInput.None, 10 * Second, () => world.Player.Health < world.Player.MaxHealth);

        Assert.True(world.Player.Health < world.Player.MaxHealth, "The ghoul never reached the player on the chest.");
    }

    [Fact]
    public void A_chest_still_under_the_floor_is_no_wall_for_the_enemies_way()
    {
        // A corridor; the hidden chest lies between ghoul and player. As a wall it would cut the only way.
        var level = LevelParser.Parse("#P#\n#.#\n#.#\n#.#\n#.#\n#G#");
        var world = new GameWorld(level, 1, Balance.Default, chests: [Plan(1, 3, rises: true)]);
        world.Ghouls[0].TakeDamage(1, []);

        RunTicks(world, PlayerInput.None, 6 * Second, () => world.Player.Health < world.Player.MaxHealth);

        Assert.True(world.Player.Health < world.Player.MaxHealth, "The ghoul never got past the hidden chest.");
    }

    [Fact]
    public void A_ghoul_on_a_chest_s_spot_is_pushed_off_it_and_still_finds_its_way()
    {
        // The ghoul starts right where the chest stands: the chest's body pushes it out at once, and from inside the
        // chest's cell it still finds a way (the cell is wall only for planning).
        var level = LevelParser.Parse("#######\n#..P..#\n#.....#\n#.....#\n#..G..#\n#######");
        var world = new GameWorld(level, 1, Balance.Default, chests: [Plan(3, 4)]);
        var chest = world.Chests[0];
        var ghoul = world.Ghouls[0];

        // Pushed off by the whole chest (its corners included), not just part of it. It sees the player, so nothing
        // else moves it: it has to find its way from inside the chest's cell.
        RunTicks(world, PlayerInput.None, 2);
        var corner = new Vector2(Chest.Width / 2f, Chest.Depth / 2f).Length();
        Assert.True(Flat(ghoul.Position - chest.Position) >= corner + BodySizes.Ghoul.Radius - 0.01f, $"Still on the chest: {Flat(ghoul.Position - chest.Position)} m from its middle.");

        RunTicks(world, PlayerInput.None, 6 * Second, () => world.Player.Health < world.Player.MaxHealth);
        Assert.True(world.Player.Health < world.Player.MaxHealth, "The ghoul never left the chest's cell.");
    }

    [Fact]
    public void A_ghoul_in_the_corner_of_a_chest_s_cell_still_finds_its_way()
    {
        // Sliding round a chest a ghoul can end up in a corner of the chest's cell - a wall on its map for planning.
        // From there it plans on the plain level, or it could not even start.
        var level = LevelParser.Parse("#######\n#..P..#\n#.....#\n#.....#\n#.....#\n#######");
        var chest = level.CellCenter(3, 4);
        var paths = level.WithCells([(3, 4, Cell.Wall)]);
        var player = new Player(level);
        var ghoul = new Ghoul(1, chest + new Vector3(-0.85f, 0f, -0.85f), -Vector3.UnitZ);
        Assert.Equal((3, 4), Level.ToGrid(ghoul.Position.X, ghoul.Position.Z));
        var start = ghoul.Position;

        for (var tick = 0; tick < Second; tick++)
        {
            ghoul.Act(level, [player], [], [], paths);
        }

        Assert.True(Flat(ghoul.Position - start) > 1f, "The ghoul did not move.");
    }

    [Fact]
    public void Gorm_on_a_chest_s_spot_is_pushed_off_it_and_still_finds_his_way()
    {
        var noAttacks = Balance.Default with { Gorm = Balance.Default.Gorm with { NearRange = 0f, FarRange = 0f } };
        var level = LevelParser.Parse("#########\n#...P...#\n#.......#\n#.......#\n#.......#\n#...T...#\n#########");
        var world = new GameWorld(level, 1, noAttacks, chests: [Plan(4, 5)]);
        var chest = world.Chests[0];
        var gorm = world.Gorm!;
        var start = gorm.Position;

        RunTicks(world, PlayerInput.None, 2);
        Assert.True(Flat(gorm.Position - chest.Position) >= chest.RoundBody.Radius, "Gorm is still on the chest.");

        RunTicks(world, PlayerInput.None, 6 * Second);
        Assert.True(Flat(gorm.Position - start) > 3f, "Gorm never left the chest's cell.");
    }

    private static float Flat(Vector3 offset) => new Vector2(offset.X, offset.Z).Length();
}
