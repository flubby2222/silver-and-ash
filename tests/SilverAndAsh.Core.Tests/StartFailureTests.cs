using SilverAndAsh.Core.Photos;

namespace SilverAndAsh.Core.Tests;

/// <summary>
/// The line the game prints when it cannot start (issue #651): a full <c>--out</c> folder in its own words, every other
/// failure as a level that could not be loaded.
/// </summary>
public sealed class StartFailureTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"start-failure-{Guid.NewGuid():N}");

    public StartFailureTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void A_full_photo_folder_is_reported_in_its_own_words_without_the_level()
    {
        File.WriteAllBytes(Path.Combine(_root, "inventory.png"), [1, 2, 3]);
        var error = Assert.Throws<PhotoFolderNotEmptyException>(() => PhotoFolder.EnsureEmpty(_root));

        var line = StartFailure.Describe(error);

        Assert.StartsWith($"The folder for the pictures is not empty: '{_root}'.", line, StringComparison.Ordinal);
        Assert.DoesNotContain("level", line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Any_other_failure_is_reported_as_a_level_that_could_not_be_loaded()
    {
        var error = new ArgumentException("Unknown level 'nowhere'.");

        Assert.Equal("Could not load the level: Unknown level 'nowhere'.", StartFailure.Describe(error));
    }
}
