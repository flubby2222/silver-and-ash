using System.Text.RegularExpressions;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Issue #657: the game draws everything that moves with the clock of the views (in photos: the ticks run so far), so
/// two photos of the same scene are the same picture (issue #651). <c>Raylib.GetTime()</c> - the time since the game
/// window opened - would break that, but only where a moving effect is in the picture: the pulse of a Vampiric weapon
/// in the inventory, which no weapon has yet. No photo would notice it coming back, and the game has no tests of its
/// own, so this reads its source: no file of the game calls <c>GetTime</c>, also not as <c>using static Raylib</c>.
/// </summary>
public partial class FixedClockSourceTests
{
    [Fact]
    public void No_file_of_the_game_takes_the_time_from_GetTime()
    {
        var game = RepositoryFiles.Find("src/SilverAndAsh.Game");
        var files = Directory.EnumerateFiles(game, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .ToList();

        // The rendering code is found - else the test proves nothing.
        Assert.Contains(files, file => file.EndsWith("InventoryWindow.cs", StringComparison.Ordinal));

        var calls = files
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, line, number: index + 1)))
            .Where(entry => GetTimeCall().IsMatch(WithoutLineComment(entry.line)))
            .Select(entry => $"{Path.GetRelativePath(game, entry.file)}:{entry.number}: {entry.line.Trim()}")
            .ToList();

        Assert.True(calls.Count == 0,
            "The game takes the time from GetTime() - take the clock of the views instead (the seconds handed to Draw, "
            + "issue #651): " + string.Join(" | ", calls));
    }

    [Theory]
    [InlineData("var seconds = (float)Raylib.GetTime();", true)]
    [InlineData("var now = GetTime();", true)]
    [InlineData("double t = Raylib.GetTime ();", true)]
    [InlineData("var started = System.Diagnostics.Stopwatch.GetTimestamp();", false)]
    [InlineData("var dt = Raylib.GetFrameTime();", false)]
    [InlineData("var x = 1; // not Raylib.GetTime()", false)]
    public void The_search_finds_a_call_of_GetTime_and_nothing_else(string line, bool found)
    {
        Assert.Equal(found, GetTimeCall().IsMatch(WithoutLineComment(line)));
    }

    private static string WithoutLineComment(string line)
    {
        var comment = line.IndexOf("//", StringComparison.Ordinal);
        return comment < 0 ? line : line[..comment];
    }

    [GeneratedRegex(@"\bGetTime\s*\(")]
    private static partial Regex GetTimeCall();
}
