using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// The corners (vertex positions) of each part of a .glb file, around the part's pivot - for tests that need to know
/// how far a part really reaches, not just its box.
/// </summary>
internal static class GlbVertices
{
    public static IReadOnlyDictionary<string, Vector3[]> Read(byte[] file)
    {
        var jsonLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(12));
        using var json = JsonDocument.Parse(Encoding.UTF8.GetString(file, 20, jsonLength));
        var root = json.RootElement;
        var binary = 20 + jsonLength + 8;
        var parts = new Dictionary<string, Vector3[]>();
        foreach (var node in root.GetProperty("nodes").EnumerateArray())
        {
            var mesh = root.GetProperty("meshes")[node.GetProperty("mesh").GetInt32()];
            var accessor = root.GetProperty("accessors")[mesh.GetProperty("primitives")[0].GetProperty("attributes").GetProperty("POSITION").GetInt32()];
            var view = root.GetProperty("bufferViews")[accessor.GetProperty("bufferView").GetInt32()];
            var start = binary + Offset(view) + Offset(accessor);
            var stride = view.TryGetProperty("byteStride", out var s) ? s.GetInt32() : 12;
            var corners = new Vector3[accessor.GetProperty("count").GetInt32()];
            for (var i = 0; i < corners.Length; i++)
            {
                var at = file.AsSpan(start + (i * stride));
                corners[i] = new Vector3(
                    BinaryPrimitives.ReadSingleLittleEndian(at),
                    BinaryPrimitives.ReadSingleLittleEndian(at[4..]),
                    BinaryPrimitives.ReadSingleLittleEndian(at[8..]));
            }

            parts[node.GetProperty("name").GetString()!] = corners;
        }

        return parts;
    }

    private static int Offset(JsonElement element) => element.TryGetProperty("byteOffset", out var o) ? o.GetInt32() : 0;
}
