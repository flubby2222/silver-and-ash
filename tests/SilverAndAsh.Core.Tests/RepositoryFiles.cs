namespace SilverAndAsh.Core.Tests;

/// <summary>Finds files of the repository from inside the test output folder.</summary>
internal static class RepositoryFiles
{
    /// <summary>
    /// Returns the full path of <paramref name="relativePath"/> (e.g. "assets/levels/test.txt"),
    /// searching upwards from the test output directory until the repository root is found.
    /// </summary>
    public static string Find(string relativePath)
    {
        var parts = relativePath.Split('/', '\\');
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find '{relativePath}' above the test output directory.");
    }
}
