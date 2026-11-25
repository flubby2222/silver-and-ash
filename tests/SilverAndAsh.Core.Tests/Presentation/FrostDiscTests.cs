using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Issue #785: the disc of Grave Chill lies on the floors below the enemy - at a step neither floating over the lower
/// floor nor sunk into the higher one, cut off at walls and at floors higher than the enemy's. The enemies walk tick by
/// tick through levels with steps of the level parser ('1' a floor 0.25 m high, '3' one of 0.75 m).
/// </summary>
public class FrostDiscTests
{
    // A whole disc of FrostLook.DiscSegments corners: inside one triangle per corner, the rim two.
    private static float WholeArea(float radius) =>
        0.5f * FrostLook.DiscSegments * radius * radius * MathF.Sin(MathF.Tau / FrostLook.DiscSegments);

    [Fact]
    public void On_a_flat_floor_the_disc_is_whole_and_lies_at_the_feet()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\n.\nG"));
        var ghoul = world.Ghouls[0];
        Assert.True(world.ApplyStatus(ghoul, StatusKind.GraveChill, world.Player));
        world.Tick(PlayerInput.None);

        var readout = Assert.Single(EnemyStatusReadout.Of(world));
        var disc = Assert.IsType<FrostDisc>(readout.Disc);

        Assert.Equal(FrostLook.DiscSegments, disc.Inside.Count);
        Assert.Equal(2 * FrostLook.DiscSegments, disc.Rim.Count);
        Assert.All(disc.Inside.Concat(disc.Rim), triangle => Assert.All(Corners(triangle), corner => Assert.Equal(ghoul.Position.Y, corner.Y)));
        Assert.Equal(WholeArea(readout.DiscRadius), Area(disc), 3);
        AssertLaidOnTheFloors(world.Level, readout);
    }

    [Fact]
    public void A_ghoul_climbing_a_low_step_has_its_disc_cut_at_the_step_then_lying_on_both_floors()
    {
        // The ghoul walks from the floor (0 m) up onto the step ('1', 0.25 m) towards the player.
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\n1\nG"));
        var ghoul = world.Ghouls[0];
        var step = world.Level[0, 3].FloorHeight;
        var (cut, onBoth) = (false, false);

        for (var tick = 0; tick < 900 && !(cut && onBoth); tick++)
        {
            if (!ghoul.Status.Has(StatusKind.GraveChill))
            {
                Assert.True(world.ApplyStatus(ghoul, StatusKind.GraveChill, world.Player));
            }

            world.Tick(PlayerInput.None);
            var readout = Assert.Single(EnemyStatusReadout.Of(world));
            AssertLaidOnTheFloors(world.Level, readout);
            var disc = readout.Disc!;
            var heights = disc.Inside.Concat(disc.Rim).Select(triangle => triangle.A.Y).Distinct().ToList();

            // Still below the step, the disc stops where the step begins; on top, it reaches down onto the floor.
            cut |= readout.Floor == 0f && Area(disc) < WholeArea(readout.DiscRadius) - 0.01f;
            onBoth |= readout.Floor == step && heights.Contains(0f) && heights.Contains(step);
        }

        Assert.True(cut, "The disc of the ghoul below the step was never cut at it.");
        Assert.True(onBoth, "The disc of the ghoul on the step never lay on both floors.");
    }

    [Fact]
    public void At_a_step_too_high_to_climb_the_disc_stops_at_its_edge()
    {
        // The ghoul presses against a step of 0.75 m, higher than it can climb, between it and the player.
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\n3\nG"));
        var ghoul = world.Ghouls[0];
        ghoul.PlaceAt(new Vector3(1f, 0f, (4 * Level.CellSize) + 0.4f));
        Assert.True(world.ApplyStatus(ghoul, StatusKind.GraveChill, world.Player));

        for (var tick = 0; tick < 60; tick++)
        {
            world.Tick(PlayerInput.None);
            var readout = Assert.Single(EnemyStatusReadout.Of(world));
            AssertLaidOnTheFloors(world.Level, readout);
            Assert.Equal(0f, readout.Floor);
            if (readout.Feet.Z - readout.DiscRadius < 4 * Level.CellSize)
            {
                Assert.True(Area(readout.Disc!) < WholeArea(readout.DiscRadius) - 0.01f);
            }
        }
    }

    [Fact]
    public void A_wall_cuts_the_disc_off()
    {
        var level = LevelParser.Parse("P");
        var disc = FrostDisc.Of(level, new Vector3(0.3f, 0f, 1f), 0.62f, 0f);

        var readout = new EnemyStatusReadout(1, new Vector3(0.3f, 0f, 1f), 1f, BodySizes.Ghoul.Radius, []) { Floor = 0f, Disc = disc };
        AssertLaidOnTheFloors(level, readout);
        Assert.True(Area(disc) < WholeArea(0.62f) - 0.1f);
        Assert.All(disc.Inside.Concat(disc.Rim).SelectMany(Corners), corner => Assert.True(corner.X >= -1e-5f));
    }

    [Fact]
    public void Gorm_at_a_step_has_his_disc_on_both_floors()
    {
        // Gorm stands on the step ('1') with his middle out over the lower floor.
        var level = LevelParser.Parse("P.\n1.");
        var body = new CylinderBody(new Vector3(1.6f, 0.25f, 2.2f), BodySizes.Gorm.Radius, BodySizes.Gorm.Height);
        var floor = FrostDisc.FloorBelow(level, body);
        var disc = FrostDisc.Of(level, body.Position, FrostLook.DiscRadiusFor(body.Radius), floor);

        Assert.Equal(0.25f, floor);
        var readout = new EnemyStatusReadout(1, body.Position, body.Height, body.Radius, []) { Floor = floor, Disc = disc };
        AssertLaidOnTheFloors(level, readout);
        var heights = disc.Inside.Concat(disc.Rim).Select(triangle => triangle.A.Y).Distinct().ToList();
        Assert.Contains(0f, heights);
        Assert.Contains(0.25f, heights);
    }

    [Theory]
    [InlineData("ghoul-chilled-step")]
    [InlineData("gorm-chilled-step")]
    public void In_the_photos_at_a_step_the_enemy_stands_on_it_and_its_disc_lies_on_both_floors(string scene)
    {
        var studio = new PhotoStudio(RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms")), Balance.Default);
        var setup = PhotoScenes.Find(scene)!.Build(studio);
        setup.Run();

        var readout = Assert.Single(EnemyStatusReadout.Of(setup.World));
        AssertLaidOnTheFloors(setup.World.Level, readout);
        Assert.Equal(0.25f, readout.Floor);
        Assert.Equal(0.25f, readout.Feet.Y, 3);
        var heights = readout.Disc!.Inside.Concat(readout.Disc.Rim).Select(triangle => triangle.A.Y).Distinct().ToList();
        Assert.Contains(0f, heights);
        Assert.Contains(0.25f, heights);
    }

    [Fact]
    public void The_floor_below_is_the_highest_floor_the_body_covers_not_above_its_feet()
    {
        var level = LevelParser.Parse("1P");   // x 0..2 at 0.25 m, x 2..4 at 0 m
        Assert.Equal(0.25f, FrostDisc.FloorBelow(level, new CylinderBody(new Vector3(2.1f, 0.25f, 1f), 0.3f, 1f)));
        Assert.Equal(0f, FrostDisc.FloorBelow(level, new CylinderBody(new Vector3(2.1f, 0f, 1f), 0.3f, 1f)));
        Assert.Equal(0f, FrostDisc.FloorBelow(level, new CylinderBody(new Vector3(3f, 1.5f, 1f), 0.3f, 1f)));
    }

    [Fact]
    public void Inside_a_wall_the_floor_below_is_the_floor_of_the_cell_it_is_in()
    {
        var level = LevelParser.Parse("#P");
        var inWall = new CylinderBody(new Vector3(0.5f, -1f, 1f), 0.3f, 1f);

        Assert.Equal(level.CellAt(inWall.Position).FloorHeight, FrostDisc.FloorBelow(level, inWall));
    }

    [Fact]
    public void A_disc_needs_a_level_and_a_radius()
    {
        var level = LevelParser.Parse("P");
        Assert.Throws<ArgumentNullException>(() => FrostDisc.Of(null!, Vector3.One, 0.5f, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => FrostDisc.Of(level, Vector3.One, 0f, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => FrostDisc.Of(level, Vector3.One, -0.1f, 0f));
        Assert.Throws<ArgumentNullException>(() => FrostDisc.FloorBelow(null!, new CylinderBody(Vector3.One, 0.3f, 1f)));
    }

    [Fact]
    public void An_enemy_without_grave_chill_has_no_disc()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\n.\nG"));
        world.ApplyStatus(world.Ghouls[0], StatusKind.Marked, world.Player);
        world.Tick(PlayerInput.None);

        Assert.Null(Assert.Single(EnemyStatusReadout.Of(world)).Disc);
    }

    // Every piece lies flat on the floor of the cell it is over: no wall, no floor higher than the enemy's, inside the disc.
    private static void AssertLaidOnTheFloors(Level level, EnemyStatusReadout readout)
    {
        var disc = Assert.IsType<FrostDisc>(readout.Disc);
        foreach (var triangle in disc.Inside.Concat(disc.Rim))
        {
            Assert.Equal(triangle.A.Y, triangle.B.Y);
            Assert.Equal(triangle.A.Y, triangle.C.Y);
            var middle = (triangle.A + triangle.B + triangle.C) / 3f;
            var (x, z) = Level.ToGrid(middle.X, middle.Z);
            var cell = level[x, z];
            Assert.False(cell.IsSolid, $"A piece of the disc lies on a wall at {middle}.");
            Assert.Equal(cell.FloorHeight, triangle.A.Y);
            Assert.True(triangle.A.Y <= readout.Floor + 1e-3f, $"A piece lies on a floor higher than the enemy's at {middle}.");
            Assert.All(Corners(triangle), corner => Assert.True(Flat(corner - readout.Feet) <= FrostLook.DiscRadiusFor(readout.Radius) + 1e-4f));
            Assert.True(Vector3.Cross(triangle.B - triangle.A, triangle.C - triangle.A).Y != 0f, "A piece without area.");
        }
    }

    private static Vector3[] Corners(DiscTriangle triangle) => [triangle.A, triangle.B, triangle.C];

    private static float Flat(Vector3 vector) => new Vector2(vector.X, vector.Z).Length();

    private static float Area(FrostDisc disc) =>
        disc.Inside.Concat(disc.Rim).Sum(triangle => MathF.Abs(Vector3.Cross(triangle.B - triangle.A, triangle.C - triangle.A).Y) / 2f);
}
