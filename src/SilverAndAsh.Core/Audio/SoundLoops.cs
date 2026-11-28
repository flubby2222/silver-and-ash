using System.Collections.Immutable;
using System.Numerics;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Audio;

/// <summary>
/// How one looping sound of the world is to be heard right now: which file, how loud (0 to 1, before
/// <see cref="SoundBoard.MasterVolume"/>; 0 = silent) and how far to the side (<see cref="SoundMix.Pan"/>).
/// </summary>
public readonly record struct LoopCue(string File, float Volume, float Pan);

/// <summary>
/// The sounds of the world that run as a loop for as long as their thing is there (issue #815) - unlike the short sounds
/// of <see cref="SoundCues"/> they hang on no event but on the state of the world: the water of Sirius' Relief runs
/// while the well is ready. Pure logic, testable without a sound device; the game only plays what this says.
/// <para>
/// Each loop is heard from its place like every placed sound (<see cref="Hearing.Mix"/>, issue #491), but with a range
/// of its own. A new loop is one more entry in <see cref="Sources"/>.
/// </para>
/// </summary>
public static class SoundLoops
{
    /// <summary>The water of Sirius' Relief: "C gurgling" of the listening test of issue #438, a seamless loop of 12 s (tools/audio/brunnenklang.py).</summary>
    public const string WellWater = "brunnen-wasser.wav";

    /// <summary>One loop: its file, where it sounds in a world (null: not at all), how far it carries and how loud it is at its place.</summary>
    private sealed record Source(string File, Func<GameWorld, Vector3?> Place, Func<Balance, float> Range, Func<Balance, float> Volume);

    private static readonly ImmutableArray<Source> Sources =
    [
        new(WellWater, WellWaterPlace, balance => balance.Well.WaterRange, balance => balance.Well.WaterVolume),
    ];

    /// <summary>Every loop file the game loads from assets/audio/.</summary>
    public static ImmutableArray<string> Files => [.. Sources.Select(source => source.File)];

    /// <summary>
    /// How <paramref name="listener"/> hears every loop in <paramref name="world"/> right now - one entry per file of
    /// <see cref="Files"/>, in that order, silent (volume 0, in the middle) where the loop's thing is not there or out of
    /// its range. Reads the world only; call it as often as wanted.
    /// </summary>
    public static IReadOnlyList<LoopCue> For(GameWorld world, SoundListener listener)
    {
        ArgumentNullException.ThrowIfNull(world);
        return [.. Sources.Select(source => Cue(source, world, listener))];
    }

    /// <summary>Every loop silent: what plays while there is no world to listen to (title screen, end of a run).</summary>
    public static IReadOnlyList<LoopCue> Silent => [.. Sources.Select(source => new LoopCue(source.File, 0f, 0f))];

    private static LoopCue Cue(Source source, GameWorld world, SoundListener listener)
    {
        if (source.Place(world) is not { } place)
        {
            return new LoopCue(source.File, 0f, 0f);
        }

        // The loop's own range instead of the general one; near distance, bend and pan as for every placed sound.
        var hearing = world.Balance.Hearing with { Range = source.Range(world.Balance) };
        var mix = Hearing.Mix(listener.Ears, listener.Facing, place, hearing);
        var volume = mix.Volume * source.Volume(world.Balance);
        return volume > 0f ? new LoopCue(source.File, volume, mix.Pan) : new LoopCue(source.File, 0f, 0f);
    }

    /// <summary>
    /// The water runs only while the well is ready: the spent well is dry (its model has no water, see
    /// <see cref="WellOfSirius.IsSpent"/>), so picture and sound say the same - and a well that is heard is one that can
    /// still be used. It sounds from the middle of its height, where the bowls are.
    /// </summary>
    private static Vector3? WellWaterPlace(GameWorld world) =>
        world.Well is { IsSpent: false } well ? well.Position + new Vector3(0f, WellOfSirius.Height / 2f, 0f) : null;
}

/// <summary>
/// Smooths the loops of <see cref="SoundLoops"/> from frame to frame (issue #815): a loop whose thing goes away at once
/// (the well is spent, a new world comes) fades out over <see cref="FadeSeconds"/> instead of breaking off with a
/// click, and comes in as softly. While it fades out it stays on the side it was last heard from. Presentation only.
/// </summary>
public sealed class LoopFader
{
    /// <summary>Seconds a loop takes from full volume to silence or back.</summary>
    public const float FadeSeconds = 0.4f;

    private readonly Dictionary<string, LoopCue> _playing = new(StringComparer.Ordinal);

    /// <summary>One drawn frame of <paramref name="seconds"/>: each loop of <paramref name="wishes"/>, its volume moved towards the wished one.</summary>
    public IReadOnlyList<LoopCue> Step(IReadOnlyList<LoopCue> wishes, float seconds)
    {
        ArgumentNullException.ThrowIfNull(wishes);
        var step = MathF.Max(0f, seconds) / FadeSeconds;
        var result = new List<LoopCue>(wishes.Count);
        foreach (var wish in wishes)
        {
            var last = _playing.GetValueOrDefault(wish.File, new LoopCue(wish.File, 0f, wish.Pan));
            var volume = wish.Volume > last.Volume
                ? MathF.Min(wish.Volume, last.Volume + step)
                : MathF.Max(wish.Volume, last.Volume - step);

            // A silent wish has no side: keep the last one until the loop has faded out.
            var cue = new LoopCue(wish.File, volume, wish.Volume > 0f ? wish.Pan : last.Pan);
            _playing[wish.File] = cue;
            result.Add(cue);
        }

        return result;
    }
}
