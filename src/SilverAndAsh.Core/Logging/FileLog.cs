using System.Globalization;

namespace SilverAndAsh.Core.Logging;

/// <summary>
/// Writes log entries to a new file for every game start. Purely local - nothing is ever sent anywhere.
/// Thread-safe: entries from different threads (e.g. a crash handler) do not get mixed up.
/// </summary>
public sealed class FileLog : IDisposable
{
    /// <summary>How many log files (including the current one) are kept.</summary>
    public const int DefaultFilesToKeep = 10;

    private readonly StreamWriter _writer;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private bool _disposed;

    private FileLog(string filePath, StreamWriter writer, TimeProvider time)
    {
        FilePath = filePath;
        _writer = writer;
        _time = time;
    }

    /// <summary>Full path of the file this log writes to.</summary>
    public string FilePath { get; }

    /// <summary>
    /// Creates <paramref name="directory"/> if needed, deletes old log files so that at most
    /// <paramref name="filesToKeep"/> remain, and opens a new file named after the current time.
    /// </summary>
    public static FileLog Start(string directory, int filesToKeep = DefaultFilesToKeep, TimeProvider? time = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(filesToKeep, 1);
        time ??= TimeProvider.System;

        Directory.CreateDirectory(directory);

        // Make room for the new file, so that afterwards exactly filesToKeep files exist.
        LogRetention.DeleteOldest(directory, filesToKeep - 1);

        var (filePath, stream) = CreateUniqueFile(directory, time.GetLocalNow());

        // AutoFlush: every entry lands on disk immediately, so nothing is lost if the game crashes.
        var writer = new StreamWriter(stream) { AutoFlush = true };

        return new FileLog(filePath, writer, time);
    }

    /// <summary>Writes a normal event.</summary>
    public void Info(string message) => Write(LogLevel.Info, message, null);

    /// <summary>Writes something unexpected that the game could recover from.</summary>
    public void Warning(string message, Exception? exception = null) => Write(LogLevel.Warning, message, exception);

    /// <summary>Writes an error, optionally with the exception that caused it.</summary>
    public void Error(string message, Exception? exception = null) => Write(LogLevel.Error, message, exception);

    /// <summary>Writes an entry with the given level.</summary>
    public void Write(LogLevel level, string message, Exception? exception)
    {
        var line = LogFormatter.Format(_time.GetLocalNow(), level, message, exception);

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _writer.WriteLine(line);
        }
    }

    /// <summary>Closes the file. Later entries are silently ignored.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer.Dispose();
        }
    }

    private static (string Path, FileStream Stream) CreateUniqueFile(string directory, DateTimeOffset now)
    {
        // Sortable timestamp in the name, so "newest" = "last in alphabetical order".
        var stamp = now.ToString("yyyy-MM-dd_HH-mm-ss-fff", CultureInfo.InvariantCulture);

        // Two starts within the same millisecond are unlikely, but must not crash.
        // CreateNew fails if the file already exists - then simply try the next name.
        const int MaxAttempts = 100;
        for (var attempt = 1; ; attempt++)
        {
            var suffix = attempt == 1 ? string.Empty : $"_{attempt}";
            var path = Path.Combine(directory, $"game-{stamp}{suffix}.log");

            try
            {
                return (path, new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
            }
            catch (IOException) when (attempt < MaxAttempts && File.Exists(path))
            {
                // Name already taken - try the next one.
            }
        }
    }
}
