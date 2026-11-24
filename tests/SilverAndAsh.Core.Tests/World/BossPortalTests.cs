using System.Numerics;
using System.Text.Json;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.World;

/// <summary>
/// The stone of the boss portal (issue #598): its measure (<see cref="BossPortalShape"/>) matches the model
/// (assets/models/door_frame_boss_*.glb), and walking, sight, shots and way-finding stop at it.
/// </summary>
public class BossPortalTests
{
    // An open floor 12 x 8 m with a high ceiling; the portal stands at x 6 m, its door plane at z 3 m (the doorway
    // is cells (2, 1) and (3, 1)), the passage along Z.
    private static readonly BossPortal Portal = new(6f, 3f, 0f, PassesAlongZ: true);

    private static readonly float[] Radii =
        [BodySizes.Player.Radius, BodySizes.Ghoul.Radius, BodySizes.Bat.Radius, BodySizes.Gorm.Radius];

    [Theory]
    [InlineData(0.99f, 0f, 1f, false)]     // door plane: the opening is 1 m to either side
    [InlineData(1.01f, 0f, 1f, true)]
    [InlineData(1.59f, 0.99f, 1f, false)]  // at the wall face: 1.6 m
    [InlineData(1.61f, 0.99f, 1f, true)]
    [InlineData(1.19f, 0.5f, 1f, false)]   // the second run from inside: 1.2 m
    [InlineData(1.21f, 0.5f, 1f, true)]
    [InlineData(1.5f, 1.1f, 0.4f, true)]   // the plinth before the wall face, 0.5 m high ...
    [InlineData(1.5f, 1.1f, 0.6f, false)]  // ... above it free (the buttress begins at 1.72 m)
    [InlineData(1.8f, 1.2f, 2f, true)]     // the buttress
    [InlineData(1.5f, 1.2f, 0.2f, false)]  // beyond the plinth
    [InlineData(2.01f, 0f, 1f, false)]     // beside the portal: the wall, not its stone
    public void The_stone_has_the_measure_of_the_model(float across, float along, float up, bool stone)
    {
        Assert.Equal(stone, BossPortalShape.IsStone(across, along, up));
        Assert.Equal(stone, BossPortalShape.IsStone(-across, -along, up));
    }

    [Fact]
    public void A_ray_up_under_the_arch_hits_its_underside_with_the_normal_towards_the_arch_centre()
    {
        // 0.9 m from the middle, 0.2 m from the door plane: the innermost run, radius 2.5 m about (-1.5 m, 3.2 m).
        var level = OpenLevel().WithPortals([Portal]);

        var hit = Raycast.AgainstLevel(level, new Vector3(6.9f, 1f, 3.2f), Vector3.UnitY, 10f);

        Assert.NotNull(hit);
        var rise = MathF.Sqrt((2.5f * 2.5f) - (2.4f * 2.4f));
        Assert.Equal(BossPortalShape.Spring + rise, hit.Value.Point.Y, 0.02f);
        Assert.Equal(-2.4f / 2.5f, hit.Value.Normal.X, 0.02f);
        Assert.Equal(-rise / 2.5f, hit.Value.Normal.Y, 0.02f);
        Assert.Equal(0f, hit.Value.Normal.Z, 0.0001f);
    }

    [Theory]
    [InlineData(1.5f, 3.3f, 1f)]    // through the outermost run's arch onto the face of the second run (0.85 m deep)
    [InlineData(1.3f, 3.5f, 1f)]    // through two runs onto the face of the third (0.6 m)
    [InlineData(1f, 4f, 1f)]        // through three runs onto the face of the innermost (0.35 m)
    [InlineData(-1.3f, 3.5f, 1f)]   // the other side of the passage
    [InlineData(1.3f, 3.5f, -1f)]   // from behind the door
    public void A_ray_along_the_passage_above_the_springing_hits_the_face_of_a_run_with_the_normal_back_along_the_passage(
        float across, float up, float face)
    {
        // Above the springing the runs step inwards like the stairs of a pulpit: a ray along the passage into the doorway
        // goes through the arch of the wider runs and hits the upright face of a narrower one - no underside of an arch
        // (issue #628).
        var level = OpenLevel().WithPortals([Portal]);
        var origin = new Vector3(Portal.X + across, up, Portal.Z + (face * 2f));
        Assert.False(LineOfSight.IsInsideGeometry(level, origin));

        var hit = Raycast.AgainstLevel(level, origin, new Vector3(0f, 0f, -face), 5f);

        Assert.NotNull(hit);
        Assert.True(Portal.Contains(hit.Value.Point - new Vector3(0f, 0f, face * 0.01f)), $"{hit.Value.Point} is not before the stone.");
        Assert.Equal(new Vector3(0f, 0f, face), hit.Value.Normal);
    }

    [Fact]
    public void A_ray_at_the_portal_never_passes_through_its_stone()
    {
        // Rays from all over the floor at points of the portal (seeded, the same every run): along each, up to its hit,
        // no stretch of 2 cm or more lies in the stone - also not where it only grazes a thin corner of a block or an
        // arch (issue #628).
        var level = OpenLevel().WithPortals([Portal]);
        var random = new Random(628);
        const float Sample = 0.002f;
        var longest = 0f;
        var worst = "";
        for (var ray = 0; ray < 4000; ray++)
        {
            var origin = new Vector3(Between(0.2f, 11.8f), Between(0.2f, 5.8f), Between(0.2f, 7.8f));
            var target = new Vector3(
                Portal.X + Between(-BossPortalShape.HalfWidth, BossPortalShape.HalfWidth),
                Between(0f, BossPortalShape.Height),
                Portal.Z + Between(-1.3f, 1.3f));
            if (LineOfSight.IsInsideGeometry(level, origin) || Vector3.Distance(origin, target) < 0.5f)
            {
                continue;
            }

            var direction = Vector3.Normalize(target - origin);
            var reach = Vector3.Distance(origin, target) + 3f;
            var end = Raycast.AgainstLevel(level, origin, direction, reach)?.Distance ?? reach;
            var inStone = 0f;
            for (var t = 0f; t < end; t += Sample)
            {
                inStone = Portal.Contains(origin + (direction * t)) ? inStone + Sample : 0f;
                if (inStone > longest)
                {
                    longest = inStone;
                    worst = $"from {origin} towards {target}";
                }
            }
        }

        Assert.True(longest < 0.02f, $"{longest:0.000} m through the stone {worst}.");

        float Between(float min, float max) => min + ((float)random.NextDouble() * (max - min));
    }

    [Theory]
    [InlineData(-0.05f, true)]
    [InlineData(0.01f, false)]
    public void A_body_mostly_beside_the_plinth_stands_on_it_while_it_overlaps_it(float clear, bool onIt)
    {
        // The plinth reaches out to the side of the box around the portal's stone (2 m beside the middle). A player
        // standing on its outer edge has his middle nearly a radius beside that box - 5 cm of him still on the stone, he
        // stays on it, tick by tick. With his middle 1 cm more than a radius off he is beside it and falls to the floor.
        // The filter of CharacterCollision.MoveVertically (issue #642) must not drop the portal while a body reaches it.
        var level = OpenLevel().WithPortals([Portal]);
        var plinth = BossPortalShape.Blocks[BossPortalShape.Runs];
        var radius = BodySizes.Player.Radius;
        var at = new Vector3(Portal.X + plinth.AcrossTo + radius + clear, plinth.Top, Portal.Z + ((plinth.AlongFrom + plinth.AlongTo) / 2f));
        var velocity = Vector3.Zero;
        var gravity = Balance.Default.Player.Gravity;

        for (var tick = 0; tick < SimulationClock.TicksPerSecond; tick++)
        {
            (at, velocity, _) = CharacterCollision.MoveVertically(
                level, at, velocity, radius, BodySizes.Player.Height, gravity, SimulationClock.SecondsPerTick);
        }

        Assert.Equal(onIt ? plinth.Top : 0f, at.Y);
    }

    [Fact]
    public void A_ray_that_grazes_the_outer_corner_of_a_buttress_hits_it()
    {
        // The buttress stands 1.72-2 m beside the middle, 1-1.24 m from the door plane - its outer face (2 m) is the edge
        // of the box around the stone, and the box reaches on to the cover plates (1.3 m). A ray from outside comes in
        // diagonally through that outer face 1 cm before the corner and leaves through the buttress's front at once:
        // only 1.4 cm of it lie in the stone. Only the short steps from a step's length before the box catch that
        // (Raycast.StepAt, BossPortal.IsNear with its margin, issue #642); from just at the box a long step would
        // land past the corner. From origins a tenth to a fifth of a metre outside, so the long steps fall differently.
        var level = OpenLevel().WithPortals([Portal]);
        var buttress = BossPortalShape.Blocks.Single(block => block.Bottom == 0.5f && block.Top > BossPortalShape.Spring);
        Assert.Equal(BossPortalShape.HalfWidth, buttress.AcrossTo);
        var corner = new Vector3(Portal.X + buttress.AcrossTo, 2f, Portal.Z + buttress.AlongTo);
        var entry = corner - new Vector3(0f, 0f, 0.01f);
        var direction = Vector3.Normalize(new Vector3(-1f, 0f, 1f));

        for (var outside = 0.1f; outside <= 0.2001f; outside += 0.01f)
        {
            var origin = entry - (direction * outside * MathF.Sqrt(2f));
            Assert.False(Portal.IsNear(origin, 0f), $"{origin} is already in the box around the stone.");

            var hit = Raycast.AgainstLevel(level, origin, direction, 1f);

            Assert.True(hit is not null, $"the ray from {origin} passes the corner.");
            Assert.Equal(entry.X, hit.Value.Point.X, 0.002f);
            Assert.Equal(entry.Z, hit.Value.Point.Z, 0.002f);
            Assert.Equal(Vector3.UnitX, hit.Value.Normal);
        }
    }

    [Fact]
    public void Sight_ends_in_the_stone_and_passes_through_the_opening()
    {
        var level = OpenLevel().WithPortals([Portal]);

        Assert.True(LineOfSight.IsInsideGeometry(level, new Vector3(4.5f, 1f, 3f)));
        Assert.False(LineOfSight.IsInsideGeometry(level, new Vector3(6f, 1f, 3f)));
        Assert.False(LineOfSight.IsInsideGeometry(OpenLevel(), new Vector3(4.5f, 1f, 3f)));
    }

    [Fact]
    public void The_portals_stay_through_every_copy_of_the_level()
    {
        var level = OpenLevel().WithPortals([Portal]);

        Assert.Equal([Portal], level.WithCells([(5, 3, Cell.Wall)]).Portals);
        Assert.Equal([Portal], level.WithColumns([]).Portals);
        Assert.Equal([Portal], level.WithWallCutouts([]).Portals);
        Assert.Equal([Portal], level.WithEnemies([], []).Portals);
    }

    [Fact]
    public void A_portal_outside_the_level_is_refused()
    {
        var error = Assert.Throws<ArgumentException>(() => OpenLevel().WithPortals([Portal with { X = 30f }]));
        Assert.Contains("30/3", error.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => OpenLevel().WithPortals([Portal with { Z = float.NaN }]));
    }

    [Fact]
    public void A_way_through_the_doorway_goes_through_the_middle_of_the_opening()
    {
        // Through the left door cell: without the portal straight on from cell middle to cell middle, with it through
        // the middle of the opening - straight on would lead through the jamb.
        (int, int)[] path = [(2, 0), (2, 1), (2, 2)];
        var level = OpenLevel().WithPortals([Portal]);

        Assert.Equal([new Vector3(5f, 0f, 5f)], Pathfinder.Smooth(OpenLevel(), path, BodySizes.Ghoul.Radius));
        Assert.Equal([new Vector3(6f, 0f, 3f), new Vector3(5f, 0f, 5f)], Pathfinder.Smooth(level, path, BodySizes.Ghoul.Radius));
        Assert.False(Pathfinder.CanWalkStraight(level, new Vector3(5f, 0f, 1f), new Vector3(5f, 0f, 5f), BodySizes.Ghoul.Radius));
        Assert.True(Pathfinder.CanWalkStraight(level, new Vector3(6f, 0f, 1f), new Vector3(6f, 0f, 5f), BodySizes.Ghoul.Radius));
        Assert.Equal(new Vector3(6f, 0f, 3f), level.WayPoint(3, 1));
        Assert.Equal(level.CellCenter(2, 2), level.WayPoint(2, 2));
    }

    [Fact]
    public void A_body_pushed_out_of_the_doorway_never_stands_in_the_stone()
    {
        // The shutting door puts a body just past the wall face (OnThreshold). Beside the opening the plinth stands out
        // of that wall: across, the body goes into the opening (PushedAcross). Clamped only to the door's two cells it
        // would stand in the plinth.
        var level = OpenLevel().WithPortals([Portal]);
        var intoThePlinth = 0;
        foreach (var radius in Radii)
        {
            var z = Portal.Z + BossPortalShape.HalfDepth + radius + 0.01f;
            for (var across = -1.9f; across <= 1.9f; across += 0.1f)
            {
                var x = Portal.PushedAcross(Portal.X + across, radius);
                Assert.False(CharacterCollision.Overlaps(level, new Vector3(x, 0f, z), radius), $"radius {radius}, across {across}");

                var inTheCells = Math.Clamp(Portal.X + across, Portal.X - BossPortalShape.HalfWidth + radius, Portal.X + BossPortalShape.HalfWidth - radius);
                intoThePlinth += CharacterCollision.Overlaps(level, new Vector3(inTheCells, 0f, z), radius) ? 1 : 0;
            }
        }

        Assert.True(intoThePlinth > 0);
        Assert.Equal(Portal.X, Portal.PushedAcross(Portal.X + 1.5f, 1.2f));
    }

    public static TheoryData<string> PortalModels => new() { "door_frame_boss_a.glb", "door_frame_boss_b.glb", "door_frame_boss_c.glb" };

    [Theory]
    [MemberData(nameof(PortalModels))]
    public void Every_corner_of_the_model_lies_on_the_stone_and_every_corner_of_the_stone_in_the_model(string file)
    {
        // The model in its own measure: x across, y up, z along (portal_front before the door plane, portal_back behind).
        // Only the pinnacles on the cover plates (above 5.45 m) are left out of the stone.
        var vertices = Vertices(RepositoryFiles.Find($"assets/models/{file}"));
        var pinnacles = BossPortalShape.Blocks[^1].Top + 0.001f;
        Assert.NotEmpty(vertices);

        foreach (var vertex in vertices.Where(vertex => vertex.Y <= pinnacles))
        {
            Assert.True(OnTheStone(vertex), $"{file}: {vertex} lies off the stone.");
        }

        foreach (var corner in Corners())
        {
            Assert.True(vertices.Any(vertex => Vector3.Distance(vertex, corner) < 0.001f), $"{file}: no vertex at {corner}.");
        }

        // On the surface: a point within 2 mm lies in the stone. The model draws each arch as eight straight pieces
        // (tools/models/boss_portal.py, arc_pts) and splits them at its texture seams: those points lie on a chord,
        // up to 6 mm inside the circle - above the springing within 1 cm.
        static bool OnTheStone(Vector3 at)
        {
            var near = at.Y > BossPortalShape.Spring ? 0.01f : 0.002f;
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dz = -1; dz <= 1; dz++)
                    {
                        if (BossPortalShape.IsStone(at.X + (dx * near), at.Z + (dz * near), at.Y + (dy * near)))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        // The edges where the stone meets the passage and the room: of every run its face at the floor and at the
        // springing, of every block before the wall its eight corners - in front of the door plane and behind it.
        static IEnumerable<Vector3> Corners()
        {
            foreach (var face in new[] { 1f, -1f })
            {
                for (var run = 0; run < BossPortalShape.Runs; run++)
                {
                    var opening = BossPortalShape.RunArchRadius(run) + BossPortalShape.ArchCentre;
                    foreach (var along in new[] { BossPortalShape.RunStart(run), BossPortalShape.RunEnd(run) })
                    {
                        yield return new Vector3(opening, 0f, face * along);
                        yield return new Vector3(opening, BossPortalShape.Spring, face * along);
                    }
                }

                foreach (var block in BossPortalShape.Blocks.Skip(BossPortalShape.Runs))
                {
                    foreach (var across in new[] { block.AcrossFrom, block.AcrossTo })
                    {
                        foreach (var up in new[] { block.Bottom, block.Top })
                        {
                            yield return new Vector3(across, up, face * block.AlongTo);
                        }
                    }
                }
            }
        }
    }

    private static Level OpenLevel()
    {
        var level = LevelParser.Parse("P.....\n......\n......\n......");
        var high = Cell.Floor with { CeilingHeight = BossPortalShape.Height };
        return level.WithCells(Enumerable.Range(0, 6).SelectMany(x => Enumerable.Range(0, 4).Select(z => (x, z, high))));
    }

    // The positions of all vertices of the portal's two parts, read from the binary chunk of the GLB file (floats,
    // three to a vertex) - GlbScene reads only the scene description.
    private static List<Vector3> Vertices(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var jsonLength = BitConverter.ToInt32(bytes, 12);
        using var json = JsonDocument.Parse(bytes.AsMemory(20, jsonLength));
        var binary = 20 + jsonLength + 8;
        var root = json.RootElement;
        var meshes = root.GetProperty("meshes");
        var accessors = root.GetProperty("accessors");
        var views = root.GetProperty("bufferViews");
        var vertices = new List<Vector3>();
        foreach (var node in root.GetProperty("nodes").EnumerateArray())
        {
            if (!node.GetProperty("name").GetString()!.StartsWith("portal_", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.False(node.TryGetProperty("translation", out _), "The portal parts are placed at the origin.");
            foreach (var primitive in meshes[node.GetProperty("mesh").GetInt32()].GetProperty("primitives").EnumerateArray())
            {
                var accessor = accessors[primitive.GetProperty("attributes").GetProperty("POSITION").GetInt32()];
                var view = views[accessor.GetProperty("bufferView").GetInt32()];
                var stride = view.TryGetProperty("byteStride", out var s) ? s.GetInt32() : 12;
                var start = binary + Offset(view) + Offset(accessor);
                for (var i = 0; i < accessor.GetProperty("count").GetInt32(); i++)
                {
                    var at = start + (i * stride);
                    vertices.Add(new Vector3(BitConverter.ToSingle(bytes, at), BitConverter.ToSingle(bytes, at + 4), BitConverter.ToSingle(bytes, at + 8)));
                }
            }
        }

        return vertices;

        static int Offset(JsonElement element) => element.TryGetProperty("byteOffset", out var offset) ? offset.GetInt32() : 0;
    }
}
