using System.Text;
using System.Text.RegularExpressions;

namespace SilverAndAsh.Core.Tests;

/// <summary>
/// The repository is public: generated text files must not contain absolute paths of a local user folder
/// (they leak the Windows or Linux user name). Issue #295.
/// </summary>
public class RepositoryHygieneTests
{
    private static readonly Regex UserPath = new(@"[a-z]:[/\\]+users[/\\]|/home/", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] SkippedFolders = ["bin", "obj", "__pycache__", ".git"];

    [Theory]
    [InlineData("tools")]
    [InlineData("assets")]
    public void Text_files_contain_no_absolute_user_paths(string folder)
    {
        var root = RepositoryFiles.Find(folder);
        var hits = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file);
            if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(SkippedFolders.Contains))
            {
                continue;
            }

            var bytes = File.ReadAllBytes(file);
            if (IsBinary(bytes))
            {
                continue;
            }

            var lines = Encoding.UTF8.GetString(bytes).Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (UserPath.IsMatch(lines[i]))
                {
                    hits.Add($"{folder}/{relative.Replace('\\', '/')}:{i + 1}");
                }
            }
        }

        Assert.True(
            hits.Count == 0,
            "Absolute user paths found (use a path relative to the script or a placeholder such as {TMP}):\n"
            + string.Join("\n", hits));
    }

    [Theory]
    [InlineData(@"C:\Users\someone\AppData\x.wav")]
    [InlineData("C:/Users/someone/AppData/Local/Temp/sc-versuch/q-glocke.wav")]
    [InlineData("c://users/someone/")]
    [InlineData("/home/someone/game")]
    public void Pattern_recognises_user_paths(string text) => Assert.Matches(UserPath, text);

    [Theory]
    [InlineData("{TMP}/q-glocke.wav")]
    [InlineData("C:/Program Files/ArtCraft/SoundCraft/soundcraft-cli.exe")]
    [InlineData(@"D:\Program Files\Blender Foundation\Blender 5.3\blender.exe")]
    [InlineData("%LOCALAPPDATA%/SilverAndAsh/logs")]
    public void Pattern_accepts_ordinary_paths(string text) => Assert.DoesNotMatch(UserPath, text);

    /// <summary>A NUL byte in the first 8 KiB marks a binary file (WAV, PNG, OGG, GLB …), as Git does.</summary>
    private static bool IsBinary(byte[] bytes) => Array.IndexOf(bytes, (byte)0, 0, Math.Min(bytes.Length, 8192)) >= 0;
}
