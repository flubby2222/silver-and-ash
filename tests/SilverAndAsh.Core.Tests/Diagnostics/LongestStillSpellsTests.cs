using SilverAndAsh.Core.Diagnostics;

namespace SilverAndAsh.Core.Tests.Diagnostics;

/// <summary>
/// The evaluation behind the bot mode <c>stuck</c> (issue #699): which seed and which enemy stood still longest without
/// an attack, and how many ticks it was short of the threshold.
/// </summary>
public class LongestStillSpellsTests
{
    // The ghoul's threshold as the bot works it out (tools/SilverAndAsh.BotRun/HangingEnemies.cs): twice its longest
    // legitimate pause - wind-up, rest, way searched again.
    private static readonly int GhoulThreshold = SimulationClock.SecondsToTicks(
        (Balance.Default.Ghoul.WindUpTime + Balance.Default.Ghoul.RecoverTime + Balance.Default.Ghoul.RepathTime) * 2f);

    [Fact]
    public void The_longest_spell_over_all_seeds_names_its_seed_enemy_and_place()
    {
        var spells = new LongestStillSpells();

        // As the bot plays them: seed by seed in ascending order, each spell counted up tick by tick (QC round of
        // 2026-11-24: seed 11 four ticks short, seed 27 six, seed 34 eight).
        Stand(spells, seed: 11, "room 4 at the door", enemyId: 17, GhoulThreshold - 4);
        Stand(spells, seed: 27, "room 2 in the middle", enemyId: 9, GhoulThreshold - 6);
        Stand(spells, seed: 34, "room 6 at the door", enemyId: 23, GhoulThreshold - 8);

        var longest = spells.Longest("ghoul");
        Assert.NotNull(longest);
        Assert.Equal(11, longest.Seed);
        Assert.Equal(17, longest.EnemyId);
        Assert.Equal("room 4 at the door", longest.Place);
        Assert.Equal(GhoulThreshold - 4, longest.Ticks);
        Assert.Equal(
            $"stuck: ghoul: 3 watched, longest still without an attack {GhoulThreshold - 4} ticks (ghoul 17, seed 11, room 4 at the door), hangs from {GhoulThreshold} ticks (4 short)",
            spells.Line("ghoul", watched: 3, GhoulThreshold));
    }

    [Fact]
    public void A_later_seed_with_a_longer_spell_takes_its_place()
    {
        var spells = new LongestStillSpells();

        Stand(spells, seed: 3, "room 1 at the door", enemyId: 5, GhoulThreshold - 30);
        Stand(spells, seed: 8, "boss room in the middle", enemyId: 2, GhoulThreshold - 1);

        Assert.Equal(8, spells.Longest("ghoul")?.Seed);
        Assert.EndsWith($"hangs from {GhoulThreshold} ticks (1 short)", spells.Line("ghoul", watched: 2, GhoulThreshold), StringComparison.Ordinal);
    }

    [Fact]
    public void Of_two_equally_long_spells_the_first_seed_stays()
    {
        var spells = new LongestStillSpells();

        Stand(spells, seed: 11, "room 4 at the door", enemyId: 17, GhoulThreshold - 4);
        Stand(spells, seed: 12, "room 3 at the door", enemyId: 30, GhoulThreshold - 4);

        Assert.Equal(11, spells.Longest("ghoul")?.Seed);
    }

    [Fact]
    public void Every_kind_keeps_its_own_longest_spell()
    {
        var spells = new LongestStillSpells();

        Stand(spells, seed: 1, "room 2 at the door", enemyId: 4, GhoulThreshold - 50);
        Stand(spells, seed: 2, "room 3 in the middle", enemyId: 7, GhoulThreshold - 2, kind: "bat");

        Assert.Equal(1, spells.Longest("ghoul")?.Seed);
        Assert.Equal(2, spells.Longest("bat")?.Seed);
        Assert.Null(spells.Longest("Gorm"));
    }

    [Fact]
    public void A_kind_without_a_spell_prints_zero_ticks_and_no_seed()
    {
        Assert.Equal(
            "stuck: Gorm: 0 watched, longest still without an attack 0 ticks, hangs from 900 ticks",
            new LongestStillSpells().Line("Gorm", watched: 0, threshold: 900));
    }

    [Fact]
    public void With_the_threshold_lowered_the_spell_hangs_and_is_no_candidate_for_the_longest()
    {
        // Gegenprobe: the same spell as seed 11, the threshold lowered to its length - now it is a finding.
        var spell = new StillSpell("ghoul", 11, "room 4 at the door", 17, GhoulThreshold - 4, GhoulThreshold - 4);

        Assert.True(spell.Hangs);
        var spells = new LongestStillSpells();
        var thrown = Assert.Throws<ArgumentException>(() => spells.Record(spell));
        Assert.Contains("seed 11", thrown.Message, StringComparison.Ordinal);
        Assert.Null(spells.Longest("ghoul"));
    }

    [Fact]
    public void One_tick_below_the_threshold_does_not_hang()
    {
        Assert.False(new StillSpell("ghoul", 11, "room 4 at the door", 17, GhoulThreshold - 1, GhoulThreshold).Hangs);
    }

    [Fact]
    public void A_missing_spell_is_refused()
    {
        Assert.Throws<ArgumentNullException>(() => new LongestStillSpells().Record(null!));
    }

    // One enemy standing still: the bot records the spell every tick while it grows, 0, 1, ... ticks.
    private static void Stand(LongestStillSpells spells, int seed, string place, int enemyId, int ticks, string kind = "ghoul")
    {
        var threshold = GhoulThreshold;
        for (var still = 0; still <= ticks; still++)
        {
            spells.Record(new StillSpell(kind, seed, place, enemyId, still, threshold));
        }
    }
}
