using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

public class CrackedWallLookTests
{
    private const float Tolerance = 0.0001f;

    private static readonly IReadOnlyList<RoomTemplate> Rooms = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    private static readonly Lazy<GeneratedLevel[]> ManyLevels = new(() => Enumerable.Range(0, 150)
        .Select(seed => LevelGenerator.Generate(Rooms, seed, Balance.Default.Layout))
        .ToArray());

    [Theory]
    [InlineData(TextureVariant.A, "cracked_wall_a.glb")]
    [InlineData(TextureVariant.B, "cracked_wall_b.glb")]
    [InlineData(TextureVariant.C, "cracked_wall_c.glb")]
    public void Every_wall_variant_has_its_own_file(TextureVariant variant, string file)
    {
        Assert.Equal(file, CrackedWallLook.FileName(variant));
        Assert.True(File.Exists(RepositoryFiles.Find($"assets/models/{file}")), $"{file} is missing.");
    }

    [Fact]
    public void The_parts_are_both_plates_both_heaps_of_debris_and_one_chunk()
    {
        Assert.Equal(
            [
                new RigPart("panel_front", null), new RigPart("panel_back", null),
                new RigPart("debris_front", null), new RigPart("debris_back", null), new RigPart("chunk", null),
            ],
            CrackedWallLook.Parts);
    }

    [Theory]
    [InlineData(TextureVariant.A)]
    [InlineData(TextureVariant.B)]
    [InlineData(TextureVariant.C)]
    public void Every_file_has_the_parts_where_the_drawing_expects_them(TextureVariant variant)
    {
        var parts = GlbScene.Read(File.ReadAllBytes(RepositoryFiles.Find($"assets/models/{CrackedWallLook.FileName(variant)}")))
            .ToDictionary(part => part.Name);

        Assert.Equal(CrackedWallLook.Parts.Select(part => part.Name).Order(), parts.Keys.Order());

        // The plates cover the whole 2 m x 2 m face of the cell, just in front of it (+z and -z), from the floor up.
        var front = parts[CrackedWallLook.PanelFront];
        var back = parts[CrackedWallLook.PanelBack];
        foreach (var plate in new[] { front, back })
        {
            Assert.Equal(-1f, plate.PositionMin.X, Tolerance);
            Assert.Equal(1f, plate.PositionMax.X, Tolerance);
            Assert.Equal(0f, plate.PositionMin.Y, Tolerance);
            Assert.Equal(2f, plate.PositionMax.Y, Tolerance);
        }

        Assert.InRange(front.PositionMin.Z, 1.001f, 1.02f);
        Assert.InRange(front.PositionMax.Z, 1.001f, 1.02f);
        Assert.InRange(back.PositionMin.Z, -1.02f, -1.001f);
        Assert.InRange(back.PositionMax.Z, -1.02f, -1.001f);

        // The debris lies on the floor in front of its own face, within the width of the wall.
        Assert.True(parts[CrackedWallLook.DebrisFront].PositionMin.Z > 1f);
        Assert.True(parts[CrackedWallLook.DebrisBack].PositionMax.Z < -1f);
        foreach (var debris in new[] { parts[CrackedWallLook.DebrisFront], parts[CrackedWallLook.DebrisBack] })
        {
            Assert.Equal(0f, debris.PositionMin.Y, Tolerance);
            Assert.InRange(debris.PositionMin.X, -1f, 1f);
            Assert.InRange(debris.PositionMax.X, -1f, 1f);
        }

        // The chunk lies around its origin, about ChunkSize big.
        var chunk = parts[CrackedWallLook.Chunk];
        foreach (var extent in new[] { -chunk.PositionMin, chunk.PositionMax })
        {
            Assert.InRange(extent.X, CrackedWallLook.ChunkSize / 4f, CrackedWallLook.ChunkSize);
            Assert.InRange(extent.Y, CrackedWallLook.ChunkSize / 4f, CrackedWallLook.ChunkSize);
            Assert.InRange(extent.Z, CrackedWallLook.ChunkSize / 4f, CrackedWallLook.ChunkSize);
        }
    }

    [Theory]
    [InlineData(RoomSide.North, 5, 7, 5, 8, 5, 6)]
    [InlineData(RoomSide.South, 5, 7, 5, 8, 5, 6)]
    [InlineData(RoomSide.East, 5, 7, 6, 7, 4, 7)]
    [InlineData(RoomSide.West, 5, 7, 6, 7, 4, 7)]
    public void The_faces_look_into_the_cells_on_either_side_of_the_passage(
        RoomSide side, int x, int z, int frontX, int frontZ, int backX, int backZ)
    {
        var (front, back) = CrackedWallLook.FaceCells(x, z, side);

        Assert.Equal((frontX, frontZ), front);
        Assert.Equal((backX, backZ), back);
    }

    [Theory]
    [InlineData(RoomSide.North)]
    [InlineData(RoomSide.East)]
    [InlineData(RoomSide.South)]
    [InlineData(RoomSide.West)]
    public void The_front_plate_turned_like_a_door_stands_before_the_front_cell(RoomSide side)
    {
        // The file's +z (the front plate) after DoorPose.Yaw points from the wall's cell to its front cell.
        var front = Vector3.Transform(Vector3.UnitZ, Matrix4x4.CreateRotationY(DoorPose.Yaw(side)));
        var (cell, back) = CrackedWallLook.FaceCells(10, 10, side);

        Assert.Equal(cell.X - 10, front.X, Tolerance);
        Assert.Equal(cell.Z - 10, front.Z, Tolerance);
        Assert.Equal((20 - cell.X, 20 - cell.Z), back);
    }

    [Fact]
    public void A_hand_made_level_shows_variant_a_on_both_faces()
    {
        Assert.Equal((TextureVariant.A, TextureVariant.A), CrackedWallLook.Variants(SurfaceVariants.Plain, 3, 4, RoomSide.East));
    }

    [Fact]
    public void Every_cracked_wall_shows_on_each_face_the_variant_of_the_level_wall_beside_it()
    {
        var checkedWalls = 0;
        var differentFaces = 0;
        var sideways = 0;
        foreach (var generated in ManyLevels.Value)
        {
            var walls = generated.Doors.Where(door => door.Kind == DoorKind.Cracked).ToList();
            if (walls.Count == 0)
            {
                continue;
            }

            // As the game draws it: the cracked cells are solid, the level puts wall faces around them.
            var level = generated.Level.WithCells(walls.Select(wall => (wall.X, wall.Z, Cell.Wall)));
            var surfaces = SurfaceVariants.For(generated);
            var faces = LevelGeometry.Build(level).Where(face => face.Kind == SurfaceKind.Wall).ToList();
            foreach (var wall in walls)
            {
                var (frontCell, backCell) = CrackedWallLook.FaceCells(wall.X, wall.Z, wall.Side);
                var (front, back) = CrackedWallLook.Variants(surfaces, wall.X, wall.Z, wall.Side);
                Assert.Equal(VariantOfLevelWall(faces, surfaces, wall, frontCell), front);
                Assert.Equal(VariantOfLevelWall(faces, surfaces, wall, backCell), back);
                checkedWalls++;
                differentFaces += front != back ? 1 : 0;
                sideways += wall.Side is RoomSide.East or RoomSide.West ? 1 : 0;
            }
        }

        // The seeds really cover what the test is about.
        Assert.True(checkedWalls > 50, $"Only {checkedWalls} cracked walls.");
        Assert.True(differentFaces > 0, "No cracked wall with two different variants.");
        Assert.True(sideways > 0, "No cracked wall in an East/West passage.");
    }

    [Fact]
    public void Every_cracked_wall_sits_where_the_plates_fit_without_a_seam()
    {
        foreach (var generated in ManyLevels.Value)
        {
            var level = generated.Level;
            foreach (var wall in generated.Doors.Where(door => door.Kind == DoorKind.Cracked))
            {
                var floor = level[wall.X, wall.Z].FloorHeight;
                var (front, back) = CrackedWallLook.FaceCells(wall.X, wall.Z, wall.Side);

                // Both faces look into walkable cells on the same floor: the plate stands on it, 2 m high, and the
                // wall's texture (one tile per metre, from height 0) starts at a tile edge.
                foreach (var (x, z) in new[] { front, back })
                {
                    Assert.False(level[x, z].IsSolid, $"Seed {generated.Seed}: the cell ({x}, {z}) before the cracked wall is solid.");
                    Assert.Equal(floor, level[x, z].FloorHeight);
                    Assert.True(level[x, z].CeilingHeight >= floor + 2f, $"Seed {generated.Seed}: ceiling lower than the plate.");
                }

                Assert.Equal(MathF.Round(floor), floor);

                // Its neighbours across the passage are solid: no plate edge sticks out past a corner.
                var (sideA, sideB) = wall.Side is RoomSide.East or RoomSide.West
                    ? ((wall.X, wall.Z - 1), (wall.X, wall.Z + 1))
                    : ((wall.X - 1, wall.Z), (wall.X + 1, wall.Z));
                Assert.True(level[sideA.Item1, sideA.Item2].IsSolid, $"Seed {generated.Seed}: open beside the cracked wall.");
                Assert.True(level[sideB.Item1, sideB.Item2].IsSolid, $"Seed {generated.Seed}: open beside the cracked wall.");
            }
        }
    }

    [Theory]
    [InlineData(true, true, true, true, CrackedWallDrawing.Panels)]
    [InlineData(true, true, true, false, CrackedWallDrawing.Panels)]
    [InlineData(false, true, true, true, CrackedWallDrawing.Lines)]
    [InlineData(true, false, true, true, CrackedWallDrawing.Lines)]
    [InlineData(true, true, false, true, CrackedWallDrawing.Lines)]
    [InlineData(false, false, true, true, CrackedWallDrawing.Lines)]
    [InlineData(false, true, true, false, CrackedWallDrawing.Placeholder)]
    [InlineData(true, true, false, false, CrackedWallDrawing.Placeholder)]
    [InlineData(false, false, false, false, CrackedWallDrawing.Placeholder)]
    public void Without_both_plates_the_wall_falls_back_to_the_lines_then_to_the_placeholder(
        bool frontLoaded, bool backLoaded, bool wallsTextured, bool linesLoaded, CrackedWallDrawing expected)
    {
        Assert.Equal(expected, CrackedWallLook.Choose(frontLoaded, backLoaded, wallsTextured, linesLoaded));
    }

    [Fact]
    public void The_plates_were_painted_on_the_wall_textures_in_the_game()
    {
        // tools/models/cracked_wall.py paints the cracks on the wall textures and notes which ones it read. Once a wall
        // texture changes, the plate no longer continues the wall around it: run the script again.
        var painted = Lines(RepositoryFiles.Find("tools/models/cracked_wall_sources.sha256"));
        var current = Lines(RepositoryFiles.Find("assets/textures/textures.sha256"));

        Assert.Equal(3, painted.Count);
        foreach (var line in painted)
        {
            Assert.True(
                current.Contains(line),
                $"The cracked walls were painted on '{line}', which is no longer in assets/textures. " +
                "Run tools/models/cracked_wall.py again (see assets/HERKUNFT.md).");
        }
    }

    private static TextureVariant VariantOfLevelWall(List<Face> faces, SurfaceVariants surfaces, LevelDoor wall, (int X, int Z) cell)
    {
        // The level's wall face between the cracked cell and this cell: in front of the cell, facing away from the wall.
        var middle = new Vector3((wall.X + 0.5f) * Level.CellSize, 0f, (wall.Z + 0.5f) * Level.CellSize);
        var toward = new Vector3(cell.X - wall.X, 0f, cell.Z - wall.Z);
        var face = Assert.Single(faces, face =>
            LevelGeometry.CellOf(face) == cell
            && Vector3.Dot(face.Normal, toward) > 0.9f
            && Vector3.Distance(((face.A + face.B + face.C + face.D) / 4f) with { Y = 0f }, middle + (toward * (Level.CellSize / 2f))) < Tolerance);
        var (x, z) = LevelGeometry.CellOf(face);
        return surfaces.At(x, z, SurfaceKind.Wall);
    }

    private static List<string> Lines(string path) => File.ReadAllLines(path)
        .Select(line => line.Trim())
        .Where(line => line.Length > 0 && !line.StartsWith('#'))
        .ToList();
}
