namespace SilverAndAsh.Core.Logging;

/// <summary>Deletes old log files so the log folder does not grow forever.</summary>
public static class LogRetention
{
    /// <summary>File name pattern of our log files. Other files in the folder are never touched.</summary>
    public const string SearchPattern = "game-*.log";

    /// <summary>
    /// Keeps the <paramref name="filesToKeep"/> newest log files in <paramref name="directory"/>
    /// and deletes the rest. "Newest" is decided by file name, which starts with a sortable timestamp.
    /// Files that cannot be deleted (e.g. still open in another program) are skipped.
    /// </summary>
    /// <returns>The paths of the files that were deleted.</returns>
    public static IReadOnlyList<string> DeleteOldest(string directory, int filesToKeep)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(filesToKeep);

        if (!Directory.Exists(directory))
        {
            return [];
        }

        var outdated = Directory.GetFiles(directory, SearchPattern)
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal)
            .Skip(filesToKeep);

        var deleted = new List<string>();
        foreach (var path in outdated)
        {
            try
            {
                File.Delete(path);
                deleted.Add(path);
            }
            catch (IOException)
            {
                // Still in use - try again on the next start.
            }
            catch (UnauthorizedAccessException)
            {
                // No permission - leave it alone.
            }
        }

        return deleted;
    }
}
