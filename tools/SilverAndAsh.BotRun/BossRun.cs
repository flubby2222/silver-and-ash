using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.BotRun;

/// <summary>
/// Mode "boss": the way to the boss and the win (issues #561, #565). The bot clears all enemies, stands in front of the
/// antechamber, walks in, unlocks the gate teleporter by stepping on it, travels to the start and back, opens the boss
/// door, walks in until the fight starts and checks there is no way back. Then the boss dies, the stairs rise and the
/// bot climbs them. Every place it gets stuck is a finding that says where.
/// </summary>
internal static class BossRun
{
    public static void Run(LevelZero zero, int seed, Report report)
    {
        var play = new Play(zero.NewRun(seed, Play.Balance), report, $"seed {seed} boss");
        if (!ToBoss(play, report, first: true))
        {
            return;
        }

        var world = play.World;
        world.Gorm!.TakeDamage(1_000_000, []);
        if (report.Check(!play.TicksUntil(1500, PlayerInput.None, () => world.Stairs is { CanInteract: true }), $"{play.Context}: stairs never usable after the boss died"))
        {
            return;
        }

        report.Count("stairs rose after the win");
        report.Check(world.Rooms.Single(room => room.Kind == RoomKind.Boss).IsSealed, $"{play.Context}: boss room still sealed after the win");
        report.Check(world.Doors.Single(door => door.Kind == DoorKind.Boss).IsBarred, $"{play.Context}: boss door still barred after the win");
        if (report.Check(!play.WalkTo(world.Stairs!.Position), $"{play.Context}: stuck on the way to the stairs at {world.Player.Position}"))
        {
            return;
        }

        play.Face(world.Stairs!.Position);
        play.TicksUntil(5, Play.Interact, () => world.IsWon);
        report.Count(world.IsWon ? "won: stairs climbed" : "NOT won at the stairs");
        report.Check(!world.IsWon, $"{play.Context}: not won after pressing F at the stairs");
    }

    /// <summary>From in front of the antechamber into the boss fight; false (with a finding) where it gets stuck.</summary>
    public static bool ToBoss(Play play, Report report, bool first)
    {
        var world = play.World;
        var ctx = play.Context;
        var ante = world.Rooms.Single(room => room.Kind == RoomKind.Antechamber);
        var boss = world.Rooms.Single(room => room.Kind == RoomKind.Boss);
        var bossDoor = world.Doors.Single(door => door.Kind == DoorKind.Boss);
        var entrance = world.Doors.Single(door => (door.RoomA == ante.Index || door.RoomB == ante.Index) && door.Kind != DoorKind.Boss);
        var before = Play.OtherRoom(world, entrance, ante);
        if (Play.NeighbourIn(entrance, before) is not { } outside || Play.NeighbourIn(entrance, ante) is not { } inside
            || Play.NeighbourIn(bossDoor, ante) is not { } atBossDoor || Play.NeighbourIn(bossDoor, boss) is not { } inBoss)
        {
            report.Finding($"{ctx}: antechamber doors have no floor cell on both sides");
            return false;
        }

        Play.KillEnemies(world, null);
        play.PlaceAt(outside);
        play.Ticks(200, PlayerInput.None);
        report.Check(ante.IsEntered, $"{ctx}: antechamber entered before walking in");
        report.Check(first && world.Teleporters.Any(t => t.Plan.Site == TeleporterSite.BossGate && t.IsUnlocked), $"{ctx}: gate teleporter unlocked before entering");
        play.GiveKeys();
        play.Face(inside);
        if (entrance.CanInteract)
        {
            play.Tick(Play.Interact);
        }

        if (report.Check(!play.TicksUntil(400, PlayerInput.None, () => !entrance.Blocks), $"{ctx}: stuck at the antechamber entrance (kind {entrance.Kind}, barred {entrance.IsBarred}, grate {entrance.HasGrate}, room before sealed {before.IsSealed})")
            || report.Check(!play.WalkTo(world.Level.CellCenter(inside.X, inside.Z)), $"{ctx}: stuck in the antechamber entrance at {world.Player.Position}"))
        {
            return false;
        }

        report.Check(!ante.IsEntered, $"{ctx}: antechamber not entered");
        report.Check(ante.IsSealed, $"{ctx}: antechamber sealed");
        var gate = world.Teleporters.Single(t => t.Plan.Site == TeleporterSite.BossGate);
        var start = world.Teleporters.Single(t => t.Plan.Site == TeleporterSite.Start);
        play.WalkTo(gate.Position);
        play.Ticks(10, PlayerInput.None);
        report.Check(!gate.IsUnlocked, $"{ctx}: gate teleporter not unlocked after stepping on it");
        report.Check(!play.Events.OfType<TeleporterUnlocked>().Any(e => e.TeleporterId == gate.Id), $"{ctx}: no TeleporterUnlocked event for the gate");
        report.Count(first ? "gate teleporter unlocked (first run)" : "gate teleporter unlocked (after restart)");
        if (!start.IsUnlocked)
        {
            play.PlaceAt(start.Position);
            play.Ticks(2, PlayerInput.None);
            play.PlaceAt(gate.Position);
            play.Tick(PlayerInput.None);
        }

        report.Check(!Travel(play, start), $"{ctx}: travel gate -> start failed, player at {world.Player.Position}");
        report.Check(!Travel(play, gate), $"{ctx}: travel start -> gate failed, player at {world.Player.Position}");
        if (report.Check(!play.WalkTo(world.Level.CellCenter(atBossDoor.X, atBossDoor.Z)), $"{ctx}: stuck on the way to the boss door at {world.Player.Position}"))
        {
            return false;
        }

        play.Face((bossDoor.BoxMin + bossDoor.BoxMax) / 2);
        report.Check(!bossDoor.CanInteract, $"{ctx}: boss door cannot be used (open {bossDoor.IsOpen}, barred {bossDoor.IsBarred})");
        play.Tick(Play.Interact);
        report.Check(!bossDoor.IsOpen, $"{ctx}: boss door did not open on F");
        var target = world.Level.CellCenter(inBoss.X, inBoss.Z);
        if (report.Check(!play.TicksUntil(600, Play.Forward, () => { play.Face(target); return boss.IsSealed; }), $"{ctx}: boss room not sealed after walking in, player at {world.Player.Position}"))
        {
            return false;
        }

        report.Count("boss room sealed on entering");
        play.Ticks(60, PlayerInput.None);
        report.Check(!play.Events.OfType<BossFightStarted>().Any(), $"{ctx}: no BossFightStarted event");
        report.Check(!world.IsBossFightOn, $"{ctx}: boss fight not on");
        report.Check(!bossDoor.Blocks, $"{ctx}: boss door does not block during the fight");
        var back = world.Level.CellCenter(atBossDoor.X, atBossDoor.Z);
        for (var i = 0; i < 240; i++)
        {
            play.Face(back);
            play.Tick(Play.Forward);
        }

        report.Check(ante.Cells.Contains(Play.Cell(world.Player.Position)), $"{ctx}: player got back into the antechamber during the fight");
        report.Count(first ? "boss reached" : "boss reached again after restart");
        return true;
    }

    private static bool Travel(Play play, Teleporter to)
    {
        play.Tick(PlayerInput.None with { TeleportTo = to.Id });
        return play.TicksUntil(300, PlayerInput.None, () => Play.Distance(play.World.Player.Position, to.Position) <= 1.5f);
    }
}
