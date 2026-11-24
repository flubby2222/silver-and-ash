using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

public class DoorFrameLookTests
{
    private const float Tolerance = 0.0001f;

    private static readonly DoorKind[] FramedKinds = [DoorKind.Normal, DoorKind.Locked, DoorKind.Boss];

    // How far the stone of each frame stands out of the wall in front of the doorway. The plates of the door frames sit
    // flush in the wall plane (issue #715, art bible rule 26: nothing stands into an opening without a purpose); only the
    // buttresses of the boss portal stand out, 0.3 m (issue #568).
    private static readonly Dictionary<DoorKind, float> OutOfWall = new()
    {
        [DoorKind.Normal] = 0f,
        [DoorKind.Locked] = 0f,
        [DoorKind.Boss] = 0.3f,
    };

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

        // Both faces fill the doorway's width and height: from the floor to the ceiling of its cells, across all its cells
        // (the boss portal two, issue #568), the wall at +-1 m. Each face ends exactly OutOfWall in front of the wall: the
        // door frames in the wall plane (issue #715, a plate standing out again is red), the buttresses of the boss portal
        // 0.3 m in front of it. The front face lies on the file's +z side, the back face on its -z side.
        var front = parts[DoorFrameLook.Front];
        var back = parts[DoorFrameLook.Back];
        var halfWidth = DoorFrameLook.CellsWide(kind) * Level.CellSize / 2f;
        foreach (var face in new[] { front, back })
        {
            Assert.Equal(Vector3.Zero, face.Pivot);
            Assert.Equal(-halfWidth, face.PositionMin.X, Tolerance);
            Assert.Equal(halfWidth, face.PositionMax.X, Tolerance);
            Assert.Equal(0f, face.PositionMin.Y, Tolerance);
            Assert.Equal(DoorFrameLook.Height(kind), face.PositionMax.Y, Tolerance);
        }

        var reach = (Level.CellSize / 2f) + OutOfWall[kind];
        Assert.InRange(front.PositionMin.Z, -Tolerance, 0.2f);
        Assert.Equal(reach, front.PositionMax.Z, Tolerance);
        Assert.Equal(-reach, back.PositionMin.Z, Tolerance);
        Assert.InRange(back.PositionMax.Z, -0.2f, Tolerance);
    }

    [Theory]
    [InlineData(DoorKind.Normal, TextureVariant.A)]
    [InlineData(DoorKind.Normal, TextureVariant.B)]
    [InlineData(DoorKind.Normal, TextureVariant.C)]
    [InlineData(DoorKind.Locked, TextureVariant.A)]
    [InlineData(DoorKind.Locked, TextureVariant.B)]
    [InlineData(DoorKind.Locked, TextureVariant.C)]
    public void The_arch_has_its_underside_in_the_depth_of_the_leaf_too(DoorKind kind, TextureVariant variant)
    {
        // Issue #714: between the two halves of the frame, where the leaf stands (0.07 m either side of the door plane), the
        // arch had no surface. Closed, the head panel of the leaf filled it; open, the leaves took it away and a player below
        // the arch saw a dark curved stripe into the empty space above. Each half therefore reaches up to the door plane
        // along the whole arch, up to its crown (a 1.4375 m circle from the springing at 2 m, crown at 3.32 m).
        var bytes = File.ReadAllBytes(RepositoryFiles.Find($"assets/models/{DoorFrameLook.FileName(kind, variant)}"));
        var corners = GlbVertices.Read(bytes);

        foreach (var face in new[] { DoorFrameLook.Front, DoorFrameLook.Back })
        {
            var inDoorPlane = corners[face].Where(corner => MathF.Abs(corner.Z) < Tolerance && corner.Y > 2.05f).ToList();
            Assert.True(inDoorPlane.Count >= 6, $"{face} of the {kind} frame has no arch surface in the door plane.");
            Assert.True(inDoorPlane.Max(corner => corner.Y) > 3.3f, $"{face} of the {kind} frame stops short of the crown of the arch.");
        }
    }

    // The arch of the innermost run: circles of 1.4375 m around (+-0.5625, 2) (door.py, stufenportal: R0 1.5625 less the run's
    // width 0.125; the springing at 2 m). The right half of the arch is a piece of the circle around the LEFT centre.
    private const float ArchRadius = 1.4375f;
    private const float ArchCentreX = 0.5625f;
    private const float Springing = 2f;

    public static TheoryData<DoorKind, TextureVariant> NormalAndLocked()
    {
        var data = new TheoryData<DoorKind, TextureVariant>();
        foreach (var kind in new[] { DoorKind.Normal, DoorKind.Locked })
        {
            foreach (var variant in new[] { TextureVariant.A, TextureVariant.B, TextureVariant.C })
            {
                data.Add(kind, variant);
            }
        }

        return data;
    }

    private static IEnumerable<Vector3[]> TrianglesOf(string file, string part)
    {
        var (_, pivot, corners, indices) = WeaponPoseTests.Triangles(file).Single(p => p.Name == part);
        for (var first = 0; first < indices.Length; first += 3)
        {
            yield return [corners[indices[first]] + pivot, corners[indices[first + 1]] + pivot, corners[indices[first + 2]] + pivot];
        }
    }

    private static Vector2 CentreOf(Vector3 point) => new(point.X > 0 ? -ArchCentreX : ArchCentreX, Springing);

    [Theory]
    [MemberData(nameof(NormalAndLocked))]
    public void The_underside_strip_lies_on_the_innermost_run_and_faces_down_into_the_opening(DoorKind kind, TextureVariant variant)
    {
        // Issue #714, review: the strip must close the gap of the innermost run (radius 1.4375), not the outer one of the
        // frame (1.5625), and it must face the middle of the arch - seen from below. A strip facing up is invisible from
        // below and leaves the dark stripe as it was.
        var file = DoorFrameLook.FileName(kind, variant);
        foreach (var part in new[] { DoorFrameLook.Front, DoorFrameLook.Back })
        {
            var strip = TrianglesOf(file, part)
                .Where(triangle => triangle.All(corner => corner.Y > Springing - Tolerance && MathF.Abs(corner.Z) < 0.07f + Tolerance))
                .ToList();
            Assert.True(strip.Count >= 24, $"{part} of the {kind} frame has only {strip.Count} strip triangles under the arch.");
            foreach (var triangle in strip)
            {
                foreach (var corner in triangle)
                {
                    var radius = Vector2.Distance(new Vector2(corner.X, corner.Y), CentreOf(corner));
                    // The arch is a polygon of 6 chords per half: the corners lie on the circle, a point cut into a chord (at
                    // y = 3) up to 0.01 m inside it (sagitta of a 12 degree chord).
                    Assert.True(radius is > ArchRadius - 0.01f and < ArchRadius + 0.001f, $"A strip corner of the {part} at {corner} has radius {radius}, not {ArchRadius}.");
                }

                var middle = (triangle[0] + triangle[1] + triangle[2]) / 3f;
                var normal = Vector3.Cross(triangle[1] - triangle[0], triangle[2] - triangle[0]);
                var toCentre = new Vector3(CentreOf(middle) - new Vector2(middle.X, middle.Y), 0f);
                Assert.True(Vector3.Dot(normal, toCentre) > 0f, $"A strip triangle of the {part} faces away from the middle of the arch.");
            }
        }
    }

    [Theory]
    [InlineData(TextureVariant.A)]
    [InlineData(TextureVariant.B)]
    [InlineData(TextureVariant.C)]
    public void The_guide_ring_lies_behind_the_innermost_run_of_the_arch_not_in_the_opening(TextureVariant variant)
    {
        // Issue #730: the ring stays (without it the edge of the arch shows a dark sliver, see the pictures of the issue).
        // It hides behind the reveal: no corner of the ring may come closer to the middle of its arch than the innermost
        // run (radius 1.4375) - else a bit of it would stand in the opening beside the reveal.
        var ring = TrianglesOf(GratePose.GuideFileName(variant), GratePose.Guide).ToList();
        Assert.NotEmpty(ring);
        foreach (var triangle in ring)
        {
            foreach (var corner in triangle)
            {
                var distance = Vector2.Distance(new Vector2(corner.X, corner.Y), CentreOf(corner));
                Assert.True(
                    distance >= ArchRadius - Tolerance,
                    $"A corner of the guide ring at {corner} lies {distance} m from the middle of its arch - inside the reveal ({ArchRadius} m).");
            }
        }
    }

    [Theory]
    [MemberData(nameof(NormalAndLocked))]
    public void No_face_of_the_guide_ring_lies_on_a_face_of_the_frame(DoorKind kind, TextureVariant variant)
    {
        // Issue #714, review: a door with a portcullis (Door.HasGrate) draws the frame AND the guide ring of
        // portcullis_guide_<v>.glb at the same place. Two faces on the same plane, facing the same way, flicker against each
        // other (z-fighting) under the arch. Samples inside every ring triangle must not lie inside a frame triangle of the
        // same plane and direction.
        var ring = TrianglesOf(GratePose.GuideFileName(variant), GratePose.Guide).ToList();
        var frame = new List<Vector3[]>();
        foreach (var part in new[] { DoorFrameLook.Front, DoorFrameLook.Back })
        {
            frame.AddRange(TrianglesOf(DoorFrameLook.FileName(kind, variant), part));
        }

        Assert.NotEmpty(ring);
        Assert.NotEmpty(frame);
        var weights = new[] { (1 / 3f, 1 / 3f), (0.6f, 0.2f), (0.2f, 0.6f), (0.2f, 0.2f) };
        foreach (var a in ring)
        {
            var normalA = Vector3.Normalize(Vector3.Cross(a[1] - a[0], a[2] - a[0]));
            foreach (var b in frame)
            {
                var normalB = Vector3.Normalize(Vector3.Cross(b[1] - b[0], b[2] - b[0]));
                if (Vector3.Dot(normalA, normalB) < 0.99f || MathF.Abs(Vector3.Dot(normalA, b[0] - a[0])) > 0.001f)
                {
                    continue;
                }

                foreach (var (u, v) in weights)
                {
                    var sample = (a[0] * u) + (a[1] * v) + (a[2] * (1 - u - v));
                    Assert.False(
                        InsideTriangle(sample, b, normalB),
                        $"A face of the guide ring lies on a face of the {kind} frame near {sample}: the two flicker (z-fighting).");
                }
            }
        }
    }

    [Theory]
    [InlineData(TextureVariant.A)]
    [InlineData(TextureVariant.B)]
    [InlineData(TextureVariant.C)]
    public void The_step_between_the_hinge_post_and_the_springing_of_a_normal_door_is_closed_from_below(TextureVariant variant)
    {
        // Issue #730 (review of #726): the stone post on the hinge side of a normal door stands at x = 0.895, the arch begins
        // at x = 0.875. At the springing (y = 2) a strip 0.02 m wide, as deep as the post (0.1 m either side of the door
        // plane), was open downwards: no face of the frame covered it. Every point of the strip must lie in a face at y = 2
        // that faces down. Both posts, front and back.
        var file = DoorFrameLook.FileName(DoorKind.Normal, variant);
        foreach (var (part, side) in new[] { (DoorFrameLook.Front, 1f), (DoorFrameLook.Back, -1f) })
        {
            var ledges = TrianglesOf(file, part)
                .Where(triangle => triangle.All(corner => MathF.Abs(corner.Y - Springing) < Tolerance))
                .Where(triangle => Vector3.Normalize(Vector3.Cross(triangle[1] - triangle[0], triangle[2] - triangle[0])).Y < -0.99f)
                .ToList();
            foreach (var x in new[] { -1f, 1f })
            {
                foreach (var xStep in new[] { 0.877f, 0.885f, 0.893f })
                {
                    foreach (var zStep in new[] { 0.005f, 0.05f, 0.095f })
                    {
                        var point = new Vector3(x * xStep, Springing, side * zStep);
                        Assert.True(
                            ledges.Any(triangle => InsideTriangle(point, triangle, -Vector3.UnitY)),
                            $"The {part} of the normal frame has no face looking down at {point}: the step beside the hinge post is open.");
                    }
                }
            }
        }
    }

    private static bool InsideTriangle(Vector3 point, Vector3[] triangle, Vector3 normal)
    {
        for (var i = 0; i < 3; i++)
        {
            var edge = triangle[(i + 1) % 3] - triangle[i];
            if (Vector3.Dot(Vector3.Cross(edge, point - triangle[i]), normal) < -Tolerance)
            {
                return false;
            }
        }

        return true;
    }

    [Fact]
    public void The_boss_portal_spans_both_cells_of_the_boss_door_and_reaches_the_ceiling_of_its_rooms()
    {
        Assert.Equal(2, DoorFrameLook.CellsWide(DoorKind.Boss));
        Assert.Equal(1, DoorFrameLook.CellsWide(DoorKind.Normal));
        Assert.Equal(1, DoorFrameLook.CellsWide(DoorKind.Locked));
        Assert.Equal(RoomTemplate.RoomCeilingHeight, DoorFrameLook.Height(DoorKind.Boss));
        Assert.Equal(Cell.DefaultCeilingHeight, DoorFrameLook.Height(DoorKind.Normal));

        // Every framed door the generator builds has exactly as many cells as its model is wide, and is as high.
        var boss = 0;
        foreach (var generated in ManyLevels.Value)
        {
            foreach (var door in generated.Doors.Where(door => DoorFrameLook.HasFrame(door.Kind)))
            {
                Assert.True(DoorFrameLook.Fits(door.Kind, door.Cells.Count), $"Seed {generated.Seed}: the {door.Kind} door has {door.Cells.Count} cells.");
                Assert.All(door.Cells, cell => Assert.Equal(DoorFrameLook.Height(door.Kind), generated.Level[cell.X, cell.Z].CeilingHeight));
                boss += door.Kind == DoorKind.Boss ? 1 : 0;
            }
        }

        Assert.Equal(ManyLevels.Value.Length, boss);
    }

    [Theory]
    [InlineData(DoorKind.Boss, 2, true)]
    [InlineData(DoorKind.Boss, 1, false)]
    [InlineData(DoorKind.Normal, 1, true)]
    [InlineData(DoorKind.Normal, 2, false)]
    [InlineData(DoorKind.Locked, 1, true)]
    public void A_door_model_stands_only_in_a_doorway_exactly_as_wide_as_it(DoorKind kind, int cells, bool expected)
    {
        Assert.Equal(expected, DoorFrameLook.Fits(kind, cells));
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

    [Fact]
    public void Every_cell_of_a_boss_door_shows_the_variants_of_its_first_cell()
    {
        // Issue #603: DoorView draws the boss portal once, from the wall variants at the door's first cell. That is only
        // right if its second cell faces the same walls - no seam where the left and the right half would differ.
        var bossDoors = 0;
        var differentFaces = 0;
        foreach (var generated in ManyLevels.Value)
        {
            var surfaces = SurfaceVariants.For(generated);
            foreach (var door in generated.Doors.Where(door => door.Kind == DoorKind.Boss))
            {
                var first = DoorFrameLook.Variants(surfaces, door.X, door.Z, door.Side);
                Assert.All(door.Cells, cell => Assert.True(
                    DoorFrameLook.Variants(surfaces, cell.X, cell.Z, door.Side) == first,
                    $"Seed {generated.Seed}: the boss door cell ({cell.X}, {cell.Z}) shows {DoorFrameLook.Variants(surfaces, cell.X, cell.Z, door.Side)}, its first cell {first}."));
                bossDoors++;
                differentFaces += first.Front != first.Back ? 1 : 0;
            }
        }

        // The seeds really cover what the test is about: every level has its boss door, some with two variants.
        Assert.Equal(ManyLevels.Value.Length, bossDoors);
        Assert.True(differentFaces > 0, "No boss door with two different variants.");
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

    [Fact]
    public void The_boss_portal_was_built_on_the_wall_textures_in_the_game()
    {
        // tools/models/boss_portal.py builds the portal on the wall textures, like door.py the other frames (issue #568).
        var built = Lines(RepositoryFiles.Find("tools/models/boss_portal_sources.sha256"));
        var current = Lines(RepositoryFiles.Find("assets/textures/textures.sha256"));

        Assert.Equal(3, built.Count);
        foreach (var line in built)
        {
            Assert.True(
                current.Contains(line),
                $"The boss portal was built on '{line}', which is no longer in assets/textures. " +
                "Run tools/models/boss_portal.py again (see assets/HERKUNFT.md).");
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
