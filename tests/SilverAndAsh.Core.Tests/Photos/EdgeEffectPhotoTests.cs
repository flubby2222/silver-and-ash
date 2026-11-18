using System.Buffers.Binary;
using System.IO.Compression;

namespace SilverAndAsh.Core.Tests.Photos;

/// <summary>
/// The photos of the effects at the edge of the picture (issue #370) are fully opaque. The darkening after a hit once
/// lowered the alpha of the small picture with the default blending, so the black behind it showed through: the hit
/// looked darker than allowed and the photo was half transparent (review of #385). Raylib needs a window, so this
/// checks what the window wrote.
/// </summary>
public class EdgeEffectPhotoTests
{
    private const string Folder = "design/proben/grafik/bildrand-effekte";

    private static string[] PhotoNames() =>
        Directory.GetFiles(RepositoryFiles.Find(Folder), "*.png")
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    public static TheoryData<string> Photos() => new(PhotoNames());

    [Fact]
    public void There_is_a_photo_of_the_hit()
    {
        Assert.Contains("hud-hit.png", PhotoNames());
        Assert.Contains("hud-hit-screen.png", PhotoNames());
    }

    [Theory]
    [MemberData(nameof(Photos))]
    public void Every_pixel_of_the_photo_is_opaque(string name)
    {
        var (width, height, alphas) = Png.ReadAlpha(Path.Combine(RepositoryFiles.Find(Folder), name));

        Assert.True(width > 0 && height > 0);
        var see = Array.FindIndex(alphas, a => a != 255);
        Assert.True(see < 0, see < 0 ? string.Empty : $"pixel {see % width},{see / width} has alpha {alphas[see]}");
    }

    /// <summary>Just enough of PNG to read the alpha of 8-bit RGB and RGBA pictures without interlacing.</summary>
    private static class Png
    {
        public static (int Width, int Height, byte[] Alphas) ReadAlpha(string path)
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

            var alphas = new byte[width * height];
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
                    alphas[(y * width) + x] = channels == 4 ? line[(x * 4) + 3] : (byte)255;
                }

                (previous, line) = (line, previous);
            }

            return (width, height, alphas);
        }

        private static int Paeth(int a, int b, int c)
        {
            var p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }
    }
}
