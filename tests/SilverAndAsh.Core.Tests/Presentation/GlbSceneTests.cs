using System.Buffers.Binary;
using System.Numerics;
using System.Text;
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
