using System.Numerics;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Physics;

/// <summary>Issue #539: a free-standing column blocks walking, shots and sight like a wall.</summary>
public class ColumnCollisionTests
{
    private const float Tick = SimulationClock.SecondsPerTick;
    private const float Tolerance = 0.001f;
    private static readonly float Radius = BodySizes.Player.Radius;

    // 9 x 9 open cells (0 .. 18 m), the column in the middle cell, its middle at (9, 9).
    private static readonly Column Middle = Column.AtCell(4, 4);
    private static readonly Level Hall = Open(9, 9).WithColumns([Middle]);

    /// <summary>An open level of <paramref name="width"/> x <paramref name="depth"/> cells, the player's mark in a corner.</summary>
    internal static Level Open(int width, int depth) =>
        LevelParser.Parse(string.Join("\n", Enumerable.Range(0, depth).Select(z => z == 0 ? "P" + new string('.', width - 1) : new string('.', width))));

    public static TheoryData<float, float> Directions => new()
    {
        { 1f, 0f }, { -1f, 0f }, { 0f, 1f }, { 0f, -1f }, { 0.6f, 0.8f }, { -0.8f, -0.6f },
    };

    [Theory]
    [MemberData(nameof(Directions))]
    public void Running_straight_at_the_column_stops_at_its_side_from_every_direction(float x, float z)
    {
        var towards = new Vector3(x, 0f, z);
        var start = new Vector3(Middle.X, 0f, Middle.Z) - (towards * 5f);

        var (position, velocity) = RunTicks(Hall, start, towards * 6f, 120);

        // Pressed against the round side, no speed left into it. Aimed at a slant, the rounding of the numbers puts the
        // runner a hair beside the middle, so it may slide a little along the side - as on any round thing.
        var outward = Vector3.Normalize(new Vector3(position.X - Middle.X, 0f, position.Z - Middle.Z));
        Assert.Equal(Column.Radius + Radius, FlatDistance(position), Tolerance);
        Assert.Equal(0f, Vector3.Dot(velocity, outward), Tolerance);
        Assert.False(CharacterCollision.Overlaps(Hall, position, Radius));
    }

    [Fact]
    public void On_a_raised_floor_the_column_blocks_just_the_same()
    {
        // Every cell one metre up, the ceiling with it: the column stands from that floor to that ceiling.
        var raised = Open(9, 9)
            .WithCells(Enumerable.Range(0, 9).SelectMany(z => Enumerable.Range(0, 9).Select(x => (x, z, new Cell(false, 1f, 4.5f)))))
            .WithColumns([Middle]);
        var start = new Vector3(Middle.X - 5f, 1f, Middle.Z);

        var (position, _) = RunTicks(raised, start, new Vector3(6f, 0f, 0f), 120);

        Assert.Equal(Middle.X - Column.Radius - Radius, position.X, Tolerance);
        Assert.Equal(1f, position.Y);
    }

    [Fact]
    public void Without_the_column_the_same_run_goes_through()
    {
        // The counter-check of the tests above: the same level without its column lets the runner pass the middle.
        var (position, _) = RunTicks(Open(9, 9), new Vector3(Middle.X - 5f, 0f, Middle.Z), new Vector3(6f, 0f, 0f), 120);

        Assert.True(position.X > Middle.X + 1f);
    }

    [Fact]
    public void Running_slightly_beside_its_middle_slides_round_the_column()
    {
        // Aimed 0.2 m beside the middle: the runner is pushed aside along the round side and gets past.
        var start = new Vector3(Middle.X - 5f, 0f, Middle.Z + 0.2f);
        var position = start;
        var velocity = new Vector3(5f, 0f, 0f);
        var nearest = float.MaxValue;

        for (var tick = 0; tick < 150; tick++)
        {
            (position, _) = CharacterCollision.Move(Hall, position, velocity, Radius, Tick);
            nearest = MathF.Min(nearest, FlatDistance(position));
            Assert.False(CharacterCollision.Overlaps(Hall, position, Radius), $"Inside the column at tick {tick}.");
        }

        Assert.True(position.X > Middle.X + 1f, $"Stuck at {position}.");
        Assert.Equal(Column.Radius + Radius, nearest, 0.01f);
    }

    [Fact]
    public void Standing_in_it_overlaps_standing_beside_it_does_not()
    {
        Assert.True(CharacterCollision.Overlaps(Hall, new Vector3(Middle.X + Column.Radius + Radius - 0.01f, 0f, Middle.Z), Radius));
        Assert.False(CharacterCollision.Overlaps(Hall, new Vector3(Middle.X + Column.Radius + Radius + 0.01f, 0f, Middle.Z), Radius));
    }

    [Fact]
    public void Pressed_into_the_column_or_a_wall_a_body_is_not_blocked_at_its_height_only_a_low_ceiling_blocks_it()
    {
        // Issue #635: the column and a solid wall stand in the way at every height alike, so they do not hold a flier's
        // height; a cell whose ceiling is too low for the body does. East of the column a cell with a 1.5 m ceiling,
        // north of it a solid one.
        var level = Hall.WithCells([(5, 4, new Cell(false, 0f, 1.5f)), (4, 5, new Cell(true, 0f, Cell.DefaultCeilingHeight))]);
        var inTheColumn = new Vector3(Middle.X - Column.Radius - Radius + 0.01f, 0f, Middle.Z);
        var inTheWall = new Vector3(Middle.X - 0.8f, 0f, 10f - Radius + 0.01f);
        var underTheLowCeiling = new Vector3(10f + 0.5f, 0f, Middle.Z);
        const float Step = BodySizes.Player.StepHeight;

        Assert.True(CharacterCollision.Overlaps(level, inTheColumn, Radius));
        Assert.False(CharacterCollision.BlockedAtHeight(level, inTheColumn, Radius, BodySizes.Player.Height, Step));
        Assert.True(CharacterCollision.Overlaps(level, inTheWall, Radius));
        Assert.False(CharacterCollision.BlockedAtHeight(level, inTheWall, Radius, BodySizes.Player.Height, Step));
        Assert.True(CharacterCollision.BlockedAtHeight(level, underTheLowCeiling, Radius, BodySizes.Player.Height, Step));
        Assert.False(CharacterCollision.BlockedAtHeight(level, underTheLowCeiling, Radius, 1f, Step));
    }

    [Fact]
    public void A_shot_stops_at_its_side_with_the_side_facing_back()
    {
        var hit = Raycast.AgainstLevel(Hall, new Vector3(Middle.X - 5f, 1.5f, Middle.Z), Vector3.UnitX, 20f);

        Assert.NotNull(hit);
        Assert.Equal(5f - Column.Radius, hit.Value.Distance, 0.01f);
        Assert.Equal(-Vector3.UnitX, hit.Value.Normal);
    }

    [Fact]
    public void A_shot_at_its_flank_gets_the_round_side_s_direction()
    {
        // Aimed half a radius beside the middle: the side there faces back and outwards, 30 degrees off the ray.
        var hit = Raycast.AgainstLevel(Hall, new Vector3(Middle.X - 5f, 1.5f, Middle.Z + (Column.Radius / 2f)), Vector3.UnitX, 20f);

        Assert.NotNull(hit);
        var expected = Vector3.Normalize(new Vector3(hit.Value.Point.X - Middle.X, 0f, hit.Value.Point.Z - Middle.Z));
        Assert.Equal(expected.X, hit.Value.Normal.X, 0.01f);
        Assert.Equal(expected.Z, hit.Value.Normal.Z, 0.01f);
        Assert.Equal(0f, hit.Value.Normal.Y);
        Assert.Equal(MathF.Cos(MathF.PI / 6f), -hit.Value.Normal.X, 0.02f);
    }

    [Fact]
    public void A_shot_past_it_flies_on()
    {
        var hit = Raycast.AgainstLevel(Hall, new Vector3(Middle.X - 5f, 1.5f, Middle.Z + Column.Radius + 0.05f), Vector3.UnitX, 8f);

        Assert.Null(hit);
    }

    [Fact]
    public void It_hides_what_is_behind_it_from_sight_and_not_what_is_beside_it()
    {
        var eye = new Vector3(Middle.X - 5f, 1.6f, Middle.Z);

        Assert.False(LineOfSight.IsClear(Hall, eye, new Vector3(Middle.X + 5f, 1f, Middle.Z)));
        Assert.True(LineOfSight.IsClear(Hall, eye, new Vector3(Middle.X + 5f, 1f, Middle.Z + 3f)));
        Assert.True(LineOfSight.IsClear(Open(9, 9), eye, new Vector3(Middle.X + 5f, 1f, Middle.Z)));
    }

    [Fact]
    public void It_reaches_from_floor_to_ceiling()
    {
        Assert.True(LineOfSight.IsInsideGeometry(Hall, new Vector3(Middle.X, 0.01f, Middle.Z)));
        Assert.True(LineOfSight.IsInsideGeometry(Hall, new Vector3(Middle.X + 0.4f, Cell.DefaultCeilingHeight - 0.01f, Middle.Z)));
        Assert.False(LineOfSight.IsInsideGeometry(Hall, new Vector3(Middle.X + Column.Radius + 0.01f, 1f, Middle.Z)));
    }

    private static float FlatDistance(Vector3 position) => Vector2.Distance(new Vector2(position.X, position.Z), Middle.Centre);

    private static (Vector3 Position, Vector3 Velocity) RunTicks(Level level, Vector3 start, Vector3 velocity, int ticks)
    {
        var position = start;
        var current = velocity;
        for (var tick = 0; tick < ticks; tick++)
        {
            (position, current) = CharacterCollision.Move(level, position, velocity, Radius, Tick);
        }

        return (position, current);
    }
}
