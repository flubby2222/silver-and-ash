using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// What moves beside the stone of a boss portal stays out of it, in a running world (issue #627): a circling bat under
/// the impost, a dead bat above the plinth, Gorm's loosened stones and his loot.
/// </summary>
public class BossPortalNeighbourTests
{
    // Room 0 (start) in rows 0-3 (z 0-8 m), the boss door in cells (4, 4) and (5, 4) - its portal's middle at x 10 m,
    // its door plane at z 9 m - and room 1 (boss room) in rows 5-11. The player starts at x 11 m, Gorm stands right
    // behind the door at x 11 m, so he looks straight along -Z. Bat: in row 1 of the start room, awake.
    private const float MiddleX = 10f;
    private const float DoorPlaneZ = 9f;
    private const int Second = SimulationClock.TicksPerSecond;

    private static readonly Balance Tough = Balance.Default with { Player = Balance.Default.Player with { MaxHealth = 100_000 } };
    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };
    private static readonly PlayerInput Forward = new(new Vector2(0f, 1f), Vector2.Zero, false);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void A_bat_circling_beside_the_portal_never_flies_into_its_stone(int seed)
    {
        // The player stands so that the bat's circle reaches 0.3 m into the wall of the start room right below the left
        // impost (x 8.28-9 m, jutting 0.125 m out of the wall at 3.075-3.2 m): the wall pushes it along the wall while
        // it rises and sinks between 2 and 2.8 m (its body 0.4 m higher), dives, bites and rises again.
        var world = World(seed, withBat: true);
        var bat = Assert.Single(world.Bats);
        var underTheImpost = MiddleX - 1.3f;
        var atTheWall = DoorPlaneZ - BossPortalShape.HalfDepth - BodySizes.Bat.Radius;
        world.Player.PlaceAt(new Vector3(underTheImpost, 0f, atTheWall - bat.CircleRadius + 0.3f));
        bat.PlaceAt(new Vector3(underTheImpost, BodySizes.Bat.HoverHeight, atTheWall));
        var portal = Assert.Single(world.Level.Portals);
        var highest = 0f;

        for (var tick = 0; tick < 600; tick++)
        {
            world.Tick(PlayerInput.None);

            Assert.False(portal.Contains(bat.Centre), $"tick {tick}: its middle at {bat.Centre}");
            Assert.False(
                BossPortalStoneTests.InStone(world.Level, bat.Position, BodySizes.Bat.Radius, BodySizes.Bat.Height),
                $"tick {tick}: {bat.Position}");
            if (MathF.Abs(bat.Position.X - underTheImpost) < 0.7f && bat.Position.Z > atTheWall - 0.2f)
            {
                highest = MathF.Max(highest, bat.Position.Y);
            }
        }

        // It really flew along the wall below the impost, at circling height - the test would prove nothing otherwise.
        Assert.True(highest >= Balance.Default.Bat.CircleHeightMin, $"highest at the wall below the impost: {highest}");
    }

    [Fact]
    public void A_bat_that_dies_above_the_plinth_comes_to_lie_on_it()
    {
        // The left plinth stands 1-2 m beside the middle, 0.125 m out of the wall, 0.5 m high. The bat hovers with half
        // its body above it (and clear of the buttress at 1.72-2 m) and dies there.
        var world = World(1, withBat: true);
        var bat = Assert.Single(world.Bats);
        var plinth = BossPortalShape.Blocks[BossPortalShape.Runs];
        var abovePlinth = new Vector3(MiddleX - 1.3f, BodySizes.Bat.HoverHeight, DoorPlaneZ - BossPortalShape.HalfDepth - BodySizes.Bat.Radius);
        bat.PlaceAt(abovePlinth);
        Assert.False(BossPortalStoneTests.InStone(world.Level, abovePlinth, BodySizes.Bat.Radius, BodySizes.Bat.Height));

        bat.TakeDamage(bat.Health, new List<GameEvent>());
        for (var tick = 0; tick < 2 * Second; tick++)
        {
            world.Tick(PlayerInput.None);

            // A body lying on the plinth touches its top: look from just above its feet.
            var above = bat.Position + new Vector3(0f, 0.01f, 0f);
            Assert.False(
                BossPortalStoneTests.InStone(world.Level, above, BodySizes.Bat.Radius, BodySizes.Bat.Height - 0.01f),
                $"tick {tick}: {bat.Position}");
        }

        Assert.Equal(plinth.Top, bat.Position.Y, 0.001f);
        Assert.Equal(abovePlinth.X, bat.Position.X, 0.001f);
        Assert.Equal(abovePlinth.Z, bat.Position.Z, 0.001f);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Gorms_loosened_stones_never_fall_into_the_portal_stone(int seed)
    {
        // The player walks through the opening into the boss room and stays right behind the door: his stones fall up to
        // 4 m around the player, many of them along the wall where the portal's plinths and buttresses stand out of it.
        var balance = Tough with { Gorm = Tough.Gorm with { StoneCount = 400 } };
        var world = World(seed, withBat: false, balance);
        var portal = Assert.Single(world.Level.Portals);
        var events = new List<GameEvent>();
        void Run(PlayerInput input, int ticks, Func<bool> until)
        {
            for (var tick = 0; tick < ticks && !until(); tick++)
            {
                world.Tick(input);
                events.AddRange(world.Events);
            }
        }

        world.Player.PlaceAt(new Vector3(MiddleX, 0f, 6f));
        Run(PressF, 1, () => false);
        Run(Forward, 5 * Second, () => world.Rooms[1].IsSealed);
        Assert.True(world.Rooms[1].IsSealed, "The boss room never sealed - the test would prove nothing.");
        world.Gorm!.TakeDamage(DownToHalf(balance.Gorm), events);
        Run(PlayerInput.None, 60 * Second, () => events.OfType<StoneFalling>().Count() >= 2 * balance.Gorm.StoneCount);

        var stones = events.OfType<StoneFalling>().ToList();
        Assert.NotEmpty(stones);
        var nearTheWall = stones.Count(stone => stone.At.Z < DoorPlaneZ + 1.5f && MathF.Abs(stone.At.X - MiddleX) < 2.5f);
        Assert.True(nearTheWall > 20, $"only {nearTheWall} stones fell near the portal - the test would prove nothing");
        foreach (var stone in stones)
        {
            for (var y = stone.At.Y + 0.01f; y < BossPortalShape.Height; y += 0.1f)
            {
                Assert.False(portal.Contains(stone.At with { Y = y }), $"a stone falls into the stone at {stone.At} (height {y})");
            }
        }
    }

    [Fact]
    public void Gorms_vial_does_not_lie_in_the_plinth_when_he_falls_beside_it()
    {
        // He falls 1.5 m in front of the wall of the boss room, 1.5 m beside the middle, looking along -Z: the vial would
        // lie 1.2 m in front of him - on open floor of the boss room, but reaching into the plinth and the buttress.
        var world = World(1, withBat: false);
        var gorm = world.Gorm!;
        Assert.Equal(-Vector3.UnitZ, gorm.Facing);
        var fell = new Vector3(MiddleX + 1.5f, 0f, DoorPlaneZ + BossPortalShape.HalfDepth + 1.5f);
        ((IEnemy)gorm).PlaceAt(fell);
        var portal = Assert.Single(world.Level.Portals);
        Assert.True(portal.Covers(new Vector2(fell.X, fell.Z - 1.2f), BodySizes.Pickup.Radius), "the vial would not reach the stone - the test would prove nothing");

        gorm.TakeDamage(gorm.Health, new List<GameEvent>());
        world.Tick(PlayerInput.None);

        var vial = Assert.Single(world.Pickups, pickup => pickup.Kind == ConsumableKind.BloodVial);
        Assert.False(portal.Covers(new Vector2(vial.Position.X, vial.Position.Z), BodySizes.Pickup.Radius), $"the vial lies at {vial.Position}");
        Assert.Equal(1.2f, Vector2.Distance(new Vector2(vial.Position.X, vial.Position.Z), new Vector2(fell.X, fell.Z)), 0.001f);
        for (var y = 0.01f; y < BossPortalShape.Height; y += 0.1f)
        {
            foreach (var corner in new[] { new Vector2(-1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(1f, 1f) })
            {
                var at = vial.Position + new Vector3(corner.X * BodySizes.Pickup.Radius * 0.99f, 0f, corner.Y * BodySizes.Pickup.Radius * 0.99f);
                Assert.False(portal.Contains(at with { Y = y }), $"the vial at {vial.Position} reaches into the stone");
            }
        }
    }

    private static int DownToHalf(GormBalance gorm) => gorm.MaxHealth - (int)MathF.Floor(gorm.MaxHealth * gorm.Phase2Health);

    private static GameWorld World(int seed, bool withBat, Balance? balance = null)
    {
        var map = string.Join('\n',
            "#....P...#",
            withBat ? "#.B......#" : "#........#",
            "#........#",
            "#........#",
            "####..####",
            "#........#",
            "#....T...#",
            "#........#",
            "#........#",
            "#........#",
            "#........#",
            "#........#",
            "##########");
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 10, 4, 0, Rows(0, 3), [], []),
            new(1, RoomKind.Boss, "boss", 0, 5, 10, 7, 1, Rows(5, 11), [], []),
        ];
        LevelDoor[] doors = [new(4, 4, 0, 1, RoomSide.South, DoorKind.Boss) { MoreCells = [(5, 4)] }];
        return new GameWorld(LevelParser.Parse(map), seed, balance ?? Tough, doors, rooms);

        static List<(int X, int Z)> Rows(int from, int to) =>
            Enumerable.Range(from, to - from + 1).SelectMany(z => Enumerable.Range(1, 8).Select(x => (x, z))).ToList();
    }
}
