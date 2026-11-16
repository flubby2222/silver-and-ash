using System.Diagnostics;
using System.Globalization;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Balancing;
using SilverAndAsh.Core.World;

// The balance report (issue #151). Usage, from the repository folder:
//   dotnet run --project tools/SilverAndAsh.BalanceReport                         -> assets/balance.json, 500 levels
//   dotnet run --project tools/SilverAndAsh.BalanceReport -- --seeds 1000
//   dotnet run --project tools/SilverAndAsh.BalanceReport -- my-balance.json
//   dotnet run --project tools/SilverAndAsh.BalanceReport -- --compare my-balance.json   -> standard file next to another one

var culture = CultureInfo.InvariantCulture;
string? file = null;
string? compare = null;
var seeds = 500;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--seeds" when i + 1 < args.Length && int.TryParse(args[i + 1], culture, out var count) && count > 0:
            seeds = count;
            i++;
            break;
        case "--compare" when i + 1 < args.Length:
            compare = args[++i];
            break;
        case "--help" or "-h":
            Console.WriteLine("Usage: [balance.json] [--seeds N] [--compare other-balance.json]");
            return 0;
        default:
            if (args[i].StartsWith('-') || file is not null)
            {
                Console.Error.WriteLine($"Unknown argument '{args[i]}'. Usage: [balance.json] [--seeds N] [--compare other-balance.json]");
                return 1;
            }

            file = args[i];
            break;
    }
}

var root = FindRepository();
if (root is null)
{
    Console.Error.WriteLine("Could not find the 'assets' folder - run the report from inside the repository.");
    return 1;
}

file ??= Path.Combine(root, "assets", "balance.json");
var templates = RoomTemplateParser.LoadAll(Path.Combine(root, "assets", "rooms"));

var clock = Stopwatch.StartNew();
var first = Report(file);
var second = compare is null ? null : Report(compare);
if (first is null || (compare is not null && second is null))
{
    return 1;
}

Console.WriteLine();
Console.WriteLine(second is null
    ? $"Balance report - {Path.GetFileName(file)}, {seeds} levels"
    : $"Balance report - {Path.GetFileName(file)} (A) against {Path.GetFileName(compare)} (B), {seeds} levels each");

const int NameWidth = 38;
string? group = null;
for (var index = 0; index < first.Count; index++)
{
    var line = first[index];
    if (line.Group != group)
    {
        group = line.Group;
        Console.WriteLine();
        Console.WriteLine(second is null
            ? $"{group,-NameWidth} {"average",9} {"min",9} {"max",9}"
            : $"{group,-NameWidth} {"A",9} {"B",9} {"B - A",9}");
        Console.WriteLine(new string('-', NameWidth + 30));
    }

    Console.WriteLine(second is null
        ? $"{line.Name,-NameWidth} {Number(line.Average),9} {Number(line.Min),9} {Number(line.Max),9}"
        : $"{line.Name,-NameWidth} {Number(line.Average),9} {Number(second[index].Average),9} {Signed(second[index].Average - line.Average),9}");
}

Console.WriteLine();
Console.WriteLine("Chances count as expected values (chance x number). The time is a rough estimate: every floor cell");
Console.WriteLine("crossed once at top speed, every enemy cut down with the blade alone - compare it with runs.log.");
Console.WriteLine(string.Create(culture, $"({clock.Elapsed.TotalSeconds:0.0} s)"));
return 0;

IReadOnlyList<FigureSummary>? Report(string path)
{
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"No balancing file at '{path}'.");
        return null;
    }

    var loaded = BalanceFile.Load(path);
    foreach (var warning in loaded.Warnings)
    {
        Console.Error.WriteLine($"{Path.GetFileName(path)}: {warning}");
    }

    return BalanceReport.Run(templates, loaded.Balance, seeds);
}

string Number(double value) => value.ToString(Math.Abs(value) >= 100 ? "0" : "0.0#", culture);

string Signed(double value) => (value > 0 ? "+" : string.Empty) + Number(value);

// The repository folder: the working folder or one above it that holds assets/balance.json.
static string? FindRepository()
{
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "assets", "balance.json")))
        {
            return directory.FullName;
        }
    }

    return null;
}
