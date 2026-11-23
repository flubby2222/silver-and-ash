using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.BotRun;

/// <summary>
/// Mode "death": the player dies and the level restarts (<see cref="GameWorld.Restarted"/>). Once in every sealed arena
/// (issues #560, #578): afterwards no door may be barred, no room sealed, and the same arena must seal again when walked
/// into - with the room outside cleared first, as in the first run. Once in the boss fight (issue #565): afterwards the
/// antechamber is new, its teleporter locked, the boss door shut and the boss alive, and a second run reaches him again.
/// </summary>
internal static class DeathAndRestart
{
    public static void Run(LevelZero zero, int seed, Report report)
    {
        var probe = zero.NewRun(seed, Play.Balance);
        foreach (var door in probe.Doors.Where(door => door.Kind != DoorKind.Cracked && Arena.Find(probe, door) is not null))
        {
            report.Try($"seed {seed} door {door.Id} death", () => InArena(zero, seed, door.Id, report));
        }

        report.Try($"seed {seed} boss death", () => InBossFight(zero, seed, report));
    }

    private static GameWorld? DieAndRestart(Play play, Report report)
    {
        play.World.Player.TakeDamage(10_000_000, []);
        if (report.Check(!play.TicksUntil(2000, PlayerInput.None, () => play.World.ShouldRestart), $"{play.Context}: the level never restarts"))
        {
            return null;
        }

        report.Count("restarted");
        return play.World.Restarted();
    }

    private static void InArena(LevelZero zero, int seed, int doorId, Report report)
    {
        var world = zero.NewRun(seed, Play.Balance);
        var arena = Arena.Find(world, world.Doors[doorId])!;
        var play = new Play(world, report, $"seed {seed} door {doorId} death");
        arena.Enter(play);
        if (!arena.Room.IsSealed)
        {
            report.Count("arena: not sealed on walking in");
            return;
        }

        play.Ticks(world.Balance.Grates.FallTicks + 10, PlayerInput.None);
        if (DieAndRestart(play, report) is not { } restarted)
        {
            return;
        }

        report.Check(restarted.Doors.Any(door => door.IsBarred || (door.HasGrate && door.GrateHeight < GratePose.OpenHeight)), $"{play.Context}: restarted level has a barred door or a lowered grate");
        report.Check(restarted.Rooms.Any(room => room.IsSealed || room.IsEntered), $"{play.Context}: restarted level has a sealed or entered room");
        report.Check(restarted.Doors.Count(door => door.HasGrate) != world.Doors.Count(door => door.HasGrate), $"{play.Context}: number of grates differs after the restart");

        var again = Arena.Find(restarted, restarted.Doors[doorId])!;
        var second = new Play(restarted, report, play.Context + " (after restart)");
        again.Enter(second);
        report.Count(again.Room.IsSealed ? "arena: restarted level seals again" : "arena: restarted level NOT sealing");
        report.Check(!again.Room.IsSealed, $"{play.Context}: restarted level does not seal again");
    }

    private static void InBossFight(LevelZero zero, int seed, Report report)
    {
        var play = new Play(zero.NewRun(seed, Play.Balance), report, $"seed {seed} boss death");
        if (!BossRun.ToBoss(play, report, first: true) || DieAndRestart(play, report) is not { } restarted)
        {
            return;
        }

        var ante = restarted.Rooms.Single(room => room.Kind == RoomKind.Antechamber);
        var gate = restarted.Teleporters.Single(teleporter => teleporter.Plan.Site == TeleporterSite.BossGate);
        var bossDoor = restarted.Doors.Single(door => door.Kind == DoorKind.Boss);
        var boss = restarted.Rooms.Single(room => room.Kind == RoomKind.Boss);
        report.Check(
            ante.IsEntered || gate.IsUnlocked || bossDoor.IsOpen || bossDoor.IsBarred || boss.IsSealed || restarted.Gorm is not { IsDead: false },
            $"{play.Context}: restarted state: antechamber entered {ante.IsEntered}, gate unlocked {gate.IsUnlocked}, boss door open {bossDoor.IsOpen}, barred {bossDoor.IsBarred}, boss room sealed {boss.IsSealed}");
        report.Check(
            restarted.Ghouls.Any(ghoul => ante.Cells.Contains(Play.Cell(ghoul.Position))) || restarted.Bats.Any(bat => ante.Cells.Contains(Play.Cell(bat.Position))),
            $"{play.Context}: enemy in the antechamber after the restart");
        if (BossRun.ToBoss(new Play(restarted, report, play.Context + " (after restart)"), report, first: false))
        {
            report.Count("boss: second run to the boss after dying");
        }
    }
}
