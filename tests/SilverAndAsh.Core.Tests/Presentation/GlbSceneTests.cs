using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

public class GlbSceneTests
{
    private const string TwoParts = """
        {
          "asset": { "version": "2.0" },
          "nodes": [
            { "name": "head", "mesh": 0, "translation": [0, 0.68, 0.24] },
            { "name": "camera" },
            { "name": "jaw", "mesh": 1 }
          ],
          "meshes": [
            { "primitives": [ { "attributes": { "POSITION": 0 }, "indices": 2 } ] },
            { "primitives": [ { "attributes": { "POSITION": 1 }, "indices": 3 } ] }
          ],
          "accessors": [
            { "bufferView": 0, "componentType": 5126, "type": "VEC3", "count": 86, "min": [-0.1, 0.04, -0.01], "max": [0.1, 0.25, 0.19] },
            { "bufferView": 1, "componentType": 5126, "type": "VEC3", "count": 33, "min": [-0.05, -0.07, -0.04], "max": [0, 0, 0] },
            { "bufferView": 2, "componentType": 5123, "type": "SCALAR", "count": 432 },
            { "bufferView": 3, "componentType": 5123, "type": "SCALAR", "count": 108 }
          ],
          "bufferViews": [
            { "buffer": 0, "byteOffset": 0, "byteLength": 1032 },
            { "buffer": 0, "byteOffset": 1032, "byteLength": 396 },
            { "buffer": 0, "byteOffset": 1428, "byteLength": 864 },
            { "buffer": 0, "byteOffset": 2292, "byteLength": 216 }
          ],
          "buffers": [ { "byteLength": 2508 } ]
        }
        """;

    // Bytes of data the description above points to (86 + 33 corners of 12 bytes, 432 + 108 indices of 2 bytes).
    private const int DataLength = 2508;

    [Fact]
    public void A_whole_file_with_its_data_is_read()
    {
        Assert.Equal(2, GlbScene.Read(Glb(TwoParts)).Count);
    }

    [Fact]
    public void The_real_ghoul_model_passes_every_check()
    {
        Assert.Equal(11, GlbScene.Read(File.ReadAllBytes(RepositoryFiles.Find("assets/models/ghoul.glb"))).Count);
    }

    [Fact]
    public void A_buffer_longer_than_the_binary_chunk_is_refused()
    {
        // The reviewer's case: one digit more in the description, 4 bytes - raylib crashed on it.
        var longer = TwoParts.Replace("\"byteLength\": 2508 }", "\"byteLength\": 2512 }", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => GlbScene.Read(Glb(longer)));
    }

    [Fact]
    public void A_binary_chunk_with_half_the_data_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => GlbScene.Read(Glb(TwoParts, DataLength / 2)));
    }

    [Fact]
    public void A_file_without_binary_chunk_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => GlbScene.Read(Glb(TwoParts, dataLength: null)));
    }

    [Fact]
    public void A_buffer_in_another_file_is_refused()
    {
        var outside = TwoParts.Replace("{ \"byteLength\": 2508 }", "{ \"uri\": \"missing.bin\", \"byteLength\": 2508 }", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => GlbScene.Read(Glb(outside)));
    }

    [Fact]
    public void A_buffer_view_beyond_the_buffer_is_refused()
    {
        var beyond = TwoParts.Replace("\"byteOffset\": 2292, \"byteLength\": 216", "\"byteOffset\": 2400, \"byteLength\": 216", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => GlbScene.Read(Glb(beyond)));
    }

    [Fact]
    public void An_accessor_with_more_corners_than_its_view_holds_is_refused()
    {
        var more = TwoParts.Replace("\"count\": 33,", "\"count\": 34,", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => GlbScene.Read(Glb(more)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-12)]
    public void A_stride_smaller_than_one_corner_is_refused(int stride)
    {
        // The reviewer's case: stride 0 and a huge count passed the length check and crashed raylib.
        var strided = TwoParts
            .Replace("\"byteOffset\": 0, \"byteLength\": 1032", $"\"byteOffset\": 0, \"byteLength\": 1032, \"byteStride\": {stride}", StringComparison.Ordinal)
            .Replace("\"count\": 86,", "\"count\": 2000000000,", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => GlbScene.Read(Glb(strided)));
    }

    [Fact]
    public void An_accessor_without_data_is_refused()
    {
        var without = TwoParts.Replace("\"bufferView\": 3, ", string.Empty, StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => GlbScene.Read(Glb(without)));
    }

    [Fact]
    public void Reads_the_parts_with_mesh_in_file_order_with_name_pivot_and_corners()
    {
        var parts = GlbScene.Read(Glb(TwoParts));

        Assert.Equal(["head", "jaw"], parts.Select(part => part.Name));
        Assert.Equal(new Vector3(0f, 0.68f, 0.24f), parts[0].Pivot);
        Assert.Equal(Vector3.Zero, parts[1].Pivot);
        Assert.Equal(86, parts[0].VertexCount);
        Assert.Equal(33, parts[1].VertexCount);
        Assert.Equal(new Vector3(-0.05f, -0.07f, -0.04f), parts[1].PositionMin);
        Assert.Equal(new Vector3(0.1f, 0.25f, 0.19f), parts[0].PositionMax);
    }

    [Fact]
    public void A_file_that_is_no_glb_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => GlbScene.Read("not a model at all, really"u8));
    }

    [Fact]
    public void A_cut_off_file_is_refused()
    {
        var glb = Glb(TwoParts);

        Assert.Throws<InvalidDataException>(() => GlbScene.Read(glb.AsSpan(0, 40)));
    }

    [Fact]
    public void A_file_cut_off_after_its_description_is_refused()
    {
        // The description is complete, but the data behind it is missing: the header still says the old length.
        var glb = Glb(TwoParts);
        var longer = new byte[glb.Length + 400];
        glb.CopyTo(longer, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(longer.AsSpan(8), (uint)longer.Length + 1000);

        Assert.Throws<InvalidDataException>(() => GlbScene.Read(longer));
    }

    [Fact]
    public void Broken_json_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => GlbScene.Read(Glb("{ \"nodes\": [ { \"mesh\": ")));
    }

    [Fact]
    public void Turned_or_nested_parts_are_refused_instead_of_drawn_wrong()
    {
        var turned = TwoParts.Replace("\"translation\": [0, 0.68, 0.24]", "\"rotation\": [0, 0, 0, 1]", StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => GlbScene.Read(Glb(turned)));
    }

    [Fact]
    public void A_part_without_indices_is_refused()
    {
        var withoutIndices = TwoParts.Replace(", \"indices\": 3", string.Empty, StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => GlbScene.Read(Glb(withoutIndices)));
    }

    [Fact]
    public void A_part_made_of_two_primitives_is_refused()
    {
        var twoPrimitives = TwoParts.Replace(
            "{ \"attributes\": { \"POSITION\": 1 }, \"indices\": 3 }",
            "{ \"attributes\": { \"POSITION\": 1 }, \"indices\": 3 }, { \"attributes\": { \"POSITION\": 1 }, \"indices\": 3 }",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => GlbScene.Read(Glb(twoPrimitives)));
    }

    [Fact]
    public void A_file_without_buffers_is_refused()
    {
        var message = Refusal(root => root.AsObject().Remove("buffers"));

        Assert.Equal("The .glb file has 0 buffers - the game needs exactly one, in the file.", message);
    }

    [Fact]
    public void Buffers_that_are_no_list_are_refused()
    {
        var message = Refusal(root => root["buffers"] = new JsonObject { ["byteLength"] = DataLength });

        Assert.Equal("The .glb file has 0 buffers - the game needs exactly one, in the file.", message);
    }

    [Fact]
    public void A_file_with_two_buffers_is_refused()
    {
        var message = Refusal(root => root["buffers"]!.AsArray().Add(new JsonObject { ["byteLength"] = 4 }));

        Assert.Equal("The .glb file has 2 buffers - the game needs exactly one, in the file.", message);
    }

    [Fact]
    public void A_buffer_with_negative_length_is_refused()
    {
        var message = Refusal(root => root["buffers"]![0]!["byteLength"] = -1);

        Assert.Equal($"The buffer claims -1 bytes, the binary chunk has {DataLength}.", message);
    }

    [Fact]
    public void A_buffer_view_of_a_second_buffer_is_refused()
    {
        var message = Refusal(root => root["bufferViews"]![1]!["buffer"] = 1);

        Assert.Equal($"A buffer view (1032 + 396 bytes) lies outside the buffer ({DataLength} bytes).", message);
    }

    [Fact]
    public void A_buffer_view_before_the_buffer_is_refused()
    {
        // -4 + 1032 ends inside the buffer: only the check on the start catches it.
        var message = Refusal(root => root["bufferViews"]![0]!["byteOffset"] = -4);

        Assert.Equal($"A buffer view (-4 + 1032 bytes) lies outside the buffer ({DataLength} bytes).", message);
    }

    [Fact]
    public void A_buffer_view_with_negative_length_is_refused()
    {
        var message = Refusal(root => root["bufferViews"]![3]!["byteLength"] = -216);

        Assert.Equal($"A buffer view (2292 + -216 bytes) lies outside the buffer ({DataLength} bytes).", message);
    }

    [Fact]
    public void A_buffer_view_whose_end_overflows_is_refused()
    {
        // Offset and length are each far beyond the buffer; their sum does not fit a long and wraps to a negative number.
        var message = Refusal(root =>
        {
            root["bufferViews"]![0]!["byteOffset"] = 9_000_000_000_000_000_000L;
            root["bufferViews"]![0]!["byteLength"] = 9_000_000_000_000_000_000L;
        });

        Assert.Equal($"A buffer view (9000000000000000000 + 9000000000000000000 bytes) lies outside the buffer ({DataLength} bytes).", message);
    }

    [Fact]
    public void A_buffer_view_without_offset_starts_at_the_beginning_of_the_buffer()
    {
        // The whole buffer fits only if the missing offset counts as 0.
        var root = JsonNode.Parse(TwoParts)!;
        root["bufferViews"]![0]!.AsObject().Remove("byteOffset");
        root["bufferViews"]![0]!["byteLength"] = DataLength;

        Assert.Equal(2, GlbScene.Read(Glb(root.ToJsonString())).Count);
    }

    [Fact]
    public void Accessors_without_any_buffer_views_are_refused()
    {
        var message = Refusal(root => root.AsObject().Remove("bufferViews"));

        Assert.Equal("An accessor points to buffer view 0, the file has 0.", message);
    }

    [Fact]
    public void Buffer_views_that_are_no_list_are_refused()
    {
        var message = Refusal(root => root["bufferViews"] = new JsonObject());

        Assert.Equal("An accessor points to buffer view 0, the file has 0.", message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void An_accessor_pointing_to_a_missing_buffer_view_is_refused(int view)
    {
        var message = Refusal(root => root["accessors"]![3]!["bufferView"] = view);

        Assert.Equal($"An accessor points to buffer view {view}, the file has 4.", message);
    }

    [Fact]
    public void A_stride_longer_than_its_buffer_view_is_refused()
    {
        var message = Refusal(root => root["bufferViews"]![1]!["byteStride"] = 400);

        Assert.Equal("A buffer view has a stride of 400 bytes, its elements need 12.", message);
    }

    [Fact]
    public void An_accessor_offset_pushing_its_corners_past_the_view_is_refused()
    {
        var message = Refusal(root => root["accessors"]![0]!["byteOffset"] = 12);

        Assert.Equal("An accessor needs 1044 bytes, its buffer view has 1032.", message);
    }

    [Fact]
    public void An_accessor_starting_before_its_view_is_refused()
    {
        // -12 + 86 corners of 12 bytes ends inside the view: only the check on the start catches it.
        var message = Refusal(root => root["accessors"]![0]!["byteOffset"] = -12);

        Assert.Equal("An accessor needs 1020 bytes, its buffer view has 1032.", message);
    }

    [Fact]
    public void An_accessor_offset_that_overflows_is_refused()
    {
        // The largest offset a long holds: offset plus the corners wraps to a negative number of needed bytes.
        var message = Refusal(root => root["accessors"]![0]!["byteOffset"] = long.MaxValue);

        Assert.EndsWith("its buffer view has 1032.", message);
    }

    [Fact]
    public void An_accessor_with_negative_count_is_refused()
    {
        var message = Refusal(root => root["accessors"]![2]!["count"] = -1);

        Assert.Equal("An accessor needs -2 bytes, its buffer view has 864.", message);
    }

    [Fact]
    public void An_accessor_count_that_overflows_is_refused()
    {
        // (2^62 + 1 - 1) indices of 2 bytes are 2^63 bytes - one more than a long holds, so "needed" wraps around.
        var message = Refusal(root => root["accessors"]![2]!["count"] = (1L << 62) + 1);

        Assert.EndsWith("its buffer view has 864.", message);
    }

    [Fact]
    public void An_accessor_with_no_elements_needs_no_data()
    {
        var root = JsonNode.Parse(TwoParts)!;
        root["accessors"]![1]!["count"] = 0;

        var parts = GlbScene.Read(Glb(root.ToJsonString()));

        Assert.Equal(0, parts[1].VertexCount);
    }

    [Theory]
    [InlineData("SCALAR", 5120, 1)]
    [InlineData("SCALAR", 5121, 1)]
    [InlineData("VEC2", 5122, 4)]
    [InlineData("VEC3", 5123, 6)]
    [InlineData("VEC4", 5125, 16)]
    [InlineData("MAT2", 5126, 16)]
    [InlineData("MAT3", 5126, 36)]
    [InlineData("MAT4", 5126, 64)]
    public void Each_accessor_type_needs_its_own_number_of_bytes(string type, int componentType, int element)
    {
        // A stride of exactly one element passes, one byte less is refused - that pins the size the reader computes.
        Assert.Equal(2, GlbScene.Read(WithExtraAccessor(type, componentType, stride: element)).Count);

        var refused = Assert.Throws<InvalidDataException>(() => GlbScene.Read(WithExtraAccessor(type, componentType, stride: element - 1)));
        Assert.Equal($"A buffer view has a stride of {element - 1} bytes, its elements need {element}.", refused.Message);
    }

    [Theory]
    [InlineData("VEC5", "Unknown accessor type 'VEC5'.")]
    [InlineData(null, "Unknown accessor type ''.")]
    public void An_unknown_accessor_type_is_refused(string? type, string expected)
    {
        var message = Refusal(root => root["accessors"]![3]!["type"] = type);

        Assert.Equal(expected, message);
    }

    [Fact]
    public void An_unknown_component_type_is_refused()
    {
        var message = Refusal(root => root["accessors"]![3]!["componentType"] = 5124);

        Assert.Equal("Unknown component type 5124.", message);
    }

    [Fact]
    public void A_buffer_in_another_file_is_refused_with_its_reason()
    {
        var message = Refusal(root => root["buffers"]![0]!["uri"] = "missing.bin");

        Assert.Equal("The buffer lies in another file - the game only reads data inside the .glb.", message);
    }

    [Fact]
    public void A_buffer_longer_than_the_binary_chunk_is_refused_with_both_lengths()
    {
        var message = Refusal(root => root["buffers"]![0]!["byteLength"] = DataLength + 4);

        Assert.Equal($"The buffer claims {DataLength + 4} bytes, the binary chunk has {DataLength}.", message);
    }

    [Fact]
    public void A_buffer_in_a_file_without_binary_chunk_is_refused_with_an_empty_chunk()
    {
        // Without a binary chunk there is no data at all: the buffer is measured against 0 bytes, not left unchecked.
        var refused = Assert.Throws<InvalidDataException>(() => GlbScene.Read(Glb(TwoParts, dataLength: null)));

        Assert.Equal($"The buffer claims {DataLength} bytes, the binary chunk has 0.", refused.Message);
    }

    [Fact]
    public void An_accessor_without_data_is_refused_with_its_reason()
    {
        var message = Refusal(root => root["accessors"]![3]!.AsObject().Remove("bufferView"));

        Assert.Equal("An accessor has no data in the file.", message);
    }

    [Fact]
    public void An_accessor_with_more_corners_than_its_view_holds_is_refused_with_the_bytes_it_needs()
    {
        // 34 corners of 12 bytes need 408 bytes, the view holds 396 (33 corners).
        var message = Refusal(root => root["accessors"]![1]!["count"] = 34);

        Assert.Equal("An accessor needs 408 bytes, its buffer view has 396.", message);
    }

    // A description that leaves out a value the checks need: the reader names the file as broken instead of
    // letting the KeyNotFoundException of System.Text.Json escape.
    [Theory]
    [InlineData("buffers", 0, "byteLength")]
    [InlineData("bufferViews", 2, "byteLength")]
    [InlineData("bufferViews", 2, "buffer")]
    [InlineData("accessors", 2, "count")]
    [InlineData("accessors", 2, "componentType")]
    [InlineData("accessors", 2, "type")]
    public void A_missing_value_is_refused_as_a_broken_description(string list, int index, string value)
    {
        var refused = BrokenDescription(root => root[list]![index]!.AsObject().Remove(value));

        Assert.IsType<KeyNotFoundException>(refused.InnerException);
    }

    [Fact]
    public void A_file_without_accessors_is_refused_as_a_broken_description()
    {
        var refused = BrokenDescription(root => root.AsObject().Remove("accessors"));

        Assert.IsType<KeyNotFoundException>(refused.InnerException);
    }

    // A value of the wrong kind (text instead of a number, a number instead of an object or a list).
    [Theory]
    [InlineData("buffers", 0, "byteLength", "2508")]
    [InlineData("bufferViews", 1, "byteOffset", "1032")]
    [InlineData("bufferViews", 1, "byteLength", "396")]
    [InlineData("bufferViews", 1, "buffer", "0")]
    [InlineData("bufferViews", 1, "byteStride", "12")]
    [InlineData("accessors", 1, "bufferView", "1")]
    [InlineData("accessors", 1, "byteOffset", "0")]
    [InlineData("accessors", 1, "count", "33")]
    [InlineData("accessors", 1, "componentType", "5126")]
    public void A_number_written_as_text_is_refused_as_a_broken_description(string list, int index, string value, string text)
    {
        var refused = BrokenDescription(root => root[list]![index]![value] = text);

        Assert.IsType<InvalidOperationException>(refused.InnerException);
    }

    [Fact]
    public void An_accessor_type_written_as_a_number_is_refused_as_a_broken_description()
    {
        var refused = BrokenDescription(root => root["accessors"]![1]!["type"] = 3);

        Assert.IsType<InvalidOperationException>(refused.InnerException);
    }

    [Fact]
    public void A_buffer_that_is_no_object_is_refused_as_a_broken_description()
    {
        var refused = BrokenDescription(root => root["buffers"]![0] = DataLength);

        Assert.IsType<InvalidOperationException>(refused.InnerException);
    }

    [Fact]
    public void A_buffer_view_that_is_no_object_is_refused_as_a_broken_description()
    {
        var refused = BrokenDescription(root => root["bufferViews"]![3] = 216);

        Assert.IsType<InvalidOperationException>(refused.InnerException);
    }

    [Fact]
    public void Accessors_that_are_no_list_are_refused_as_a_broken_description()
    {
        var refused = BrokenDescription(root => root["accessors"] = new JsonObject());

        Assert.IsType<InvalidOperationException>(refused.InnerException);
    }

    // Numbers that are no whole numbers, or too large for the reader: System.Text.Json throws a FormatException.
    [Theory]
    [InlineData("buffers", 0, "byteLength", "2508.5")]
    [InlineData("buffers", 0, "byteLength", "1e30")]
    [InlineData("bufferViews", 1, "byteOffset", "1032.5")]
    [InlineData("bufferViews", 1, "buffer", "0.5")]
    [InlineData("accessors", 1, "bufferView", "4294967297")]
    [InlineData("accessors", 1, "count", "33.5")]
    [InlineData("accessors", 1, "componentType", "5126.5")]
    public void A_number_that_is_no_whole_number_is_refused_as_a_broken_description(string list, int index, string value, string number)
    {
        var refused = BrokenDescription(root => root[list]![index]![value] = JsonNode.Parse(number));

        Assert.IsType<FormatException>(refused.InnerException);
    }

    // The refusal of the two-part file after the given change to its description, when the change breaks the
    // description itself (a value missing or of the wrong kind) rather than the data it points to.
    private static InvalidDataException BrokenDescription(Action<JsonNode> edit)
    {
        var root = JsonNode.Parse(TwoParts)!;
        edit(root);
        var glb = Glb(root.ToJsonString());
        var refused = Assert.Throws<InvalidDataException>(() => GlbScene.Read(glb));
        Assert.StartsWith("The .glb description is broken: ", refused.Message, StringComparison.Ordinal);
        return refused;
    }

    // The two-part file with a fifth buffer view (64 bytes from the start of the buffer, no offset given) and a fifth
    // accessor of one element on it.
    private static byte[] WithExtraAccessor(string type, int componentType, int stride)
    {
        var root = JsonNode.Parse(TwoParts)!;
        root["bufferViews"]!.AsArray().Add(new JsonObject { ["buffer"] = 0, ["byteLength"] = 64, ["byteStride"] = stride });
        root["accessors"]!.AsArray().Add(new JsonObject
        {
            ["bufferView"] = 4,
            ["componentType"] = componentType,
            ["type"] = type,
            ["count"] = 1,
        });
        return Glb(root.ToJsonString());
    }

    // The message the reader refuses the two-part file with after the given change to its description.
    private static string Refusal(Action<JsonNode> edit)
    {
        var root = JsonNode.Parse(TwoParts)!;
        edit(root);
        var glb = Glb(root.ToJsonString());
        return Assert.Throws<InvalidDataException>(() => GlbScene.Read(glb)).Message;
    }

    // A minimal .glb: 12-byte header, the JSON chunk (padded with spaces to 4 bytes), then - unless dataLength is null -
    // a binary chunk of that many zero bytes.
    private static byte[] Glb(string json, int? dataLength = DataLength)
    {
        var text = Encoding.UTF8.GetBytes(json);
        var padded = (text.Length + 3) / 4 * 4;
        var binary = dataLength is { } length ? 8 + (length + 3) / 4 * 4 : 0;
        var file = new byte[20 + padded + binary];
        BinaryPrimitives.WriteUInt32LittleEndian(file, 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(8), (uint)file.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(12), (uint)padded);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(16), 0x4E4F534A);
        text.CopyTo(file, 20);
        file.AsSpan(20 + text.Length, padded - text.Length).Fill((byte)' ');
        if (binary > 0)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(20 + padded), (uint)(binary - 8));
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(24 + padded), 0x004E4942);
        }

        return file;
    }
}
