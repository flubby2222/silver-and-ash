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
    private const uint BinaryChunk = 0x004E4942; // "BIN"

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

        // The binary chunk follows the JSON chunk: its 8-byte head, then the data the corners and the texture are read from.
        var binaryLength = 0L;
        var binaryStart = 20L + jsonLength;
        if (binaryStart + 8 <= file.Length)
        {
            var length = BinaryPrimitives.ReadUInt32LittleEndian(file[(int)binaryStart..]);
            if (BinaryPrimitives.ReadUInt32LittleEndian(file[((int)binaryStart + 4)..]) != BinaryChunk
                || binaryStart + 8 + length > file.Length)
            {
                throw new InvalidDataException("The binary chunk of the .glb file is missing or longer than the file.");
            }

            binaryLength = length;
        }

        try
        {
            using var json = JsonDocument.Parse(Encoding.UTF8.GetString(file.Slice(20, (int)jsonLength)));
            CheckData(json.RootElement, binaryLength);
            return ReadParts(json.RootElement);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException
            or IndexOutOfRangeException or FormatException)
        {
            throw new InvalidDataException($"The .glb description is broken: {exception.Message}", exception);
        }
    }

    // Raylib (cgltf) reads corners, indices and the texture straight from the data the description points to - and on
    // data that is not there it crashes in native code, where no catch helps. So every place the description points to
    // must lie inside the binary chunk before raylib sees the file.
    private static void CheckData(JsonElement root, long binaryLength)
    {
        var buffers = root.TryGetProperty("buffers", out var b) ? b : default;
        var bufferCount = buffers.ValueKind == JsonValueKind.Array ? buffers.GetArrayLength() : 0;
        if (bufferCount != 1)
        {
            throw new InvalidDataException($"The .glb file has {bufferCount} buffers - the game needs exactly one, in the file.");
        }

        var buffer = buffers[0];
        if (buffer.TryGetProperty("uri", out _))
        {
            throw new InvalidDataException("The buffer lies in another file - the game only reads data inside the .glb.");
        }

        var bufferLength = buffer.GetProperty("byteLength").GetInt64();
        if (bufferLength < 0 || bufferLength > binaryLength)
        {
            throw new InvalidDataException($"The buffer claims {bufferLength} bytes, the binary chunk has {binaryLength}.");
        }

        var views = root.TryGetProperty("bufferViews", out var v) ? v : default;
        var viewLengths = new List<long>();
        if (views.ValueKind == JsonValueKind.Array)
        {
            foreach (var view in views.EnumerateArray())
            {
                var offset = view.TryGetProperty("byteOffset", out var o) ? o.GetInt64() : 0L;
                var length = view.GetProperty("byteLength").GetInt64();
                if (view.GetProperty("buffer").GetInt32() != 0 || offset < 0 || length < 0 || offset + length > bufferLength)
                {
                    throw new InvalidDataException($"A buffer view ({offset} + {length} bytes) lies outside the buffer ({bufferLength} bytes).");
                }

                viewLengths.Add(length);
            }
        }

        foreach (var accessor in root.GetProperty("accessors").EnumerateArray())
        {
            if (!accessor.TryGetProperty("bufferView", out var viewIndex))
            {
                throw new InvalidDataException("An accessor has no data in the file.");
            }

            var view = viewIndex.GetInt32();
            if (view < 0 || view >= viewLengths.Count)
            {
                throw new InvalidDataException($"An accessor points to buffer view {view}, the file has {viewLengths.Count}.");
            }

            var offset = accessor.TryGetProperty("byteOffset", out var o) ? o.GetInt64() : 0L;
            var count = accessor.GetProperty("count").GetInt64();
            var element = ComponentSize(accessor.GetProperty("componentType").GetInt32()) * Components(accessor.GetProperty("type").GetString());
            var stride = views[view].TryGetProperty("byteStride", out var s) ? s.GetInt64() : element;
            if (stride < element || stride > viewLengths[view])
            {
                throw new InvalidDataException($"A buffer view has a stride of {stride} bytes, its elements need {element}.");
            }

            var needed = count == 0 ? 0 : offset + (count - 1) * stride + element;
            if (offset < 0 || count < 0 || count > viewLengths[view] || needed > viewLengths[view])
            {
                throw new InvalidDataException($"An accessor needs {needed} bytes, its buffer view has {viewLengths[view]}.");
            }
        }
    }

    private static long ComponentSize(int componentType) => componentType switch
    {
        5120 or 5121 => 1, // byte, unsigned byte
        5122 or 5123 => 2, // short, unsigned short
        5125 or 5126 => 4, // unsigned int, float
        _ => throw new InvalidDataException($"Unknown component type {componentType}."),
    };

    private static long Components(string? type) => type switch
    {
        "SCALAR" => 1,
        "VEC2" => 2,
        "VEC3" => 3,
        "VEC4" => 4,
        "MAT2" => 4,
        "MAT3" => 9,
        "MAT4" => 16,
        _ => throw new InvalidDataException($"Unknown accessor type '{type}'."),
    };

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
