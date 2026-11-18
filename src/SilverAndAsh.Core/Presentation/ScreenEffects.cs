using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The effects at the edge of the picture for one player (issue #370): the picture darkens for a moment when the player
/// is hit, a dark red edge pulses slowly while little life is left, and a short gold shine runs round the edge when a
/// skill point is won. This class decides when and how strongly (0 to 1), counted in simulation ticks; the Game project
/// only draws it. Every value of the look is a constant here, in one place.
/// </summary>
public sealed class ScreenEffects
{
    /// <summary>How long the picture stays darkened after a hit, in seconds.</summary>
    public const float HitSeconds = 0.3f;

    /// <summary>How dark a hit makes the picture at most (0 to 1) - the target in the middle stays visible.</summary>
    public const float HitDarkness = 0.45f;

    /// <summary>Below this share of the life the dark red edge shows.</summary>
    public const float LowLifeShare = 0.25f;

    /// <summary>One slow beat of the dark red edge, in seconds.</summary>
    public const float LowLifePulseSeconds = 1.6f;

    /// <summary>The dark red edge at its weakest and at its strongest (0 to 1).</summary>
    public const float LowLifeMin = 0.35f;

    /// <inheritdoc cref="LowLifeMin"/>
    public const float LowLifeMax = 0.8f;

    /// <summary>How long the gold shine of a new skill point lasts, in seconds.</summary>
    public const float SkillPointSeconds = 0.8f;

    /// <summary>
    /// The gold shine at its start (0 to 1, issue #389): half of a full edge, so it stays below the dark red edge at
    /// low life even at its weakest. Provisional, until the next playtest.
    /// </summary>
    public const float SkillPointStrength = 0.5f;

    /// <summary>How far the gold shine reaches into the picture, as a share of its height: half the band of the red edge.</summary>
    public const float SkillPointBand = 0.06f;

    /// <summary>How far the edge effects reach into the picture, as a share of its height.</summary>
    public const float EdgeBand = 0.12f;

    private readonly int _hitTicks = SimulationClock.SecondsToTicks(HitSeconds);
    private readonly int _pulseTicks = SimulationClock.SecondsToTicks(LowLifePulseSeconds);
    private readonly int _skillPointTicks = SimulationClock.SecondsToTicks(SkillPointSeconds);
    private int _hitTicksLeft;
    private int _skillPointTicksLeft;
    private int _lowLifeTicks;

    /// <summary>The effects of player <paramref name="playerId"/>; the events of other players are ignored.</summary>
    public ScreenEffects(int playerId) => PlayerId = playerId;

    /// <summary>The player whose picture this is.</summary>
    public int PlayerId { get; }

    /// <summary>How dark the picture is from a hit, 0 to <see cref="HitDarkness"/>.</summary>
    public float Darkness => HitDarkness * Fade(_hitTicksLeft, _hitTicks);

    /// <summary>How strong the dark red edge is: 0 while enough life is left, else between <see cref="LowLifeMin"/> and <see cref="LowLifeMax"/>.</summary>
    public float LowLife { get; private set; }

    /// <summary>How strong the gold shine of a new skill point is, 1 at its start, 0 when gone.</summary>
    public float SkillPoint => Fade(_skillPointTicksLeft, _skillPointTicks);

    /// <summary>Starts an effect if <paramref name="gameEvent"/> is a hit or a level up of this player.</summary>
    public void OnEvent(GameEvent gameEvent)
    {
        switch (gameEvent)
        {
            case PlayerHit hit when hit.PlayerId == PlayerId && hit.Damage > 0:
                _hitTicksLeft = _hitTicks;
                break;
            case LevelUp up when up.PlayerId == PlayerId:
                _skillPointTicksLeft = _skillPointTicks;
                break;
        }
    }

    /// <summary>One simulation tick: the effects fade, the low-life edge follows the life of <paramref name="player"/>.</summary>
    public void Tick(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        _hitTicksLeft = Math.Max(0, _hitTicksLeft - 1);
        _skillPointTicksLeft = Math.Max(0, _skillPointTicksLeft - 1);

        var low = !player.IsDead && player.MaxHealth > 0 && player.Health < player.MaxHealth * LowLifeShare;
        if (!low)
        {
            _lowLifeTicks = 0;
            LowLife = 0f;
            return;
        }

        // Starts at its weakest, swells to its strongest half a beat later and back again.
        var phase = (float)(_lowLifeTicks % _pulseTicks) / _pulseTicks;
        var swell = 0.5f - (0.5f * MathF.Cos(phase * MathF.Tau));
        LowLife = LowLifeMin + ((LowLifeMax - LowLifeMin) * swell);
        _lowLifeTicks++;
    }

    /// <summary>Every effect gone at once (a new run).</summary>
    public void Reset()
    {
        _hitTicksLeft = 0;
        _skillPointTicksLeft = 0;
        _lowLifeTicks = 0;
        LowLife = 0f;
    }

    /// <summary>
    /// True if the pixel at <paramref name="x"/>, <paramref name="y"/> of a picture <paramref name="width"/> by
    /// <paramref name="height"/> belongs to an edge effect of strength <paramref name="strength"/> (0 to 1): the closer
    /// to the edge, the more pixels, set in the pattern of the dithering - hard pixels, no soft glow. Nothing lies further
    /// in than <see cref="EdgeBand"/> of the height, so the middle of the picture stays clear.
    /// </summary>
    public static bool IsEdgePixelOn(int x, int y, int width, int height, float strength) =>
        IsBandPixelOn(x, y, width, height, strength, EdgeBandPixels(height));

    /// <summary>
    /// True if the pixel at <paramref name="x"/>, <paramref name="y"/> belongs to the gold shine of a new skill point
    /// at <paramref name="shine"/> (<see cref="SkillPoint"/>, 1 at its start): the same pattern as
    /// <see cref="IsEdgePixelOn"/>, but at <see cref="SkillPointStrength"/> and in the narrower
    /// <see cref="SkillPointBand"/> (issue #389: the full edge was too much).
    /// </summary>
    public static bool IsSkillPointPixelOn(int x, int y, int width, int height, float shine) =>
        IsBandPixelOn(x, y, width, height, Math.Min(shine, 1f) * SkillPointStrength,
            Math.Max(1, (int)MathF.Round(height * SkillPointBand)));

    private static bool IsBandPixelOn(int x, int y, int width, int height, float strength, int band)
    {
        if (strength <= 0f || width <= 0 || height <= 0)
        {
            return false;
        }

        var distance = Math.Min(Math.Min(x, width - 1 - x), Math.Min(y, height - 1 - y));
        if (distance >= band)
        {
            return false;
        }

        // Squared, so the pixels thin out quickly inwards: a shine at the edge, not a solid frame.
        var linear = 1f - ((float)distance / band);
        var cover = linear * linear;
        return Math.Min(strength, 1f) * cover > Dithering.Threshold(x, y);
    }

    /// <summary>How many pixels the edge band of a picture <paramref name="height"/> lines high reaches in (at least 1).</summary>
    public static int EdgeBandPixels(int height) => Math.Max(1, (int)MathF.Round(height * EdgeBand));

    private static float Fade(int left, int total) => total > 0 ? (float)left / total : 0f;
}
