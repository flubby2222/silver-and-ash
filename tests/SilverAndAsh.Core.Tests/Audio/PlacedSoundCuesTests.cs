using System.Numerics;
using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Tests.Audio;

/// <summary>
/// The sounds of a tick heard by one player (issue #491): placed ones to their side and quieter with distance, the
/// rest in the middle at full volume as before.
/// </summary>
public class PlacedSoundCuesTests
{
    private static readonly HearingBalance Values = Balance.Default.Hearing;
    private static readonly SoundPack Old = SoundPack.Old;
    private static readonly Vector3 Ears = new(4f, 1.6f, 4f);

    // Listener 0 at the ears, looking along +Z: the right is -X.
    private static readonly SoundListener Listener = new(0, Ears, Vector3.UnitZ);
    private static readonly float Middle = (Values.NearDistance + Values.Range) / 2f;
    private static readonly Vector3 ToTheRight = Ears + new Vector3(-Middle, 0f, 0f);

    private const float Tolerance = 1e-5f;

    [Fact]
    public void A_ghoul_to_the_right_is_heard_on_the_right_and_quieter()
    {
        var mix = Hearing.Mix(Ears, Vector3.UnitZ, ToTheRight, Values);

        var cue = Assert.Single(new SoundCues(1).For([new GhoulWindUp(2) { Place = ToTheRight }], Old, Listener, Values));

        Assert.Equal(new SoundCue(SoundKind.GhoulWindUp, "ghul-ankuendigung.wav", SoundBoard.SoundVolume * mix.Volume, mix.Pan), cue);
        Assert.Equal(Values.MaxPan, cue.Pan, Tolerance);
        Assert.True(cue.Volume is > 0f and < SoundBoard.SoundVolume, $"volume {cue.Volume}");
    }

    [Fact]
    public void A_ghoul_out_of_hearing_range_is_not_played()
    {
        var far = Ears + new Vector3(0f, 0f, Values.Range + 1f);

        Assert.Empty(new SoundCues(1).For([new GhoulWindUp(2) { Place = far }], Old, Listener, Values));
    }

    [Fact]
    public void Of_two_of_a_kind_in_one_tick_the_loudest_is_heard()
    {
        var near = Ears + new Vector3(Values.NearDistance, 0f, 0f); // to the left, at full volume
        var events = new GameEvent[] { new GhoulWindUp(2) { Place = ToTheRight }, new GhoulWindUp(3) { Place = near } };

        var cue = Assert.Single(new SoundCues(1).For(events, Old, Listener, Values));

        Assert.Equal(SoundBoard.SoundVolume, cue.Volume);
        Assert.Equal(-Values.MaxPan, cue.Pan, Tolerance);
    }

    [Fact]
    public void A_silent_one_does_not_hide_a_heard_one_of_the_same_kind()
    {
        var far = Ears + new Vector3(0f, 0f, Values.Range + 1f);
        var events = new GameEvent[] { new GhoulWindUp(2) { Place = far }, new GhoulWindUp(3) { Place = ToTheRight } };

        var cue = Assert.Single(new SoundCues(1).For(events, Old, Listener, Values));

        Assert.Equal(Values.MaxPan, cue.Pan, Tolerance);
    }

    [Fact]
    public void The_listeners_own_step_is_in_the_middle_another_players_step_to_the_side()
    {
        var own = Assert.Single(new SoundCues(1).For([new PlayerStepped(0, Foot.Left) { Place = ToTheRight }], Old, Listener, Values));
        var other = Assert.Single(new SoundCues(1).For([new PlayerStepped(1, Foot.Left) { Place = ToTheRight }], Old, Listener, Values));

        Assert.Equal((0f, SoundBoard.SoundVolume), (own.Pan, own.Volume));
        Assert.Equal(Values.MaxPan, other.Pan, Tolerance);
        Assert.True(other.Volume < SoundBoard.SoundVolume);
    }

    [Fact]
    public void Sounds_without_a_place_play_as_before_in_the_middle()
    {
        // Being hit, a shot and a blade hit without a place: the same cues as without directional sound.
        GameEvent[] events = [new ShotFired(0, ToTheRight, Vector3.UnitZ), new PlayerHit(0, 10), new TargetHitBy(0, 0, Weapon.Blade)];

        var placed = new SoundCues(1).For(events, Old, Listener, Values);
        var before = new SoundCues(1).For(events, Old);

        Assert.NotEmpty(placed);
        Assert.Equal(before, placed);
        Assert.All(placed, cue => Assert.Equal((0f, SoundBoard.SoundVolume), (cue.Pan, cue.Volume)));
    }

    [Fact]
    public void A_menu_sound_is_in_the_middle_at_full_volume()
    {
        var pack = SoundPack.All.First(candidate => !candidate.Variants(SoundKind.MenuClick).IsEmpty);

        var cue = Assert.Single(new SoundCues(1).For(SoundKind.MenuClick, pack));

        Assert.Equal((0f, SoundBoard.SoundVolume), (cue.Pan, cue.Volume));
    }

    [Fact]
    public void Without_a_listener_placed_sounds_stay_in_the_middle()
    {
        var cue = Assert.Single(new SoundCues(1).For([new GhoulWindUp(2) { Place = ToTheRight }], Old));

        Assert.Equal((0f, SoundBoard.SoundVolume), (cue.Pan, cue.Volume));
    }

    [Fact]
    public void A_far_wing_beat_stays_silent_with_a_listener_too()
    {
        // The wing beat keeps its own short earshot (issue #297), inside the hearing range.
        var at = Ears + new Vector3(0f, 0f, SoundBoard.BatFlapHearingDistance + 1f);
        Assert.True(Vector3.Distance(at, Ears) < Values.Range);

        Assert.Empty(new SoundCues(1).For([new BatFlapped(5, at) { Place = at }], SoundPack.All.First(p => !p.Variants(SoundKind.BatFlap).IsEmpty), Listener, Values));
    }
}
