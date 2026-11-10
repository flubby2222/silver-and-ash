using SilverAndAsh.Core.Logging;

namespace SilverAndAsh.Core.Tests.Logging;

public sealed class LogRetentionTests : IDisposable
{
    private readonly TemporaryDirectory _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Keeps_the_newest_files_and_deletes_the_rest()
    {
        for (var day = 1; day <= 12; day++)
        {
            _folder.CreateFile($"game-2026-11-{day:00}_12-00-00-000.log");
        }

        var deleted = LogRetention.DeleteOldest(_folder.Path, filesToKeep: 10);

        Assert.Equal(2, deleted.Count);
        var remaining = _folder.FileNames();
        Assert.Equal(10, remaining.Length);
        Assert.Equal("game-2026-11-03_12-00-00-000.log", remaining[0]);
        Assert.Equal("game-2026-11-12_12-00-00-000.log", remaining[^1]);
    }

    [Fact]
    public void Does_nothing_when_there_are_few_files()
    {
        _folder.CreateFile("game-2026-11-01_12-00-00-000.log");
        _folder.CreateFile("game-2026-11-02_12-00-00-000.log");

        var deleted = LogRetention.DeleteOldest(_folder.Path, filesToKeep: 10);

        Assert.Empty(deleted);
        Assert.Equal(2, _folder.FileNames().Length);
    }

    [Fact]
    public void Never_touches_other_files()
    {
        _folder.CreateFile("notes.txt");
        _folder.CreateFile("crash-report.log");
        _folder.CreateFile("game-2026-11-01_12-00-00-000.log");

        LogRetention.DeleteOldest(_folder.Path, filesToKeep: 0);

        Assert.Equal(["crash-report.log", "notes.txt"], _folder.FileNames());
    }

    [Fact]
    public void Missing_folder_is_not_an_error()
    {
        var missing = Path.Combine(_folder.Path, "does-not-exist");

        Assert.Empty(LogRetention.DeleteOldest(missing, filesToKeep: 10));
    }

    [Fact]
    public void Negative_count_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LogRetention.DeleteOldest(_folder.Path, filesToKeep: -1));
    }
}
