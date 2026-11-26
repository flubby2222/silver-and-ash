using SilverAndAsh.BotRun;
using Xunit;

namespace SilverAndAsh.Core.Tests.Bot;

// Issue #805 (hint 261): the table of the bot's modes. "all" plays every mode with InAll, so "fight" - a long run that
// ends in a loss or a win by design, not a check - must not be one of them.
public class BotModesTests
{
    private static BotMode[] Table() => BotModes.Create(new HangingEnemies(), new FightMode());

    [Fact]
    public void FightIsNotPartOfAll() =>
        Assert.False(Table().Single(m => m.Name == "fight").InAll);

    [Fact]
    public void EveryOtherModeIsPartOfAll() =>
        Assert.All(Table().Where(m => m.Name != "fight"), m => Assert.True(m.InAll, m.Name));
}
