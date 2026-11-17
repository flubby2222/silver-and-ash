using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

public class DoorFrameLookTests
{
    private const float Tolerance = 0.0001f;

    private static readonly DoorKind[] FramedKinds = [DoorKind.Normal, DoorKind.Locked, DoorKind.Boss];

    private static readonly IReadOnlyList<RoomTemplate> Rooms = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    private static readonly Lazy<GeneratedLevel[]> ManyLevels = new(() => Enumerable.Range(0, 150)
        .Select(seed => LevelGenerator.Generate(Rooms, seed, Balance.Default.Layout))
        .ToArray());

    public static TheoryData<DoorKind, TextureVariant> EveryFrame()
    {
        var data = new TheoryData<DoorKind, TextureVariant>();
        foreach (var kind in FramedKinds)
        {
            foreach (var variant in new[] { TextureVariant.A, TextureVariant.B, TextureVariant.C })
            {
                data.Add(kind, variant);
            }
        }

        return data;
    }

    [Theory]
    [InlineData(DoorKind.Normal, TextureVariant.A, "door_frame_normal_a.glb")]
    [InlineData(DoorKind.Locked, TextureVariant.B, "door_frame_locked_b.glb")]
    [InlineData(DoorKind.Boss, TextureVariant.C, "door_frame_boss_c.glb")]
    public void Every_kind_and_wall_variant_has_its_own_file(DoorKind kind, TextureVariant variant, string file)
    {
        Assert.Equal(file, DoorFrameLook.FileName(kind, variant));
    }

    [Fact]
    public void Only_a_cracked_wall_has_no_frame()
    {
        Assert.Equal(FramedKinds.Order(), Enum.GetValues<DoorKind>().Where(DoorFrameLook.HasFrame).Order());
        Assert.Throws<ArgumentOutOfRangeException>(() => DoorFrameLook.FileName(DoorKind.Cracked, TextureVariant.A));
    }

    [Fact]
    public void The_parts_are_the_stone_of_the_front_face_and_of_the_back_face()
    {
        Assert.Equal([new RigPart("portal_front", null), new RigPart("portal_back", null)], DoorFrameLook.Parts);
    }

    [Theory]
    [MemberData(nameof(EveryFrame))]
    public void Every_frame_file_has_both_faces_where_the_drawing_expects_them(DoorKind kind, TextureVariant variant)
    {
        var path = RepositoryFiles.Find($"assets/models/{DoorFrameLook.FileName(kind, variant)}");
        Assert.True(File.Exists(path), $"{path} is missing.");
        var parts = GlbScene.Read(File.ReadAllBytes(path)).ToDictionary(part => part.Name);

        Assert.Equal(DoorFrameLook.Parts.Select(part => part.Name).Order(), parts.Keys.Order());

        // Both faces fill the cell's width and height: from the floor to the ceiling, flush with the wall at +-1 m, the
        // plates at most 1/8 m in front of it. The front face lies on the file's +z side, the back face on its -z side.
        var front = parts[DoorFrameLook.Front];
        var back = parts[DoorFrameLook.Back];
        foreach (var face in new[] { front, back })
        {
            Assert.Equal(Vector3.Zero, face.Pivot);
            Assert.Equal(-1f, face.PositionMin.X, Tolerance);
            Assert.Equal(1f, face.PositionMax.X, Tolerance);
            Assert.Equal(0f, face.PositionMin.Y, Tolerance);
            Assert.Equal(3.5f, face.PositionMax.Y, Tolerance);
        }

        Assert.InRange(front.PositionMin.Z, -Tolerance, 0.2f);
        Assert.Equal(1.125f, front.PositionMax.Z, Tolerance);
        Assert.Equal(-1.125f, back.PositionMin.Z, Tolerance);
        Assert.InRange(back.PositionMax.Z, -0.2f, Tolerance);
    }

    [Fact]
    public void A_hand_made_level_shows_variant_a_on_both_faces()
    {
        Assert.Equal((TextureVariant.A, TextureVariant.A), DoorFrameLook.Variants(SurfaceVariants.Plain, 3, 4, RoomSide.North));
    }

    [Fact]
    public void Every_door_frame_shows_on_each_face_the_variant_of_the_level_walls_beside_it()
    {
        var checkedFaces = 0;
        var differentFaces = 0;
        var sideways = 0;
        foreach (var generated in ManyLevels.Value)
        {
            var surfaces = SurfaceVariants.For(generated);
            var faces = LevelGeometry.Build(generated.Level).Where(face => face.Kind == SurfaceKind.Wall).ToList();
            foreach (var door in generated.Doors.Where(door => DoorFrameLook.HasFrame(door.Kind)))
            {
                var (frontCell, backCell) = CrackedWallLook.FaceCells(door.X, door.Z, door.Side);
                var (front, back) = DoorFrameLook.Variants(surfaces, door.X, door.Z, door.Side);
                Assert.Equal(surfaces.At(frontCell.X, frontCell.Z, SurfaceKind.Wall), front);
                Assert.Equal(surfaces.At(backCell.X, backCell.Z, SurfaceKind.Wall), back);

                // The frame's face continues the level's wall beside the doorway: every wall face in the same plane,
                // right beside the door cell, shows the same variant.
                foreach (var (cell, variant) in new[] { (frontCell, front), (backCell, back) })
                {
                    foreach (var beside in WallFacesBeside(faces, door, cell))
                    {
                        var (x, z) = LevelGeometry.CellOf(beside);
                        Assert.True(
                            surfaces.At(x, z, SurfaceKind.Wall) == variant,
                            $"Seed {generated.Seed}: the frame of the door at ({door.X}, {door.Z}) shows {variant}, the wall beside it {surfaces.At(x, z, SurfaceKind.Wall)}.");
                        checkedFaces++;
                    }
                }

                differentFaces += front != back ? 1 : 0;
                sideways += door.Side is RoomSide.East or RoomSide.West ? 1 : 0;
            }
        }

        // The seeds really cover what the test is about.
        Assert.True(checkedFaces > 500, $"Only {checkedFaces} wall faces beside a door frame.");
        Assert.True(differentFaces > 0, "No door with two different variants.");
        Assert.True(sideways > 0, "No door in an East/West passage.");
    }

    [Theory]
    [InlineData(TextureVariant.B, true, true, TextureVariant.B)]
    [InlineData(TextureVariant.B, true, false, TextureVariant.B)]
    [InlineData(TextureVariant.C, false, true, TextureVariant.A)]
    [InlineData(TextureVariant.A, true, true, TextureVariant.A)]
    [InlineData(TextureVariant.C, false, false, null)]
    [InlineData(TextureVariant.A, false, false, null)]
    public void A_missing_frame_falls_back_to_variant_a_then_to_none(
        TextureVariant wanted, bool wantedLoaded, bool variantALoaded, TextureVariant? expected)
    {
        Assert.Equal(expected, DoorFrameLook.Pick(wanted, wantedLoaded, variantALoaded));
    }

    [Theory]
    [InlineData(true, TextureVariant.A, TextureVariant.B, true)]
    [InlineData(false, TextureVariant.A, TextureVariant.B, false)]
    [InlineData(true, null, TextureVariant.B, false)]
    [InlineData(true, TextureVariant.A, null, false)]
    public void Without_the_model_or_a_frame_for_both_faces_the_door_is_the_placeholder(
        bool modelLoaded, TextureVariant? front, TextureVariant? back, bool expected)
    {
        Assert.Equal(expected, DoorFrameLook.DrawsModel(modelLoaded, front, back));
    }

    [Fact]
    public void The_frames_were_built_on_the_wall_textures_in_the_game()
    {
        // tools/models/door.py builds the frames on the wall textures and notes which ones it read. Once a wall texture
        // changes, the frame no longer continues the wall around it: run the script again.
        var built = Lines(RepositoryFiles.Find("tools/models/door_frame_sources.sha256"));
        var current = Lines(RepositoryFiles.Find("assets/textures/textures.sha256"));

        Assert.Equal(3, built.Count);
        foreach (var line in built)
        {
            Assert.True(
                current.Contains(line),
                $"The door frames were built on '{line}', which is no longer in assets/textures. " +
                "Run tools/models/door.py again (see assets/HERKUNFT.md).");
        }
    }

    // The level's wall faces in the plane of the frame's face toward `cell`, on the cells right and left of the door cell.
    private static IEnumerable<Face> WallFacesBeside(List<Face> faces, LevelDoor door, (int X, int Z) cell)
    {
        var toward = new Vector3(cell.X - door.X, 0f, cell.Z - door.Z);
        var across = new Vector3(MathF.Abs(toward.Z), 0f, MathF.Abs(toward.X));
        foreach (var step in new[] { -1f, 1f })
        {
            var solid = new Vector3(door.X + 0.5f, 0f, door.Z + 0.5f) + (across * step);
            var middle = (solid + (toward * 0.5f)) * Level.CellSize;
            foreach (var face in faces.Where(face =>
                Vector3.Dot(face.Normal, toward) > 0.9f
                && Vector3.Distance(((face.A + face.B + face.C + face.D) / 4f) with { Y = 0f }, middle) < Tolerance))
            {
                yield return face;
            }
        }
    }

    private static List<string> Lines(string path) => File.ReadAllLines(path)
        .Select(line => line.Trim())
        .Where(line => line.Length > 0 && !line.StartsWith('#'))
        .ToList();
}
