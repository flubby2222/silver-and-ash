using System.Diagnostics;
using System.Text.Json;

namespace SilverAndAsh.Core.Tests;

/// <summary>
/// tools/crap-report.py (issue #320) without the long coverage run: the script reads small hand-made Cobertura files
/// (--coverage) and skips the ReportGenerator cross check (--no-check). The comparison with the previous report only
/// happens from the second report on, so these tests always write two reports in a row.
/// </summary>
public class CrapReportScriptTests
{
    private const string FirstDate = "2026-12-01";
    private const string SecondDate = "2026-12-15";

    [Fact]
    public void Second_report_compares_with_the_first_one()
    {
        using var folder = new TempFolder();
        var first = WriteCoverage(folder, "first.xml", decideCovered: 2);
        var second = WriteCoverage(folder, "second.xml", decideCovered: 4);

        RunReport(folder, first, FirstDate).AssertSucceeded();
        RunReport(folder, second, SecondDate).AssertSucceeded();

        var report = File.ReadAllText(Path.Combine(folder.Path, $"crap-{SecondDate}.md"));
        Assert.Contains("## Vergleich zum vorigen Bericht", report);
        Assert.Contains($"Vorher: `crap-{FirstDate}.md`.", report);
        Assert.Contains("- Methoden 3 → 3, über 30: 1 → 1", report);
        Assert.DoesNotContain("Kein voriger Bericht", report);

        // Decide: complexity 6, half the lines covered (6² × 0,5³ + 6 = 10,5), then all of them (6): Δ −4,5.
        Assert.Equal("−4,5", DeltaOf(report, "`Decide`"));
        // Both lambdas kept their own entry in the first report, so neither shows up as new.
        Assert.Equal("=", DeltaOf(report, "`Run › Lambda 0()`"));
        Assert.Equal("=", DeltaOf(report, "`Run › Lambda 0() #2`"));
    }

    [Fact]
    public void Lambdas_with_the_same_name_keep_separate_entries_in_the_data_block()
    {
        using var folder = new TempFolder();
        RunReport(folder, WriteCoverage(folder, "first.xml", decideCovered: 2), FirstDate).AssertSucceeded();

        var scores = DataBlock(File.ReadAllText(Path.Combine(folder.Path, $"crap-{FirstDate}.md"))).GetProperty("scores");
        var lambdas = scores.EnumerateObject().Where(score => score.Name.Contains("Run › Lambda 0")).ToList();

        Assert.Equal(2, lambdas.Count);
        Assert.NotEqual(lambdas[0].Name, lambdas[1].Name);
        Assert.Equal([20.0, 72.0], lambdas.Select(score => score.Value.GetDouble()).Order());
    }

    [Fact]
    public void The_checked_in_first_report_can_be_read_as_previous_report()
    {
        using var folder = new TempFolder();
        File.Copy(RepositoryFiles.Find("docs/qualitaet/crap-2026-11-18.md"), Path.Combine(folder.Path, "crap-2026-11-18.md"));

        RunReport(folder, WriteCoverage(folder, "first.xml", decideCovered: 2), FirstDate).AssertSucceeded();

        var report = File.ReadAllText(Path.Combine(folder.Path, $"crap-{FirstDate}.md"));
        Assert.Contains("Vorher: `crap-2026-11-18.md`.", report);
        Assert.Contains("- Methoden 871 → 3, über 30: 17 → 1", report);
    }

    [Theory]
    [InlineData("18.11.2026")]
    [InlineData("2026-13-01")]
    public void A_date_that_is_not_iso_stops_with_a_message_instead_of_a_traceback(string date)
    {
        using var folder = new TempFolder();

        var result = RunReport(folder, WriteCoverage(folder, "first.xml", decideCovered: 2), date);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--date", result.Error);
        Assert.DoesNotContain("Traceback", result.Error);
        Assert.Empty(Directory.GetFiles(folder.Path, "crap-*.md"));
    }

    [Fact]
    public void A_folder_as_report_file_stops_with_a_message_before_anything_runs()
    {
        using var folder = new TempFolder();
        var coverage = WriteCoverage(folder, "first.xml", decideCovered: 2);
        var target = Directory.CreateDirectory(Path.Combine(folder.Path, "crap-2026-12-01.md")).FullName;

        var result = Run(["--coverage", coverage, "--no-check", "--date", FirstDate, "--out", target]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--out", result.Error);
        Assert.DoesNotContain("Traceback", result.Error);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Three methods: Widget.Decide (complexity 6, four lines, <paramref name="decideCovered"/> of them covered) and two
    /// uncovered lambdas from different closures of Widget.Run that the compiler gave the same name, with complexity
    /// 4 and 8, i.e. scores 4² + 4 = 20 and 8² + 8 = 72.
    /// </summary>
    private static string WriteCoverage(TempFolder folder, string name, int decideCovered)
    {
        static string Lines(int first, int count, int covered) => string.Concat(Enumerable.Range(0, count).Select(i =>
            $"""<line number="{first + i}" hits="{(i < covered ? 1 : 0)}" branch="False" />"""));

        static string Class(string className, string method, string signature, int complexity, int first, int count, int covered) =>
            $"""
            <class name="{className}" filename="src/SilverAndAsh.Core/Sample/Widget.cs" line-rate="0" branch-rate="0" complexity="{complexity}">
              <methods>
                <method name="{method}" signature="{signature}" line-rate="0" branch-rate="0" complexity="{complexity}">
                  <lines>{Lines(first, count, covered)}</lines>
                </method>
              </methods>
              <lines>{Lines(first, count, covered)}</lines>
            </class>
            """;

        var xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <coverage line-rate="0" branch-rate="0" version="1.9" timestamp="0">
              <packages>
                <package name="SilverAndAsh.Core" line-rate="0" branch-rate="0" complexity="0">
                  <classes>
                    {Class("SilverAndAsh.Core.Sample.Widget", "Decide", "(System.Int32)", 6, 10, 4, decideCovered)}
                    {Class("SilverAndAsh.Core.Sample.Widget/&lt;&gt;c__DisplayClass1_0", "&lt;Run&gt;b__0", "()", 4, 20, 2, 0)}
                    {Class("SilverAndAsh.Core.Sample.Widget/&lt;&gt;c__DisplayClass1_1", "&lt;Run&gt;b__0", "()", 8, 30, 2, 0)}
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var path = Path.Combine(folder.Path, name);
        File.WriteAllText(path, xml);
        return path;
    }

    private static string DeltaOf(string report, string method)
    {
        var row = report.Split('\n').Single(line => line.StartsWith('|') && line.Contains($"| {method} |"));
        return row.Split('|', StringSplitOptions.TrimEntries)[^2];
    }

    private static JsonElement DataBlock(string report)
    {
        var start = report.IndexOf('{', report.IndexOf("<!-- crap-data", StringComparison.Ordinal));
        var end = report.LastIndexOf("-->", StringComparison.Ordinal);
        return JsonDocument.Parse(report[start..end]).RootElement;
    }

    private static ScriptResult RunReport(TempFolder folder, string coverage, string date) =>
        Run(["--coverage", coverage, "--no-check", "--date", date, "--out", Path.Combine(folder.Path, $"crap-{date}.md")]);

    private static ScriptResult Run(string[] arguments)
    {
        var script = RepositoryFiles.Find("tools/crap-report.py");
        var start = new ProcessStartInfo(OperatingSystem.IsWindows() ? "python" : "python3")
        {
            WorkingDirectory = Path.GetDirectoryName(Path.GetDirectoryName(script))!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        start.Environment["PYTHONIOENCODING"] = "utf-8";
        start.ArgumentList.Add(script);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(TimeSpan.FromMinutes(1)), "crap-report.py did not finish within a minute.");
        return new ScriptResult(process.ExitCode, output.Result, error.Result);
    }

    private sealed record ScriptResult(int ExitCode, string Output, string Error)
    {
        public void AssertSucceeded() =>
            Assert.True(ExitCode == 0, $"crap-report.py failed ({ExitCode}):\n{Output}\n{Error}");
    }

    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("silverash-crap-test-").FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
