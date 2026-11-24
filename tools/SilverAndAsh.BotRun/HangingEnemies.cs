using System.Globalization;
using System.Numerics;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Diagnostics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.BotRun;

/// <summary>
/// Mode "stuck": enemies that hang (issue #591). In every combat room the bot walks in until the arena seals, then
/// stands still (it cannot die) first where it came in, then in the middle of the room; in the boss room the same with
/// Gorm. Meanwhile it watches every enemy. One hangs if it is awake and hunting, sees a living player by its own rule
/// (or is in the same room with him), and for <see cref="Kind.Ticks"/> ticks in a row moves less than its own body
/// radius (flat) from where it was and starts no attack. The number of ticks comes from the balancing: the longest
/// pause a healthy one of its kind takes standing still between two attacks, twice over. One instance is one run over all
/// its seeds (no static state).
/// </summary>
internal sealed class HangingEnemies
{
    /// <summary>How many times the longest legitimate pause an enemy may stand still before it counts as hanging.</summary>
    private const float Margin = 2f;

    /// <summary>How many times the longest threshold of the room the bot stands at each place.</summary>
    private const int StandFor = 2;

    // Over all seeds of the run, per kind: how many were watched, and the longest still spell without an attack that
    // stayed below the threshold, with its seed and enemy - printed once at the end, so a threshold far too generous
    // shows and a kind close to hanging can be replayed (issue #699).
    private readonly Dictionary<string, int> _watched = [];
    private readonly LongestStillSpells _longest = new();

    /// <summary>
    /// One kind of enemy: its name, its body radius (it hangs while it stays within it), the longest pause it takes
    /// standing still between two attacks (seconds, from the balancing), its enemies in a world, whether it hunts
    /// (awake, not lurking, not flung away by a blow), whether it sees a player by its own rule, its state for the
    /// finding, and which of its events are an attack (the id of the enemy that attacked, else null). One that
    /// <paramref name="Queues"/> may wait pressed against another enemy (ghouls walk up to a player one behind the other).
    /// </summary>
    private sealed record Kind(
        string Name,
        bool Queues,
        float Radius,
        Func<Balance, float> PauseSeconds,
        Func<GameWorld, IEnumerable<ITarget>> All,
        Func<ITarget, bool> Hunts,
        Func<GameWorld, ITarget, Player, bool> Sees,
        Func<ITarget, string> State,
        Func<GameEvent, int?> Attacked)
    {
        public int Ticks(Balance balance) => SimulationClock.SecondsToTicks(PauseSeconds(balance) * Margin);
    }

    // A new kind of enemy is one more line here. Each sight rule is the enemy's own (Ghoul/Bat.CanSee, Gorm.IsInSight).
    private static readonly Kind[] Kinds =
    [
        new(
            "ghoul",
            true,
            BodySizes.Ghoul.Radius,
            // wind-up, blow, rest, and the way searched again before it walks on
            b => b.Ghoul.WindUpTime + b.Ghoul.RecoverTime + b.Ghoul.RepathTime,
            world => world.Ghouls,
            e => e is Ghoul ghoul && ghoul.State != GhoulState.Lurking && !ghoul.Knockback.IsActive,
            (world, e, player) => e is Ghoul ghoul
                && Play.Distance(ghoul.Position, player.Position) <= world.Balance.Ghoul.SightRange
                && LineOfSight.IsClear(world.EnemyLevel, ghoul.Position + new Vector3(0f, BodySizes.Ghoul.EyeHeight, 0f), player.EyePosition),
            e => ((Ghoul)e).State.ToString(),
            ev => ev switch
            {
                GhoulWindUp w => w.GhoulId,
                GhoulStruck s => s.GhoulId,
                _ => null,
            }),
        new(
            "bat",
            false,
            BodySizes.Bat.Radius,
            // a spit (count and glow), a screech, rising again from a dive straight down, the way searched again
            b => b.Bat.SpitInterval + b.Bat.SpitWindUpTime + b.Bat.ScreechTime + (b.Bat.CircleHeightMax / b.Bat.ClimbSpeed) + b.Bat.RepathTime,
            world => world.Bats,
            e => e is Bat bat && bat.State != BatState.Lurking && !bat.Knockback.IsActive,
            (world, e, player) => e is Bat bat
                && Vector3.Distance(bat.Centre, player.EyePosition) <= world.Balance.Bat.SightRange
                && LineOfSight.IsClear(world.EnemyLevel, bat.Centre, player.EyePosition),
            e => ((Bat)e).State.ToString(),
            ev => ev switch
            {
                BatSpitWindUp w => w.BatId,
                BatSpat s => s.BatId,
                BatScreeched s => s.BatId,
                BatBit b => b.BatId,
                BatDiveMissed m => m.BatId,
                _ => null,
            }),
        new(
            "Gorm",
            false,
            BodySizes.Gorm.Radius,
            // hammer wind-up, shock wave, club stuck, rest, the roar of phase 2, the way searched again
            b => b.Gorm.HammerWindUp + (b.Gorm.ShockwaveRadius / b.Gorm.ShockwaveSpeed) + b.Gorm.StuckTime + b.Gorm.PauseTime + b.Gorm.RoarTime + b.Gorm.RepathTime,
            world => world.Gorm is { } gorm ? [gorm] : [],
            _ => true,
            (world, e, player) => e is Gorm gorm
                && LineOfSight.IsClear(world.EnemyLevel, gorm.Hand, player.Position + new Vector3(0f, BodySizes.Player.Height / 2f, 0f)),
            e => ((Gorm)e).State.ToString(),
            ev => ev switch
            {
                GormWindUp w => w.GormId,
                GormSlammed s => s.GormId,
                GormSwingStarted s => s.GormId,
                GormSwung s => s.GormId,
                GormHookLocked l => l.GormId,
                GormThrew t => t.GormId,
                ClubFreed f => f.GormId,
                GormRoared r => r.GormId,
                _ => null,
            }),
    ];

    public void Run(LevelZero zero, int seed, Report report)
    {
        var probe = zero.NewRun(seed, Play.Balance);
        var watched = new Dictionary<string, int>();
        foreach (var room in probe.Rooms.Where(room => room.Kind == RoomKind.Combat))
        {
            report.Try($"seed {seed} room {room.Index}", () => InArena(zero, seed, room.Index, report, watched));
        }

        report.Try($"seed {seed} boss room", () => InBossRoom(zero, seed, report, watched));

        // A mode that watches nobody must fail loudly, not end with "0 finding(s)" (PR #641): every level has ghouls
        // and Gorm. Bats are not in every level; they are checked over the whole run (PrintLongest).
        foreach (var kind in new[] { "ghoul", "Gorm" })
        {
            report.Check(watched.GetValueOrDefault(kind) == 0, $"seed {seed} stuck: no {kind} watched while it hunted a player - the mode checks (almost) nothing");
        }

        foreach (var (kind, count) in watched)
        {
            _watched[kind] = _watched.GetValueOrDefault(kind) + count;
        }
    }

    /// <summary>
    /// After the last seed: per kind the threshold and the longest still spell seen below it, with seed, enemy and place.
    /// </summary>
    public void PrintLongest(Report report)
    {
        report.Check(_watched.GetValueOrDefault("bat") == 0, "stuck: no bat watched in any seed - the mode checks (almost) nothing for bats");
        foreach (var kind in Kinds)
        {
            Console.WriteLine(_longest.Line(kind.Name, _watched.GetValueOrDefault(kind.Name), kind.Ticks(Play.Balance)));
        }
    }

    /// <summary>Walks into the combat room until it seals, then stands where it came in and in the middle.</summary>
    private void InArena(LevelZero zero, int seed, int roomIndex, Report report, Dictionary<string, int> watched)
    {
        var world = zero.NewRun(seed, Play.Balance);
        var room = world.Rooms[roomIndex];
        var arena = room.DoorIds.Select(id => world.Doors[id])
            .Where(door => door.Kind != DoorKind.Cracked)
            .Select(door => Arena.Find(world, door))
            .FirstOrDefault(arena => arena?.Room == room);
        if (arena is null)
        {
            report.Count("combat room without an arena door (skipped)");
            return;
        }

        var play = new Play(world, report, $"seed {seed} room {roomIndex}");
        arena.Enter(play);
        if (!room.IsSealed)
        {
            report.Count("arena not sealed on walking in (skipped)");
            return;
        }

        report.Count("arenas played");
        var place = new Place(seed, $"room {roomIndex}");
        Stand(play, report, place, "at the door", room, watched);
        play.PlaceAt(Middle(world, room));
        Stand(play, report, place, "in the middle", room, watched);
    }

    /// <summary>Opens the boss door, walks in until the fight starts, then stands where it came in and in the middle.</summary>
    private void InBossRoom(LevelZero zero, int seed, Report report, Dictionary<string, int> watched)
    {
        var world = zero.NewRun(seed, Play.Balance);
        var play = new Play(world, report, $"seed {seed} boss room");
        Play.KillEnemies(world, null);
        if (PortalSite.Find(world, report, play.Context) is not { } site || !site.Open(play, report) || !site.WalkIn(play, report))
        {
            return;
        }

        report.Count("boss rooms played");
        var place = new Place(seed, "boss room");
        Stand(play, report, place, "at the boss door", site.Boss, watched);
        play.PlaceAt(Middle(world, site.Boss));
        Stand(play, report, place, "in the middle", site.Boss, watched);
    }

    /// <summary>The floor cell of <paramref name="room"/> nearest its middle.</summary>
    private static (int X, int Z) Middle(GameWorld world, RoomState room)
    {
        var x = room.Cells.Average(cell => cell.X);
        var z = room.Cells.Average(cell => cell.Z);
        return room.Cells
            .Where(cell => !world.Level[cell.X, cell.Z].IsSolid)
            .OrderBy(cell => ((cell.X - x) * (cell.X - x)) + ((cell.Z - z) * (cell.Z - z)))
            .ThenBy(cell => cell.X)
            .ThenBy(cell => cell.Z)
            .First();
    }

    /// <summary>
    /// Stands still for <see cref="StandFor"/> times the longest threshold of the enemies there and watches every one of
    /// them, tick by tick.
    /// </summary>
    private void Stand(Play play, Report report, Place place, string where, RoomState room, Dictionary<string, int> watched)
    {
        var world = play.World;
        var watches = Kinds.SelectMany(kind => kind.All(world).Where(enemy => !IsDead(enemy)).Select(enemy => new Watch(kind, enemy, kind.Ticks(world.Balance)))).ToList();
        var inRoom = watches.Where(watch => room.Cells.Contains(Play.Cell(watch.Enemy.Body.Position))).ToList();
        if (inRoom.Count == 0)
        {
            report.Count($"{where}: no enemy in the room (skipped)");
            return;
        }

        var ticks = StandFor * inRoom.Max(watch => watch.Threshold);
        var spot = place with { Name = $"{place.Name} {where}" };
        foreach (var watch in watches)
        {
            watch.Restart(world);
        }

        for (var i = 0; i < ticks; i++)
        {
            play.Tick(PlayerInput.None);
            var attacked = world.Events.Select(ev => Kinds.Select(kind => kind.Attacked(ev)).FirstOrDefault(id => id is not null)).OfType<int>().ToHashSet();
            foreach (var watch in watches)
            {
                Observe(play, report, spot, where, watch, attacked);
            }
        }

        foreach (var watch in watches.Where(watch => watch.WasWatched))
        {
            watched[watch.Kind.Name] = watched.GetValueOrDefault(watch.Kind.Name) + 1;
        }
    }

    private void Observe(Play play, Report report, Place place, string where, Watch watch, HashSet<int> attacked)
    {
        var world = play.World;
        var enemy = watch.Enemy;
        var at = enemy.Body.Position;
        var hunting = enemy.IsHittable && !world.IsAsleep(enemy) && watch.Kind.Hunts(enemy);
        var prey = hunting ? world.Players.FirstOrDefault(player => !player.IsDead && (watch.Kind.Sees(world, enemy, player) || SameRoom(world, at, player.Position))) : null;
        if (prey is null || attacked.Contains(enemy.Id) || Play.Distance(at, watch.Anchor) > watch.Kind.Radius)
        {
            watch.Restart(world);
            return;
        }

        watch.WasWatched = true;
        var spell = new StillSpell(watch.Kind.Name, place.Seed, place.Name, enemy.Id, (int)(world.Ticks - watch.Since), watch.Threshold);
        var still = spell.Ticks;
        if (!spell.Hangs)
        {
            if (!watch.Kind.Queues || !Crowded(world, enemy))
            {
                _longest.Record(spell);
            }

            return;
        }

        // A ghoul queuing behind another one that strikes, pressed against it, waits its turn; it does not hang. Counted,
        // so a level where that happens a lot still shows. A bat flies round the others, Gorm is alone.
        if (watch.Kind.Queues && Crowded(world, enemy))
        {
            report.Count($"{watch.Kind.Name}: stood {watch.Threshold} ticks pressed against another enemy (not counted as hanging)");
            watch.Restart(world);
            return;
        }

        if (!watch.Reported)
        {
            watch.Reported = true;
            var cell = Play.Cell(at);
            var others = Kinds.SelectMany(kind => kind.All(world)).Where(other => other != enemy && !IsDead(other)).Select(other => Play.Distance(other.Body.Position, at)).DefaultIfEmpty(float.PositiveInfinity).Min();
            report.Finding(string.Create(CultureInfo.InvariantCulture,
                $"{play.Context} ({where}): {watch.Kind.Name} {enemy.Id} hangs from tick {watch.Since} to {world.Ticks}: moved less than {watch.Kind.Radius:0.00} m for {still} ticks without an attack, state {watch.Kind.State(enemy)}, at ({at.X:0.00}, {at.Y:0.00}, {at.Z:0.00}) cell {cell.X},{cell.Z}, player at ({prey.Position.X:0.00}, {prey.Position.Z:0.00}) {Play.Distance(prey.Position, at):0.00} m away, {(float.IsInfinity(others) ? "no other enemy" : "next enemy " + others.ToString("0.00", CultureInfo.InvariantCulture) + " m away")}"));
        }
    }

    private static bool IsDead(ITarget enemy) => enemy switch
    {
        Ghoul ghoul => ghoul.IsDead,
        Bat bat => bat.IsDead,
        Gorm gorm => gorm.IsDead,
        _ => !enemy.IsHittable,
    };

    // Its body touches that of another living enemy (with 5 cm to spare; a bat flying over a ghoul does not touch it).
    private static bool Crowded(GameWorld world, ITarget enemy)
    {
        var body = enemy.Body;
        return Kinds.SelectMany(kind => kind.All(world)).Any(other => other != enemy && !IsDead(other)
            && Play.Distance(other.Body.Position, body.Position) < body.Radius + other.Body.Radius + 0.05f
            && other.Body.Position.Y < body.Position.Y + body.Height
            && body.Position.Y < other.Body.Position.Y + other.Body.Height);
    }

    // Both in the same room (by the cells under their feet).
    private static bool SameRoom(GameWorld world, Vector3 a, Vector3 b)
    {
        var (ca, cb) = (Play.Cell(a), Play.Cell(b));
        return world.Rooms.Any(room => room.Cells.Contains(ca) && room.Cells.Contains(cb));
    }

    /// <summary>Where the bot stands: the seed and the place, e.g. "room 4" or, while it stands, "room 4 at the door".</summary>
    private readonly record struct Place(int Seed, string Name);

    /// <summary>One enemy being watched: where and since which world tick it has stood still without an attack.</summary>
    private sealed class Watch(Kind kind, ITarget enemy, int threshold)
    {
        public Kind Kind => kind;

        public ITarget Enemy => enemy;

        public int Threshold => threshold;

        public Vector3 Anchor { get; private set; }

        public long Since { get; private set; }

        public bool WasWatched { get; set; }

        public bool Reported { get; set; }

        public void Restart(GameWorld world)
        {
            Anchor = enemy.Body.Position;
            Since = world.Ticks;
        }
    }
}
