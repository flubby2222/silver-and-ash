using SilverAndAsh.Core.Logging;

namespace SilverAndAsh.Core.Tests.Logging;

public sealed class FileLogTests : IDisposable
{
    private readonly TemporaryDirectory _folder = new();
    private readonly FixedTime _time = new(new DateTimeOffset(2026, 11, 10, 21, 30, 15, 123, TimeSpan.FromHours(1)));

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Creates_a_file_named_after_the_start_time()
    {
        using var log = FileLog.Start(_folder.Path, time: _time);

        Assert.Equal("game-2026-11-10_21-30-15-123.log", Path.GetFileName(log.FilePath));
        Assert.True(File.Exists(log.FilePath));
    }

    [Fact]
    public void Creates_the_folder_if_it_is_missing()
    {
        var nested = Path.Combine(_folder.Path, "SilverAndAsh", "logs");

        using var log = FileLog.Start(nested, time: _time);

        Assert.True(Directory.Exists(nested));
    }

    [Fact]
    public void Entries_are_written_immediately()
    {
        using var log = FileLog.Start(_folder.Path, time: _time);

        log.Info("Game started");
        log.Warning("Low on memory");

        // Read while the log is still open - proves AutoFlush works (important for crashes).
        var lines = ReadShared(log.FilePath);
        Assert.Equal(
            [
                "2026-11-10 21:30:15.123 +01:00 [INFO ] Game started",
                "2026-11-10 21:30:15.123 +01:00 [WARN ] Low on memory",
            ],
            lines);
    }

    [Fact]
    public void Errors_include_the_exception()
    {
        using var log = FileLog.Start(_folder.Path, time: _time);

        log.Error("Crash", new InvalidOperationException("Boom"));

        var text = string.Join("\n", ReadShared(log.FilePath));
        Assert.Contains("[ERROR] Crash", text);
        Assert.Contains("System.InvalidOperationException: Boom", text);
    }

    [Fact]
    public void Keeps_at_most_ten_files_including_the_new_one()
    {
        for (var day = 1; day <= 12; day++)
        {
            _folder.CreateFile($"game-2026-11-{day:00}_12-00-00-000.log");
        }

        using var log = FileLog.Start(_folder.Path, time: new FixedTime(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero)));

        var files = _folder.FileNames();
        Assert.Equal(FileLog.DefaultFilesToKeep, files.Length);
        Assert.Contains(Path.GetFileName(log.FilePath), files);
        Assert.DoesNotContain("game-2026-11-03_12-00-00-000.log", files);
        Assert.Contains("game-2026-11-04_12-00-00-000.log", files);
    }

    [Fact]
    public void Two_starts_in_the_same_millisecond_get_different_files()
    {
        using var first = FileLog.Start(_folder.Path, time: _time);
        using var second = FileLog.Start(_folder.Path, time: _time);

        Assert.NotEqual(first.FilePath, second.FilePath);
    }

    [Fact]
    public void Writing_after_dispose_is_ignored()
    {
        var log = FileLog.Start(_folder.Path, time: _time);
        log.Dispose();

        var exception = Record.Exception(() => log.Info("Too late"));

        Assert.Null(exception);
    }

    private static string[] ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>A clock that always shows the same time - makes file names and timestamps predictable.</summary>
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone { get; } =
            TimeZoneInfo.CreateCustomTimeZone("Test", now.Offset, "Test", "Test");
    }
}
