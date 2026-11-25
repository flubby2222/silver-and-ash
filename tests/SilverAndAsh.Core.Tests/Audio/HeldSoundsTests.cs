using SilverAndAsh.Core.Audio;

namespace SilverAndAsh.Core.Tests.Audio;

/// <summary>
/// Which sounds stand still with the world (issue #776, review of PR #781): held in the pause, going on after it - but a
/// sound stopped while held (a new run from the menu) must stay silent.
/// </summary>
public class HeldSoundsTests
{
    private const string Charge = "axt-aufladen.wav";

    /// <summary>
    /// A sound as raylib plays it: a pause holds it, a resume lets a paused one go on, and a stop does nothing to a
    /// paused sound (checked with Raylib-cs by the review) - the order the sound player keeps is what makes it silent.
    /// </summary>
    private sealed class RaylibLikeSound
    {
        public bool Playing { get; private set; } = true;

        public bool Paused { get; private set; }

        public bool Audible => Playing && !Paused;

        public void Pause() => Paused = Playing;

        public void Resume() => Paused = false;

        public void Stop()
        {
            if (!Paused)
            {
                Playing = false;
            }
        }
    }

    /// <summary>What SoundPlayer does with <paramref name="sound"/>: the frame's pauses and resumes, and a stop cue.</summary>
    private static void Apply(HoldStep step, RaylibLikeSound sound)
    {
        if (step.Pause.Contains(Charge))
        {
            sound.Pause();
        }

        if (step.Resume.Contains(Charge))
        {
            sound.Resume();
        }
    }

    private static void StopCue(HeldSounds held, RaylibLikeSound sound)
    {
        if (held.Stop(Charge))
        {
            sound.Resume();
        }

        sound.Stop();
    }

    [Fact]
    public void A_sound_playing_when_the_world_stops_is_held_and_goes_on_after()
    {
        var held = new HeldSounds();
        var sound = new RaylibLikeSound();

        Apply(held.Frame([Charge], frozen: true), sound);
        Assert.False(sound.Audible);
        Apply(held.Frame([], frozen: true), sound);
        Assert.False(sound.Audible);
        Apply(held.Frame([], frozen: false), sound);

        Assert.True(sound.Audible);
        Assert.Empty(held.Held);
    }

    [Fact]
    public void Held_then_stopped_then_let_go_leaves_no_sound_playing()
    {
        var held = new HeldSounds();
        var sound = new RaylibLikeSound();

        Apply(held.Frame([Charge], frozen: true), sound);
        StopCue(held, sound);
        var release = held.Frame([], frozen: false);
        Apply(release, sound);

        Assert.Empty(release.Resume);
        Assert.False(sound.Playing);
        Assert.False(sound.Audible);
    }

    [Fact]
    public void Stopping_a_sound_that_is_not_held_needs_no_resume()
    {
        var held = new HeldSounds();

        Assert.False(held.Stop(Charge));
        Assert.Empty(held.Frame([], frozen: false).Resume);
    }

    [Fact]
    public void The_sounds_are_held_once_when_the_stillness_begins()
    {
        var held = new HeldSounds();

        Assert.Equal([Charge], held.Frame([Charge, Charge], frozen: true).Pause);
        Assert.Empty(held.Frame([Charge], frozen: true).Pause);
        Assert.Equal([Charge], held.Frame([], frozen: false).Resume);
        Assert.Empty(held.Frame([], frozen: false).Resume);
    }
}
