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
        // The player stands 1.5 m in front of the start room's wall, 0.7 m beside the middle of the portal: the bat's
        // circle (4-6 m) runs into the wall, which pushes it along the wall and past the left impost (x 8.28-9 m,
        // jutting 0.125 m out of the wall at 3.075-3.2 m) while it rises and sinks between 2 and 2.8 m (its body 0.4 m
        // higher), dives, bites and rises again.
        var world = World(seed, withBat: true);
        var bat = Assert.Single(world.Bats);
        var atTheWall = DoorPlaneZ - BossPortalShape.HalfDepth - BodySizes.Bat.Radius;
        var aside = MiddleX - 0.7f;
        world.Player.PlaceAt(new Vector3(aside, 0f, atTheWall - 1.5f));
        bat.PlaceAt(new Vector3(aside, BodySizes.Bat.HoverHeight, atTheWall));
        var portal = Assert.Single(world.Level.Portals);
        var impost = BossPortalShape.Blocks.Single(block => block.Top == BossPortalShape.Spring);
        var underTheImpost = MiddleX - ((impost.AcrossFrom + impost.AcrossTo) / 2f);
        var highest = 0f;

        for (var tick = 0; tick < 600; tick++)
        {
            world.Tick(PlayerInput.None);

            Assert.False(portal.Contains(bat.Centre), $"tick {tick}: its middle at {bat.Centre}");
            Assert.False(
                BossPortalStoneTests.InStone(world.Level, bat.Position, BodySizes.Bat.Radius, BodySizes.Bat.Height),
                $"tick {tick}: {bat.Position}");
            if (MathF.Abs(bat.Position.X - underTheImpost) < 0.5f && bat.Position.Z > atTheWall - 0.2f)
            {
                highest = MathF.Max(highest, bat.Position.Y);
            }
        }

        // It really flew along the wall below the impost, at circling height - the test would prove nothing otherwise.
        Assert.True(highest >= Balance.Default.Bat.CircleHeightMin, $"highest at the wall below the impost: {highest}");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void A_bat_circling_beside_the_portal_with_a_column_in_its_way_keeps_out_of_both_and_climbs_on(int seed)
    {
        // As above, but a column stands 0.8 m in front of the wall, left of the impost (x 8.28-9 m): the bat's circle
        // runs between them, the wall pushes it back against the column. Its middle never gets into the stone or the
        // column - and the column (in its way at every height alike) does not hold its height: while it touches it, it
        // goes on rising and sinking (issue #635).
        var column = new Column(8f, 7.2f);
        var world = World(seed, withBat: true, columns: [column]);
        var bat = Assert.Single(world.Bats);
        var atTheWall = DoorPlaneZ - BossPortalShape.HalfDepth - BodySizes.Bat.Radius;
        var aside = MiddleX - 0.7f;
        world.Player.PlaceAt(new Vector3(aside, 0f, atTheWall - 1.5f));
        bat.PlaceAt(new Vector3(aside, BodySizes.Bat.HoverHeight, atTheWall));
        var portal = Assert.Single(world.Level.Portals);
        var (touching, held) = (0, 0);

        for (var tick = 0; tick < 600; tick++)
        {
            var before = bat.Position.Y;
            world.Tick(PlayerInput.None);

            Assert.False(portal.Contains(bat.Centre), $"tick {tick}: its middle at {bat.Centre}");
            Assert.False(column.Contains(bat.Centre), $"tick {tick}: its middle at {bat.Centre}");
            Assert.False(
                BossPortalStoneTests.InStone(world.Level, bat.Position, BodySizes.Bat.Radius, BodySizes.Bat.Height),
                $"tick {tick}: {bat.Position}");
            var gap = Vector2.Distance(new Vector2(bat.Position.X, bat.Position.Z), column.Centre)
                - Column.Radius - BodySizes.Bat.Radius;
            if (gap < 0.01f && bat.State == BatState.Circling)
            {
                touching++;
                held += bat.Position.Y == before ? 1 : 0;
            }
        }

        // It really was pressed against the column while circling - the test would prove nothing otherwise. Only where
        // its up-and-down wave turns may its height stay the same for a tick: held, it would stay the same nearly always.
        Assert.True(touching >= 20, $"ticks circling at the column: {touching}");
        Assert.True(held * 10 <= touching, $"ticks its height was held at the column: {held} of {touching}");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(9)]
    [InlineData(12)]
    public void A_bat_rising_below_the_impost_stops_under_it_and_circles_on(int seed)
    {
        // The player stands on the left plinth at the wall, 1.3 m beside the middle - right below the impost (x 8.28-9 m,
        // 0.125 m out of the wall at 3.075-3.2 m). Whenever the bat screeches, he steps back off the plinth and jumps
        // back onto it, so the bat dives past him and rises again at the wall, below the impost: there it must stop
        // climbing before its body reaches the stone - and circle on from there instead of hanging in the air rising.
        var world = World(seed, withBat: true);
        var bat = Assert.Single(world.Bats);
        var wall = DoorPlaneZ - BossPortalShape.HalfDepth;
        var plinth = BossPortalShape.Blocks[BossPortalShape.Runs];
        var onThePlinth = new Vector3(MiddleX - 1.3f, plinth.Top, wall - BodySizes.Player.Radius - 0.01f);
        world.Player.PlaceAt(onThePlinth);
        bat.PlaceAt(new Vector3(onThePlinth.X, BodySizes.Bat.HoverHeight, wall - 3f));
        var back = new PlayerInput(new Vector2(0f, -1f), Vector2.Zero, false);
        var jumpBack = new PlayerInput(new Vector2(0f, 1f), Vector2.Zero, true);
        int dodge = 0, held = 0, rising = 0, longestRise = 0;
        var before = (bat.State, bat.Position.Y);

        for (var tick = 0; tick < 30 * Second; tick++)
        {
            world.Tick(dodge > 20 ? back : dodge > 0 ? jumpBack : PlayerInput.None);
            dodge = world.Events.OfType<BatScreeched>().Any() ? 40 : Math.Max(0, dodge - 1);

            Assert.False(
                BossPortalStoneTests.InStone(world.Level, bat.Position, BodySizes.Bat.Radius, BodySizes.Bat.Height),
                $"tick {tick}: {bat.State} at {bat.Position}");
            if (before.State == BatState.Rising && bat.State == BatState.Circling && bat.Position.Y < before.Y + 1e-4f)
            {
                held++;
            }

            rising = bat.State == BatState.Rising ? rising + 1 : 0;
            longestRise = Math.Max(longestRise, rising);
            before = (bat.State, bat.Position.Y);
        }

        // It really was stopped below the impost at least once - the test would prove nothing otherwise - and never rose
        // longer than a second at a time.
        Assert.True(held >= 1, "the bat was never stopped below the impost");
        Assert.True(longestRise < Second, $"the bat rose {longestRise} ticks at a time");
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
    public void Gorm_walking_along_the_wall_past_the_portal_stays_out_of_its_plinths_and_buttresses()
    {
        // The player walks into the boss room (it seals) and waits 1.1 m in front of its wall, 6 m to the left of the
        // portal's middle; Gorm starts as far to the right. His way straight to the player runs along the wall: his body
        // (0.9 m) reaches past the plinths (0.125 m out of the wall, 0.5 m high) to the buttresses (0.24 m out of it, up
        // to 5.3 m) on the boss room's side. He only walks here: no attack reaches the player, else he would stand and
        // strike from afar.
        var walker = Tough with { Gorm = Tough.Gorm with { NearRange = 0f, FarRange = 0f } };
        var world = World(1, withBat: false, walker);
        var gorm = world.Gorm!;
        world.Player.PlaceAt(new Vector3(MiddleX, 0f, 6f));
        world.Tick(PressF);
        for (var tick = 0; tick < 5 * Second && !world.Rooms[1].IsSealed; tick++)
        {
            world.Tick(Forward);
        }

        Assert.True(world.Rooms[1].IsSealed, "The boss room never sealed - the test would prove nothing.");
        var wall = DoorPlaneZ + BossPortalShape.HalfDepth;
        world.Player.PlaceAt(new Vector3(MiddleX - 6f, 0f, wall + 1.1f));
        ((IEnemy)gorm).PlaceAt(new Vector3(MiddleX + 6f, 0f, wall + 1.1f));
        var touched = 0;

        for (var tick = 0; tick < 10 * Second; tick++)
        {
            world.Tick(PlayerInput.None);

            Assert.False(
                BossPortalStoneTests.InStone(world.Level, gorm.Position, BodySizes.Gorm.Radius, BodySizes.Gorm.Height),
                $"tick {tick}: {gorm.State} at {gorm.Position}");
            if (BossPortalStoneTests.InStone(world.Level, gorm.Position, BodySizes.Gorm.Radius + 0.05f, BodySizes.Gorm.Height))
            {
                touched++;
            }
        }

        // He really brushed along the stone and got past it - the test would prove nothing otherwise.
        Assert.True(touched > 0, "Gorm never came within 5 cm of the stone");
        Assert.True(gorm.Position.X < MiddleX - BossPortalShape.HalfWidth, $"Gorm never got past the portal: {gorm.Position}");
    }

    [Fact]
    public void Gorms_vial_does_not_lie_in_the_plinth_when_he_falls_beside_it()
    {
        // He falls 1.5 m in front of the wall of the boss room, 1.5 m beside the middle, looking along -Z: the vial would
        // lie Balance.Gorm.VialDistance (1.2 m) in front of him - on open floor of the boss room, but reaching into the plinth and the buttress.
        var world = World(1, withBat: false);
        var gorm = world.Gorm!;
        Assert.Equal(-Vector3.UnitZ, gorm.Facing);
        var fell = new Vector3(MiddleX + 1.5f, 0f, DoorPlaneZ + BossPortalShape.HalfDepth + 1.5f);
        ((IEnemy)gorm).PlaceAt(fell);
        var portal = Assert.Single(world.Level.Portals);
        Assert.True(portal.Covers(new Vector2(fell.X, fell.Z - Balance.Default.Gorm.VialDistance), BodySizes.Pickup.Radius), "the vial would not reach the stone - the test would prove nothing");

        gorm.TakeDamage(gorm.Health, new List<GameEvent>());
        world.Tick(PlayerInput.None);

        var vial = Assert.Single(world.Pickups, pickup => pickup.Kind == ConsumableKind.BloodVial);
        Assert.False(portal.Covers(new Vector2(vial.Position.X, vial.Position.Z), BodySizes.Pickup.Radius), $"the vial lies at {vial.Position}");
        Assert.Equal(Balance.Default.Gorm.VialDistance, Vector2.Distance(new Vector2(vial.Position.X, vial.Position.Z), new Vector2(fell.X, fell.Z)), 0.001f);
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

    private static GameWorld World(int seed, bool withBat, Balance? balance = null, Column[]? columns = null)
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
        return new GameWorld(LevelParser.Parse(map).WithColumns(columns ?? []), seed, balance ?? Tough, doors, rooms);

        static List<(int X, int Z)> Rows(int from, int to) =>
            Enumerable.Range(from, to - from + 1).SelectMany(z => Enumerable.Range(1, 8).Select(x => (x, z))).ToList();
    }
}
