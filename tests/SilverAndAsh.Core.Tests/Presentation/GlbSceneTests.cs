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
            { "count": 86, "min": [-0.1, 0.04, -0.01], "max": [0.1, 0.25, 0.19] },
            { "count": 33, "min": [-0.05, -0.07, -0.04], "max": [0, 0, 0] },
            { "count": 432 },
            { "count": 108 }
          ]
        }
        """;

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

    // A minimal .glb: 12-byte header, then the JSON chunk (padded with spaces to 4 bytes).
    private static byte[] Glb(string json)
    {
        var text = Encoding.UTF8.GetBytes(json);
        var padded = (text.Length + 3) / 4 * 4;
        var file = new byte[20 + padded];
        BinaryPrimitives.WriteUInt32LittleEndian(file, 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(8), (uint)file.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(12), (uint)padded);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(16), 0x4E4F534A);
        text.CopyTo(file, 20);
        file.AsSpan(20 + text.Length).Fill((byte)' ');
        return file;
    }
}
