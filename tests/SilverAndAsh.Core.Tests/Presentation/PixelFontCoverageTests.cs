using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Every character the game can show is in its pixel font (issue #367) - otherwise it would draw the fallback box.
/// </summary>
public class PixelFontCoverageTests
{
    /// <summary>
    /// Texts of the code that never reach the screen and need characters the font does not have - each with the file it
    /// is in. Kept short and exact: a new text on the screen is never one of these.
    /// </summary>
    private static readonly HashSet<(string File, string Literal)> NeverDrawn =
    [
        // A warning in the log file.
        ("BalanceFile.cs", "The balancing file must contain one { ... } block - using the standard values."),

        // The pattern for --window 854x480.
        ("LaunchOptions.cs", @"^([0-9]{1,5})x([0-9]{1,5})\z"),

        // Line breaks in files the game reads and writes.
        ("SettingsFile.cs", "\n"),
        ("LevelParser.cs", "\n"),
        ("RoomTemplateParser.cs", "\n"),
    ];

    [Fact]
    public void Every_character_in_the_texts_of_the_game_code_is_in_the_font()
    {
        var font = GameFont();
        var missing = new List<string>();
        foreach (var file in SourceFiles())
        {
            foreach (var literal in CSharpStringLiterals.In(File.ReadAllText(file)))
            {
                if (NeverDrawn.Contains((Name(file), literal)))
                {
                    continue;
                }

                missing.AddRange(font.Missing(literal).Select(character => $"{Name(file)}: '{character}' in \"{literal}\""));
            }
        }

        Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
    }

    [Fact]
    public void Every_name_and_boon_text_from_the_game_rules_is_in_the_font()
    {
        var font = GameFont();
        var boons = Balance.Default.Boons;
        var texts = Equipment.All.Select(equipment => equipment.Name)
            .Concat(Enum.GetValues<Boon>().SelectMany(boon => new[]
            {
                BoonTexts.Name(boon), BoonTexts.What(boon), BoonTexts.Effect(boon, boons), BoonTexts.Amount(boon, boons, 3),
            }));

        Assert.Empty(texts.SelectMany(text => font.Missing(text).Select(character => $"'{character}' in \"{text}\"")));
    }

    [Fact]
    public void The_scanner_finds_the_text_of_every_kind_of_string_and_nothing_from_comments()
    {
        const string Source = """"
            // "comment"
            /* "block" */
            var a = "plain\tq\"x";
            var b = @"verbatim ""y""";
            var c = $"hole {count + 1:0.0} and {(on ? "inner" : "other")} {{braces}}";
            var d = '"';
            var e = $$"""raw {{x}} {z}""";
            """";

        Assert.Equal(
            ["plain\tq\"x", "verbatim \"y\"", "inner", "other", "hole  and  {braces}", "raw  {z}"],
            CSharpStringLiterals.In(Source));
    }

    private static IEnumerable<string> SourceFiles()
    {
        var game = RepositoryFiles.Find("src/SilverAndAsh.Game");
        var core = RepositoryFiles.Find("src/SilverAndAsh.Core");
        return Directory.EnumerateFiles(game, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(core, "*.cs", SearchOption.AllDirectories))
            .Where(file => !IsBuildOutput(file));
    }

    private static bool IsBuildOutput(string file)
    {
        var parts = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Contains("bin") || parts.Contains("obj");
    }

    private static string Name(string file) => Path.GetFileName(file);

    private static PixelFont GameFont() =>
        PixelFont.Parse(File.ReadAllText(RepositoryFiles.Find("assets/fonts/kapitell.json")));
}
