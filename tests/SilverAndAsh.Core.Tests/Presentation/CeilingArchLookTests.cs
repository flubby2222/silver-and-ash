using System.Numerics;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

public class CeilingArchLookTests
{
    private const float Tolerance = 0.0001f;

    // A corridor one cell wide along z (west and east walls) and one along x (north and south walls), both under the
    // corridor ceiling; the middle cell of the first stands one step high (0.5 m).
    private const string AlongZ = "###\n#P#\n#2#\n#.#\n###";
    private const string AlongX = "#####\n#P..#\n#####";

    [Theory]
    [InlineData(TextureVariant.A, "ceiling_arch_a.glb")]
    [InlineData(TextureVariant.B, "ceiling_arch_b.glb")]
    [InlineData(TextureVariant.C, "ceiling_arch_c.glb")]
    public void Every_wall_variant_has_its_own_file(TextureVariant variant, string file)
    {
        Assert.Equal(file, CeilingArchLook.FileName(variant));
    }

    [Fact]
    public void Only_the_three_wall_variants_have_files()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CeilingArchLook.FileName((TextureVariant)7));
    }

    [Fact]
    public void The_parts_are_the_arch_and_its_two_pillars()
    {
        Assert.Equal([new RigPart("arch", null), new RigPart("pillar_left", null), new RigPart("pillar_right", null)], CeilingArchLook.Parts);
    }

    [Theory]
    [InlineData(TextureVariant.A)]
    [InlineData(TextureVariant.B)]
    [InlineData(TextureVariant.C)]
    public void Every_arch_file_spans_a_corridor_one_cell_wide_from_wall_to_wall_into_its_ceiling(TextureVariant variant)
    {
        var path = RepositoryFiles.Find($"assets/models/{CeilingArchLook.FileName(variant)}");
        Assert.True(File.Exists(path), $"{path} is missing.");
        var parts = GlbScene.Read(File.ReadAllBytes(path)).ToDictionary(part => part.Name);
        Assert.Equal(CeilingArchLook.Parts.Select(part => part.Name).Order(), parts.Keys.Order());
        Assert.All(parts.Values, part => Assert.Equal(Vector3.Zero, part.Pivot));

        // The walls stand at x = -1 and +1; the teeth of the arch's feet and the pillars' backs reach 1/8 m into them,
        // so no gap shows where a wall meets them.
        var half = CeilingArchLook.Width / 2f;
        var arch = parts[CeilingArchLook.Arch];
        Assert.Equal(-half - 0.125f, arch.PositionMin.X, Tolerance);
        Assert.Equal(half + 0.125f, arch.PositionMax.X, Tolerance);

        // The arch rises from the springing line on the capitals (2 m) and its keystone's tooth reaches into the
        // ceiling - but less than the 1/4 m a ceiling's tooth may hide, so it never shows above it.
        Assert.Equal(2f, arch.PositionMin.Y, Tolerance);
        Assert.InRange(arch.PositionMax.Y, CeilingArchLook.CeilingHeight + 0.01f, CeilingArchLook.CeilingHeight + 0.25f);
        Assert.Equal(-0.25f, arch.PositionMin.Z, Tolerance);
        Assert.Equal(0.25f, arch.PositionMax.Z, Tolerance);

        // Each pillar stands from the floor up to the springing line against its wall, mirrored.
        foreach (var (name, side) in new[] { (CeilingArchLook.PillarLeft, -1f), (CeilingArchLook.PillarRight, 1f) })
        {
            var pillar = parts[name];
            Assert.Equal(0f, pillar.PositionMin.Y, Tolerance);
            Assert.Equal(2f, pillar.PositionMax.Y, Tolerance);
            var (wall, front) = side < 0f ? (pillar.PositionMin.X, pillar.PositionMax.X) : (-pillar.PositionMax.X, -pillar.PositionMin.X);
            Assert.Equal(-half - 0.125f, wall, Tolerance);
            Assert.InRange(front, -half + 0.05f, -half + 0.2f);
            Assert.Equal(-pillar.PositionMax.Z, pillar.PositionMin.Z, Tolerance);
        }
    }

    [Theory]
    [InlineData(AlongZ, 1, 2, 0f, 1f, 0.5f)]
    [InlineData(AlongX, 2, 1, 1f, 0f, 0f)]
    public void An_arch_stands_on_the_floor_in_the_middle_of_the_cell_looking_along_the_corridor(
        string map, int x, int z, float facingX, float facingZ, float floor)
    {
        var level = LevelParser.Parse(map);
        var (position, facing) = CeilingArchLook.Place(level, x, z);

        Assert.Equal(new Vector3(facingX, 0f, facingZ), facing);
        var center = level.CellCenter(x, z);
        Assert.Equal(floor, position.Y, Tolerance);
        Assert.Equal(center.X, position.X, Tolerance);
        Assert.Equal(center.Z, position.Z, Tolerance);

        // Its walls are the cell's own edges across the corridor: a corridor's width beside its middle.
        var across = new Vector3(facing.Z, 0f, facing.X) * (CeilingArchLook.Width / 2f);
        foreach (var side in new[] { -1f, 1f })
        {
            var wall = position + (across * side) + (Vector3.Normalize(across) * side * 0.01f);
            Assert.True(level[Level.ToGrid(wall.X, wall.Z).X, Level.ToGrid(wall.X, wall.Z).Z].IsSolid);
        }
    }

    [Theory]
    [InlineData(AlongZ)]
    [InlineData(AlongX)]
    public void The_texture_of_a_placed_arch_s_pillars_runs_in_the_grid_of_the_walls(string map)
    {
        // The pillars' texture is cut at whole metres from the arch's plane (tools/models/ceiling_arch.py). It lines up
        // with the wall's tiles only where the wall's own coordinate along the corridor is whole in that plane.
        var level = LevelParser.Parse(map);
        var (x, z) = map == AlongZ ? (1, 2) : (2, 1);
        var (position, facing) = CeilingArchLook.Place(level, x, z);
        var across = new Vector3(facing.Z, 0f, facing.X);
        foreach (var side in new[] { -1f, 1f })
        {
            var onWall = position + (across * side * (CeilingArchLook.Width / 2f));
            var u = SurfaceTexturing.TexCoord(onWall, -across * side, SurfaceKind.Wall, 1f).X;
            Assert.Equal(MathF.Round(u), u, Tolerance);
        }
    }

    [Theory]
    [InlineData("#####\n#...#\n#.P.#\n#...#\n#####", 2, 2)]
    [InlineData("###\n#P#\n#.#\n###", 1, 1)]
    [InlineData("###\n#P#\n###", 1, 1)]
    [InlineData("###\n#P#\n###", 0, 0)]
    public void Outside_a_corridor_one_cell_wide_no_arch_is_placed(string map, int x, int z)
    {
        // An open room, a dead end, a closet, a wall: no corridor with walls on two opposite sides.
        var level = LevelParser.Parse(map);
        Assert.Throws<ArgumentException>(() => CeilingArchLook.Place(level, x, z));
    }

    [Fact]
    public void Under_a_room_ceiling_no_arch_is_placed()
    {
        var corridor = LevelParser.Parse(AlongZ);
        var cells = new Cell[corridor.Width, corridor.Depth];
        for (var x = 0; x < corridor.Width; x++)
        {
            for (var z = 0; z < corridor.Depth; z++)
            {
                cells[x, z] = corridor[x, z].IsSolid ? corridor[x, z] : corridor[x, z] with { CeilingHeight = RoomTemplate.RoomCeilingHeight };
            }
        }

        var room = new Level(cells, 1, 1, null, [], [], []);
        Assert.Throws<ArgumentException>(() => CeilingArchLook.Place(room, 1, 2));
    }

    [Theory]
    [InlineData(TextureVariant.B, true, true, TextureVariant.B)]
    [InlineData(TextureVariant.C, false, true, TextureVariant.A)]
    [InlineData(TextureVariant.C, false, false, null)]
    public void A_missing_arch_falls_back_to_variant_a_then_to_none(
        TextureVariant wanted, bool wantedLoaded, bool variantALoaded, TextureVariant? expected)
    {
        Assert.Equal(expected, CeilingArchLook.Pick(wanted, wantedLoaded, variantALoaded));
    }

    [Fact]
    public void The_arches_were_built_on_the_wall_textures_in_the_game()
    {
        // tools/models/ceiling_arch.py builds the arches on the wall textures and notes which ones it read. Once a wall
        // texture changes, the arch no longer matches the walls it stands on: run the script again.
        var built = Lines(RepositoryFiles.Find("tools/models/ceiling_arch_sources.sha256"));
        var current = Lines(RepositoryFiles.Find("assets/textures/textures.sha256"));

        Assert.Equal(3, built.Count);
        foreach (var line in built)
        {
            Assert.True(
                current.Contains(line),
                $"The ceiling arches were built on '{line}', which is no longer in assets/textures. " +
                "Run tools/models/ceiling_arch.py again (see assets/HERKUNFT.md).");
        }
    }

    private static List<string> Lines(string path) => File.ReadAllLines(path)
        .Select(line => line.Trim())
        .Where(line => line.Length > 0 && !line.StartsWith('#'))
        .ToList();
}
