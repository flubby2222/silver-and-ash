using System.Numerics;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Audio;

/// <summary>
/// Who hears the sounds (issue #491): a player's id, where their ears are (the eyes) and where they face, flat on the
/// ground. Any player can be the listener - on one computer the game passes its own player.
/// </summary>
public readonly record struct SoundListener(int PlayerId, Vector3 Ears, Vector3 Facing)
{
    /// <summary>The ears of <paramref name="player"/>: their eyes, facing the way they look (flat).</summary>
    public static SoundListener Of(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return new SoundListener(player.Id, player.EyePosition, player.View.FlatForward);
    }
}

/// <summary>
/// How one sound reaches the ears: <paramref name="Pan"/> from -1 (only the left ear) over 0 (the middle) to 1 (only the
/// right ear), the same scale as raylib's SetSoundPan; <paramref name="Volume"/> from 0 (silent) to 1 (full).
/// </summary>
public readonly record struct SoundMix(float Pan, float Volume)
{
    /// <summary>In the middle at full volume: a sound without a place (menu, music, one's own weapon, being hit).</summary>
    public static SoundMix Centre => new(0f, 1f);
}

/// <summary>
/// Directional sound (issue #491), pure logic without a sound device: from where a sound happens and where a player's
/// ears are and face, how far to the side and how loud it is heard. The game then hands both to raylib.
/// <list type="bullet">
/// <item>Volume: full up to <see cref="HearingBalance.NearDistance"/>, then falling to silence at
/// <see cref="HearingBalance.Range"/>, bent by <see cref="HearingBalance.Rolloff"/>.</item>
/// <item>Pan: how much of the way to the source points to the listener's right, times
/// <see cref="HearingBalance.MaxPan"/>. Straight ahead and straight behind are in the middle (stereo cannot tell front
/// from back). Height does not count towards the side.</item>
/// </list>
/// </summary>
public static class Hearing
{
    /// <summary>
    /// Where <paramref name="listenerId"/> hears <paramref name="gameEvent"/> from: its <see cref="GameEvent.Place"/>,
    /// but null (the middle) for a sound the listener makes themselves (<see cref="IPlayerSound"/>) - one's own steps
    /// are in the head, another player's come from where they walk.
    /// </summary>
    public static Vector3? PlaceFor(GameEvent gameEvent, int listenerId)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        return gameEvent is IPlayerSound own && own.PlayerId == listenerId ? null : gameEvent.Place;
    }

    /// <summary>How <paramref name="listener"/> hears <paramref name="gameEvent"/>: placed if it has a place for them, else <see cref="SoundMix.Centre"/>.</summary>
    public static SoundMix MixFor(GameEvent gameEvent, SoundListener listener, HearingBalance hearing) =>
        PlaceFor(gameEvent, listener.PlayerId) is { } place
            ? Mix(listener.Ears, listener.Facing, place, hearing)
            : SoundMix.Centre;

    /// <summary>
    /// How a sound at <paramref name="source"/> reaches ears at <paramref name="ears"/> that face
    /// <paramref name="facing"/> (only its flat part counts; a facing straight up or zero leaves the sound in the middle).
    /// A source at the ears themselves is in the middle at full volume.
    /// </summary>
    public static SoundMix Mix(Vector3 ears, Vector3 facing, Vector3 source, HearingBalance hearing)
    {
        ArgumentNullException.ThrowIfNull(hearing);
        var offset = source - ears;
        var distance = offset.Length();
        return distance >= hearing.Range
            ? new SoundMix(0f, 0f)
            : new SoundMix(Pan(offset, distance, facing, hearing), Volume(distance, hearing));
    }

    /// <summary>1 up to the near distance, 0 at the range, in between (1 - t)^rolloff.</summary>
    private static float Volume(float distance, HearingBalance hearing)
    {
        if (distance <= hearing.NearDistance)
        {
            return 1f;
        }

        // Here NearDistance < distance < Range, so the span is above 0.
        var t = (distance - hearing.NearDistance) / (hearing.Range - hearing.NearDistance);
        return MathF.Pow(1f - t, hearing.Rolloff);
    }

    /// <summary>
    /// The share of the way that points to the right (-1 to 1), times the largest pan. Divided by at least the near
    /// distance: close to the ears the sound moves gently to the middle instead of jumping from side to side, and a
    /// source at the ears divides by no zero.
    /// </summary>
    private static float Pan(Vector3 offset, float distance, Vector3 facing, HearingBalance hearing)
    {
        // Right of the flat facing - the same as FirstPersonView.FlatRight: yaw 0 faces +Z, the right is -X.
        var right = new Vector3(-facing.Z, 0f, facing.X);
        var length = right.Length();
        if (length <= 1e-6f)
        {
            return 0f;
        }

        var side = Vector3.Dot(offset, right / length) / MathF.Max(distance, hearing.NearDistance);
        return Math.Clamp(side, -1f, 1f) * hearing.MaxPan;
    }
}
