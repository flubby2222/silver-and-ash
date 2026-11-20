using System.Numerics;
using SilverAndAsh.Core.Audio;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Audio;

/// <summary>
/// Directional sound (issue #491): how far to the side and how loud a sound with a place reaches a player's ears. The
/// side is checked against <see cref="FirstPersonView.FlatRight"/> - the right the player sees on the screen.
/// </summary>
public class HearingTests
{
    private static readonly HearingBalance Values = Balance.Default.Hearing;

    // Between the near distance and the range: placed, but neither at full volume nor silent.
    private static readonly float Middle = (Values.NearDistance + Values.Range) / 2f;

    private static readonly Vector3 Ears = new(3f, 1.6f, 5f);

    private const float Tolerance = 1e-5f;

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(MathF.PI / 2f)]
    [InlineData(-2f)]
    [InlineData(MathF.PI)]
    public void A_sound_to_the_right_of_the_view_goes_to_the_right_and_one_to_the_left_to_the_left(float yaw)
    {
        var view = new FirstPersonView(yaw);

        var right = Hearing.Mix(Ears, view.FlatForward, Ears + (view.FlatRight * Middle), Values);
        var left = Hearing.Mix(Ears, view.FlatForward, Ears - (view.FlatRight * Middle), Values);

        Assert.Equal(Values.MaxPan, right.Pan, Tolerance);
        Assert.Equal(-Values.MaxPan, left.Pan, Tolerance);
        Assert.True(right.Pan > 0f, $"right came out at {right.Pan}");
        Assert.Equal(right.Volume, left.Volume, Tolerance);
    }

    [Fact]
    public void At_yaw_0_the_right_is_minus_x()
    {
        // The player starts looking along +Z, down the level text; their right is the lower column numbers.
        var mix = Hearing.Mix(Ears, Vector3.UnitZ, Ears + new Vector3(-Middle, 0f, 0f), Values);

        Assert.Equal(Values.MaxPan, mix.Pan, Tolerance);
    }

    [Fact]
    public void A_sound_half_to_the_side_goes_half_way()
    {
        var view = new FirstPersonView(0.3f);
        var way = Vector3.Normalize(view.FlatForward + (view.FlatRight * MathF.Sqrt(3f))); // 60 degrees to the right

        var mix = Hearing.Mix(Ears, view.FlatForward, Ears + (way * Middle), Values);

        Assert.Equal(Values.MaxPan * MathF.Sin(MathF.PI / 3f), mix.Pan, Tolerance);
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(-1f)]
    public void A_sound_straight_ahead_or_behind_is_in_the_middle(float sign)
    {
        var view = new FirstPersonView(1.1f);

        var mix = Hearing.Mix(Ears, view.FlatForward, Ears + (view.FlatForward * (sign * Middle)), Values);

        Assert.Equal(0f, mix.Pan, Tolerance);
        Assert.True(mix.Volume is > 0f and < 1f, $"volume {mix.Volume}");
    }

    [Fact]
    public void Height_does_not_move_a_sound_to_the_side()
    {
        var mix = Hearing.Mix(Ears, Vector3.UnitZ, Ears + new Vector3(0f, Middle, 0f), Values);

        Assert.Equal(0f, mix.Pan, Tolerance);
    }

    [Fact]
    public void Up_to_the_near_distance_a_sound_is_at_full_volume()
    {
        var near = Hearing.Mix(Ears, Vector3.UnitZ, Ears + new Vector3(0f, 0f, Values.NearDistance), Values);
        var nearer = Hearing.Mix(Ears, Vector3.UnitZ, Ears + new Vector3(0f, 0f, Values.NearDistance / 2f), Values);

        Assert.Equal(1f, near.Volume);
        Assert.Equal(1f, nearer.Volume);
    }

    [Fact]
    public void Farther_away_a_sound_is_quieter_bent_by_the_rolloff()
    {
        var volumes = Enumerable.Range(1, 9)
            .Select(step => Values.NearDistance + ((Values.Range - Values.NearDistance) * step / 10f))
            .Select(distance => Hearing.Mix(Ears, Vector3.UnitZ, Ears + new Vector3(0f, 0f, distance), Values).Volume)
            .ToList();

        Assert.All(volumes.Zip(volumes.Skip(1)), pair => Assert.True(pair.Second < pair.First, $"{pair.Second} after {pair.First}"));
        Assert.Equal(MathF.Pow(0.5f, Values.Rolloff), volumes[4], Tolerance); // half way from near to the range
    }

    [Theory]
    [InlineData("near")]
    [InlineData("middle")]
    [InlineData("far")]
    [InlineData("beyond")]
    public void Near_middle_and_far_count_the_whole_way_up_and_across_too(string where)
    {
        // Issue #501: aslant - above, to the side and behind. The distance counts in all three directions, not only on
        // the ground; the same distance straight ahead would give the same volume.
        var span = Values.Range - Values.NearDistance;
        var (distance, volume) = where switch
        {
            "near" => (Values.NearDistance * 0.9f, 1f),
            "middle" => (Values.NearDistance + (span * 0.25f), MathF.Pow(0.75f, Values.Rolloff)),
            "far" => (Values.NearDistance + (span * 0.75f), MathF.Pow(0.25f, Values.Rolloff)),
            _ => (Values.Range * 1.1f, 0f),
        };
        var aslant = Vector3.Normalize(new Vector3(1f, 0.5f, -2f));

        var mix = Hearing.Mix(Ears, Vector3.UnitZ, Ears + (aslant * distance), Values);

        Assert.Equal(volume, mix.Volume, Tolerance);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.01f)]
    [InlineData(25f)]
    public void At_the_range_and_beyond_a_sound_is_silent(float beyond)
    {
        var mix = Hearing.Mix(Ears, Vector3.UnitZ, Ears + new Vector3(Values.Range + beyond, 0f, 0f), Values);

        Assert.Equal(0f, mix.Volume);
    }

    [Fact]
    public void A_sound_at_the_ears_is_in_the_middle_at_full_volume()
    {
        var mix = Hearing.Mix(Ears, Vector3.UnitZ, Ears, Values);

        Assert.Equal(new SoundMix(0f, 1f), mix);
        Assert.False(float.IsNaN(mix.Pan) || float.IsNaN(mix.Volume));
    }

    [Fact]
    public void Close_to_the_ears_a_sound_moves_gently_to_the_middle()
    {
        // Half the near distance to the right: half the way to the side, not all of it - no jump from ear to ear when
        // something passes close by.
        var view = new FirstPersonView(0f);

        var mix = Hearing.Mix(Ears, view.FlatForward, Ears + (view.FlatRight * (Values.NearDistance / 2f)), Values);

        Assert.Equal(Values.MaxPan / 2f, mix.Pan, Tolerance);
    }

    [Fact]
    public void Without_a_facing_a_sound_stays_in_the_middle()
    {
        var mix = Hearing.Mix(Ears, Vector3.UnitY, Ears + new Vector3(Middle, 0f, 0f), Values);

        Assert.Equal(0f, mix.Pan);
        Assert.False(float.IsNaN(mix.Volume));
    }

    [Fact]
    public void A_near_distance_as_far_as_the_range_divides_by_no_zero()
    {
        var hearing = Values with { NearDistance = 10f, Range = 10f };

        var inside = Hearing.Mix(Ears, Vector3.UnitZ, Ears + new Vector3(0f, 0f, 9.99f), hearing);
        var at = Hearing.Mix(Ears, Vector3.UnitZ, Ears + new Vector3(0f, 0f, 10f), hearing);

        Assert.Equal(1f, inside.Volume);
        Assert.Equal(0f, at.Volume);
    }

    [Fact]
    public void Ones_own_body_sounds_are_in_the_middle_another_players_come_from_where_they_are()
    {
        var listener = new SoundListener(0, Ears, Vector3.UnitZ);
        var there = Ears + new Vector3(-Middle, 0f, 0f); // to the listener's right
        var own = new PlayerStepped(0, Foot.Left) { Place = there };
        var others = new PlayerStepped(1, Foot.Left) { Place = there };

        Assert.Null(Hearing.PlaceFor(own, 0));
        Assert.Equal(SoundMix.Centre, Hearing.MixFor(own, listener, Values));
        Assert.Equal(there, Hearing.PlaceFor(others, 0));
        Assert.Equal(Values.MaxPan, Hearing.MixFor(others, listener, Values).Pan, Tolerance);
    }

    [Fact]
    public void Ones_own_shot_and_swing_are_in_the_middle_another_players_come_from_where_they_are()
    {
        // Issue #501: the weapon in one's own hand stays in the head; in co-op another player's shot comes from them.
        var listener = new SoundListener(0, Ears, Vector3.UnitZ);
        var there = Ears + new Vector3(-Middle, 0f, 0f); // to the listener's right
        GameEvent[] own = [new ShotFired(0, there, Vector3.UnitZ) { Place = there }, new MeleeSwung(0, there, Vector3.UnitZ) { Place = there }];
        GameEvent[] others = [new ShotFired(1, there, Vector3.UnitZ) { Place = there }, new MeleeSwung(1, there, Vector3.UnitZ) { Place = there }];

        Assert.All(own, gameEvent => Assert.Equal(SoundMix.Centre, Hearing.MixFor(gameEvent, listener, Values)));
        Assert.All(others, gameEvent => Assert.Equal(Values.MaxPan, Hearing.MixFor(gameEvent, listener, Values).Pan, Tolerance));
    }

    [Fact]
    public void A_chest_opened_by_the_listener_is_still_heard_from_the_chest()
    {
        // Opening is the chest's sound, not the player's body: it has a player, but is no IPlayerSound.
        var chest = new ChestOpened(4, 0, ChestTier.Guard) { Place = Ears + new Vector3(-Middle, 0f, 0f) };

        Assert.Equal(Values.MaxPan, Hearing.MixFor(chest, new SoundListener(0, Ears, Vector3.UnitZ), Values).Pan, Tolerance);
    }

    [Fact]
    public void A_sound_without_a_place_is_in_the_middle_at_full_volume()
    {
        var listener = new SoundListener(0, Ears, Vector3.UnitZ);

        Assert.Equal(SoundMix.Centre, Hearing.MixFor(new PlayerHit(0, 10), listener, Values));
        Assert.Equal(SoundMix.Centre, Hearing.MixFor(new ShotFired(0, Ears + Vector3.UnitX, Vector3.UnitZ), listener, Values));
        Assert.Equal(new SoundMix(0f, 1f), SoundMix.Centre);
    }

    [Fact]
    public void The_listener_is_the_player_passed_in_with_their_eyes_and_view()
    {
        var player = new Player(LevelParser.Parse("...\n.P.\n..."));
        player.Look(new PlayerInput(Vector2.Zero, new Vector2(0.7f / ControlSettings.MouseSensitivity, 0f), false));

        var listener = SoundListener.Of(player);

        Assert.Equal(player.Id, listener.PlayerId);
        Assert.Equal(player.EyePosition, listener.Ears);
        Assert.Equal(player.View.FlatForward, listener.Facing);
        Assert.NotEqual(Vector3.UnitZ, listener.Facing);
    }

    [Fact]
    public void Where_an_event_is_heard_does_not_count_for_its_equality()
    {
        var here = new GhoulDied(3) { Place = Vector3.UnitX };
        var there = new GhoulDied(3) { Place = Vector3.UnitZ };

        Assert.Equal(new GhoulDied(3), here);
        Assert.Equal(here, there);
        Assert.Equal(here.GetHashCode(), there.GetHashCode());
        Assert.NotEqual<GameEvent>(here, new GhoulDied(4) { Place = Vector3.UnitX });
        Assert.NotEqual<GameEvent>(here, new BatDied(3) { Place = Vector3.UnitX });
    }
}
