using System.Numerics;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

public class WallNicheLookTests
{
    private const float Tolerance = 0.0001f;

    // A room of three by three cells under the corridor ceiling: every middle cell of a side has plain wall behind it
    // and open floor with plain wall on both sides along it.
    private const string Room = "#####\n#...#\n#.P.#\n#...#\n#####";

    [Theory]
    [InlineData(WallNicheSize.Corridor, TextureVariant.A, "wall_niche_corridor_a.glb")]
    [InlineData(WallNicheSize.Corridor, TextureVariant.B, "wall_niche_corridor_b.glb")]
    [InlineData(WallNicheSize.Corridor, TextureVariant.C, "wall_niche_corridor_c.glb")]
    [InlineData(WallNicheSize.Room, TextureVariant.A, "wall_niche_room_a.glb")]
    [InlineData(WallNicheSize.Room, TextureVariant.B, "wall_niche_room_b.glb")]
    [InlineData(WallNicheSize.Room, TextureVariant.C, "wall_niche_room_c.glb")]
    public void Every_size_and_wall_variant_has_its_own_file(WallNicheSize size, TextureVariant variant, string file)
    {
        Assert.Equal(file, WallNicheLook.FileName(size, variant));
    }

    [Fact]
    public void Only_the_three_wall_variants_and_the_two_sizes_have_files()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WallNicheLook.FileName(WallNicheSize.Room, (TextureVariant)7));
        Assert.Throws<ArgumentOutOfRangeException>(() => WallNicheLook.FileName((WallNicheSize)7, TextureVariant.A));
        Assert.Throws<ArgumentOutOfRangeException>(() => WallNicheLook.Depth((WallNicheSize)7));
    }

    [Fact]
    public void The_parts_are_frame_back_plate_and_sill()
    {
        Assert.Equal([new RigPart("frame", null), new RigPart("back_plate", null), new RigPart("sill", null)], WallNicheLook.Parts);
    }

    [Theory]
    [InlineData(3.5f, WallNicheSize.Corridor)]
    [InlineData(6f, WallNicheSize.Room)]
    [InlineData(3.5005f, WallNicheSize.Corridor)]
    [InlineData(5.9995f, WallNicheSize.Room)]
    public void Each_clear_height_of_the_game_has_its_size_within_the_tolerance(float clearHeight, WallNicheSize size)
    {
        Assert.Equal(size, WallNicheLook.SizeFor(clearHeight));
    }

    [Theory]
    [InlineData(3f)]
    [InlineData(3.51f)]
    [InlineData(4f)]
    [InlineData(5.5f)]
    public void Other_clear_heights_have_no_size(float clearHeight)
    {
        Assert.Null(WallNicheLook.SizeFor(clearHeight));
    }

    [Theory]
    [InlineData(WallNicheSize.Corridor, TextureVariant.A)]
    [InlineData(WallNicheSize.Corridor, TextureVariant.B)]
    [InlineData(WallNicheSize.Corridor, TextureVariant.C)]
    [InlineData(WallNicheSize.Room, TextureVariant.A)]
    [InlineData(WallNicheSize.Room, TextureVariant.B)]
    [InlineData(WallNicheSize.Room, TextureVariant.C)]
    public void Every_niche_file_stands_out_of_the_wall_by_its_depth_and_no_further(WallNicheSize size, TextureVariant variant)
    {
        var path = RepositoryFiles.Find($"assets/models/{WallNicheLook.FileName(size, variant)}");
        Assert.True(File.Exists(path), $"{path} is missing.");
        var parts = GlbScene.Read(File.ReadAllBytes(path)).ToDictionary(part => part.Name);
        Assert.Equal(WallNicheLook.Parts.Select(part => part.Name).Order(), parts.Keys.Order());
        Assert.All(parts.Values, part => Assert.Equal(Vector3.Zero, part.Pivot));

        // As wide as Width, centred on the origin, up to the apex at Height; the backs hide 1/8 m in the wall.
        var min = parts.Values.Select(part => part.PositionMin).Aggregate(Vector3.Min);
        var max = parts.Values.Select(part => part.PositionMax).Aggregate(Vector3.Max);
        Assert.Equal(-WallNicheLook.Width / 2f, min.X, Tolerance);
        Assert.Equal(WallNicheLook.Width / 2f, max.X, Tolerance);
        Assert.Equal(WallNicheLook.Height, max.Y, Tolerance);
        Assert.Equal(-0.125f, min.Z, Tolerance);
        Assert.Equal(WallNicheLook.Depth(size), max.Z, Tolerance);

        // Nothing stands below the floor; the back plate lies within the frame.
        Assert.True(min.Y >= 0f);
        Assert.True(parts[WallNicheLook.BackPlate].PositionMax.Z < parts[WallNicheLook.Frame].PositionMax.Z);
    }

    [Fact]
    public void The_room_niche_stands_further_out_than_the_corridor_niche()
    {
        Assert.Equal(0.125f, WallNicheLook.Depth(WallNicheSize.Corridor));
        Assert.Equal(0.25f, WallNicheLook.Depth(WallNicheSize.Room));
    }

    [Theory]
    [InlineData(2, 1, RoomSide.North, 0f, 1f)]
    [InlineData(2, 3, RoomSide.South, 0f, -1f)]
    [InlineData(1, 2, RoomSide.West, 1f, 0f)]
    [InlineData(3, 2, RoomSide.East, -1f, 0f)]
    public void A_niche_stands_on_the_floor_in_the_middle_of_the_cell_s_wall_looking_into_the_cell(
        int x, int z, RoomSide wall, float facingX, float facingZ)
    {
        var level = LevelParser.Parse(Room);
        var (position, facing, size) = WallNicheLook.Place(level, x, z, wall);

        Assert.Equal(new Vector3(facingX, 0f, facingZ), facing);
        Assert.Equal(WallNicheSize.Corridor, size);
        var center = level.CellCenter(x, z);
        Assert.Equal(center.Y, position.Y, Tolerance);
        Assert.Equal(center - (facing * (Level.CellSize / 2f)), position);

        // Just behind the position is the wall, just in front the cell.
        var behind = position - (facing * 0.01f);
        var before = position + (facing * 0.01f);
        Assert.True(level[Level.ToGrid(behind.X, behind.Z).X, Level.ToGrid(behind.X, behind.Z).Z].IsSolid);
        Assert.Equal((x, z), Level.ToGrid(before.X, before.Z));
    }

    [Theory]
    [InlineData(2, 1, RoomSide.North)]
    [InlineData(2, 3, RoomSide.South)]
    [InlineData(1, 2, RoomSide.West)]
    [InlineData(3, 2, RoomSide.East)]
    public void The_middle_of_a_placed_niche_sits_on_a_whole_metre_of_the_wall_s_texture(int x, int z, RoomSide wall)
    {
        // The back plate shows the wall's texture in the wall's grid (tools/models/wall_niche.py): it lines up only where
        // the niche's middle lies on a whole metre along the wall.
        var (position, facing, _) = WallNicheLook.Place(LevelParser.Parse(Room), x, z, wall);
        var u = SurfaceTexturing.TexCoord(position, facing, SurfaceKind.Wall, 1f).X;
        Assert.Equal(MathF.Round(u), u, Tolerance);
    }

    [Fact]
    public void Under_a_room_ceiling_the_niche_takes_the_room_size()
    {
        var level = Changed(LevelParser.Parse(Room), cell => cell with { CeilingHeight = RoomTemplate.RoomCeilingHeight });
        Assert.Equal(WallNicheSize.Room, WallNicheLook.Place(level, 2, 1, RoomSide.North).Size);
    }

    [Fact]
    public void A_raised_room_takes_its_size_from_the_clear_height_not_from_the_absolute_ceiling()
    {
        // Every open cell 0.5 m up, its ceiling as far: the clear height stays 3.5 m, the absolute ceiling is 4 m.
        var level = Changed(LevelParser.Parse(Room), cell => cell with { FloorHeight = 0.5f, CeilingHeight = 4f });
        var (position, _, size) = WallNicheLook.Place(level, 2, 1, RoomSide.North);

        Assert.Equal(WallNicheSize.Corridor, size);
        Assert.Equal(0.5f, position.Y, Tolerance);
    }

    [Fact]
    public void A_clear_height_off_by_less_than_the_tolerance_still_takes_a_niche()
    {
        var level = Changed(LevelParser.Parse(Room), cell => cell with { CeilingHeight = Cell.DefaultCeilingHeight + 0.0005f });
        Assert.Equal(WallNicheSize.Corridor, WallNicheLook.Place(level, 2, 1, RoomSide.North).Size);
    }

    [Fact]
    public void Under_a_ceiling_no_niche_is_built_for_no_niche_is_placed()
    {
        // The cell itself has a clear height of 3 m; its neighbours are plain.
        var level = With(LevelParser.Parse(Room), 2, 1, cell => cell with { CeilingHeight = 3f });
        Assert.Throws<ArgumentException>(() => WallNicheLook.Place(level, 2, 1, RoomSide.North));
    }

    [Fact]
    public void On_a_wall_cell_no_niche_is_placed_even_with_plain_wall_behind_and_beside_it()
    {
        // The cell (2, 1) is a wall; wall behind it, open floor with wall behind on both sides - only its being a wall
        // forbids the niche.
        var level = LevelParser.Parse("#####\n#.#.#\n#.P.#\n#####");
        Assert.Equal(Cell.Wall, level[2, 1]);
        Assert.True(level[2, 0].IsSolid && !level[1, 1].IsSolid && !level[3, 1].IsSolid && level[1, 0].IsSolid && level[3, 0].IsSolid);

        Assert.Throws<ArgumentException>(() => WallNicheLook.Place(level, 2, 1, RoomSide.North));
    }

    [Fact]
    public void Without_a_wall_behind_no_niche_is_placed()
    {
        // North of (2, 2) opens a passage; its neighbours have wall behind them.
        var level = LevelParser.Parse("#####\n##.##\n#.P.#\n#####");
        Assert.Throws<ArgumentException>(() => WallNicheLook.Place(level, 2, 2, RoomSide.North));
        WallNicheLook.Place(level, 2, 2, RoomSide.South);
    }

    [Fact]
    public void Beside_a_corner_no_niche_is_placed()
    {
        // West of (2, 2) is wall: the frame would hang into the corner.
        var level = LevelParser.Parse("#####\n#.P.#\n##..#\n#####");
        Assert.True(level[1, 2].IsSolid);
        Assert.Throws<ArgumentException>(() => WallNicheLook.Place(level, 2, 2, RoomSide.South));
    }

    [Fact]
    public void Beside_a_step_no_niche_is_placed()
    {
        // East of (2, 2) is a step 0.5 m up: the frame would stand over it.
        var level = LevelParser.Parse("#####\n#.P.#\n#..2#\n#####");
        Assert.Equal(0.5f, level[3, 2].FloorHeight);
        Assert.Throws<ArgumentException>(() => WallNicheLook.Place(level, 2, 2, RoomSide.South));
        WallNicheLook.Place(level, 2, 1, RoomSide.North);
    }

    [Fact]
    public void Beside_an_opening_no_niche_is_placed()
    {
        // South of (3, 2), beside (2, 2), opens a passage: the frame would hang into it.
        var level = LevelParser.Parse("#####\n#.P.#\n#...#\n###.#\n#...#\n#####");
        Assert.True(level[2, 3].IsSolid);
        Assert.False(level[3, 3].IsSolid);
        Assert.Throws<ArgumentException>(() => WallNicheLook.Place(level, 2, 2, RoomSide.South));
    }

    [Fact]
    public void Only_the_four_sides_of_a_cell_take_a_niche()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WallNicheLook.Place(LevelParser.Parse(Room), 2, 1, (RoomSide)7));
    }

    [Theory]
    [InlineData(TextureVariant.B, true, true, TextureVariant.B)]
    [InlineData(TextureVariant.C, false, true, TextureVariant.A)]
    [InlineData(TextureVariant.C, false, false, null)]
    public void A_missing_niche_falls_back_to_variant_a_then_to_none(
        TextureVariant wanted, bool wantedLoaded, bool variantALoaded, TextureVariant? expected)
    {
        Assert.Equal(expected, WallNicheLook.Pick(wanted, wantedLoaded, variantALoaded));
    }

    [Fact]
    public void The_niches_were_built_on_the_wall_textures_in_the_game()
    {
        // tools/models/wall_niche.py builds the niches on the wall textures and notes which ones it read. Once a wall
        // texture changes, the niche no longer matches the wall it is set on: run the script again.
        var built = Lines(RepositoryFiles.Find("tools/models/wall_niche_sources.sha256"));
        var current = Lines(RepositoryFiles.Find("assets/textures/textures.sha256"));

        Assert.Equal(3, built.Count);
        foreach (var line in built)
        {
            Assert.True(
                current.Contains(line),
                $"The wall niches were built on '{line}', which is no longer in assets/textures. " +
                "Run tools/models/wall_niche.py again (see assets/HERKUNFT.md).");
        }
    }

    // A copy of level with every open cell changed by change.
    private static Level Changed(Level level, Func<Cell, Cell> change)
    {
        var cells = new Cell[level.Width, level.Depth];
        for (var x = 0; x < level.Width; x++)
        {
            for (var z = 0; z < level.Depth; z++)
            {
                cells[x, z] = level[x, z].IsSolid ? level[x, z] : change(level[x, z]);
            }
        }

        return new Level(cells, 1, 1, null, [], [], []);
    }

    // A copy of level with the cell (x, z) changed by change.
    private static Level With(Level level, int x, int z, Func<Cell, Cell> change)
    {
        var cells = new Cell[level.Width, level.Depth];
        for (var cx = 0; cx < level.Width; cx++)
        {
            for (var cz = 0; cz < level.Depth; cz++)
            {
                cells[cx, cz] = level[cx, cz];
            }
        }

        cells[x, z] = change(cells[x, z]);
        return new Level(cells, 1, 1, null, [], [], []);
    }

    private static List<string> Lines(string path) => File.ReadAllLines(path)
        .Select(line => line.Trim())
        .Where(line => line.Length > 0 && !line.StartsWith('#'))
        .ToList();
}
