using System.Text.RegularExpressions;
using System.Xml.Linq;
using SilverAndAsh.Core.Tests.Presentation;

namespace SilverAndAsh.Core.Tests;

/// <summary>
/// Guards the most important architecture rule: the game logic (Core) must not depend on
/// Raylib or any other rendering library, so it stays testable without a window. And within Core (issue #695): no
/// game rule hangs on a class of the picture - the rule part of Core does not use the Presentation namespace.
/// </summary>
public partial class ArchitectureTests
{
    private static readonly string[] ForbiddenPrefixes = ["Raylib"];

    [Fact]
    public void Core_assembly_does_not_reference_rendering_libraries()
    {
        var references = typeof(SimulationClock).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty);

        var forbidden = references
            .Where(name => ForbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Core_project_file_does_not_reference_rendering_packages()
    {
        // The compiler drops references that are never used, so the assembly check above
        // would miss a package that was added but not yet used. Check the project file too.
        var project = XDocument.Load(RepositoryFiles.Find("src/SilverAndAsh.Core/SilverAndAsh.Core.csproj"));

        var packages = project.Descendants("PackageReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty);

        var forbidden = packages
            .Where(name => ForbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.Empty(forbidden);
    }

    // The parts of Core that only show or play the game (issue #695): the picture itself, the photo studio, the showroom
    // and the choice of sounds. Every other folder - Gameplay, World, Physics, Input, Balancing, the files at the top,
    // and any folder still to come - is the rule part.
    private static readonly string[] PresentationFolders = ["Presentation", "Photos", "Showroom", "Audio"];

    // Files of the rule part that use Presentation today, each with why - for a time only: issue #704 takes them out.
    // None of them decides a rule with it; they carry data for the picture along.
    private static readonly Dictionary<string, string> PresentationExceptions = new()
    {
        ["Gameplay/GameWorld.cs"] = "carries the texture variants of the surfaces (SurfaceVariants) for the picture - #704",
        ["Gameplay/LevelZero.cs"] = "builds those SurfaceVariants for a new world - #704",
        ["Gameplay/RoomPreview.cs"] = "builds those SurfaceVariants for a room preview - #704",
        ["Course/TestCourse.cs"] = "builds those SurfaceVariants for the world of the test course (#718) - #704",
        ["Gameplay/ShowroomExhibits.cs"] = "exhibits of the showroom with their model looks - #704",
    };

    [Fact]
    public void Rule_part_of_Core_does_not_reference_Presentation()
    {
        var core = RepositoryFiles.Find("src/SilverAndAsh.Core");
        var files = Directory.EnumerateFiles(core, "*.cs", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(core, file).Replace('\\', '/'))
            .Where(file => !file.Split('/').Any(part => part is "bin" or "obj"))
            .Where(file => !PresentationFolders.Any(folder => file.StartsWith(folder + "/", StringComparison.Ordinal)))
            .ToList();

        // The door and the two rules are among the files read - else the test proves nothing.
        Assert.Contains("Gameplay/Door.cs", files);
        Assert.Contains("GrateTravel.cs", files);
        Assert.Contains("World/BossPortal.cs", files);

        var uses = files
            .SelectMany(file => PresentationUses(File.ReadAllText(Path.Combine(core, file))).Select(use => (File: file, Use: use)))
            .ToList();

        var forbidden = uses.Where(use => !PresentationExceptions.ContainsKey(use.File)).Select(use => $"{use.File}:{use.Use}").ToList();
        Assert.True(forbidden.Count == 0,
            "A game rule uses the Presentation namespace - put the rule into Core's rule part (Gameplay, World, Balance) "
            + "and let the picture read it (issue #695): " + string.Join(" | ", forbidden));

        var stale = PresentationExceptions.Keys.Where(file => uses.All(use => use.File != file)).ToList();
        Assert.True(stale.Count == 0,
            "These files no longer use Presentation - take them off the exceptions: " + string.Join(", ", stale));
    }

    [Theory]
    [InlineData("using SilverAndAsh.Core.Presentation;", true)]
    [InlineData("var height = Presentation.GratePose.OpenHeight;", true)]
    [InlineData("var height = SilverAndAsh.Core.Presentation.GratePose.OpenHeight;", true)]
    [InlineData("using static SilverAndAsh.Core.Presentation.GratePose;", true)]
    [InlineData("using Look = SilverAndAsh.Core.Presentation;", true)]
    [InlineData("var height = GrateTravel.OpenHeight;", false)]
    [InlineData("var view = PresentationMode.Full;", false)]
    public void The_search_finds_a_use_of_Presentation_and_nothing_else(string source, bool found)
    {
        Assert.Equal(found, PresentationUses(source).Count > 0);
    }

    [Fact]
    public void The_search_finds_no_Presentation_in_comments_or_strings()
    {
        Assert.Empty(PresentationUses(
            "/// <see cref=\"Presentation.GratePose\"/>\n// using SilverAndAsh.Core.Presentation;\nvar text = \"Presentation.GratePose\"; /* Presentation */"));
    }

    [Fact]
    public void The_search_gives_the_line_of_a_use()
    {
        Assert.Equal(["3: using SilverAndAsh.Core.Presentation;"], PresentationUses("// first\n\nusing SilverAndAsh.Core.Presentation;\n"));
    }

    // Every use of the Presentation namespace in the code of source (not in its comments or strings), as "line: text".
    private static List<string> PresentationUses(string source)
    {
        var code = CSharpStringLiterals.CodeOf(source);
        var lines = source.Split('\n');
        return PresentationName().Matches(code)
            .Select(match => code.AsSpan(0, match.Index).Count('\n'))
            .Distinct()
            .Select(line => $"{line + 1}: {lines[line].Trim()}")
            .ToList();
    }

    [GeneratedRegex(@"\bPresentation\b")]
    private static partial Regex PresentationName();
}
