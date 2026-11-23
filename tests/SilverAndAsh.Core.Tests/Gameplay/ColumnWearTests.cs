using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>Issue #590: the wear of a level's columns - counting blows, breaking, and nothing after the break.</summary>
public class ColumnWearTests
{
    private static readonly int HitsToBreak = Balance.Default.Columns.HitsToBreak;
    private static readonly Column Column = Column.AtCell(2, 2);

    [Fact]
    public void A_column_counts_its_blows_and_breaks_with_the_last()
    {
        var wear = new ColumnWear(HitsToBreak);
        var events = new List<GameEvent>();

        for (var blow = 1; blow < HitsToBreak; blow++)
        {
            wear.Strike(Column, 1f, events);
            Assert.False(wear.IsBroken(Column));
        }

        wear.Strike(Column, 1f, events);

        Assert.True(wear.IsBroken(Column));
        Assert.Equal([Column], wear.Broken);
        Assert.Equal(Enumerable.Range(1, HitsToBreak).Select(hits => new ColumnHit(Column, hits, HitsToBreak)), events.OfType<ColumnHit>());
        Assert.Equal([new ColumnBroke(Column)], events.OfType<ColumnBroke>());
        Assert.Equal(0, wear.HitsOn(Column.AtCell(5, 5)));
    }

    [Fact]
    public void A_broken_column_takes_no_more_blows()
    {
        var wear = new ColumnWear(HitsToBreak);
        for (var blow = 0; blow < HitsToBreak; blow++)
        {
            wear.Strike(Column, 1f, []);
        }

        var events = new List<GameEvent>();
        wear.Strike(Column, 1f, events);

        Assert.Empty(events);
        Assert.Equal(HitsToBreak, wear.HitsOn(Column));
        Assert.True(wear.IsBroken(Column));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_column_that_breaks_without_a_blow_is_refused(int hitsToBreak) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ColumnWear(hitsToBreak));
}
