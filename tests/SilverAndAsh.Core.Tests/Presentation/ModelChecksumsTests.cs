using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

public sealed class ModelChecksumsTests : IDisposable
{
    private static readonly byte[] Content = [.. Enumerable.Range(0, 300).Select(i => (byte)(i * 7))];

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "silver-and-ash-checksums-" + Guid.NewGuid().ToString("N"));

    public ModelChecksumsTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void A_whole_file_matches()
    {
        var path = Write("ghoul.glb", Content);

        Assert.Null(List().Check(path));
    }

    [Fact]
    public void A_missing_file_does_not_match()
    {
        Assert.Contains("missing", List().Check(Path.Combine(_folder, "ghoul.glb")), StringComparison.Ordinal);
    }

    [Fact]
    public void One_changed_byte_does_not_match()
    {
        var changed = Content.ToArray();
        changed[150] ^= 1;

        Assert.Contains("SHA-256", List().Check(Write("ghoul.glb", changed)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_cut_off_file_does_not_match()
    {
        Assert.Contains("bytes", List().Check(Write("ghoul.glb", Content[..200])), StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_without_entry_does_not_match()
    {
        Assert.Contains("not in the checksum list", List().Check(Write("bat.glb", Content)), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not a checksum list")]
    [InlineData("abc 300 ghoul.glb")]
    [InlineData("ee184f3c4c063a33622ead52e8496440ab3682e1fa75c3084e9e29619dd9bbf6 many ghoul.glb")]
    [InlineData("ee184f3c4c063a33622ead52e8496440ab3682e1fa75c3084e9e29619dd9bbf6 300")]
    public void A_broken_list_is_refused(string text)
    {
        Assert.Throws<InvalidDataException>(() => ModelChecksums.Parse(text));
    }

    [Fact]
    public void A_name_twice_in_the_list_is_refused()
    {
        var line = ModelChecksums.LineFor("ghoul.glb", Content);

        Assert.Throws<InvalidDataException>(() => ModelChecksums.Parse(line + "\n" + line));
    }

    [Fact]
    public void The_checked_in_list_matches_every_checked_in_model_file()
    {
        // Renewing a model without renewing the list (tools/models/checksums.py) would leave the placeholder in the game.
        var folder = RepositoryFiles.Find("assets/models");
        var list = ModelChecksums.Parse(File.ReadAllText(Path.Combine(folder, ModelChecksums.FileName)));
        var files = Directory.GetFiles(folder)
            .Where(path => path.EndsWith(".glb", StringComparison.Ordinal) || path.EndsWith(".png", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(files.Select(Path.GetFileName).Order(), list.Names.Order());
        Assert.All(files, path => Assert.Null(list.Check(path)));
    }

    private ModelChecksums List() => ModelChecksums.Parse("# comment\n\n" + ModelChecksums.LineFor("ghoul.glb", Content) + "\n");

    private string Write(string name, byte[] content)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, content);
        return path;
    }
}
