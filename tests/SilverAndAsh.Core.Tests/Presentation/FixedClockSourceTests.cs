using System.Text.RegularExpressions;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Issue #657: the game draws everything that moves with the clock of the views (in photos: the ticks run so far), so
/// two photos of the same scene are the same picture (issue #651). <c>Raylib.GetTime()</c> - the time since the game
/// window opened - would break that, but only where a moving effect is in the picture: the pulse of a Vampiric weapon
/// in the inventory, which no weapon has yet. No photo would notice it coming back, and the game has no tests of its
/// own, so this reads its source: no file of the game takes <c>GetTime</c>, also not as <c>using static Raylib</c> or
/// as a method group (<c>Raylib.GetTime;</c>). Comments and strings are no code (issue #672, <see cref="CSharpStringLiterals.CodeOf"/>).
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
            .SelectMany(file => Uses(File.ReadAllText(file)).Select(use => $"{Path.GetRelativePath(game, file)}:{use}"))
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
    public void The_search_finds_a_call_of_GetTime_and_nothing_else(string source, bool found)
    {
        Assert.Equal(found, Uses(source).Count > 0);
    }

    // Issue #672: the cases a search line by line, cut at the first "//", got wrong.
    [Fact]
    public void The_search_finds_GetTime_as_a_method_group_without_parentheses()
    {
        Assert.Equal(["1: Func<double> clock = Raylib.GetTime;"], Uses("Func<double> clock = Raylib.GetTime;"));
    }

    [Fact]
    public void The_search_finds_a_call_of_GetTime_behind_a_string_with_two_slashes()
    {
        Assert.Equal(["1: var url = \"https://example.org\"; var t = Raylib.GetTime();"], Uses("var url = \"https://example.org\"; var t = Raylib.GetTime();"));
    }

    [Fact]
    public void The_search_finds_no_GetTime_in_a_block_comment()
    {
        Assert.Empty(Uses("/* the clock used to be\n   Raylib.GetTime() */\nvar x = 1; /* GetTime( */"));
    }

    [Fact]
    public void The_search_finds_no_GetTime_in_a_string()
    {
        Assert.Empty(Uses("var hint = \"never call GetTime() here\"; var raw = \"\"\"GetTime(\"\"\"; var at = @\"C:\\GetTime(\";"));
    }

    [Fact]
    public void The_search_finds_a_call_of_GetTime_in_the_hole_of_an_interpolated_string()
    {
        Assert.Single(Uses("var text = $\"time {Raylib.GetTime():0.0} s\";"));
    }

    // Issue #676: two gaps the review of PR #675 found.
    [Fact]
    public void The_search_finds_no_GetTime_inside_a_longer_name()
    {
        Assert.Empty(Uses("var t = MyGetTime(); var day = GetTimeOfDay();"));
    }

    [Fact]
    public void The_search_finds_a_call_of_GetTime_in_the_hole_of_a_raw_interpolated_string()
    {
        Assert.Equal(
            ["1: var text = $$\"\"\"{time} {{Raylib.GetTime():0.0}} s\"\"\";"],
            Uses("var text = $$\"\"\"{time} {{Raylib.GetTime():0.0}} s\"\"\";"));
    }

    // Every use of GetTime in the code of source (not in its comments or strings), as "line: text of the line".
    private static List<string> Uses(string source)
    {
        var code = CSharpStringLiterals.CodeOf(source);
        var lines = source.Split('\n');
        return GetTimeName().Matches(code)
            .Select(match => code.AsSpan(0, match.Index).Count('\n'))
            .Distinct()
            .Select(line => $"{line + 1}: {lines[line].Trim()}")
            .ToList();
    }

    [GeneratedRegex(@"\bGetTime\b")]
    private static partial Regex GetTimeName();
}
