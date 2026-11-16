using System.Numerics;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

public class SurfaceTexturingTests
{
    private const float Tolerance = 0.0001f;

    // Two rooms joined by cells of different floor heights (steps 1-3) - walls of many lengths and heights.
    private static readonly Level Stepped = LevelParser.Parse(
        """
        #######
        #P.12.#
        #..3#.#
        #######
        """);

    private static readonly Vector3[] WallNormals = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitZ];

    [Fact]
    public void Floors_and_ceilings_follow_x_and_z_in_tiles()
    {
        var position = new Vector3(3f, 1.7f, -5f);

        Assert.Equal(new Vector2(1.5f, -2.5f), SurfaceTexturing.TexCoord(position, Vector3.UnitY, SurfaceKind.Floor, 2f));
        Assert.Equal(new Vector2(1.5f, -2.5f), SurfaceTexturing.TexCoord(position, -Vector3.UnitY, SurfaceKind.Ceiling, 2f));
    }

    [Fact]
    public void Floors_and_ceilings_do_not_depend_on_the_height()
    {
        var low = SurfaceTexturing.TexCoord(new Vector3(1.2f, 0f, 3.4f), Vector3.UnitY, SurfaceKind.Floor, 1f);
        var high = SurfaceTexturing.TexCoord(new Vector3(1.2f, 0.75f, 3.4f), Vector3.UnitY, SurfaceKind.Floor, 1f);

        Assert.Equal(low, high);
    }

    [Theory]
    [MemberData(nameof(Normals))]
    public void A_wall_counts_v_from_the_height_above_zero_whatever_its_lower_edge(Vector3 normal)
    {
        // A course of stones lies at the same height on a wall from the floor and on a step edge starting at 0.5 m.
        var v = SurfaceTexturing.TexCoord(new Vector3(2f, 1.25f, 7f), normal, SurfaceKind.Wall, 1f).Y;

        Assert.Equal(-1.25f, v, Tolerance);
    }

    [Theory]
    [MemberData(nameof(Normals))]
    public void A_wall_is_not_turned_u_runs_along_it_and_not_up(Vector3 normal)
    {
        var bottom = SurfaceTexturing.TexCoord(new Vector3(2f, 0f, 7f), normal, SurfaceKind.Wall, 1f);
        var top = SurfaceTexturing.TexCoord(new Vector3(2f, 3f, 7f), normal, SurfaceKind.Wall, 1f);
        var along = SurfaceTexturing.TexCoord(new Vector3(2f, 0f, 7f) + Vector3.Cross(normal, Vector3.UnitY), normal, SurfaceKind.Wall, 1f);

        Assert.Equal(bottom.X, top.X, Tolerance);
        Assert.Equal(1f, MathF.Abs(along.X - bottom.X), Tolerance);
        Assert.Equal(bottom.Y, along.Y, Tolerance);
    }

    [Fact]
    public void The_picture_of_a_wall_stands_upright_and_is_not_mirrored()
    {
        // Corners A, B, C, D run counter-clockwise as seen from the room: A bottom left, B bottom right, D top left.
        // Upright (raylib: first PNG row at v = 0, the top): v falls going up. Not mirrored: u grows to the right.
        foreach (var face in LevelGeometry.Build(Stepped).Where(face => face.Kind == SurfaceKind.Wall))
        {
            var a = UV(face, face.A);
            Assert.True(UV(face, face.B).X > a.X, $"Wall facing {face.Normal} is mirrored.");
            Assert.True(UV(face, face.D).Y < a.Y, $"Wall facing {face.Normal} stands on its head.");
        }
    }

    [Fact]
    public void One_tile_has_the_given_size_in_metres()
    {
        var tile = LevelTextureSet.Catacombs.TileSize;
        var start = SurfaceTexturing.TexCoord(new Vector3(0.3f, 0.4f, 0f), Vector3.UnitZ, SurfaceKind.Wall, tile);
        var next = SurfaceTexturing.TexCoord(new Vector3(0.3f + tile, 0.4f + tile, 0f), Vector3.UnitZ, SurfaceKind.Wall, tile);

        Assert.Equal(1f, MathF.Abs(next.X - start.X), Tolerance);
        Assert.Equal(1f, MathF.Abs(next.Y - start.Y), Tolerance);
    }

    [Fact]
    public void Faces_in_one_plane_share_the_coordinates_of_their_shared_corners()
    {
        // No seam at cell borders, steps or different heights: wherever two faces of the same kind and direction touch,
        // the texture continues exactly.
        var corners = new Dictionary<(SurfaceKind, Vector3, Vector3), Vector2>();
        var shared = 0;
        foreach (var face in LevelGeometry.Build(Stepped))
        {
            foreach (var corner in new[] { face.A, face.B, face.C, face.D })
            {
                var uv = UV(face, corner);
                if (corners.TryGetValue((face.Kind, face.Normal, corner), out var earlier))
                {
                    shared++;
                    Assert.Equal(earlier, uv);
                }
                else
                {
                    corners[(face.Kind, face.Normal, corner)] = uv;
                }
            }
        }

        Assert.True(shared > 20, $"Only {shared} shared corners - the test level is too small to show anything.");
    }

    [Fact]
    public void A_wall_continues_across_a_cell_border_by_whole_cells()
    {
        // The first corner of the next cell's wall continues where the last one ended: u moves by the cell size.
        var walls = LevelGeometry.Build(Stepped)
            .Where(face => face.Kind == SurfaceKind.Wall && face.Normal == -Vector3.UnitZ && face.A.Y == 0f && face.D.Y > 2f)
            .Select(face => (Start: UV(face, face.A).X, End: UV(face, face.B).X))
            .ToList();

        Assert.True(walls.Count >= 2);
        Assert.All(walls, wall => Assert.Equal(Level.CellSize, wall.End - wall.Start, Tolerance));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void A_tile_must_have_a_size(float tileSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SurfaceTexturing.TexCoord(Vector3.One, Vector3.UnitY, SurfaceKind.Floor, tileSize));
    }

    [Theory]
    [InlineData(LevelLayer.Catacombs)]
    [InlineData(LevelLayer.Dungeon)]
    [InlineData(LevelLayer.CastleHall)]
    public void Every_layer_has_a_set_for_now_the_catacombs(LevelLayer layer)
    {
        Assert.Same(LevelTextureSet.Catacombs, LevelTextureSet.For(layer));
    }

    [Fact]
    public void Each_kind_of_surface_has_its_own_file_of_the_set()
    {
        var set = LevelTextureSet.Catacombs;

        Assert.Equal("catacomb_wall.png", set.FileFor(SurfaceKind.Wall));
        Assert.Equal("catacomb_floor.png", set.FileFor(SurfaceKind.Floor));
        Assert.Equal("catacomb_ceiling.png", set.FileFor(SurfaceKind.Ceiling));
    }

    [Fact]
    public void The_checked_in_texture_list_matches_every_checked_in_texture()
    {
        // Renewing a texture without renewing the list (tools/models/checksums.py) would leave the surface plain.
        var folder = RepositoryFiles.Find("assets/textures");
        var list = ModelChecksums.Parse(File.ReadAllText(Path.Combine(folder, LevelTextureSet.ChecksumFileName)));
        var files = Directory.GetFiles(folder, "*.png").ToList();

        Assert.Equal(files.Select(Path.GetFileName).Order(), list.Names.Order());
        Assert.All(files, path => Assert.Null(list.Check(path)));
        foreach (var layer in Enum.GetValues<LevelLayer>())
        {
            var set = LevelTextureSet.For(layer);
            Assert.All(new[] { set.Wall, set.Floor, set.Ceiling }, name => Assert.Contains(name, list.Names));
        }
    }

    public static TheoryData<Vector3> Normals() => [.. WallNormals];

    private static Vector2 UV(Face face, Vector3 corner) => SurfaceTexturing.TexCoord(corner, face.Normal, face.Kind, 1f);
}
