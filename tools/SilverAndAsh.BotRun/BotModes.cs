namespace SilverAndAsh.BotRun;

/// <summary>The modes of the bot run, in the order "all" plays them (issue #629, #805: its own class so a test can reach the table).</summary>
internal static class BotModes
{
    /// <summary>A new mode is one more line here, not one more branch in Program.cs.</summary>
    public static BotMode[] Create(HangingEnemies stuck, FightMode fight) =>
    [
        new("arena", "every arena door: seal, escape attempts, clear, walk out; two arenas side by side; the boss door", ArenaDoors.Run),
        new("death", "die in a sealed arena and in the boss fight, restart, walk in again", DeathAndRestart.Run),
        new("boss", "run to the boss (antechamber, teleporters, boss door), win, climb the stairs", BossRun.Run),
        new("slam", "the boss door slams shut with a body in its doorway (player, ghoul, bat, Gorm): nobody in the stone", DoorSlam.Run),
        new("bat", "a bat at the boss portal: knocked at the stone, flying round a player there, falling dead onto a plinth", PortalBat.Run),
        new("stuck", "stand still in every arena and the boss room: no enemy that hunts a player may hang without attacking", stuck.Run, stuck.PrintLongest),
        new("seeds", "build every level twice: failures, generator tries, same level for the same seed", SeedProbe.Run, SeedProbe.PrintAttemptNote),
        new("fight", "a mortal player fights from the start to Gorm with a real weapon (--weapon blade|pistol|axe|whip|crossbow|all); not in all", fight.Run, fight.PrintWinRates, InAll: false),
    ];
}
