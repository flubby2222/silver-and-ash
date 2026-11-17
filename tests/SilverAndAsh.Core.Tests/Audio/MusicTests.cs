using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Audio;

/// <summary>Which music plays where, and how one piece gives way to the next (issue #271).</summary>
public class MusicTests
{
    private const float Frame = 1f / 60f;

    private static readonly MusicWish Title = SoundBoard.MusicFor(new MusicSituation(OnTitle: true, Paused: false, RunOver: false));
    private static readonly MusicWish Run = SoundBoard.MusicFor(new MusicSituation(OnTitle: false, Paused: false, RunOver: false));
    private static readonly MusicWish Paused = SoundBoard.MusicFor(new MusicSituation(OnTitle: false, Paused: true, RunOver: false));
    private static readonly MusicWish End = SoundBoard.MusicFor(new MusicSituation(OnTitle: false, Paused: false, RunOver: true));

    [Fact]
    public void The_title_screen_plays_the_title_theme()
    {
        Assert.Equal(new MusicWish(MusicTrack.Title, false), Title);
        Assert.Equal("musik-hauptmenue.ogg", SoundBoard.MusicFile(MusicTrack.Title));
    }

    [Fact]
    public void A_run_plays_the_catacomb_music()
    {
        Assert.Equal(new MusicWish(MusicTrack.Catacombs, false), Run);
        Assert.Equal("musik-katakomben.ogg", SoundBoard.MusicFile(MusicTrack.Catacombs));
    }

    [Fact]
    public void The_esc_menu_holds_the_catacomb_music()
    {
        Assert.Equal(new MusicWish(MusicTrack.Catacombs, true), Paused);
    }

    [Fact]
    public void The_end_screen_plays_no_music()
    {
        Assert.Equal(new MusicWish(MusicTrack.None, false), End);
        Assert.Null(SoundBoard.MusicFile(MusicTrack.None));
    }

    [Fact]
    public void Back_on_the_title_screen_the_title_theme_plays_again()
    {
        // The title wins over a menu or an end screen that was open when the player went back.
        Assert.Equal(Title, SoundBoard.MusicFor(new MusicSituation(OnTitle: true, Paused: true, RunOver: false)));
        Assert.Equal(Title, SoundBoard.MusicFor(new MusicSituation(OnTitle: true, Paused: false, RunOver: true)));
    }

    [Fact]
    public void Music_has_volumes_of_its_own_under_the_master_volume()
    {
        Assert.InRange(SoundBoard.MusicVolume(MusicTrack.Title), 0.01f, 1f);
        Assert.InRange(SoundBoard.MusicVolume(MusicTrack.Catacombs), 0.01f, 1f);
        Assert.Equal(0f, SoundBoard.MusicVolume(MusicTrack.None));
        Assert.InRange(SoundBoard.MasterVolume, 0.01f, 1f);
    }

    [Fact]
    public void The_first_track_starts_once_from_silence_and_fades_in()
    {
        var mixer = new MusicMixer();
        var first = mixer.Step(Title, Frame);
        Assert.Equal(new MusicMix(MusicTrack.Title, 0f, false, true), first);

        var mixes = Steps(mixer, Title, SoundBoard.MusicFadeInSeconds + 1f);
        Assert.All(mixes, mix => Assert.False(mix.Start));
        Assert.All(mixes, mix => Assert.Equal(MusicTrack.Title, mix.Track));
        AssertRising(mixes);
        Assert.Equal(SoundBoard.MusicVolume(MusicTrack.Title), mixes[^1].Volume, 4);
    }

    [Fact]
    public void Title_to_run_fades_the_theme_out_before_the_catacomb_music_starts()
    {
        var mixer = FadedIn(Title);

        var mixes = Steps(mixer, Run, SoundBoard.MusicFadeOutSeconds + SoundBoard.MusicFadeInSeconds + 1f);
        var switchAt = mixes.FindIndex(mix => mix.Track == MusicTrack.Catacombs);

        // Out first, all the way down, then the new one from silence - never two at once.
        var fadeOut = mixes[..switchAt];
        Assert.All(fadeOut, mix => Assert.Equal(MusicTrack.Title, mix.Track));
        AssertFalling(fadeOut);
        Assert.InRange(fadeOut.Count, (int)(SoundBoard.MusicFadeOutSeconds / Frame) - 2, (int)(SoundBoard.MusicFadeOutSeconds / Frame) + 2);

        Assert.True(mixes[switchAt].Start);
        Assert.Equal(0f, mixes[switchAt].Volume);
        var fadeIn = mixes[switchAt..];
        Assert.Single(fadeIn, mix => mix.Start);
        AssertRising(fadeIn);
        Assert.Equal(SoundBoard.MusicVolume(MusicTrack.Catacombs), fadeIn[^1].Volume, 4);
    }

    [Fact]
    public void Run_to_title_fades_back_the_same_way()
    {
        var mixer = FadedIn(Run);

        var mixes = Steps(mixer, Title, SoundBoard.MusicFadeOutSeconds + SoundBoard.MusicFadeInSeconds + 1f);
        var switchAt = mixes.FindIndex(mix => mix.Track == MusicTrack.Title);

        Assert.True(switchAt > 0);
        AssertFalling(mixes[..switchAt]);
        Assert.True(mixes[switchAt].Start);
        Assert.Equal(SoundBoard.MusicVolume(MusicTrack.Title), mixes[^1].Volume, 4);
    }

    [Fact]
    public void The_esc_menu_holds_the_music_at_its_volume_and_lets_it_go_on()
    {
        var mixer = FadedIn(Run);

        var held = Steps(mixer, Paused, 2f);
        Assert.All(held, mix => Assert.Equal(new MusicMix(MusicTrack.Catacombs, SoundBoard.MusicVolume(MusicTrack.Catacombs), true, false), mix));

        var resumed = mixer.Step(Run, Frame);
        Assert.Equal(new MusicMix(MusicTrack.Catacombs, SoundBoard.MusicVolume(MusicTrack.Catacombs), false, false), resumed);
    }

    [Fact]
    public void The_end_screen_fades_the_music_out_to_silence()
    {
        var mixer = FadedIn(Run);

        var mixes = Steps(mixer, End, SoundBoard.MusicFadeOutSeconds + 1f);
        Assert.All(mixes, mix => Assert.False(mix.Start));
        Assert.Equal(new MusicMix(MusicTrack.None, 0f, false, false), mixes[^1]);
    }

    [Fact]
    public void A_new_run_from_the_end_screen_starts_the_catacomb_music_once()
    {
        var mixer = FadedIn(Run);
        Steps(mixer, End, SoundBoard.MusicFadeOutSeconds + 1f);

        var mixes = Steps(mixer, Run, SoundBoard.MusicFadeInSeconds + 1f);
        Assert.Single(mixes, mix => mix.Start);
        Assert.True(mixes[0].Start);
        Assert.Equal(SoundBoard.MusicVolume(MusicTrack.Catacombs), mixes[^1].Volume, 4);
    }

    [Fact]
    public void A_new_run_while_the_music_still_fades_out_does_not_start_it_again()
    {
        var mixer = FadedIn(Run);
        var fading = Steps(mixer, End, SoundBoard.MusicFadeOutSeconds / 2f);
        Assert.Equal(MusicTrack.Catacombs, fading[^1].Track);

        var mixes = Steps(mixer, Run, SoundBoard.MusicFadeInSeconds + 1f);
        Assert.All(mixes, mix => Assert.False(mix.Start));
        Assert.All(mixes, mix => Assert.Equal(MusicTrack.Catacombs, mix.Track));
        AssertRising(mixes);
    }

    [Fact]
    public void Asking_for_the_same_track_every_frame_starts_it_only_once()
    {
        var mixer = new MusicMixer();
        var mixes = Steps(mixer, Run, 10f);
        Assert.Single(mixes, mix => mix.Start);
    }

    [Fact]
    public void Every_music_file_is_checked_in_and_matches_the_checksum_list()
    {
        var folder = RepositoryFiles.Find("assets/audio");
        var list = ModelChecksums.Parse(File.ReadAllText(Path.Combine(folder, SoundBoard.ChecksumFileName)));
        foreach (var track in SoundBoard.MusicTracks)
        {
            var path = Path.Combine(folder, SoundBoard.MusicFile(track)!);
            Assert.True(File.Exists(path), path);
            Assert.Contains(SoundBoard.MusicFile(track)!, list.Names);
            Assert.Null(list.Check(path));
        }
    }

    private static MusicMixer FadedIn(MusicWish wish)
    {
        var mixer = new MusicMixer();
        Steps(mixer, wish, SoundBoard.MusicFadeInSeconds + 1f);
        return mixer;
    }

    private static List<MusicMix> Steps(MusicMixer mixer, MusicWish wish, float seconds) =>
        [.. Enumerable.Range(0, (int)MathF.Ceiling(seconds / Frame)).Select(_ => mixer.Step(wish, Frame))];

    private static void AssertRising(List<MusicMix> mixes)
    {
        for (var i = 1; i < mixes.Count; i++)
        {
            Assert.True(mixes[i].Volume >= mixes[i - 1].Volume, $"volume fell at frame {i}");
        }
    }

    private static void AssertFalling(List<MusicMix> mixes)
    {
        for (var i = 1; i < mixes.Count; i++)
        {
            Assert.True(mixes[i].Volume <= mixes[i - 1].Volume, $"volume rose at frame {i}");
        }
    }
}
