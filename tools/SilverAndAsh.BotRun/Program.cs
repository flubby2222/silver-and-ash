using System.Globalization;
using SilverAndAsh.BotRun;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

// The bot run (issue #573). Usage, from the repository folder:
//   dotnet run --project tools/SilverAndAsh.BotRun -c Release -- arena                 -> seeds 1..6
//   dotnet run --project tools/SilverAndAsh.BotRun -c Release -- death --seeds 1..20
//   dotnet run --project tools/SilverAndAsh.BotRun -c Release -- boss --seeds 7
//   dotnet run --project tools/SilverAndAsh.BotRun -c Release -- seeds --seeds 1..1000
//   dotnet run --project tools/SilverAndAsh.BotRun -c Release -- all
// Exit code: 0 = no findings, 1 = findings, 2 = wrong arguments or the tool itself failed.

const string Usage = "Usage: <arena|death|boss|seeds|all> [--seeds FROM..TO] [--repo FOLDER]";
string[] modes = ["arena", "death", "boss", "seeds"];

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine(Usage);
    Console.WriteLine("  arena  every arena door: seal, escape attempts, clear, walk out; two arenas side by side; the boss door");
    Console.WriteLine("  death  die in a sealed arena and in the boss fight, restart, walk in again");
    Console.WriteLine("  boss   run to the boss (antechamber, teleporters, boss door), win, climb the stairs");
    Console.WriteLine("  seeds  build every level twice: failures, generator tries, same level for the same seed");
    Console.WriteLine("  all    all of the above, one after the other");
    return args.Length == 0 ? 2 : 0;
}

var mode = args[0];
if (mode != "all" && !modes.Contains(mode))
{
    Console.Error.WriteLine($"Unknown mode '{mode}'. {Usage}");
    return 2;
}

var from = 1;
var to = 6;
string? repo = null;
for (var i = 1; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--seeds" when i + 1 < args.Length && SeedRange.TryParse(args[i + 1], out from, out to):
            i++;
            break;
        case "--repo" when i + 1 < args.Length:
            repo = args[++i];
            break;
        default:
            Console.Error.WriteLine($"Unknown or incomplete argument '{args[i]}'. {Usage}");
            return 2;
    }
}

repo ??= FindRepository();
if (repo is null || !Directory.Exists(Path.Combine(repo, "assets", "rooms")))
{
    Console.Error.WriteLine("Could not find the 'assets/rooms' folder - run the bot from inside the repository or pass --repo.");
    return 2;
}

// The balancing as the game ships it (assets/balance.json), so a balancing round is played with its new values.
var loaded = BalanceFile.Load(Path.Combine(repo, "assets", "balance.json"));
foreach (var warning in loaded.Warnings)
{
    Console.Error.WriteLine($"balance.json: {warning}");
}

Play.Use(loaded.Balance);
var zero = new LevelZero(RoomTemplateParser.LoadAll(Path.Combine(repo, "assets", "rooms")));
var report = new Report();
foreach (var current in mode == "all" ? modes : [mode])
{
    report.Section(current);
    for (var seed = from; seed <= to; seed++)
    {
        try
        {
            Run(current, seed);
        }
        catch (MissingCoreMember e)
        {
            Console.Error.WriteLine($"The bot itself failed: {e.Message}");
            return 2;
        }
        catch (Exception e)
        {
            report.Finding($"seed {seed}: {current} threw {e.GetType().Name}: {e.Message}");
        }
    }

    if (current == "seeds")
    {
        SeedProbe.PrintAttemptNote(report);
    }
}

report.Print(mode, from, to);
return report.Findings == 0 ? 0 : 1;

void Run(string current, int seed)
{
    switch (current)
    {
        case "arena":
            ArenaDoors.Run(zero, seed, report);
            break;
        case "death":
            DeathAndRestart.Run(zero, seed, report);
            break;
        case "boss":
            BossRun.Run(zero, seed, report);
            break;
        default:
            SeedProbe.Run(zero, seed, report);
            break;
    }
}

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

/// <summary>Reads "7" or "1..6" (also "1-6").</summary>
internal static class SeedRange
{
    public static bool TryParse(string text, out int from, out int to)
    {
        var parts = text.Split(["..", "-"], StringSplitOptions.None);
        from = 0;
        to = 0;
        if (parts.Length is < 1 or > 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out from)
            || !int.TryParse(parts[^1], NumberStyles.None, CultureInfo.InvariantCulture, out to))
        {
            return false;
        }

        return from <= to;
    }
}
