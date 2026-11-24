using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

namespace SilverAndAsh.Core.Tests.Audio;

/// <summary>
/// Issue #605: the breaking column is heard - a sound per stage for each blow and one for the break - played the way
/// the game plays it: tick by tick from the world's events, placed for the listening player.
/// </summary>
public class ColumnSoundTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly int HitsToBreak = Balance.Default.Columns.HitsToBreak;
    private static readonly Column Pillar = Column.AtCell(3, 6);

    [Fact]
    public void In_the_world_each_blow_plays_its_stage_and_the_last_one_only_the_break_exactly_once()
    {
        // Gorm's boss room with one column between the player and him (as in GormCoverTests); Gorm wears it down.
        var world = GormHallWorld(LevelParser.Parse(GormHallMap).WithColumns([Pillar]), balance: Tough);
        EnterGormHall(world);
        var cues = new SoundCues(world.Seed);
        var heard = new List<(ColumnHit? Hit, bool Broke, List<SoundKind> Kinds)>();

        // Run on past the break: a broken column must stay silent under the next attacks.
        int? brokeAt = null;
        for (var tick = 0; tick < 180 * Second && (brokeAt is not { } at || tick < at + (20 * Second)); tick++)
        {
            world.Tick(PlayerInput.None);
            var played = cues.For(world.Events, SoundPack.Pack1, SoundListener.Of(world.Player), world.Balance.Hearing);
            var hit = world.Events.OfType<ColumnHit>().SingleOrDefault();
            var broke = world.Events.Any(e => e is ColumnBroke);
            brokeAt ??= broke ? tick : null;
            if (hit is not null || broke || played.Any(cue => IsColumnSound(cue.Kind)))
            {
                heard.Add((hit, broke, [.. played.Where(cue => IsColumnSound(cue.Kind)).Select(cue => cue.Kind)]));
            }
        }

        Assert.True(brokeAt.HasValue, "The column never broke - the test would prove nothing.");

        // One entry per blow, in order: the stage it leaves the column in, and with the last one the break instead.
        Assert.Equal(HitsToBreak, heard.Count);
        Assert.Equal(Enumerable.Range(1, HitsToBreak), heard.Select(entry => entry.Hit!.Hits));
        foreach (var (hit, broke, kinds) in heard)
        {
            var last = hit!.Hits == HitsToBreak;
            Assert.Equal(last, broke);
            SoundKind expected = !last ? StageKind(ColumnBreakLook.StageOf(hit.Hits, HitsToBreak)) : SoundKind.ColumnBroke;
            Assert.Equal([expected], kinds);
        }

        Assert.Equal(1, heard.Count(entry => entry.Kinds.Contains(SoundKind.ColumnBroke)));
    }

    [Fact]
    public void Each_blow_asks_for_the_sound_of_the_stage_it_leaves_and_the_breaking_one_for_none()
    {
        for (var hits = 1; hits <= HitsToBreak; hits++)
        {
            SoundKind? expected = hits == HitsToBreak ? null : StageKind(ColumnBreakLook.StageOf(hits, HitsToBreak));
            Assert.Equal(expected, SoundBoard.KindFor(new ColumnHit(Pillar, hits, HitsToBreak)));
        }

        Assert.Equal(SoundKind.ColumnHit2, SoundBoard.KindFor(new ColumnHit(Pillar, HitsToBreak - 1, HitsToBreak)));
        Assert.Equal(SoundKind.ColumnBroke, SoundBoard.KindFor(new ColumnBroke(Pillar)));
    }

    [Fact]
    public void The_breaking_blow_and_the_break_in_one_tick_play_the_break_alone()
    {
        GameEvent[] tick = [new ColumnHit(Pillar, HitsToBreak, HitsToBreak), new ColumnBroke(Pillar)];

        var cue = Assert.Single(new SoundCues(1).For(tick, SoundPack.Pack1));

        Assert.Equal(SoundKind.ColumnBroke, cue.Kind);
        Assert.Equal("saeule-bruch.wav", cue.File);
    }

    [Fact]
    public void A_blow_before_the_last_plays_the_dull_stage_1_file()
    {
        // Stage 1 needs a column that bears more than two blows; with fewer, every blow before the break is stage 2.
        var hitsToBreak = Math.Max(HitsToBreak, 3);

        var cue = Assert.Single(new SoundCues(1).For([new ColumnHit(Pillar, 1, hitsToBreak)], SoundPack.Pack1));

        Assert.Equal(SoundKind.ColumnHit1, cue.Kind);
        Assert.Equal("saeule-treffer-stufe1.wav", cue.File);
    }

    private static bool IsColumnSound(SoundKind kind) =>
        kind is SoundKind.ColumnHit1 or SoundKind.ColumnHit2 or SoundKind.ColumnBroke;

    private static SoundKind StageKind(ColumnStage stage) => stage switch
    {
        ColumnStage.Hit1 => SoundKind.ColumnHit1,
        ColumnStage.Hit2 => SoundKind.ColumnHit2,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "A standing column that was struck is Z1 or Z2."),
    };
}
