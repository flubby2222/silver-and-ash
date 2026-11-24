using System.Globalization;
using SilverAndAsh.BotRun;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

// The bot run (issue #573). Usage, from the repository folder:
//   dotnet run --project tools/SilverAndAsh.BotRun -c Release -- arena                 -> seeds 1..6
//   dotnet run --project tools/SilverAndAsh.BotRun -c Release -- death --seeds 1..20
//   dotnet run --project tools/SilverAndAsh.BotRun -c Release -- boss --seeds 7
//   dotnet run --project tools/SilverAndAsh.BotRun -c Release -- slam                  -> the boss portal (issue #629)
//   dotnet run --project tools/SilverAndAsh.BotRun -c Release -- bat
//   dotnet run --project tools/SilverAndAsh.BotRun -c Release -- stuck --seeds 1..40   -> enemies that hang (issue #591)
//   dotnet run --project tools/SilverAndAsh.BotRun -c Release -- seeds --seeds 1..1000
//   dotnet run --project tools/SilverAndAsh.BotRun -c Release -- all
// Exit code: 0 = no findings, 1 = findings, 2 = wrong arguments or the tool itself failed.

// The modes, in the order "all" plays them. A new mode is one more line here (issue #629), not one more branch.
var stuck = new HangingEnemies();
BotMode[] modes =
[
    new("arena", "every arena door: seal, escape attempts, clear, walk out; two arenas side by side; the boss door", ArenaDoors.Run),
    new("death", "die in a sealed arena and in the boss fight, restart, walk in again", DeathAndRestart.Run),
    new("boss", "run to the boss (antechamber, teleporters, boss door), win, climb the stairs", BossRun.Run),
    new("slam", "the boss door slams shut with a body in its doorway (player, ghoul, bat, Gorm): nobody in the stone", DoorSlam.Run),
    new("bat", "a bat at the boss portal: knocked at the stone, flying round a player there, falling dead onto a plinth", PortalBat.Run),
    new("stuck", "stand still in every arena and the boss room: no enemy that hunts a player may hang without attacking", stuck.Run, stuck.PrintLongest),
    new("seeds", "build every level twice: failures, generator tries, same level for the same seed", SeedProbe.Run, SeedProbe.PrintAttemptNote),
];
var usage = $"Usage: <{string.Join('|', modes.Select(m => m.Name))}|all> [--seeds FROM..TO] [--repo FOLDER]";

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine(usage);
    foreach (var each in modes)
    {
        Console.WriteLine($"  {each.Name,-6} {each.Help}");
    }

    Console.WriteLine($"  {"all",-6} all of the above, one after the other");
    return args.Length == 0 ? 2 : 0;
}

var mode = args[0];
var chosen = mode == "all" ? modes : modes.Where(m => m.Name == mode).ToArray();
if (chosen.Length == 0)
{
    Console.Error.WriteLine($"Unknown mode '{mode}'. {usage}");
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
            Console.Error.WriteLine($"Unknown or incomplete argument '{args[i]}'. {usage}");
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
foreach (var current in chosen)
{
    report.Section(current.Name);
    for (var seed = from; seed <= to; seed++)
    {
        try
        {
            current.Run(zero, seed, report);
        }
        catch (MissingCoreMember e)
        {
            Console.Error.WriteLine($"The bot itself failed: {e.Message}");
            return 2;
        }
        catch (Exception e)
        {
            report.Finding($"seed {seed}: {current.Name} threw {e.GetType().Name}: {e.Message}");
        }
    }

    current.After?.Invoke(report);
}

report.Print(mode, from, to);
return report.Findings == 0 ? 0 : 1;

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

/// <summary>
/// One mode of the bot: its name on the command line, one line of help, what it plays for one seed, and what it prints
/// once after the last seed (if anything).
/// </summary>
internal sealed record BotMode(string Name, string Help, Action<LevelZero, int, Report> Run, Action<Report>? After = null);

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
