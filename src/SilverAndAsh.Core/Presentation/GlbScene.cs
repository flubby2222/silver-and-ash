using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace SilverAndAsh.Core.Presentation;

/// <summary>One part of a model file: a glTF node with a mesh.</summary>
/// <param name="Name">The name the designer gave it ("jaw").</param>
/// <param name="Pivot">The node's translation - its pivot, in metres.</param>
/// <param name="VertexCount">Number of corners of its mesh.</param>
/// <param name="PositionMin">Smallest corner position of its mesh, relative to the pivot (the "min" of the file).</param>
public sealed record GlbPart(string Name, Vector3 Pivot, int VertexCount, Vector3 PositionMin);

/// <summary>
/// Reads the description (the JSON chunk) of a binary glTF file (.glb): which parts it has, in which order, and where
/// their pivots are. Raylib loads the meshes and the texture but forgets the names and pivots - this fills the gap.
/// Only what the designer's models use is accepted: nodes without children, rotation, scale or matrix, one mesh each
/// with one primitive (one list of triangles, with indices).
/// </summary>
public static class GlbScene
{
    private const uint Magic = 0x46546C67;     // "glTF"
    private const uint JsonChunk = 0x4E4F534A; // "JSON"

    /// <summary>Reads the parts of a .glb file, in the order of its nodes (the order raylib loads the meshes in).</summary>
    /// <exception cref="InvalidDataException">Not a .glb file, or one that uses more than this game understands.</exception>
    public static IReadOnlyList<GlbPart> Read(ReadOnlySpan<byte> file)
    {
        if (file.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(file) != Magic)
        {
            throw new InvalidDataException("Not a binary glTF file (.glb).");
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(file[4..]) != 2)
        {
            throw new InvalidDataException("Only glTF version 2 is supported.");
        }

        // The header says how long the whole file is. A file cut off while copying must not reach raylib, which would
        // read its corners from missing data.
        if (BinaryPrimitives.ReadUInt32LittleEndian(file[8..]) != file.Length)
        {
            throw new InvalidDataException($"The .glb file is {file.Length} bytes long, its header says {BinaryPrimitives.ReadUInt32LittleEndian(file[8..])}.");
        }

        var jsonLength = BinaryPrimitives.ReadUInt32LittleEndian(file[12..]);
        if (BinaryPrimitives.ReadUInt32LittleEndian(file[16..]) != JsonChunk || jsonLength > file.Length - 20)
        {
            throw new InvalidDataException("The .glb file has no readable JSON chunk.");
        }

        try
        {
            using var json = JsonDocument.Parse(Encoding.UTF8.GetString(file.Slice(20, (int)jsonLength)));
            return ReadParts(json.RootElement);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException
            or IndexOutOfRangeException or FormatException)
        {
            throw new InvalidDataException($"The .glb description is broken: {exception.Message}", exception);
        }
    }

    private static List<GlbPart> ReadParts(JsonElement root)
    {
        var meshes = root.GetProperty("meshes");
        var accessors = root.GetProperty("accessors");
        var parts = new List<GlbPart>();
        foreach (var node in root.GetProperty("nodes").EnumerateArray())
        {
            if (!node.TryGetProperty("mesh", out var meshIndex))
            {
                continue;
            }

            foreach (var unsupported in (ReadOnlySpan<string>)["children", "rotation", "scale", "matrix"])
            {
                if (node.TryGetProperty(unsupported, out _))
                {
                    throw new InvalidDataException($"A node uses '{unsupported}' - the game only knows parts that are moved, not turned or nested.");
                }
            }

            var name = node.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty;
            var pivot = node.TryGetProperty("translation", out var translation) ? Vector(translation) : Vector3.Zero;

            // Raylib makes one mesh per primitive; one per part keeps meshes and parts in step.
            var primitives = meshes[meshIndex.GetInt32()].GetProperty("primitives");
            if (primitives.GetArrayLength() != 1)
            {
                throw new InvalidDataException($"The part '{name}' has {primitives.GetArrayLength()} primitives - the game needs exactly one.");
            }

            var primitive = primitives[0];
            if (!primitive.TryGetProperty("indices", out _))
            {
                throw new InvalidDataException($"The part '{name}' has no indices (list of triangles).");
            }

            var accessor = accessors[primitive.GetProperty("attributes").GetProperty("POSITION").GetInt32()];
            var vertexCount = accessor.GetProperty("count").GetInt32();
            var min = Vector(accessor.GetProperty("min"));

            parts.Add(new GlbPart(name, pivot, vertexCount, min));
        }

        return parts;
    }

    private static Vector3 Vector(JsonElement array) =>
        new(array[0].GetSingle(), array[1].GetSingle(), array[2].GetSingle());
}
