using System.Globalization;
using System.Numerics;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.BotRun;

/// <summary>
/// The stone portal round the boss door of one world (issue #598): the door, the boss room behind it and places given
/// across the passage (from its middle) and along it (from the door plane, towards the boss room).
/// </summary>
internal sealed record PortalSite(Door Door, BossPortal Portal, RoomState Boss, float Inwards)
{
    /// <summary>The portal of the boss door; null (with a finding) if the level has none.</summary>
    public static PortalSite? Find(GameWorld world, Report report, string context)
    {
        var door = world.Doors.Single(door => door.Kind == DoorKind.Boss);
        var boss = world.Rooms[door.RoomA].Kind == RoomKind.Boss ? world.Rooms[door.RoomA] : world.Rooms[door.RoomB];
        var (x, z) = door.Cells[0];
        var portals = world.Level.Portals.Where(portal => portal.Frames(x, z)).ToList();
        if (report.Check(portals.Count == 0, $"{context}: no stone portal frames boss door {door.Id}")
            || report.Check(Play.NeighbourIn(door, boss) is null, $"{context}: boss door {door.Id} has no floor cell in the boss room"))
        {
            return null;
        }

        var portal = portals[0];
        var (bx, bz) = Play.NeighbourIn(door, boss)!.Value;
        var inBoss = world.Level.CellCenter(bx, bz);
        var along = portal.PassesAlongZ ? inBoss.Z - portal.Z : inBoss.X - portal.X;
        return new PortalSite(door, portal, boss, MathF.Sign(along));
    }

    /// <summary>
    /// The point <paramref name="across"/> metres beside the middle of the passage, <paramref name="along"/> metres from
    /// the door plane towards the boss room (negative: towards the antechamber), <paramref name="up"/> above the floor.
    /// </summary>
    public Vector3 At(float across, float along, float up = 0f) => Portal.PassesAlongZ
        ? new Vector3(Portal.X + across, Portal.Floor + up, Portal.Z + (along * Inwards))
        : new Vector3(Portal.X + (along * Inwards), Portal.Floor + up, Portal.Z + across);

    /// <summary>Opens the boss door from in front of it, as in mode "arena"; false (with a finding) if it stays shut.</summary>
    public bool Open(Play play, Report report)
    {
        play.GiveKeys();
        play.PlaceAt(At(0f, -2f));
        play.Face(At(0f, 2f));
        play.Tick(Play.Interact);
        return !report.Check(!play.TicksUntil(600, PlayerInput.None, () => !Door.Blocks), $"{play.Context}: boss door {Door.Id} does not open (barred {Door.IsBarred})");
    }

    /// <summary>Walks through the open boss door until the boss room seals; false (with a finding) if it never does.</summary>
    public bool WalkIn(Play play, Report report) =>
        !report.Check(
            !play.TicksUntil(600, Play.Forward, () => { play.Face(At(0f, 4f)); return Boss.IsSealed; }),
            $"{play.Context}: boss room not sealed after walking in, player at {play.World.Player.Position}");

    /// <summary>
    /// How far <paramref name="at"/> lies past the door's box (Door.BoxMin/BoxMax, the whole door cells) on the side of
    /// the boss room, along the passage, in metres; negative inside the box or before it.
    /// </summary>
    public float BeyondDoor(Vector3 at) => Portal.PassesAlongZ
        ? Inwards > 0 ? at.Z - Door.BoxMax.Z : Door.BoxMin.Z - at.Z
        : Inwards > 0 ? at.X - Door.BoxMax.X : Door.BoxMin.X - at.X;

    /// <summary>True if the middle of a body at <paramref name="at"/> lies inside the door's box along the passage.</summary>
    public bool InDoorway(Vector3 at)
    {
        var (low, high, along) = Portal.PassesAlongZ ? (Door.BoxMin.Z, Door.BoxMax.Z, at.Z) : (Door.BoxMin.X, Door.BoxMax.X, at.X);
        return along > low && along < high;
    }

    /// <summary>
    /// Wakes the first of <paramref name="enemies"/> that waits in a combat room behind an arena door, as in a real run:
    /// the player enters its arena, which seals, and it comes out. Only an awake enemy acts - is knocked back, moves.
    /// Null (with a finding) if none does.
    /// </summary>
    public static T? WakeInArena<T>(Play play, IEnumerable<T> enemies, string kind, Report report)
        where T : class, ITarget
    {
        var world = play.World;
        foreach (var enemy in enemies)
        {
            var room = world.Rooms.FirstOrDefault(room => room.Kind == RoomKind.Combat && room.Cells.Contains(Play.Cell(enemy.Body.Position)));
            var arena = room?.DoorIds.Select(id => world.Doors[id])
                .Where(door => door.Kind != DoorKind.Cracked)
                .Select(door => Arena.Find(world, door))
                .FirstOrDefault(arena => arena?.Room == room);
            if (arena is null)
            {
                continue;
            }

            arena.Enter(play);
            return report.Check(!play.TicksUntil(600, PlayerInput.None, () => enemy.IsHittable && !world.IsAsleep(enemy)), $"{play.Context}: {kind} {enemy.Id} not awake after its arena sealed")
                ? null
                : enemy;
        }

        report.Finding($"{play.Context}: no {kind} in an arena");
        return null;
    }

    public static string Where(float across, float along) =>
        string.Create(CultureInfo.InvariantCulture, $"across {across:0.00} along {along:0.00}");

    /// <summary>The places of a grid, from <paramref name="from"/> to <paramref name="to"/> in steps of <paramref name="step"/>.</summary>
    public static IEnumerable<float> Steps(float from, float to, float step)
    {
        for (var i = 0; from + (i * step) <= to + 1e-4f; i++)
        {
            yield return from + (i * step);
        }
    }
}

/// <summary>
/// Mode "slam": the boss door slams shut (hard stop, issue #566) with a body in its doorway - the player (a second
/// player in co-op), a ghoul, a bat and Gorm, each at every free place of a grid over the doorway. The shutting door
/// pushes it into the boss room (GameWorld.PushOutOf); nobody may end up in the portal's stone (issue #598, PR #623) or in
/// the blocking door. The body is set into the doorway again every tick until the door blocks, so it stands there at the
/// slam. Then the player, a ghoul and a bat run into the doorway from the antechamber - walking in, or knocked in by a
/// club blow - timed so that they are in it in the slam tick. Each time the bot checks that the door has really pushed
/// the body just past its box (issue #647): a body that was not in the doorway at the slam is a finding, not a pass.
/// </summary>
internal static class DoorSlam
{
    // Where the push leaves a body: its radius and GameWorld.ThresholdGap past the door's box (GameWorld.OnThreshold),
    // give or take a little for rounding.
    private const float PushTolerance = 0.002f;

    // A running body starts one body width before the door's box (on the antechamber side), clear of the doorway, and is
    // let go so that it is in the doorway in the slam tick: the player walks in (full input), a ghoul or bat is knocked in
    // by a charged club blow. The run-up follows the body, not the club (issue #676): awake ghouls and bats walk into the
    // doorway on their own, so the run checks at the slam that the knock still carries them (RunningCase.Carried). A
    // run-up taken from the knock would shrink with it, and a knock too short to carry a body could never be seen.
    private static float RunUp(BodyShape shape) => 2f * shape.Radius;

    // Ticks a running body is held at its start before it is let go, in the probe that finds when it reaches the doorway.
    private const int ProbeHold = 10;

    // Walking bodies step as the player does (BodySizes.Player.StepHeight); a bat flies over any floor
    // (BodySizes.Bat.StepHeight). Standing and Running: places played per side of the middle at least - about three
    // quarters of what seeds 1..6 play today (issue #647: standing player 16, ghoul 14, bat 21, Gorm 3; running player
    // 9, ghoul 9, bat 17 on each side - the ghoul 13 until issue #676, when 4 places a side beside the middle turned out
    // to be walked in after the knock), so a grid half as fine is a finding. Gorm does not run in: he has no knockback,
    // and his own steps would make the run-up depend on the tick it starts (a gap, issue #647).
    private static readonly Body[] Bodies =
    [
        new("player", new(BodySizes.Player.Radius, BodySizes.Player.Height, BodySizes.Player.StepHeight, Flies: false), 12,
            _ => null, new(7, (_, _) => null, RunInPlayer, _ => true)),
        new("ghoul", new(BodySizes.Ghoul.Radius, BodySizes.Ghoul.Height, BodySizes.Player.StepHeight, Flies: false), 10,
            world => world.Ghouls.FirstOrDefault(ghoul => !ghoul.IsDead),
            new(7, (play, report) => PortalSite.WakeInArena(play, play.World.Ghouls.Where(ghoul => !ghoul.IsDead), "ghoul", report), KnockIn, IsKnocked)),
        new("bat", new(BodySizes.Bat.Radius, BodySizes.Bat.Height, BodySizes.Bat.StepHeight, Flies: true), 15,
            world => world.Bats.FirstOrDefault(bat => !bat.IsDead),
            new(13, (play, report) => PortalSite.WakeInArena(play, play.World.Bats.Where(bat => !bat.IsDead), "bat", report), KnockIn, IsKnocked)),
        new("Gorm", new(BodySizes.Gorm.Radius, BodySizes.Gorm.Height, BodySizes.Player.StepHeight, Flies: false), 2,
            world => world.Gorm, Running: null),
    ];

    public static void Run(LevelZero zero, int seed, Report report)
    {
        var ctx = $"seed {seed} slam";
        var start = zero.NewRun(seed, Play.Balance);
        Play.CheckPortalStoneHeights(report, ctx, Bodies.Select(body => body.Shape.Height));

        // The free places of the doorway, with the door open (the door cell is floor then).
        var probe = new Play(start.Restarted(), report, $"{ctx} probe");
        if (PortalSite.Find(probe.World, report, ctx) is not { } site || !site.Open(probe, report))
        {
            return;
        }

        // Where the push leaves a body depends on how far beside the middle it stands (the depth it gets from the door,
        // GameWorld.OnThreshold): so every 5 cm across, from one free depth there. Free places beside the middle are
        // few and narrow - the stepped jambs (PR #623: 0.8 m beside the middle, only 0.65 m deep in the outer reveal).
        foreach (var body in Bodies)
        {
            var places = report.Tally($"{ctx} {body.Name}", $"{body.Name}: no free place at this distance from the middle", body.Standing);
            foreach (var across in PortalSite.Steps(-1.95f, 1.95f, 0.05f))
            {
                var free = PortalSite.Steps(-0.95f, 0.95f, 0.05f)
                    .Select(along => (float?)along)
                    .FirstOrDefault(along => body.Shape.IsFree(probe.World.Level, body.Shape.Place(probe.World.Level, site.At(across, along!.Value))));
                if (free is not { } along)
                {
                    places.Skipped();
                    continue;
                }

                var where = $"{ctx} {body.Name} {PortalSite.Where(across, along)}";
                report.Try(where, () => Slam(start, where, body, across, along, report, places));
            }

            places.Verify();
        }

        foreach (var body in Bodies)
        {
            if (body.Running is { } running)
            {
                RunsIn(start, ctx, body, running, probe.World.Level, site, report);
            }
        }
    }

    private static void Slam(GameWorld start, string where, Body body, float across, float along, Report report, Report.Places places)
    {
        if (Prepare(start, where, body, report) is not { } prepared)
        {
            return;
        }

        var (play, site, enemy) = prepared;
        var at = body.Shape.Place(play.World.Level, site.At(across, along));
        var limit = Play.BossDoorShutTicks(play.World);
        for (var tick = 0; tick <= limit && !site.Door.Blocks; tick++)
        {
            PlaceAt(play, enemy, at);
            play.Tick(PlayerInput.None);
        }

        if (report.Check(!site.Door.Blocks, $"{where}: boss door does not block {limit} ticks after the boss room sealed")
            || !Pushed(play, site, body, enemy, where, report))
        {
            return;
        }

        play.Ticks(30, PlayerInput.None);
        report.Count($"{body.Name}: in the doorway at the slam");
        places.Played(across);
    }

    // The body runs into the doorway and is in it in the slam tick: from every free place RunUp before the door's box,
    // every 10 cm across.
    private static void RunsIn(GameWorld start, string ctx, Body body, RunningCase running, Level level, PortalSite site, Report report)
    {
        var places = report.Tally($"{ctx} {body.Name} running", $"{body.Name}: no free run-up at this distance from the middle or it does not reach the doorway while its run carries it", running.Places);
        var from = -(BossPortalShape.HalfDepth + body.Shape.Radius + RunUp(body.Shape));
        foreach (var across in PortalSite.Steps(-1.9f, 1.9f, 0.1f))
        {
            if (!body.Shape.IsFree(level, body.Shape.Place(level, site.At(across, from))))
            {
                places.Skipped();
                continue;
            }

            var where = $"{ctx} {body.Name} running {PortalSite.Where(across, from)}";
            report.Try(where, () => RunIn(start, where, body, running, across, from, report, places));
        }

        places.Verify();
    }

    // First a probe: held at the start for ProbeHold ticks, then let go - which tick after letting go has it in the
    // doorway, and which tick (counted from the sealing) the door blocks. Then the run itself: held so long that it
    // reaches the doorway in the slam tick.
    private static void RunIn(GameWorld start, string where, Body body, RunningCase running, float across, float from, Report report, Report.Places places)
    {
        if (Prepare(start, where, body, report, running) is not { } prepared)
        {
            return;
        }

        var (probe, probeSite, probeEnemy) = prepared;
        var at = body.Shape.Place(probe.World.Level, probeSite.At(across, from));
        var limit = Play.BossDoorShutTicks(probe.World);
        int? inDoorway = null;
        var carried = false;
        var shut = -1;
        for (var tick = 0; tick <= limit && shut < 0; tick++)
        {
            probe.Tick(Step(probe, probeSite, running, probeEnemy, at, tick < ProbeHold, tick == ProbeHold));
            if (tick >= ProbeHold && inDoorway is null && !probeSite.Door.Blocks && probeSite.InDoorway(Position(probe, probeEnemy)))
            {
                inDoorway = tick - ProbeHold;
                carried = running.Carried(probeEnemy);
            }

            shut = probeSite.Door.Blocks ? tick : -1;
        }

        if (report.Check(shut < 0, $"{where}: boss door does not block {limit} ticks after the boss room sealed"))
        {
            return;
        }

        // A body that gets into the doorway only after its knock is over (the stone of the jamb stopped it, or the knock
        // is too short for the run-up) walked in on its own: no run with a swing, skipped (issue #676).
        var hold = shut - (inDoorway ?? shut);
        if (inDoorway is null || hold < 0 || !carried)
        {
            places.Skipped();
            return;
        }

        if (Prepare(start, where, body, report, running) is not { } again)
        {
            return;
        }

        var (play, site, enemy) = again;
        for (var tick = 0; tick <= shut; tick++)
        {
            play.Tick(Step(play, site, running, enemy, at, tick < hold, tick == hold));
        }

        if (report.Check(!site.Door.Blocks, $"{where}: boss door does not block in tick {shut} after the sealing, as in the probe")
            || report.Check(!running.Carried(enemy), $"{where}: not carried into the doorway - the knock was over at the slam")
            || !Pushed(play, site, body, enemy, where, report))
        {
            return;
        }

        play.Ticks(30, PlayerInput.None);
        report.Count($"{body.Name}: running into the doorway at the slam");
        places.Played(across);
    }

    // One tick of a run-up: held at the start, let go (launched), or running on.
    private static PlayerInput Step(Play play, PortalSite site, RunningCase running, ITarget? enemy, Vector3 at, bool held, bool letGo)
    {
        if (held || letGo)
        {
            PlaceAt(play, enemy, at);
        }

        if (letGo)
        {
            running.Launch(play, site, enemy, at);
        }

        return held || enemy is not null ? PlayerInput.None : Play.Forward;
    }

    // The player walks straight along the passage into the boss room.
    private static void RunInPlayer(Play play, PortalSite site, ITarget? enemy, Vector3 at) => play.Face(at + (Inwards(site) * 10f));

    // A charged club blow along the passage, into the boss room (the farthest knock, as in mode "bat").
    private static void KnockIn(Play play, PortalSite site, ITarget? enemy, Vector3 at)
    {
        var club = play.World.Balance.Club;
        KnockbackOf(enemy).Start(Inwards(site), club.ChargedKnockback, club.KnockbackTime);
    }

    // Whether the knock still carries a knocked body (issue #676).
    private static bool IsKnocked(ITarget? enemy) => KnockbackOf(enemy).IsActive;

    private static Knockback KnockbackOf(ITarget? enemy) => enemy switch
    {
        Ghoul ghoul => ghoul.Knockback,
        Bat bat => bat.Knockback,
        _ => throw new InvalidOperationException($"no knockback for {enemy}"),
    };

    // The direction along the passage into the boss room, on the ground plane.
    private static Vector3 Inwards(PortalSite site) => Vector3.Normalize(site.At(0f, 1f) - site.At(0f, 0f));

    private static Vector3 Position(Play play, ITarget? enemy) => enemy?.Body.Position ?? play.World.Player.Position;

    // A finding unless the shutting door has pushed the body just past its box into the boss room, as GameWorld.PushOutOf
    // does with every body in its doorway - else it was not in the doorway at the slam, and the run checked nothing.
    private static bool Pushed(Play play, PortalSite site, Body body, ITarget? enemy, string where, Report report)
    {
        var beyond = site.BeyondDoor(Position(play, enemy));
        return !report.Check(
            MathF.Abs(beyond - (body.Shape.Radius + GameWorld.ThresholdGap)) > PushTolerance,
            string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{where}: not in the doorway at the slam - {beyond:0.000} m past the door's box, a pushed body stands {body.Shape.Radius + GameWorld.ThresholdGap:0.000} m past it"));
    }

    // A fresh world with the door open, the player walked in and the boss room sealed; the enemy that plays the body
    // (null for the player) alive and every other ghoul and bat dead, the player then in the boss room. Null if not.
    // Running: the enemy is woken first in its arena (RunningCase.Awake), so it acts - a running body must move.
    private static (Play Play, PortalSite Site, ITarget? Enemy)? Prepare(GameWorld start, string where, Body body, Report report, RunningCase? running = null)
    {
        var world = start.Restarted();
        var play = new Play(world, report, where);
        var site = PortalSite.Find(world, report, where)!;
        var enemy = running is null ? body.Enemy(world) : running.Awake(play, report);
        if (body.Name != "player" && enemy is null)
        {
            report.Count($"{body.Name}: none in the level");
            return null;
        }

        // Gorm stays alive: with every enemy of the boss room dead it would be cleared at once, and no door would shut.
        Play.KillEnemies(world, null, enemy);
        if (!site.Open(play, report) || !site.WalkIn(play, report))
        {
            return null;
        }

        if (enemy is not null)
        {
            play.PlaceAt(site.At(0f, 4f));
        }

        return (play, site, enemy);
    }

    // Sets the body there: the player (enemy null) or the enemy that plays it.
    private static void PlaceAt(Play play, ITarget? enemy, Vector3 at)
    {
        if (enemy is null)
        {
            play.PlaceAt(at);
        }
        else
        {
            Play.PlaceEnemyAt(enemy, at);
        }
    }

    // One kind of body in the doorway (issue #672: split up by task): its measures, the places it plays standing per side
    // of the middle at least, which figure of a world plays it (null: the player), and how it runs in - null if it does
    // not (Gorm).
    private sealed record Body(string Name, BodyShape Shape, int Standing, Func<GameWorld, ITarget?> Enemy, RunningCase? Running);

    // The measures of a body: where it stands (a flying body hovers over the floor) and whether a place is free for it.
    private sealed record BodyShape(float Radius, float Height, float StepHeight, bool Flies)
    {
        public Vector3 Place(Level level, Vector3 floor) => Flies ? Bat.HoverPosition(level, floor) : floor;

        public bool IsFree(Level level, Vector3 at) =>
            !CharacterCollision.Overlaps(level, at, Radius, Height, StepHeight) && !Play.InPortalStone(level, new CylinderBody(at, Radius, Height));
    }

    // How a body runs into the doorway: the places it plays per side of the middle at least, how the figure that plays it
    // is woken (null: the player), how it is let go (walking in, knocked in) and whether that still carries it at the
    // slam (the player walks under full input to the end; a knocked body only while its knock lasts, issue #676).
    private sealed record RunningCase(
        int Places, Func<Play, Report, ITarget?> Awake, Action<Play, PortalSite, ITarget?, Vector3> Launch, Func<ITarget?, bool> Carried);
}

/// <summary>
/// Mode "bat": a bat at the boss portal (issues #598, #627). The bot wakes a bat in its arena and opens the boss door.
/// Then it knocks the bat with a charged club blow in eight directions from every free place round the portal, lets it
/// fly round a player standing in front of the portal and in its doorway, and kills bats above each plinth, whose corpses
/// fall onto it. No bat - alive or dead - may end up in the stone.
/// </summary>
internal static class PortalBat
{
    public static void Run(LevelZero zero, int seed, Report report)
    {
        var ctx = $"seed {seed} bat";
        var start = zero.NewRun(seed, Play.Balance);
        Play.CheckPortalStoneHeights(report, ctx, [BodySizes.Bat.Height]);
        if (Awake(start, $"{ctx} knocks", report) is { } knocks)
        {
            Knocks(knocks.Play, knocks.Bat, knocks.Site, report);
        }

        if (Awake(start, $"{ctx} round a player", report) is { } round)
        {
            RoundAPlayer(round.Play, round.Bat, round.Site, report);
        }

        Corpses(start, ctx, report);
    }

    /// <summary>
    /// A fresh world with one bat awake (out of hiding, in the air: it is woken as in a real run, by its arena sealing)
    /// and every other ghoul and bat dead, the player in front of the open boss door. Null (with a finding) if not.
    /// </summary>
    private static (Play Play, Bat Bat, PortalSite Site)? Awake(GameWorld start, string ctx, Report report)
    {
        var world = start.Restarted();
        var play = new Play(world, report, ctx);
        if (PortalSite.WakeInArena(play, world.Bats, "bat", report) is not { } bat)
        {
            return null;
        }

        Play.KillEnemies(world, null, bat);
        if (PortalSite.Find(world, report, ctx) is not { } site || !site.Open(play, report))
        {
            return null;
        }

        return (play, bat, site);
    }

    // A charged club blow (the farthest knock) from every free place round the portal at hover height, in eight
    // directions: at the jambs, the buttresses, the plinths' edges and through the opening. Seeds 1..6 play 36 places on
    // each side of the middle (issue #647): fewer than three quarters of it is a finding.
    private static void Knocks(Play play, Bat bat, PortalSite site, Report report)
    {
        var club = play.World.Balance.Club;
        var places = report.Tally(play.Context, "knocks: places in stone or wall", 27);
        foreach (var across in PortalSite.Steps(-2.6f, 2.6f, 0.4f))
        {
            foreach (var along in PortalSite.Steps(-2f, 2f, 0.5f))
            {
                var at = Bat.HoverPosition(play.World.Level, site.At(across, along));
                if (!IsFree(play.World.Level, at))
                {
                    places.Skipped();
                    continue;
                }

                places.Played(across);

                for (var i = 0; i < 8; i++)
                {
                    var angle = MathF.Tau * i / 8;
                    Play.PlaceEnemyAt(bat, at);
                    bat.Knockback.Start(new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)), club.ChargedKnockback, club.KnockbackTime);
                    play.TicksUntil(60, PlayerInput.None, () => !bat.Knockback.IsActive);
                    play.Tick(PlayerInput.None);
                    report.Count("knocks");
                }
            }
        }

        places.Verify();
    }

    // The bat flies round a player who stands in front of the portal, beside it, or in its doorway (never in the boss
    // room: that would seal it), starting in the boss room, beside the portal and in front of it.
    private static void RoundAPlayer(Play play, Bat bat, PortalSite site, Report report)
    {
        (float Across, float Along)[] players = [(0f, -2f), (-1.2f, -1.6f), (0f, -0.3f), (0.6f, 0.4f)];
        (float Across, float Along)[] bats = [(0f, 3f), (-2.5f, 1.6f), (2.5f, -1.6f)];
        foreach (var (pa, pl) in players)
        {
            foreach (var (ba, bl) in bats)
            {
                play.PlaceAt(site.At(pa, pl));
                Play.PlaceEnemyAt(bat, Bat.HoverPosition(play.World.Level, site.At(ba, bl)));
                play.Ticks(300, PlayerInput.None);
                report.Count("flights round a player");
            }
        }
    }

    // A bat killed above a plinth (its body reaching over it) falls onto it and lies there until it is gone. Each in a
    // fresh world (a sleeping bat falls as well, Bat.Tick).
    private static void Corpses(GameWorld start, string ctx, Report report)
    {
        // Only 4 places per seed lie free over a plinth, 2 on each side of the middle (seeds 1..6, issue #647): one left
        // on a side is already a finding.
        var places = report.Tally($"{ctx} corpse", "corpses: places not over a plinth or in stone", 2);
        foreach (var side in new[] { -1f, 1f })
        {
            foreach (var face in new[] { -1f, 1f })
            {
                foreach (var across in PortalSite.Steps(1.2f, 1.8f, 0.3f))
                {
                    foreach (var along in PortalSite.Steps(1.15f, 1.45f, 0.15f))
                    {
                        report.Try($"{ctx} corpse", () => Corpse(start, ctx, report, side * across, face * along, places));
                    }
                }
            }
        }

        places.Verify();
    }

    private static void Corpse(GameWorld start, string ctx, Report report, float across, float along, Report.Places places)
    {
        var world = start.Restarted();
        var where = $"{ctx} corpse {PortalSite.Where(across, along)}";
        var play = new Play(world, report, where);
        if (PortalSite.Find(world, report, where) is not { } site || world.Bats.FirstOrDefault(bat => !bat.IsDead) is not { } bat)
        {
            return;
        }

        var at = Bat.HoverPosition(world.Level, site.At(across, along));
        var overPlinth = Play.InPortalStone(world.Level, new CylinderBody(site.At(across, along, 0.2f), BodySizes.Bat.Radius, 0.1f));
        if (!overPlinth || !IsFree(world.Level, at))
        {
            places.Skipped();
            return;
        }

        Play.PlaceEnemyAt(bat, at);
        bat.TakeDamage(1_000_000, []);
        play.Ticks(120, PlayerInput.None);
        report.Count(bat.Position.Y >= site.Portal.Floor + 0.45f ? "corpses: lying on a plinth" : "corpses: lying on the floor");
        report.Check(!play.TicksUntil(1200, PlayerInput.None, () => bat.IsGone), $"{where}: corpse not gone after 22 s, at {bat.Position}");
        places.Played(across);
    }

    private static bool IsFree(Level level, Vector3 at) =>
        !CharacterCollision.Overlaps(level, at, BodySizes.Bat.Radius, BodySizes.Bat.Height, BodySizes.Bat.StepHeight)
        && !Play.InPortalStone(level, new CylinderBody(at, BodySizes.Bat.Radius, BodySizes.Bat.Height));
}
