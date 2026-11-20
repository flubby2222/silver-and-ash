using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// The crypt warden (issue #530, model "K1" of issue #335) as tools/models/crypt_warden.py builds it: one model per pose,
/// nine parts each, closed all round, under 3000 triangles; red only in the eye and only while it announces its blow.
/// </summary>
public class CryptWardenLookTests
{
    private static readonly string[] PartNames = ["leg_l", "leg_r", "torso", "head", "eyes", "arm_l", "arm_r", "gear", "pole"];

    // tools/models/crypt_warden.py: MAX_TRIANGLES.
    private const int MaxTriangles = 3000;

    // The eye's field in the 128 x 128 picture (tools/models/crypt_warden.py, field "eye": column 3, row 2 of the 32 x 32
    // fields, rows counted from the bottom): x 96 to 128, y 64 to 96 from the bottom, i.e. rows 32 to 64 of the PNG.
    private const int EyeX0 = 96;
    private const int EyeY0 = 32;
    private const int EyeX1 = 128;
    private const int EyeY1 = 64;

    public static TheoryData<CryptWardenStance> BothStances() => new(Enum.GetValues<CryptWardenStance>());

    [Theory]
    [MemberData(nameof(BothStances))]
    public void Every_file_has_the_nine_parts_of_the_look_and_none_moves(CryptWardenStance stance)
    {
        var parts = GlbScene.Read(File.ReadAllBytes(Model(CryptWardenLook.FileName(stance))));

        // The file keeps the parts sorted by name; the game finds them by name (TexturedModel.TryLoad).
        Assert.Equal(PartNames.Order(), parts.Select(part => part.Name).Order());
        Assert.Equal(PartNames, CryptWardenLook.Parts.Select(part => part.Name));
        Assert.All(CryptWardenLook.Parts, part => Assert.Null(part.Parent));
        Assert.All(parts, part => Assert.Equal(Vector3.Zero, part.Pivot));
    }

    [Fact]
    public void Each_pose_has_its_own_file()
    {
        var files = Enum.GetValues<CryptWardenStance>().Select(CryptWardenLook.FileName).ToList();

        Assert.Equal(files.Count, files.Distinct().Count());
        Assert.All(files, file => Assert.True(File.Exists(Model(file))));
    }

    [Theory]
    [MemberData(nameof(BothStances))]
    public void The_figure_stands_on_the_floor_and_is_about_as_high_as_the_look_says(CryptWardenStance stance)
    {
        var corners = Corners(stance);

        // The feet: never below the floor, at most 3 cm above it.
        Assert.InRange(corners.Min(corner => corner.Y), 0f, 0.03f);

        // The top: the label above it is placed from Height (the tip of the propped-up pole; the raised pole reaches about as high).
        Assert.InRange(corners.Max(corner => corner.Y), CryptWardenLook.Height - 0.15f, CryptWardenLook.Height + 0.15f);
    }

    [Theory]
    [MemberData(nameof(BothStances))]
    public void Every_file_has_fewer_than_3000_triangles_and_every_part_is_closed_all_round(CryptWardenStance stance)
    {
        var bytes = File.ReadAllBytes(Model(CryptWardenLook.FileName(stance)));
        var triangles = TrianglesOf(bytes);
        var corners = GlbVertices.Read(bytes);

        Assert.InRange(triangles.Values.Sum(indices => indices.Length / 3), 1, MaxTriangles - 1);
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

    [Theory]
    [MemberData(nameof(BothStances))]
    public void Every_point_of_the_model_lies_inside_the_bounding_radius(CryptWardenStance stance)
    {
        // The view cone culls the figure by a sphere around its middle at half height: a point outside would pop out of view.
        var middle = new Vector3(0f, CryptWardenLook.Height / 2f, 0f);
        var farthest = Corners(stance).MaxBy(corner => Vector3.Distance(corner, middle));

        Assert.True(
            Vector3.Distance(farthest, middle) <= CryptWardenLook.BoundingRadius,
            $"{stance}: the point {farthest} is {Vector3.Distance(farthest, middle):0.00} m from the middle, outside the radius {CryptWardenLook.BoundingRadius} m.");
    }

    [Fact]
    public void The_bounding_radius_is_not_much_wider_than_the_raised_pole_needs()
    {
        // The counter-proof for the test above: the raised pole reaches beyond 2 m, so a radius of 2 m would be red - and
        // the radius is no more than 0.3 m wider than the farthest point, so it culls close to the figure.
        var middle = new Vector3(0f, CryptWardenLook.Height / 2f, 0f);
        var reach = Enum.GetValues<CryptWardenStance>().SelectMany(Corners).Max(corner => Vector3.Distance(corner, middle));

        Assert.True(reach > 2f);
        Assert.InRange(CryptWardenLook.BoundingRadius - reach, 0f, 0.3f);
    }

    [Fact]
    public void The_pose_changes_where_the_pole_is()
    {
        var rest = GlbVertices.Read(File.ReadAllBytes(Model(CryptWardenLook.FileName(CryptWardenStance.Rest))));
        var announce = GlbVertices.Read(File.ReadAllBytes(Model(CryptWardenLook.FileName(CryptWardenStance.Announce))));

        // Raised for the blow when it announces: the pole's middle is higher than propped up at rest.
        static float Middle(IEnumerable<Vector3> corners) => corners.Average(corner => corner.Y);
        Assert.True(Middle(announce["pole"]) > Middle(rest["pole"]) + 0.2f);
    }

    [Theory]
    [InlineData(CryptWardenStance.Rest, false)]
    [InlineData(CryptWardenStance.Announce, true)]
    public void Blood_red_is_only_in_the_eye_and_only_while_it_announces(CryptWardenStance stance, bool red)
    {
        var blood = BloodRed();
        var (width, height, rgba) = TestPng.Read(Model(Path.ChangeExtension(CryptWardenLook.FileName(stance), ".png")));
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
            pixel.X >= EyeX0 && pixel.X < EyeX1 && pixel.Y >= EyeY0 && pixel.Y < EyeY1,
            $"Blood red at ({pixel.X}, {pixel.Y}) lies outside the eye."));
    }

    [Fact]
    public void Rest_and_announce_pictures_differ_only_in_the_eye()
    {
        var rest = TestPng.Read(Model(Path.ChangeExtension(CryptWardenLook.FileName(CryptWardenStance.Rest), ".png")));
        var announce = TestPng.Read(Model(Path.ChangeExtension(CryptWardenLook.FileName(CryptWardenStance.Announce), ".png")));

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
            Assert.True(x >= EyeX0 && x < EyeX1 && y >= EyeY0 && y < EyeY1, $"The pictures differ at ({x}, {y}), outside the eye.");
        }

        Assert.True(differ > 0);
    }

    private static List<Vector3> Corners(CryptWardenStance stance) =>
        GlbVertices.Read(File.ReadAllBytes(Model(CryptWardenLook.FileName(stance)))).Values.SelectMany(part => part).ToList();

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
