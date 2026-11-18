using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>The effects at the edge of the picture (issue #370): when they start, how strong they are, when they end.</summary>
public class ScreenEffectsTests
{
    private static Player FreshPlayer() => new GameWorld(LevelParser.Parse("P")).Player;

    private static void Run(ScreenEffects effects, Player player, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++)
        {
            effects.Tick(player);
        }
    }

    [Fact]
    public void Nothing_shows_on_a_fresh_player()
    {
        var player = FreshPlayer();
        var effects = new ScreenEffects(player.Id);

        effects.Tick(player);

        Assert.Equal(0f, effects.Darkness);
        Assert.Equal(0f, effects.LowLife);
        Assert.Equal(0f, effects.SkillPoint);
    }

    [Fact]
    public void A_hit_darkens_the_picture_at_most_by_the_limit_and_it_fades_away()
    {
        var player = FreshPlayer();
        var effects = new ScreenEffects(player.Id);

        effects.OnEvent(new PlayerHit(player.Id, 5));
        Assert.Equal(ScreenEffects.HitDarkness, effects.Darkness);

        effects.Tick(player);
        var afterOne = effects.Darkness;
        Assert.InRange(afterOne, 0.01f, ScreenEffects.HitDarkness - 0.001f);

        Run(effects, player, SimulationClock.SecondsToTicks(ScreenEffects.HitSeconds));
        Assert.Equal(0f, effects.Darkness);
    }

    [Fact]
    public void The_darkening_fades_evenly_over_the_whole_hit_time()
    {
        var player = FreshPlayer();
        var effects = new ScreenEffects(player.Id);
        var hitTicks = SimulationClock.SecondsToTicks(ScreenEffects.HitSeconds);

        effects.OnEvent(new PlayerHit(player.Id, 5));
        for (var tick = 1; tick < hitTicks; tick++)
        {
            effects.Tick(player);
            Assert.Equal(ScreenEffects.HitDarkness * (hitTicks - tick) / hitTicks, effects.Darkness, 4);
        }

        // Still a little dark on the last tick of the hit time, gone right after.
        Assert.True(effects.Darkness > 0f);
        effects.Tick(player);
        Assert.Equal(0f, effects.Darkness);
    }

    [Fact]
    public void A_hit_starts_no_gold_shine_and_a_level_up_no_darkening()
    {
        var player = FreshPlayer();
        var hit = new ScreenEffects(player.Id);
        var levelUp = new ScreenEffects(player.Id);

        hit.OnEvent(new PlayerHit(player.Id, 5));
        levelUp.OnEvent(new LevelUp(player.Id, 2));
        hit.Tick(player);
        levelUp.Tick(player);

        Assert.True(hit.Darkness > 0f);
        Assert.Equal(0f, hit.SkillPoint);
        Assert.True(levelUp.SkillPoint > 0f);
        Assert.Equal(0f, levelUp.Darkness);
    }

    [Fact]
    public void After_healing_the_red_edge_starts_again_at_its_weakest()
    {
        var player = FreshPlayer();
        var effects = new ScreenEffects(player.Id);
        var beat = SimulationClock.SecondsToTicks(ScreenEffects.LowLifePulseSeconds);
        var events = new List<GameEvent>();

        // First time at low life: run to the strongest point of the beat.
        player.TakeDamage(player.MaxHealth - 1, events);
        Run(effects, player, (beat / 2) + 1);
        Assert.True(effects.LowLife > ScreenEffects.LowLifeMax - 0.01f);

        player.Heal(player.MaxHealth, events);
        effects.Tick(player);
        Assert.Equal(0f, effects.LowLife);

        // Second time: the pulse begins anew, not where it stopped.
        player.TakeDamage(player.MaxHealth - 1, events);
        effects.Tick(player);
        Assert.Equal(ScreenEffects.LowLifeMin, effects.LowLife, 4);
    }

    [Fact]
    public void A_hit_on_another_player_or_without_damage_does_nothing()
    {
        var player = FreshPlayer();
        var effects = new ScreenEffects(player.Id);

        effects.OnEvent(new PlayerHit(player.Id + 1, 5));
        effects.OnEvent(new PlayerHit(player.Id, 0));
        effects.OnEvent(new LevelUp(player.Id + 1, 2));

        Assert.Equal(0f, effects.Darkness);
        Assert.Equal(0f, effects.SkillPoint);
    }

    [Fact]
    public void A_level_up_starts_the_gold_shine_which_is_gone_after_its_time()
    {
        var player = FreshPlayer();
        var effects = new ScreenEffects(player.Id);

        effects.OnEvent(new LevelUp(player.Id, 2));
        Assert.Equal(1f, effects.SkillPoint);

        Run(effects, player, SimulationClock.SecondsToTicks(ScreenEffects.SkillPointSeconds) - 1);
        Assert.True(effects.SkillPoint > 0f);

        effects.Tick(player);
        Assert.Equal(0f, effects.SkillPoint);
    }

    [Fact]
    public void The_red_edge_shows_only_below_the_low_life_share()
    {
        var player = FreshPlayer();
        var effects = new ScreenEffects(player.Id);
        var threshold = (int)MathF.Ceiling(player.MaxHealth * ScreenEffects.LowLifeShare);

        // Exactly at the share: nothing yet.
        player.TakeDamage(player.MaxHealth - threshold, new List<GameEvent>());
        effects.Tick(player);
        Assert.Equal(0f, effects.LowLife);

        // One point below: the edge shows.
        player.TakeDamage(1, new List<GameEvent>());
        effects.Tick(player);
        Assert.InRange(effects.LowLife, ScreenEffects.LowLifeMin, ScreenEffects.LowLifeMax);
    }

    [Fact]
    public void The_red_edge_pulses_between_its_weakest_and_strongest_once_a_beat()
    {
        var player = FreshPlayer();
        player.TakeDamage(player.MaxHealth - 1, new List<GameEvent>());
        var effects = new ScreenEffects(player.Id);
        var beat = SimulationClock.SecondsToTicks(ScreenEffects.LowLifePulseSeconds);

        var values = new List<float>();
        for (var tick = 0; tick < beat * 2; tick++)
        {
            effects.Tick(player);
            values.Add(effects.LowLife);
        }

        Assert.Equal(ScreenEffects.LowLifeMin, values.Min(), 3);
        Assert.Equal(ScreenEffects.LowLifeMax, values.Max(), 2);
        Assert.Equal(values[0], values[beat], 4);
        Assert.True(values[beat / 2] > values[0] + 0.1f);
    }

    [Fact]
    public void A_dead_player_has_no_red_edge()
    {
        var player = FreshPlayer();
        var effects = new ScreenEffects(player.Id);
        player.TakeDamage(player.MaxHealth, new List<GameEvent>());

        effects.Tick(player);

        Assert.True(player.IsDead);
        Assert.Equal(0f, effects.LowLife);
    }

    [Fact]
    public void Reset_clears_every_effect()
    {
        var player = FreshPlayer();
        player.TakeDamage(player.MaxHealth - 1, new List<GameEvent>());
        var effects = new ScreenEffects(player.Id);
        effects.OnEvent(new PlayerHit(player.Id, 5));
        effects.OnEvent(new LevelUp(player.Id, 2));
        effects.Tick(player);

        effects.Reset();

        Assert.Equal(0f, effects.Darkness);
        Assert.Equal(0f, effects.LowLife);
        Assert.Equal(0f, effects.SkillPoint);
    }

    [Theory]
    [InlineData(854, 480)]
    [InlineData(640, 480)]
    [InlineData(427, 240)]
    public void The_middle_of_the_picture_stays_clear_even_at_full_strength(int width, int height)
    {
        var band = ScreenEffects.EdgeBandPixels(height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var inside = x >= band && x < width - band && y >= band && y < height - band;
                if (inside)
                {
                    Assert.False(ScreenEffects.IsEdgePixelOn(x, y, width, height, 1f), $"pixel {x},{y} lies inside");
                }
            }
        }

        // The band is a small share of the picture: the target in the middle is far from it. But wide enough to be seen.
        Assert.True(band < height / 4);
        Assert.True(band > height / 10, $"band {band} at height {height}");
    }

    [Fact]
    public void At_full_strength_the_outermost_line_is_solid_and_the_pixels_thin_out_inwards()
    {
        const int Width = 854;
        const int Height = 480;
        var band = ScreenEffects.EdgeBandPixels(Height);

        int OnInRow(int y) => Enumerable.Range(0, Width).Count(x => ScreenEffects.IsEdgePixelOn(x, y, Width, Height, 1f));

        Assert.Equal(Width, OnInRow(0));
        Assert.True(OnInRow(band / 2) < OnInRow(1));
        Assert.True(OnInRow(band / 2) > 0);
    }

    [Fact]
    public void Halfway_into_the_band_only_about_a_quarter_of_the_pixels_are_set()
    {
        // The cover falls with the square of the way inwards: at half the band (1 - 1/2)^2 = 1/4, not 1/2.
        const int Width = 854;
        const int Height = 480;
        var band = ScreenEffects.EdgeBandPixels(Height);
        var middle = Enumerable.Range(band, Width - (2 * band)).ToArray();

        // Four lines, one whole period of the 4 x 4 pattern: a single line holds only four of its 16 thresholds.
        var rows = Enumerable.Range((band / 2) - 2, Dithering.PatternSize).ToArray();
        var on = rows.Sum(y => middle.Count(x => ScreenEffects.IsEdgePixelOn(x, y, Width, Height, 1f)));
        var expected = rows.Sum(y => MathF.Pow(1f - ((float)y / band), 2) * middle.Length);

        Assert.InRange(on, expected * 0.8f, expected * 1.2f);
    }

    [Fact]
    public void A_weaker_effect_sets_fewer_pixels_and_none_at_zero()
    {
        const int Width = 854;
        const int Height = 480;

        int OnInEdge(float strength) =>
            Enumerable.Range(0, Height).Sum(y => Enumerable.Range(0, Width).Count(x => ScreenEffects.IsEdgePixelOn(x, y, Width, Height, strength)));

        Assert.Equal(0, OnInEdge(0f));
        Assert.True(OnInEdge(ScreenEffects.LowLifeMin) < OnInEdge(ScreenEffects.LowLifeMax));
        Assert.True(OnInEdge(ScreenEffects.LowLifeMax) < OnInEdge(1f));
    }
}
