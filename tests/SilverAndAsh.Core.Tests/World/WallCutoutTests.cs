using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

/// <summary>Holes in wall faces for models set into the wall (issue #553): only the picture changes.</summary>
public class WallCutoutTests
{
    private const float Tolerance = 0.0001f;

    // A corridor cell in the middle of solid rock: walls on all four sides, none of them the level's edge.
    private static readonly Level Enclosed = LevelParser.Parse(
        """
        ###
        #P#
        ###
        """);

    public static TheoryData<RoomSide> Sides => [RoomSide.North, RoomSide.East, RoomSide.South, RoomSide.West];

    [Theory]
    [MemberData(nameof(Sides))]
    public void The_pieces_around_a_hole_cover_the_wall_except_the_hole(RoomSide side)
    {
        var cutout = new WallCutout(1, 1, side, 0.5f, 1.25f, 0.25f, 3f);
        var before = WallOn(Enclosed, cutout);
        var pieces = WallsOn(Enclosed.WithWallCutouts([cutout]), cutout);

        // Left, right, under and over the hole.
        Assert.Equal(4, pieces.Count);
        var hole = (cutout.To - cutout.From) * (cutout.Top - cutout.Bottom);
        Assert.Equal(Area(before) - hole, pieces.Sum(Area), Tolerance);

        // Every piece lies in the old face's plane, faces the same way and keeps out of the hole, so the pieces neither
        // overlap the hole nor (summing up to the rest) each other.
        foreach (var piece in pieces)
        {
            Assert.Equal(before.Normal, piece.Normal);
            Assert.All(Corners(piece), corner => Assert.Equal(0f, Vector3.Dot(corner - before.A, before.Normal), Tolerance));
            var (from, to, bottom, top) = Span(piece, cutout);
            var apart = to <= cutout.From + Tolerance || from >= cutout.To - Tolerance
                || top <= cutout.Bottom + Tolerance || bottom >= cutout.Top - Tolerance;
            Assert.True(apart, $"A piece of the {side} wall reaches into the hole.");
        }
    }

    [Theory]
    [MemberData(nameof(Sides))]
    public void The_pieces_stay_visible_from_the_cell_and_belong_to_it(RoomSide side)
    {
        var cutout = new WallCutout(1, 1, side, 0.5f, 1.25f, 0.25f, 3f);

        foreach (var piece in WallsOn(Enclosed.WithWallCutouts([cutout]), cutout))
        {
            // Counter-clockwise from the room, else backface culling would hide them.
            var computed = Vector3.Normalize(Vector3.Cross(piece.B - piece.A, piece.C - piece.A));
            Assert.Equal(1f, Vector3.Dot(computed, piece.Normal), Tolerance);
            Assert.Equal((1, 1), LevelGeometry.CellOf(piece));
        }
    }

    [Fact]
    public void A_niche_wide_hole_leaves_only_the_wall_above_it()
    {
        // The deep niche: the whole width of the cell, from the floor up to 3 m under a 3.5 m ceiling.
        var clear = Enclosed[1, 1].CeilingHeight - Enclosed[1, 1].FloorHeight;
        var cutout = new WallCutout(1, 1, RoomSide.South, 0f, Level.CellSize, 0f, 3f);

        var piece = Assert.Single(WallsOn(Enclosed.WithWallCutouts([cutout]), cutout));

        Assert.Equal((0f, Level.CellSize, 3f, clear), Span(piece, cutout));
    }

    [Fact]
    public void Every_other_face_stays_as_it_was()
    {
        var level = LevelParser.Parse(
            """
            #####
            #P..#
            #...#
            #####
            """);
        var cutout = new WallCutout(2, 1, RoomSide.North, 0.5f, 1.5f, 0f, 3f);

        var without = LevelGeometry.Build(level);
        var with = LevelGeometry.Build(level.WithWallCutouts([cutout]));

        var old = WallOn(level, cutout);
        Assert.Equal(without.Where(face => face != old), with.Where(face => !IsOn(face, cutout)));
    }

    [Fact]
    public void A_hole_at_the_edge_of_the_level_works_like_any_other()
    {
        // No cell behind the west wall at all: outside the level counts as wall.
        var level = LevelParser.Parse("P.");
        var cutout = new WallCutout(0, 0, RoomSide.West, 0.5f, 1.5f, 0.5f, 2f);

        var pieces = WallsOn(level.WithWallCutouts([cutout]), cutout);

        Assert.Equal(4, pieces.Count);
        Assert.Equal(Area(WallOn(level, cutout)) - (1f * 1.5f), pieces.Sum(Area), Tolerance);
        Assert.All(pieces, piece => Assert.All(Corners(piece), corner => Assert.Equal(0f, corner.X)));
    }

    [Fact]
    public void A_level_without_holes_has_none()
    {
        Assert.Empty(Enclosed.WallCutouts);
    }

    [Fact]
    public void Holes_stay_when_cells_or_enemies_change()
    {
        var level = LevelParser.Parse("P..").WithWallCutouts([new WallCutout(1, 0, RoomSide.North, 0f, 2f, 0f, 3f)]);

        Assert.Equal(level.WallCutouts, level.WithCells([(2, 0, new Cell(false, 0f, 3.5f))]).WallCutouts);
        Assert.Equal(level.WallCutouts, level.WithEnemies([(2, 0)], []).WallCutouts);
    }

    [Fact]
    public void A_hole_whose_wall_opened_up_is_not_drawn()
    {
        // The wall behind the hole becomes open floor (as a door would): an open border has no wall face, hole or not.
        var level = LevelParser.Parse("P\n#").WithWallCutouts([new WallCutout(0, 0, RoomSide.South, 0f, 2f, 0f, 3f)]);

        var opened = level.WithCells([(0, 1, new Cell(false, 0f, Cell.DefaultCeilingHeight))]);

        Assert.Equal(LevelGeometry.Build(LevelParser.Parse("P\n.")), LevelGeometry.Build(opened));
    }

    [Fact]
    public void A_hole_needs_an_open_cell_with_a_wall_on_its_side()
    {
        var level = LevelParser.Parse("#P.");

        // In front of the wall to the west: fine.
        _ = level.WithWallCutouts([new WallCutout(1, 0, RoomSide.West, 0f, 2f, 0f, 3f)]);

        // Toward open floor, out of a solid cell, outside the level.
        Assert.Throws<ArgumentException>(() => level.WithWallCutouts([new WallCutout(1, 0, RoomSide.East, 0f, 2f, 0f, 3f)]));
        Assert.Throws<ArgumentException>(() => level.WithWallCutouts([new WallCutout(0, 0, RoomSide.North, 0f, 2f, 0f, 3f)]));
        Assert.Throws<ArgumentException>(() => level.WithWallCutouts([new WallCutout(3, 0, RoomSide.West, 0f, 2f, 0f, 3f)]));
    }

    [Fact]
    public void A_hole_must_lie_within_its_wall_face()
    {
        var clear = Enclosed[1, 1].CeilingHeight - Enclosed[1, 1].FloorHeight;
        static WallCutout At(float from, float to, float bottom, float top) => new(1, 1, RoomSide.North, from, to, bottom, top);

        _ = Enclosed.WithWallCutouts([At(0f, Level.CellSize, 0f, clear)]);
        Assert.Throws<ArgumentException>(() => Enclosed.WithWallCutouts([At(-0.1f, 1f, 0f, 1f)]));
        Assert.Throws<ArgumentException>(() => Enclosed.WithWallCutouts([At(1f, Level.CellSize + 0.1f, 0f, 1f)]));
        Assert.Throws<ArgumentException>(() => Enclosed.WithWallCutouts([At(1f, 1f, 0f, 1f)]));
        Assert.Throws<ArgumentException>(() => Enclosed.WithWallCutouts([At(0f, 1f, -0.1f, 1f)]));
        Assert.Throws<ArgumentException>(() => Enclosed.WithWallCutouts([At(0f, 1f, 1f, 1f)]));
        Assert.Throws<ArgumentException>(() => Enclosed.WithWallCutouts([At(0f, 1f, 0f, clear + 0.1f)]));
    }

    [Fact]
    public void A_wall_side_has_at_most_one_hole()
    {
        Assert.Throws<ArgumentException>(() => Enclosed.WithWallCutouts(
        [
            new WallCutout(1, 1, RoomSide.North, 0f, 0.5f, 0f, 1f),
            new WallCutout(1, 1, RoomSide.North, 1f, 1.5f, 0f, 1f),
        ]));
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(1f, 0f)]
    [InlineData(0f, -1f)]
    [InlineData(-1f, 0f)]
    [InlineData(0.7f, 0.7f)]
    public void Walking_into_a_wall_with_a_hole_stops_where_it_did_without(float sideways, float forward)
    {
        // Holes on all four sides as big as they get: still nobody walks through, tick for tick the same as without.
        var clear = Enclosed[1, 1].CeilingHeight - Enclosed[1, 1].FloorHeight;
        var holes = Enum.GetValues<RoomSide>().Select(side => new WallCutout(1, 1, side, 0f, Level.CellSize, 0f, clear));
        var plain = new GameWorld(Enclosed, balance: Balance.Default);
        var holed = new GameWorld(Enclosed.WithWallCutouts(holes), balance: Balance.Default);
        var input = new PlayerInput(new Vector2(sideways, forward), Vector2.Zero, Jump: false);

        for (var tick = 0; tick < 120; tick++)
        {
            plain.Tick(input);
            holed.Tick(input);
            Assert.Equal(plain.Player.Position, holed.Player.Position);
        }

        Assert.Equal((1, 1), Level.ToGrid(holed.Player.Position.X, holed.Player.Position.Z));
    }

    private static Face WallOn(Level level, WallCutout cutout) => Assert.Single(WallsOn(level, cutout));

    private static List<Face> WallsOn(Level level, WallCutout cutout) =>
        LevelGeometry.Build(level).Where(face => IsOn(face, cutout)).ToList();

    // A wall face on the cutout's side of its cell: it looks away from the cell behind and belongs to the cell.
    private static bool IsOn(Face face, WallCutout cutout) =>
        face.Kind == SurfaceKind.Wall && LevelGeometry.CellOf(face) == (cutout.X, cutout.Z)
        && face.Normal == -new Vector3(cutout.Behind.Dx, 0f, cutout.Behind.Dz);

    private static Vector3[] Corners(Face face) => [face.A, face.B, face.C, face.D];

    private static float Area(Face face) => Vector3.Distance(face.A, face.B) * Vector3.Distance(face.B, face.C);

    // Where a piece lies on its wall: along it from the cell's corner (like the cutout), and above the cell's floor.
    private static (float From, float To, float Bottom, float Top) Span(Face face, WallCutout cutout)
    {
        var corner = new Vector3(cutout.X * Level.CellSize, 0f, cutout.Z * Level.CellSize);
        var alongX = cutout.Behind.Dx == 0;
        var along = Corners(face).Select(c => alongX ? c.X - corner.X : c.Z - corner.Z).ToArray();
        var heights = Corners(face).Select(c => c.Y).ToArray();
        return (along.Min(), along.Max(), heights.Min(), heights.Max());
    }
}
