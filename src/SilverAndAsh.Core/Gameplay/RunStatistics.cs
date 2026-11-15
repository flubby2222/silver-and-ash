using System.Globalization;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>How a run ended - or that it is still on.</summary>
public enum RunOutcome
{
    Running,
    Won,
    Lost,
}

/// <summary>
/// The numbers of one run, counted from the world's events (the world feeds every tick's events in). Time only
/// runs while the run is on: not in the Esc pause, not while a bonus is chosen, not after victory or death.
/// </summary>
public sealed class RunStatistics
{
    /// <summary>Ticks the run has been going.</summary>
    public int Ticks { get; private set; }

    /// <summary>How long the run has been going, in seconds.</summary>
    public float Seconds => Ticks / (float)SimulationClock.TicksPerSecond;

    public int GhoulsKilled { get; private set; }

    public int BatsKilled { get; private set; }

    /// <summary>Damage done to enemies (training dummies do not count).</summary>
    public int DamageDealt { get; private set; }

    /// <summary>Damage the players took.</summary>
    public int DamageTaken { get; private set; }

    /// <summary>Arenas whose enemies were all killed.</summary>
    public int RoomsCleared { get; private set; }

    /// <summary>One more tick of the run.</summary>
    public void AddTick() => Ticks++;

    /// <summary>Counts what <paramref name="events"/> (one tick's events) tell.</summary>
    public void Count(IEnumerable<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (var gameEvent in events)
        {
            switch (gameEvent)
            {
                case GhoulDied:
                    GhoulsKilled++;
                    break;
                case BatDied:
                    BatsKilled++;
                    break;
                case GhoulHit hit:
                    DamageDealt += hit.Damage;
                    break;
                case BatHit hit:
                    DamageDealt += hit.Damage;
                    break;
                case PlayerHit hit:
                    DamageTaken += hit.Damage;
                    break;
                case RoomCleared:
                    RoomsCleared++;
                    break;
            }
        }
    }
}

/// <summary>Everything worth keeping about a run: shown on the end screen and written to the local run log.</summary>
public sealed record RunSummary(
    int Seed,
    RunOutcome Outcome,
    float Seconds,
    int GhoulsKilled,
    int BatsKilled,
    int DamageDealt,
    int DamageTaken,
    int Level,
    int RoomsCleared,
    int ArenaRooms)
{
    /// <summary>The run time as minutes:seconds, e.g. "12:34".</summary>
    public string Time
    {
        get
        {
            var whole = (int)Seconds;
            return string.Create(CultureInfo.InvariantCulture, $"{whole / 60}:{whole % 60:00}");
        }
    }

    /// <summary>One line for the run log, e.g. "2026-11-15 18:40:12 | won | seed 123 | 12:34 | ghouls 31 | ...".</summary>
    public string ToLogLine(DateTimeOffset when) => string.Create(
        CultureInfo.InvariantCulture,
        $"{when:yyyy-MM-dd HH:mm:ss} | {Outcome.ToString().ToLowerInvariant()} | seed {Seed} | {Time} | ghouls {GhoulsKilled} | "
        + $"bats {BatsKilled} | dealt {DamageDealt} | taken {DamageTaken} | level {Level} | rooms {RoomsCleared}/{ArenaRooms}");
}

/// <summary>
/// The local run log: one line per finished run, e.g. in <c>%LOCALAPPDATA%\SilverAndAsh\runs.log</c>. Nothing is sent
/// anywhere. A log that cannot be written never stops the game.
/// </summary>
public static class RunLog
{
    /// <summary>Adds <paramref name="line"/> at the end of the file (created with its folder if needed). False if that failed.</summary>
    public static bool Append(string path, string line)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.AppendAllText(path, line + Environment.NewLine);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
