using System.Buffers.Binary;
using System.IO.Compression;

namespace SilverAndAsh.Core.Tests;

/// <summary>
/// Just enough of PNG to read 8-bit RGB and RGBA pictures without interlacing - the photos the window wrote and the
/// HUD parts picture. Core has no picture reader and the tests run without Raylib.
/// </summary>
internal static class TestPng
{
    /// <summary>The pixels row by row from the top, four bytes each (R, G, B, A); RGB pictures get alpha 255.</summary>
    public static (int Width, int Height, byte[] Rgba) Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var at = 8; // signature
        int width = 0, height = 0, colorType = -1;
        using var packed = new MemoryStream();
        while (at < bytes.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(at));
            var type = System.Text.Encoding.ASCII.GetString(bytes, at + 4, 4);
            var data = bytes.AsSpan(at + 8, length);
            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(data);
                height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                Assert.Equal(8, data[8]);
                colorType = data[9];
                Assert.Equal(0, data[12]);
            }
            else if (type == "IDAT")
            {
                packed.Write(data);
            }

            at += 12 + length;
        }

        Assert.True(colorType is 2 or 6, $"colour type {colorType} is not read here");
        var channels = colorType == 6 ? 4 : 3;
        var stride = width * channels;
        var raw = new byte[height * (stride + 1)];
        packed.Position = 0;
        using (var zlib = new ZLibStream(packed, CompressionMode.Decompress))
        {
            zlib.ReadExactly(raw);
        }

        var rgba = new byte[width * height * 4];
        var previous = new byte[stride];
        var line = new byte[stride];
        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            for (var i = 0; i < stride; i++)
            {
                var value = raw[(y * (stride + 1)) + 1 + i];
                var left = i >= channels ? line[i - channels] : 0;
                var up = previous[i];
                var upLeft = i >= channels ? previous[i - channels] : 0;
                line[i] = (byte)(value + filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new InvalidDataException($"filter {filter}"),
                });
            }

            for (var x = 0; x < width; x++)
            {
                var to = ((y * width) + x) * 4;
                rgba[to] = line[x * channels];
                rgba[to + 1] = line[(x * channels) + 1];
                rgba[to + 2] = line[(x * channels) + 2];
                rgba[to + 3] = channels == 4 ? line[(x * 4) + 3] : (byte)255;
            }

            (previous, line) = (line, previous);
        }

        return (width, height, rgba);
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}
