using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// The crossbow skeleton (issue #497, model "F flach" of issue #334) as tools/models/crossbow_skeleton.py builds it: one
/// model per pose, five parts each, closed all round; red only in the eyes and only while it announces a shot.
/// </summary>
public class CrossbowSkeletonLookTests
{
    private static readonly string[] PartNames = ["skelett", "armbrust", "ausruestung", "stoff", "sehne"];

    // tools/models/crossbow_skeleton.py: MAX_TRIANGLES (the budget the approved probe uses up exactly).
    private const int Triangles = 1650;

    // tools/models/crossbow_skeleton.py: RECTS_PX["skull"] = (0, 80, 64, 128) in the 128 x 128 picture, y counted from
    // the bottom (psx.rects_from_pixels). Rows of the PNG count from the top: rows 0 to 47.
    private const int SkullX0 = 0;
    private const int SkullY0 = 128 - 128;
    private const int SkullX1 = 64;
    private const int SkullY1 = 128 - 80;

    public static TheoryData<CrossbowSkeletonStance> BothStances() => new(Enum.GetValues<CrossbowSkeletonStance>());

    [Theory]
    [MemberData(nameof(BothStances))]
    public void Every_file_has_the_five_parts_of_the_look_and_none_moves(CrossbowSkeletonStance stance)
    {
        var parts = GlbScene.Read(File.ReadAllBytes(Model(CrossbowSkeletonLook.FileName(stance))));

        // The file keeps the parts sorted by name; the game finds them by name (TexturedModel.TryLoad).
        Assert.Equal(PartNames.Order(), parts.Select(part => part.Name).Order());
        Assert.Equal(PartNames, CrossbowSkeletonLook.Parts.Select(part => part.Name));
        Assert.All(CrossbowSkeletonLook.Parts, part => Assert.Null(part.Parent));
        Assert.All(parts, part => Assert.Equal(Vector3.Zero, part.Pivot));
    }

    [Fact]
    public void Each_pose_has_its_own_file()
    {
        var files = Enum.GetValues<CrossbowSkeletonStance>().Select(CrossbowSkeletonLook.FileName).ToList();

        Assert.Equal(files.Count, files.Distinct().Count());
        Assert.All(files, file => Assert.True(File.Exists(Model(file))));
    }

    [Theory]
    [MemberData(nameof(BothStances))]
    public void The_figure_stands_on_the_floor_about_as_high_as_the_look_says(CrossbowSkeletonStance stance)
    {
        var corners = GlbVertices.Read(File.ReadAllBytes(Model(CrossbowSkeletonLook.FileName(stance)))).Values.SelectMany(part => part).ToList();

        // The feet: never below the floor, at most 3 cm above it (the approved model's lowest corner lies 2.5 cm up).
        Assert.InRange(corners.Min(corner => corner.Y), 0f, 0.03f);
        Assert.Equal(CrossbowSkeletonLook.Height, corners.Max(corner => corner.Y), 0.05f);
    }

    [Theory]
    [MemberData(nameof(BothStances))]
    public void Every_file_has_1650_triangles_and_every_part_is_closed_all_round(CrossbowSkeletonStance stance)
    {
        var bytes = File.ReadAllBytes(Model(CrossbowSkeletonLook.FileName(stance)));
        var triangles = TrianglesOf(bytes);
        var corners = GlbVertices.Read(bytes);

        Assert.Equal(Triangles, triangles.Values.Sum(indices => indices.Length / 3));
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

            Assert.All(edges, edge => Assert.True(edge.Value == 2, $"{stance} {name}: edge {edge.Key} has {edge.Value} faces."));
        }
    }

    [Fact]
    public void The_pose_changes_where_the_crossbow_is()
    {
        var rest = GlbVertices.Read(File.ReadAllBytes(Model(CrossbowSkeletonLook.FileName(CrossbowSkeletonStance.Rest))));
        var announce = GlbVertices.Read(File.ReadAllBytes(Model(CrossbowSkeletonLook.FileName(CrossbowSkeletonStance.Announce))));

        // Raised to the shoulder when it announces a shot: the crossbow's middle is higher than at rest.
        static float Middle(IEnumerable<Vector3> corners) => corners.Average(corner => corner.Y);
        Assert.True(Middle(announce["armbrust"]) > Middle(rest["armbrust"]) + 0.2f);
    }

    [Theory]
    [InlineData(CrossbowSkeletonStance.Rest, false)]
    [InlineData(CrossbowSkeletonStance.Announce, true)]
    public void Blood_red_is_only_in_the_skull_and_only_while_it_announces(CrossbowSkeletonStance stance, bool red)
    {
        var blood = BloodRed();
        var (width, height, rgba) = TestPng.Read(Model(Path.ChangeExtension(CrossbowSkeletonLook.FileName(stance), ".png")));
        var found = new List<(int X, int Y)>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var at = ((y * width) + x) * 4;
                if (blood.Contains((rgba[at], rgba[at + 1], rgba[at + 2])))
                {
                    found.Add((x, y));
                }
            }
        }

        Assert.Equal((128, 128), (width, height));
        Assert.Equal(red, found.Count > 0);
        Assert.All(found, pixel => Assert.True(
            pixel.X >= SkullX0 && pixel.X < SkullX1 && pixel.Y >= SkullY0 && pixel.Y < SkullY1,
            $"Blood red at ({pixel.X}, {pixel.Y}) lies outside the skull."));
    }

    [Fact]
    public void Rest_and_announce_pictures_differ_only_in_the_skull()
    {
        var rest = TestPng.Read(Model(Path.ChangeExtension(CrossbowSkeletonLook.FileName(CrossbowSkeletonStance.Rest), ".png")));
        var announce = TestPng.Read(Model(Path.ChangeExtension(CrossbowSkeletonLook.FileName(CrossbowSkeletonStance.Announce), ".png")));

        Assert.Equal((rest.Width, rest.Height), (announce.Width, announce.Height));
        var differ = 0;
        for (var i = 0; i < rest.Rgba.Length; i += 4)
        {
            if (rest.Rgba.AsSpan(i, 4).SequenceEqual(announce.Rgba.AsSpan(i, 4)))
            {
                continue;
            }

            differ++;
            var (x, y) = ((i / 4) % rest.Width, (i / 4) / rest.Width);
            Assert.True(x >= SkullX0 && x < SkullX1 && y >= SkullY0 && y < SkullY1, $"The pictures differ at ({x}, {y}), outside the skull.");
        }

        Assert.True(differ > 0);
    }

    // The colours of "Blutrot" in the main palette (assets/palette/hauptpalette.json).
    private static HashSet<(byte, byte, byte)> BloodRed()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(RepositoryFiles.Find("assets/palette/hauptpalette.json")));
        var ramp = new[] { "grund", "akzent" }
            .SelectMany(group => json.RootElement.GetProperty(group).EnumerateArray())
            .Single(entry => entry.GetProperty("name").GetString() == "Blutrot");
        return ramp.GetProperty("farben").EnumerateArray()
            .Select(colour => colour.EnumerateArray().Select(channel => (byte)Math.Round(channel.GetDouble())).ToArray())
            .Select(channels => (channels[0], channels[1], channels[2]))
            .ToHashSet();
    }

    private static string Model(string file)
    {
        var path = RepositoryFiles.Find($"assets/models/{file}");
        Assert.True(File.Exists(path), $"{path} is missing.");
        return path;
    }

    // The corner indices of each part, three per triangle.
    private static Dictionary<string, int[]> TrianglesOf(byte[] file)
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
}
