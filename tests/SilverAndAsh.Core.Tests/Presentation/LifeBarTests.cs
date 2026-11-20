using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>The arithmetic of the growing life bar (issue #435): length, fill and tick marks, without the HUD.</summary>
public class LifeBarTests
{
    private const int Cap = 8;
    private const int Longest = 320;

    private static int Step => Balance.Default.Boons.Health;

    [Fact]
    public void The_bar_is_as_long_as_the_highest_health()
    {
        var start = LifeBar.Shape(Balance.Default.Player.MaxHealth, 1f, Step, Cap, Longest);
        var grown = LifeBar.Shape(Balance.Default.Player.MaxHealth + Step, 1f, Step, Cap, Longest);

        Assert.Equal(Balance.Default.Player.MaxHealth, start.Length);
        Assert.Equal(start.Length + Step, grown.Length);
    }

    [Theory]
    [InlineData(1000, Longest)]
    [InlineData(0, (2 * Cap) + 1)]
    [InlineData(-5, (2 * Cap) + 1)]
    public void The_length_stays_between_the_two_caps_and_the_longest_part(int maxLife, int length)
    {
        Assert.Equal(length, LifeBar.Shape(maxLife, 1f, Step, Cap, Longest).Length);
    }

    [Fact]
    public void Full_life_fills_the_bar_up_to_the_cap_and_the_cap_too()
    {
        var shape = LifeBar.Shape(100, 1f, Step, Cap, Longest);

        Assert.Equal(100 - Cap, shape.Filled);
        Assert.True(shape.IsFull);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    public void No_life_fills_nothing(float life)
    {
        var shape = LifeBar.Shape(100, life, Step, Cap, Longest);

        Assert.Equal(0, shape.Filled);
        Assert.False(shape.IsFull);
        Assert.DoesNotContain(shape.Ticks, tick => tick.IsOverFill);
    }

    [Fact]
    public void Half_life_fills_from_the_left_cap_half_the_inside_and_leaves_the_right_cap_empty()
    {
        var shape = LifeBar.Shape(100, 0.5f, Step, Cap, Longest);

        Assert.Equal(Cap + ((100 - (2 * Cap)) / 2), shape.Filled);
        Assert.False(shape.IsFull);
    }

    [Fact]
    public void More_than_full_counts_as_full()
    {
        var full = LifeBar.Shape(100, 1f, Step, Cap, Longest);
        var over = LifeBar.Shape(100, 3f, Step, Cap, Longest);

        Assert.Equal((full.Length, full.Filled, full.IsFull), (over.Length, over.Filled, over.IsFull));
        Assert.Equal(full.Ticks, over.Ticks);
    }

    [Fact]
    public void A_tick_every_step_from_the_left_cap_every_fifth_long_and_none_on_the_right_cap()
    {
        var shape = LifeBar.Shape(Balance.Default.Boons.HealthCap, 0.5f, Step, Cap, Longest);

        Assert.NotEmpty(shape.Ticks);
        for (var i = 0; i < shape.Ticks.Count; i++)
        {
            var tick = shape.Ticks[i];
            Assert.Equal(Cap + ((i + 1) * Step), tick.X);
            Assert.Equal((i + 1) % LifeBar.LongTickEvery == 0, tick.IsLong);
            Assert.Equal(tick.X < shape.Filled, tick.IsOverFill);
        }

        Assert.True(shape.Ticks[^1].X < shape.Length - Cap);
        Assert.True(shape.Ticks[^1].X + Step >= shape.Length - Cap, "A tick is missing before the right cap.");
        Assert.Contains(shape.Ticks, tick => tick.IsOverFill);
        Assert.Contains(shape.Ticks, tick => !tick.IsOverFill);
    }

    [Fact]
    public void A_tick_exactly_on_the_right_cap_is_left_out()
    {
        // Inside 20 columns, step 5: ticks at 5, 10, 15 - the one at 20 would stand on the cap.
        var shape = LifeBar.Shape(20 + (2 * Cap), 1f, 5, Cap, Longest);

        Assert.Equal([Cap + 5, Cap + 10, Cap + 15], shape.Ticks.Select(tick => tick.X));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Without_a_step_there_are_no_ticks(int lifePerTick)
    {
        Assert.Empty(LifeBar.Shape(100, 1f, lifePerTick, Cap, Longest).Ticks);
    }

    [Theory]
    [InlineData(0, Longest)]
    [InlineData(-1, Longest)]
    [InlineData(Cap, 2 * Cap)]
    public void Caps_without_width_or_a_bar_without_room_between_its_caps_are_refused(int capWidth, int longest)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LifeBar.Shape(100, 1f, Step, capWidth, longest));
    }
}
