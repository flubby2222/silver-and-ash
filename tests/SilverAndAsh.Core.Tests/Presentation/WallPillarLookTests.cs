using System.Numerics;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

public class WallPillarLookTests
{
    private const float Tolerance = 0.0001f;

    private static readonly TextureVariant[] WallVariants = [TextureVariant.A, TextureVariant.B, TextureVariant.C];

    public static TheoryData<WallPillarSize, TextureVariant> EveryPillar()
    {
        var data = new TheoryData<WallPillarSize, TextureVariant>();
        foreach (var size in Enum.GetValues<WallPillarSize>())
        {
            foreach (var variant in WallVariants)
            {
                data.Add(size, variant);
            }
        }

        return data;
    }

    [Theory]
    [InlineData(TextureVariant.A, 3.5f, "wall_pillar_corridor_a.glb")]
    [InlineData(TextureVariant.B, 3.5f, "wall_pillar_corridor_b.glb")]
    [InlineData(TextureVariant.C, 6f, "wall_pillar_room_c.glb")]
    [InlineData(TextureVariant.A, 6f, "wall_pillar_room_a.glb")]
    public void Every_wall_variant_and_ceiling_height_has_its_own_file(TextureVariant variant, float ceiling, string file)
    {
        Assert.Equal(file, WallPillarLook.FileName(variant, ceiling));
    }

    [Fact]
    public void The_two_sizes_reach_the_two_ceilings_of_the_game()
    {
        Assert.Equal(WallPillarSize.Corridor, WallPillarLook.SizeFor(Cell.DefaultCeilingHeight));
        Assert.Equal(WallPillarSize.Room, WallPillarLook.SizeFor(RoomTemplate.RoomCeilingHeight));
        Assert.Equal(Cell.DefaultCeilingHeight, WallPillarLook.Height(WallPillarSize.Corridor));
        Assert.Equal(RoomTemplate.RoomCeilingHeight, WallPillarLook.Height(WallPillarSize.Room));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(4f)]
    [InlineData(5.99f)]
    public void Under_any_other_ceiling_no_pillar_stands(float ceiling)
    {
        Assert.Null(WallPillarLook.SizeFor(ceiling));
        Assert.Null(WallPillarLook.FileName(TextureVariant.A, ceiling));
    }

    [Fact]
    public void Only_the_three_wall_variants_and_the_two_sizes_have_files()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WallPillarLook.FileName(WallPillarSize.Room, (TextureVariant)7));
        Assert.Throws<ArgumentOutOfRangeException>(() => WallPillarLook.FileName((WallPillarSize)7, TextureVariant.A));
        Assert.Throws<ArgumentOutOfRangeException>(() => WallPillarLook.Height((WallPillarSize)7));
    }

    [Fact]
    public void The_one_part_is_the_pillar()
    {
        Assert.Equal([new RigPart("pillar", null)], WallPillarLook.Parts);
    }

    [Theory]
    [MemberData(nameof(EveryPillar))]
    public void Every_pillar_file_stands_on_the_floor_against_the_wall_up_to_its_ceiling(WallPillarSize size, TextureVariant variant)
    {
        var path = RepositoryFiles.Find($"assets/models/{WallPillarLook.FileName(size, variant)}");
        Assert.True(File.Exists(path), $"{path} is missing.");
        var part = Assert.Single(GlbScene.Read(File.ReadAllBytes(path)));

        Assert.Equal(WallPillarLook.Part, part.Name);
        Assert.Equal(Vector3.Zero, part.Pivot);

        // From the floor to the ceiling, centred on its origin, standing out toward +z from the wall's face at z = 0.
        // The plinth reaches 1/8 m into the wall, so no gap shows where the wall meets it.
        Assert.Equal(0f, part.PositionMin.Y, Tolerance);
        Assert.Equal(WallPillarLook.Height(size), part.PositionMax.Y, Tolerance);
        Assert.Equal(-WallPillarLook.Width / 2f, part.PositionMin.X, Tolerance);
        Assert.Equal(WallPillarLook.Width / 2f, part.PositionMax.X, Tolerance);
        Assert.Equal(-0.125f, part.PositionMin.Z, Tolerance);
        Assert.Equal(size == WallPillarSize.Room ? WallPillarLook.Depth : 0.125f, part.PositionMax.Z, Tolerance);
    }

    [Theory]
    [InlineData(RoomSide.North, 0f, 1f)]
    [InlineData(RoomSide.South, 0f, -1f)]
    [InlineData(RoomSide.West, 1f, 0f)]
    [InlineData(RoomSide.East, -1f, 0f)]
    public void A_pillar_stands_on_the_floor_in_the_middle_of_the_wall_and_looks_into_the_cell(RoomSide wall, float facingX, float facingZ)
    {
        // The middle cell stands one step high (0.5 m): the pillar stands on its floor.
        var level = LevelParser.Parse("###\n#P#\n#2#\n#.#\n###");
        var (position, facing) = WallPillarLook.Place(level, 1, 2, wall);

        Assert.Equal(new Vector3(facingX, 0f, facingZ), facing);
        var center = level.CellCenter(1, 2);
        Assert.Equal(0.5f, position.Y, Tolerance);
        Assert.Equal(center.X - (facingX * Level.CellSize / 2f), position.X, Tolerance);
        Assert.Equal(center.Z - (facingZ * Level.CellSize / 2f), position.Z, Tolerance);

        // On the cell's edge toward the wall: a step back is in that wall's cell, a step forward in the cell itself.
        var behind = position - (facing * 0.01f);
        var before = position + (facing * 0.01f);
        Assert.Equal(Neighbour(1, 2, wall), Level.ToGrid(behind.X, behind.Z));
        Assert.Equal((1, 2), Level.ToGrid(before.X, before.Z));
    }

    [Theory]
    [InlineData(RoomSide.North)]
    [InlineData(RoomSide.South)]
    [InlineData(RoomSide.West)]
    [InlineData(RoomSide.East)]
    public void The_texture_of_a_placed_pillar_runs_in_the_grid_of_the_wall_behind_it(RoomSide wall)
    {
        // The pillar's texture is cut at whole metres from its middle (tools/models/wall_pillar.py). It lines up with the
        // wall's tiles only where the wall's own coordinate along it is a whole number at the pillar's middle.
        var level = LevelParser.Parse("#####\n#...#\n#.P.#\n#...#\n#####");
        foreach (var (x, z) in new[] { (1, 1), (2, 2), (3, 3) })
        {
            var (position, facing) = WallPillarLook.Place(level, x, z, wall);
            var u = SurfaceTexturing.TexCoord(position, facing, SurfaceKind.Wall, 1f).X;
            Assert.Equal(MathF.Round(u), u, Tolerance);
        }
    }

    [Fact]
    public void An_unknown_wall_side_is_refused()
    {
        var level = LevelParser.Parse("P");
        Assert.Throws<ArgumentOutOfRangeException>(() => WallPillarLook.Place(level, 0, 0, (RoomSide)7));
    }

    [Theory]
    [InlineData(TextureVariant.B, true, true, TextureVariant.B)]
    [InlineData(TextureVariant.B, true, false, TextureVariant.B)]
    [InlineData(TextureVariant.C, false, true, TextureVariant.A)]
    [InlineData(TextureVariant.A, true, true, TextureVariant.A)]
    [InlineData(TextureVariant.C, false, false, null)]
    [InlineData(TextureVariant.A, false, false, null)]
    public void A_missing_pillar_falls_back_to_variant_a_then_to_none(
        TextureVariant wanted, bool wantedLoaded, bool variantALoaded, TextureVariant? expected)
    {
        Assert.Equal(expected, WallPillarLook.Pick(wanted, wantedLoaded, variantALoaded));
    }

    [Fact]
    public void The_pillars_were_built_on_the_wall_textures_in_the_game()
    {
        // tools/models/wall_pillar.py builds the pillars on the wall textures and notes which ones it read. Once a wall
        // texture changes, the pillar no longer matches the wall behind it: run the script again.
        var built = Lines(RepositoryFiles.Find("tools/models/wall_pillar_sources.sha256"));
        var current = Lines(RepositoryFiles.Find("assets/textures/textures.sha256"));

        Assert.Equal(3, built.Count);
        foreach (var line in built)
        {
            Assert.True(
                current.Contains(line),
                $"The wall pillars were built on '{line}', which is no longer in assets/textures. " +
                "Run tools/models/wall_pillar.py again (see assets/HERKUNFT.md).");
        }
    }

    private static (int X, int Z) Neighbour(int x, int z, RoomSide side) => side switch
    {
        RoomSide.North => (x, z - 1),
        RoomSide.South => (x, z + 1),
        RoomSide.West => (x - 1, z),
        _ => (x + 1, z),
    };

    private static List<string> Lines(string path) => File.ReadAllLines(path)
        .Select(line => line.Trim())
        .Where(line => line.Length > 0 && !line.StartsWith('#'))
        .ToList();
}
