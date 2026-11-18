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
        var (width, height, rgba) = TestPng.Read(Path.Combine(RepositoryFiles.Find(Folder), name));
        var alphas = Enumerable.Range(0, width * height).Select(pixel => rgba[(pixel * 4) + 3]).ToArray();

        Assert.True(width > 0 && height > 0);
        var see = Array.FindIndex(alphas, a => a != 255);
        Assert.True(see < 0, see < 0 ? string.Empty : $"pixel {see % width},{see / width} has alpha {alphas[see]}");
    }
}
