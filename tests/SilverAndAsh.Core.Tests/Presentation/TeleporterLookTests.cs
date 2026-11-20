using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// The teleporter model "horn crystal" (issue #436) as tools/models/teleporter.py builds it: two horns on a two-step
/// dais, a crystal floating between them. The expected numbers come from the script, not from the files.
/// </summary>
public class TeleporterLookTests
{
    private const float Tolerance = 0.001f;

    private const string Body = "body";
    private const string Crystal = "crystal";

    // tools/models/teleporter.py: CRYSTAL_Y, CRYSTAL_R, CRYSTAL_H.
    private const float CrystalMiddle = 2.1f;
    private const float CrystalRadius = 0.17f;
    private const float CrystalHalfHeight = 0.35f;

    // The dais: lower step radius 0.95 up to 0.12 m, upper step radius 0.78 from 0.08 m, 0.12 m high.
    private const float LowerStepRadius = 0.95f;
    private const float UpperStepRadius = 0.78f;
    private const float DaisHeight = 0.08f + 0.12f;

    public static TheoryData<string> BothFiles() => new("teleporter_locked.glb", "teleporter_unlocked.glb");

    [Theory]
    [MemberData(nameof(BothFiles))]
    public void Every_file_has_the_body_and_the_crystal_in_this_order(string file)
    {
        var parts = GlbScene.Read(File.ReadAllBytes(Model(file)));

        Assert.Equal([Body, Crystal], parts.Select(part => part.Name));
        Assert.Equal(Vector3.Zero, parts[0].Pivot);
        Assert.Equal(new Vector3(0f, CrystalMiddle, 0f), parts[1].Pivot);
    }

    [Theory]
    [MemberData(nameof(BothFiles))]
    public void The_body_stands_on_the_floor_176_cm_wide_and_265_cm_high(string file)
    {
        var body = GlbScene.Read(File.ReadAllBytes(Model(file))).Single(part => part.Name == Body);

        // The octagon of the lower step is turned by 22.5 degrees: its flat sides face the axes.
        var halfWidth = LowerStepRadius * MathF.Cos(MathF.PI / 8f);
        Assert.Equal(new Vector3(-halfWidth, 0f, -halfWidth), body.PositionMin, VectorTolerance);
        Assert.Equal(halfWidth, body.PositionMax.X, Tolerance);
        Assert.Equal(halfWidth, body.PositionMax.Z, Tolerance);
        Assert.Equal(1.76f, 2f * halfWidth, 0.01f);

        // The tip of a horn: from (0.58, 0.15) to (0.30, 2.15), then 0.5 m further along the horn.
        var horn = Vector2.Normalize(new Vector2(0.30f - 0.58f, 2.15f - 0.15f));
        var tip = 2.15f + (0.5f * horn.Y);
        Assert.Equal(tip, body.PositionMax.Y, Tolerance);
        Assert.Equal(2.65f, tip, 0.01f);
    }

    [Theory]
    [MemberData(nameof(BothFiles))]
    public void The_dais_is_20_cm_high_below_the_step_height(string file)
    {
        var corners = GlbVertices.Read(File.ReadAllBytes(Model(file)))[Body];

        // The corners of the upper step lie 0.78 m from the middle; the highest of them is the top of the dais.
        var upperStep = corners.Where(corner => MathF.Abs(new Vector2(corner.X, corner.Z).Length() - UpperStepRadius) < Tolerance).ToList();
        Assert.Equal(16, upperStep.Select(corner => (MathF.Round(corner.X, 3), MathF.Round(corner.Y, 3), MathF.Round(corner.Z, 3))).Distinct().Count());
        Assert.Equal(DaisHeight, upperStep.Max(corner => corner.Y), Tolerance);
        Assert.Equal(0.2f, DaisHeight, Tolerance);
    }

    [Theory]
    [MemberData(nameof(BothFiles))]
    public void The_crystal_floats_above_the_eyes_of_a_player_on_the_dais(string file)
    {
        var crystal = GlbScene.Read(File.ReadAllBytes(Model(file))).Single(part => part.Name == Crystal);

        Assert.Equal(new Vector3(-CrystalRadius, -CrystalHalfHeight, -CrystalRadius), crystal.PositionMin, VectorTolerance);
        Assert.Equal(new Vector3(CrystalRadius, CrystalHalfHeight, CrystalRadius), crystal.PositionMax, VectorTolerance);

        // Bottom edge 1.75 m, middle 2.1 m.
        Assert.Equal(1.75f, crystal.Pivot.Y + crystal.PositionMin.Y, Tolerance);
        Assert.Equal(2.1f, crystal.Pivot.Y, Tolerance);
    }

    [Theory]
    [MemberData(nameof(BothFiles))]
    public void Every_file_has_272_triangles_and_every_part_is_closed_all_round(string file)
    {
        var bytes = File.ReadAllBytes(Model(file));
        var triangles = Triangles(bytes);
        var corners = GlbVertices.Read(bytes);

        // The crystal is an octahedron: eight triangles.
        Assert.Equal(8, triangles[Crystal].Length / 3);
        Assert.Equal(272, triangles.Values.Sum(indices => indices.Length / 3));
        foreach (var (name, indices) in triangles)
        {
            // Corners at the same place count as one (flat faces split them); then every edge has exactly two faces.
            var places = corners[name].Select(corner => (MathF.Round(corner.X * 1e4f), MathF.Round(corner.Y * 1e4f), MathF.Round(corner.Z * 1e4f))).ToArray();
            var edges = new Dictionary<((float, float, float), (float, float, float)), int>();
            for (var i = 0; i < indices.Length; i += 3)
            {
                for (var k = 0; k < 3; k++)
                {
                    var a = places[indices[i + k]];
                    var b = places[indices[i + ((k + 1) % 3)]];
                    var edge = a.CompareTo(b) < 0 ? (a, b) : (b, a);
                    edges[edge] = edges.GetValueOrDefault(edge) + 1;
                }
            }

            Assert.All(edges, edge => Assert.True(edge.Value == 2, $"{name}: edge {edge.Key} has {edge.Value} faces."));
        }
    }

    [Fact]
    public void Locked_and_unlocked_have_the_same_geometry()
    {
        var locked = File.ReadAllBytes(Model("teleporter_locked.glb"));
        var unlocked = File.ReadAllBytes(Model("teleporter_unlocked.glb"));

        Assert.Equal(GlbScene.Read(locked), GlbScene.Read(unlocked));
        var lockedCorners = GlbVertices.Read(locked);
        var unlockedCorners = GlbVertices.Read(unlocked);
        var lockedTriangles = Triangles(locked);
        var unlockedTriangles = Triangles(unlocked);
        foreach (var name in new[] { Body, Crystal })
        {
            Assert.Equal(lockedCorners[name], unlockedCorners[name]);
            Assert.Equal(lockedTriangles[name], unlockedTriangles[name]);
        }
    }

    // Issue #480: what the game draws and how.
    [Fact]
    public void The_look_names_the_parts_of_the_files_in_their_order()
    {
        Assert.Equal([Body, Crystal], TeleporterLook.Parts.Select(part => part.Name));
        Assert.All(TeleporterLook.Parts, part => Assert.Null(part.Parent));
        Assert.True(File.Exists(Model(TeleporterLook.FileName(unlocked: false))));
        Assert.True(File.Exists(Model(TeleporterLook.FileName(unlocked: true))));
        Assert.NotEqual(TeleporterLook.FileName(unlocked: false), TeleporterLook.FileName(unlocked: true));
    }

    [Theory]
    [InlineData(Crystal, true, true)]
    [InlineData(Crystal, false, false)]
    [InlineData(Body, true, false)]
    [InlineData(Body, false, false)]
    public void Only_the_crystal_of_an_unlocked_teleporter_glows(string part, bool unlocked, bool glows)
    {
        Assert.Equal(glows, TeleporterLook.Glows(part, unlocked));
    }

    [Fact]
    public void Every_place_has_its_own_english_name()
    {
        var names = Enum.GetValues<SilverAndAsh.Core.World.TeleporterSite>().Select(TeleporterLook.PlaceName).ToList();

        Assert.Equal(["Start Hall", "Boss Gate"], names);
    }

    private static readonly VectorComparer VectorTolerance = new();

    private static string Model(string file)
    {
        var path = RepositoryFiles.Find($"assets/models/{file}");
        Assert.True(File.Exists(path), $"{path} is missing.");
        return path;
    }

    // The corner indices of each part, three per triangle.
    private static Dictionary<string, int[]> Triangles(byte[] file)
    {
        var jsonLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(12));
        using var json = JsonDocument.Parse(Encoding.UTF8.GetString(file, 20, jsonLength));
        var root = json.RootElement;
        var binary = 20 + jsonLength + 8;
        var parts = new Dictionary<string, int[]>();
        foreach (var node in root.GetProperty("nodes").EnumerateArray())
        {
            var mesh = root.GetProperty("meshes")[node.GetProperty("mesh").GetInt32()];
            var accessor = root.GetProperty("accessors")[mesh.GetProperty("primitives")[0].GetProperty("indices").GetInt32()];
            var view = root.GetProperty("bufferViews")[accessor.GetProperty("bufferView").GetInt32()];
            var start = binary + Offset(view) + Offset(accessor);
            var wide = accessor.GetProperty("componentType").GetInt32() == 5125;
            var indices = new int[accessor.GetProperty("count").GetInt32()];
            for (var i = 0; i < indices.Length; i++)
            {
                indices[i] = wide
                    ? (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(start + (4 * i)))
                    : BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(start + (2 * i)));
            }

            parts[node.GetProperty("name").GetString()!] = indices;
        }

        return parts;
    }

    private static int Offset(JsonElement element) => element.TryGetProperty("byteOffset", out var o) ? o.GetInt32() : 0;

    private sealed class VectorComparer : IEqualityComparer<Vector3>
    {
        public bool Equals(Vector3 x, Vector3 y) => Vector3.Distance(x, y) < Tolerance;

        public int GetHashCode(Vector3 obj) => 0;
    }
}
