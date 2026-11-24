using SilverAndAsh.Core.Photos;

namespace SilverAndAsh.Core.Tests.Photos;

/// <summary>
/// <c>--photo ... --out &lt;folder&gt;</c> only takes an empty or new folder (issue #639), so pictures of an earlier run
/// are never compared with new ones by mistake.
/// </summary>
public sealed class PhotoFolderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"photo-folder-{Guid.NewGuid():N}");

    public PhotoFolderTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void A_folder_that_is_not_there_yet_is_fine()
    {
        PhotoFolder.EnsureEmpty(Path.Combine(_root, "new"));
    }

    [Fact]
    public void An_empty_folder_is_fine()
    {
        PhotoFolder.EnsureEmpty(_root);
    }

    [Fact]
    public void A_folder_with_a_picture_of_an_earlier_run_is_refused_with_its_name()
    {
        File.WriteAllBytes(Path.Combine(_root, "ghoul-4m.png"), [1, 2, 3]);

        var error = Assert.Throws<PhotoFolderNotEmptyException>(() => PhotoFolder.EnsureEmpty(_root));

        Assert.StartsWith($"The folder for the pictures is not empty: '{_root}'.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_folder_with_only_a_folder_in_it_is_refused()
    {
        Directory.CreateDirectory(Path.Combine(_root, "blade-charge"));

        Assert.Throws<PhotoFolderNotEmptyException>(() => PhotoFolder.EnsureEmpty(_root));
    }
}
