using System.Globalization;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.BotRun;

/// <summary>How a fight run ended.</summary>
internal enum FightEnd
{
    Won,
    Died,

    /// <summary>Nothing changed for <see cref="FightMode.StuckTicks"/> ticks: no enemy hurt, no room cleared, no new cell.</summary>
    Stuck,

    /// <summary>The run reached <see cref="FightMode.TickLimit"/> and was ended.</summary>
    TickLimit,
}

/// <summary>What one seed and weapon came to (issue #803): everything the output line says.</summary>
internal sealed record FightResult(
    int Seed, WeaponKind Weapon, FightEnd End, long Ticks, int Level, int CombatCleared, int CombatTotal,
    string Place, string LastDamage, int DamageTaken, int DamageDealt)
{
    public bool Won => End == FightEnd.Won;

    public string Line() => string.Create(CultureInfo.InvariantCulture,
        $"seed {Seed} {Weapon}: {Outcome(End)} after {Clock(Ticks)} ({Ticks} ticks) | level {Level} | combat rooms {CombatCleared}/{CombatTotal} cleared | {Where(End)} {Place} | last damage {LastDamage} | damage taken {DamageTaken} | damage dealt {DamageDealt}");

    private static string Outcome(FightEnd end) => end switch
    {
        FightEnd.Won => "WON",
        FightEnd.Died => "LOST",
        FightEnd.Stuck => "STUCK (no progress)",
        _ => "TICK LIMIT",
    };

    private static string Where(FightEnd end) => end == FightEnd.Died ? "died in" : "ended in";

    /// <summary>Game time as minutes:seconds.tenths from ticks (60 per second).</summary>
    public static string Clock(long ticks)
    {
        var tenths = ticks * 10 / 60;
        return string.Create(CultureInfo.InvariantCulture, $"{tenths / 600}:{tenths / 10 % 60:00}.{tenths % 10}");
    }
}

/// <summary>The weapons the fight mode plays, and the names the command line gives them.</summary>
internal static class FightWeapons
{
    public static IReadOnlyList<WeaponKind> All { get; } =
        [WeaponKind.SilverBlade, WeaponKind.Pistol, WeaponKind.HuntersAxe, WeaponKind.Whip, WeaponKind.StakeCrossbow];

    private static readonly Dictionary<string, WeaponKind> Names = new()
    {
        ["blade"] = WeaponKind.SilverBlade,
        ["silverblade"] = WeaponKind.SilverBlade,
        ["pistol"] = WeaponKind.Pistol,
        ["axe"] = WeaponKind.HuntersAxe,
        ["huntersaxe"] = WeaponKind.HuntersAxe,
        ["whip"] = WeaponKind.Whip,
        ["crossbow"] = WeaponKind.StakeCrossbow,
        ["stakecrossbow"] = WeaponKind.StakeCrossbow,
    };

    /// <summary>Reads "all" or a list like "blade,whip" (also in any case, with - or _ inside the names).</summary>
    public static bool TryParse(string text, out IReadOnlyList<WeaponKind> weapons)
    {
        weapons = [];
        if (text.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            weapons = All;
            return true;
        }

        var found = new List<WeaponKind>();
        foreach (var part in text.Split(','))
        {
            var key = part.Replace("-", "").Replace("_", "").Trim().ToLowerInvariant();
            if (!Names.TryGetValue(key, out var kind))
            {
                return false;
            }

            if (!found.Contains(kind))
            {
                found.Add(kind);
            }
        }

        weapons = found;
        return found.Count > 0;
    }

    /// <summary>The message for a --weapon value that is no weapon (issue #805, hint 263): it names the valid ones.</summary>
    public static string UnknownMessage(string text) => $"Unknown weapon '{text}'. Valid: all, {Known}.";

    public static string Known { get; } = string.Join(", ", Names.Keys.Order());
}

/// <summary>
/// Mode "fight" (issue #803): a mortal player with a real weapon fights his way from the start to Gorm, played by the
/// simple bot <see cref="FightBot"/>. Nothing is set but the start weapon, which comes the way --give gives it
/// (<see cref="GiveOptions.GiveTo"/>). Not part of "all": a run takes long, and a lost fight is no finding.
/// What it measures is a lower bound - the bot keeps away, retreats and dodges (issue #805) but does not pick its weapon.
/// </summary>
internal sealed class FightMode
{
    /// <summary>A run ends after this many ticks (20 minutes of game time).</summary>
    public const int TickLimit = 60 * 60 * 20;

    /// <summary>A run ends as stuck after this many ticks without a change (30 seconds).</summary>
    public const int StuckTicks = 60 * 30;

    private readonly Dictionary<WeaponKind, (int Won, int Played)> _tally = [];
    private bool _noted;

    /// <summary>The balancing the player and the enemies have - mortal, unlike <see cref="Play.Balance"/>.</summary>
    public Balance Balance { get; set; } = Balance.Default;

    public IReadOnlyList<WeaponKind> Weapons { get; set; } = FightWeapons.All;

    public const string LowerBound = "NOTE the fight bot plays simply and the same every time (walk or aim, attack, reload, heal at half health, rooms in order up to Gorm; it keeps its distance, steps out of reach and dodges, but picks no weapon and visits no side rooms): every result is a lower bound of what a player achieves, not what the game allows.";

    public void Run(LevelZero zero, int seed, Report report)
    {
        if (!_noted)
        {
            Console.WriteLine(LowerBound);
            _noted = true;
        }

        foreach (var weapon in Weapons)
        {
            report.Try($"seed {seed} fight {weapon}", () =>
            {
                var result = Fight(zero, seed, weapon, report);
                Console.WriteLine(result.Line());
                var (won, played) = _tally.GetValueOrDefault(weapon);
                _tally[weapon] = (won + (result.Won ? 1 : 0), played + 1);
                report.Count($"{weapon}: {Outcome(result)}");
            });
        }
    }

    /// <summary>One line per weapon: how many of the runs were won.</summary>
    public void PrintWinRates(Report report)
    {
        Console.WriteLine();
        Console.WriteLine(LowerBound);
        foreach (var weapon in Weapons)
        {
            var (won, played) = _tally.GetValueOrDefault(weapon);
            Console.WriteLine(WinRateLine(weapon, won, played));
        }
    }

    public static string WinRateLine(WeaponKind weapon, int won, int played) => string.Create(CultureInfo.InvariantCulture,
        $"WIN RATE {weapon}: {won}/{played} ({(played == 0 ? 0 : 100.0 * won / played):0}%)");

    private static string Outcome(FightResult result) => result.End switch
    {
        FightEnd.Won => "won",
        FightEnd.Died => "lost",
        FightEnd.Stuck => "stuck",
        _ => "tick limit",
    };

    /// <summary>Plays one seed with one weapon until it is won, lost, stuck or out of ticks.</summary>
    public FightResult Fight(LevelZero zero, int seed, WeaponKind weapon, Report report)
    {
        var world = zero.NewRun(seed, Balance);
        GiveStartWeapon(world, weapon);
        var play = new Play(world, report, $"seed {seed} fight {weapon}");
        var bot = new FightBot(play, weapon, Balance);
        var lastDamage = "none";
        var progress = new Progress(world);
        var end = FightEnd.TickLimit;
        while (true)
        {
            if (world.IsWon)
            {
                end = FightEnd.Won;
                break;
            }

            if (world.Player.IsDead)
            {
                end = FightEnd.Died;
                break;
            }

            if (world.Ticks >= TickLimit)
            {
                break;
            }

            play.Tick(bot.Next());
            lastDamage = DamageSource.Of(world.Events) ?? lastDamage;
            if (progress.TicksUnchanged(world) >= StuckTicks)
            {
                end = FightEnd.Stuck;
                break;
            }
        }

        return new FightResult(
            seed, weapon, end, world.Ticks, world.Player.Level,
            world.Rooms.Count(room => room.Kind == RoomKind.Combat && room.IsCleared),
            world.Rooms.Count(room => room.Kind == RoomKind.Combat),
            PlaceOf(world), lastDamage, world.Statistics.DamageTaken, world.Statistics.DamageDealt);
    }

    // The one way to a start weapon: the one --give uses. The two start weapons are already in hand.
    private static void GiveStartWeapon(GameWorld world, WeaponKind weapon)
    {
        var player = world.Player;
        if (player.Melee != weapon && !player.RangedSlots.Any(slot => slot?.Kind == weapon))
        {
            GiveOptions.GiveTo(world, [GiveOptions.Find(weapon.ToString())]);
        }
    }

    private static string PlaceOf(GameWorld world)
    {
        var cell = Play.Cell(world.Player.Position);
        return world.Rooms.FirstOrDefault(room => room.Cells.Contains(cell)) is { } room
            ? room.Kind == RoomKind.Boss ? "Gorm" : $"room {room.Index} ({room.Kind})"
            : "between rooms";
    }

    /// <summary>Counts the ticks the world stays as it is: the player's cell, the health of all enemies, the rooms cleared, the doors open.</summary>
    private sealed class Progress(GameWorld world)
    {
        private (int X, int Z, int Enemies, int Cleared, int Doors) _seen = Snapshot(world);
        private long _since = world.Ticks;

        public long TicksUnchanged(GameWorld now)
        {
            var current = Snapshot(now);
            if (current != _seen)
            {
                _seen = current;
                _since = now.Ticks;
            }

            return now.Ticks - _since;
        }

        private static (int X, int Z, int Enemies, int Cleared, int Doors) Snapshot(GameWorld now)
        {
            var (x, z) = Play.Cell(now.Player.Position);
            var health = now.Ghouls.Sum(each => each.Health) + now.Bats.Sum(each => each.Health) + (now.Gorm?.Health ?? 0);
            return (x, z, health, now.Rooms.Count(room => room.IsCleared), now.Doors.Count(door => door.IsOpen));
        }
    }
}
