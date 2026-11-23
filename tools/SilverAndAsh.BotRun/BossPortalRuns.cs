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
/// slam.
/// </summary>
internal static class DoorSlam
{
    // Walking bodies step as the player does (BodySizes.Player.StepHeight); a bat flies over any floor (Bat.FlyingStepHeight,
    // private in the core: written here as infinity).
    private static readonly Body[] Bodies =
    [
        new("player", BodySizes.Player.Radius, BodySizes.Player.Height, BodySizes.Player.StepHeight, false, _ => null),
        new("ghoul", BodySizes.Ghoul.Radius, BodySizes.Ghoul.Height, BodySizes.Player.StepHeight, false, world => world.Ghouls.FirstOrDefault(ghoul => !ghoul.IsDead)),
        new("bat", BodySizes.Bat.Radius, BodySizes.Bat.Height, float.PositiveInfinity, true, world => world.Bats.FirstOrDefault(bat => !bat.IsDead)),
        new("Gorm", BodySizes.Gorm.Radius, BodySizes.Gorm.Height, BodySizes.Player.StepHeight, false, world => world.Gorm),
    ];

    public static void Run(LevelZero zero, int seed, Report report)
    {
        var ctx = $"seed {seed} slam";
        var start = zero.NewRun(seed, Play.Balance);

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
            var places = report.Tally($"{ctx} {body.Name}", $"{body.Name}: no free place at this distance from the middle");
            foreach (var across in PortalSite.Steps(-1.95f, 1.95f, 0.05f))
            {
                var free = PortalSite.Steps(-0.95f, 0.95f, 0.05f)
                    .Select(along => (float?)along)
                    .FirstOrDefault(along => body.IsFree(probe.World.Level, body.Place(probe.World.Level, site.At(across, along!.Value))));
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
    }

    private static void Slam(GameWorld start, string where, Body body, float across, float along, Report report, Report.Places places)
    {
        var world = start.Restarted();
        var play = new Play(world, report, where);
        var site = PortalSite.Find(world, report, where)!;
        var enemy = body.Enemy(world);
        if (body.Name != "player" && enemy is null)
        {
            report.Count($"{body.Name}: none in the level");
            return;
        }

        // Gorm stays alive: with every enemy of the boss room dead it would be cleared at once, and no door would shut.
        Play.KillEnemies(world, null, enemy);
        if (!site.Open(play, report) || !site.WalkIn(play, report))
        {
            return;
        }

        if (enemy is not null)
        {
            play.PlaceAt(site.At(0f, 4f));
        }

        var at = body.Place(world.Level, site.At(across, along));
        var limit = Play.BossDoorShutTicks(world);
        for (var tick = 0; tick <= limit && !site.Door.Blocks; tick++)
        {
            if (enemy is null)
            {
                play.PlaceAt(at);
            }
            else
            {
                Play.PlaceEnemyAt(enemy, at);
            }

            play.Tick(PlayerInput.None);
        }

        if (report.Check(!site.Door.Blocks, $"{where}: boss door does not block {limit} ticks after the boss room sealed"))
        {
            return;
        }

        play.Ticks(30, PlayerInput.None);
        report.Count($"{body.Name}: in the doorway at the slam");
        places.Played(across);
    }

    private sealed record Body(string Name, float Radius, float Height, float StepHeight, bool Flies, Func<GameWorld, ITarget?> Enemy)
    {
        public Vector3 Place(Level level, Vector3 floor) => Flies ? Bat.HoverPosition(level, floor) : floor;

        public bool IsFree(Level level, Vector3 at) =>
            !CharacterCollision.Overlaps(level, at, Radius, Height, StepHeight) && !Play.InPortalStone(level, new CylinderBody(at, Radius, Height));
    }
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
        foreach (var bat in world.Bats)
        {
            var room = world.Rooms.FirstOrDefault(room => room.Kind == RoomKind.Combat && room.Cells.Contains(Play.Cell(bat.Position)));
            var arena = room?.DoorIds.Select(id => world.Doors[id])
                .Where(door => door.Kind != DoorKind.Cracked)
                .Select(door => Arena.Find(world, door))
                .FirstOrDefault(arena => arena?.Room == room);
            if (arena is null)
            {
                continue;
            }

            arena.Enter(play);
            if (report.Check(!play.TicksUntil(600, PlayerInput.None, () => bat.Emergence.IsPresent && !world.IsAsleep(bat)), $"{ctx}: bat {bat.Id} not awake after its arena sealed"))
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

        report.Finding($"{ctx}: no bat in an arena");
        return null;
    }

    // A charged club blow (the farthest knock) from every free place round the portal at hover height, in eight
    // directions: at the jambs, the buttresses, the plinths' edges and through the opening.
    private static void Knocks(Play play, Bat bat, PortalSite site, Report report)
    {
        var club = play.World.Balance.Club;
        var places = report.Tally(play.Context, "knocks: places in stone or wall");
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
        // Only 4 places per seed lie free over a plinth: none left on one side is already a finding.
        var places = report.Tally($"{ctx} corpse", "corpses: places not over a plinth or in stone");
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
        !CharacterCollision.Overlaps(level, at, BodySizes.Bat.Radius, BodySizes.Bat.Height, float.PositiveInfinity)
        && !Play.InPortalStone(level, new CylinderBody(at, BodySizes.Bat.Radius, BodySizes.Bat.Height));
}
