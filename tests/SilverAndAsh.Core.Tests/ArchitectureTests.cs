using System.Xml.Linq;

namespace SilverAndAsh.Core.Tests;

/// <summary>
/// Guards the most important architecture rule: the game logic (Core) must not depend on
/// Raylib or any other rendering library, so it stays testable without a window.
/// </summary>
public class ArchitectureTests
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
        var project = XDocument.Load(FindCoreProjectFile());

        var packages = project.Descendants("PackageReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty);

        var forbidden = packages
            .Where(name => ForbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.Empty(forbidden);
    }

    private static string FindCoreProjectFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "SilverAndAsh.Core", "SilverAndAsh.Core.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find SilverAndAsh.Core.csproj above the test output directory.");
    }
}
