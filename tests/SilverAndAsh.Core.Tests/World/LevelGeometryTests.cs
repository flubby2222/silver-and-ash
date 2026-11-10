using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

public class LevelGeometryTests
{
    private const float Tolerance = 0.0001f;

    [Fact]
    public void A_single_cell_has_floor_ceiling_and_four_walls()
    {
        // Everything outside the level counts as wall.
        var faces = LevelGeometry.Build(LevelParser.Parse("P"));

        Assert.Single(faces, f => f.Kind == SurfaceKind.Floor);
        Assert.Single(faces, f => f.Kind == SurfaceKind.Ceiling);
        Assert.Equal(4, faces.Count(f => f.Kind == SurfaceKind.Wall));
    }

    [Fact]
    public void No_wall_between_two_walkable_cells()
    {
        var faces = LevelGeometry.Build(LevelParser.Parse("P."));

        // 2 cells x (floor + ceiling) + outer walls: 2 on each long side, 1 on each short side.
        Assert.Equal(2, faces.Count(f => f.Kind == SurfaceKind.Floor));
        Assert.Equal(6, faces.Count(f => f.Kind == SurfaceKind.Wall));
    }

    [Fact]
    public void Faces_between_two_solid_cells_are_left_out()
    {
        var level = LevelParser.Parse(
            """
            #####
            #####
            ##P##
            #####
            #####
            """);

        var faces = LevelGeometry.Build(level);

        // Only the four walls around the one walkable cell, nothing for the 24 wall cells among themselves.
        Assert.Equal(4, faces.Count(f => f.Kind == SurfaceKind.Wall));
        Assert.Equal(6, faces.Count);
    }

    [Fact]
    public void The_test_level_has_one_floor_and_one_ceiling_per_walkable_cell()
    {
        var level = LevelParser.ParseFile(RepositoryFiles.Find("assets/levels/test.txt"));
        var walkable = 0;
        for (var z = 0; z < level.Depth; z++)
        {
            for (var x = 0; x < level.Width; x++)
            {
                walkable += level[x, z].IsSolid ? 0 : 1;
            }
        }

        var faces = LevelGeometry.Build(level);

        Assert.Equal(walkable, faces.Count(f => f.Kind == SurfaceKind.Floor));
        Assert.Equal(walkable, faces.Count(f => f.Kind == SurfaceKind.Ceiling));
    }

    [Fact]
    public void Wall_normals_point_into_the_walkable_cell()
    {
        var level = LevelParser.Parse(".P.");

        foreach (var wall in LevelGeometry.Build(level).Where(f => f.Kind == SurfaceKind.Wall))
        {
            var faceCentre = (wall.A + wall.B + wall.C + wall.D) / 4f;
            var cellOfFace = Level.ToGrid(faceCentre.X + wall.Normal.X * 0.01f, faceCentre.Z + wall.Normal.Z * 0.01f);

            Assert.False(level[cellOfFace.X, cellOfFace.Z].IsSolid, "A wall face must look into a walkable cell.");
        }
    }

    [Fact]
    public void Corners_run_counter_clockwise_seen_from_the_normal_side()
    {
        // Required for backface culling: otherwise faces would be invisible from the inside.
        foreach (var face in LevelGeometry.Build(LevelParser.Parse("P.\n..")))
        {
            var computed = Vector3.Normalize(Vector3.Cross(face.B - face.A, face.C - face.A));
            Assert.Equal(1f, Vector3.Dot(computed, face.Normal), Tolerance);
        }
    }

    [Fact]
    public void Faces_use_the_cell_heights()
    {
        var cells = new Cell[1, 1];
        cells[0, 0] = new Cell(false, 0.5f, 2.5f);

        var faces = LevelGeometry.Build(new Level(cells, 0, 0));

        Assert.All(faces.Where(f => f.Kind == SurfaceKind.Floor), f => Assert.Equal(0.5f, f.A.Y));
        Assert.All(faces.Where(f => f.Kind == SurfaceKind.Ceiling), f => Assert.Equal(2.5f, f.A.Y));
        Assert.All(faces.Where(f => f.Kind == SurfaceKind.Wall), f =>
        {
            var heights = new[] { f.A.Y, f.B.Y, f.C.Y, f.D.Y };
            Assert.Equal(0.5f, heights.Min());
            Assert.Equal(2.5f, heights.Max());
        });
    }

    [Fact]
    public void Every_face_is_one_cell_wide_with_equal_opposite_edges()
    {
        foreach (var face in LevelGeometry.Build(LevelParser.Parse("P")))
        {
            Assert.Equal(Level.CellSize, Vector3.Distance(face.A, face.B), Tolerance);
            Assert.Equal(Vector3.Distance(face.A, face.B), Vector3.Distance(face.C, face.D), Tolerance);
            Assert.Equal(Vector3.Distance(face.B, face.C), Vector3.Distance(face.D, face.A), Tolerance);
        }
    }
}
