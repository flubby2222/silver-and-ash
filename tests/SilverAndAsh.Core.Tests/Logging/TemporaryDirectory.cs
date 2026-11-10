namespace SilverAndAsh.Core.Tests.Logging;

/// <summary>A fresh, empty folder for one test. Deleted again afterwards.</summary>
internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SilverAndAsh.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string CreateFile(string name)
    {
        var path = System.IO.Path.Combine(Path, name);
        File.WriteAllText(path, name);
        return path;
    }

    public string[] FileNames() =>
        Directory.GetFiles(Path).Select(System.IO.Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!;

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Best effort - the OS cleans the temp folder eventually.
        }
    }
}
