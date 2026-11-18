using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// What the player sees when hurt: a short jolt of the camera (the picture darkening for a moment is
/// <see cref="ScreenEffectsView"/>, issue #370).
/// At death the picture turns red (the end screen with "YOU DIED" follows). When healed, "+50" rises next to the health bar.
/// (A level up shows the bonus window instead - <see cref="BoonWindow"/>.) Pure presentation - Core reports
/// <see cref="PlayerHit"/>, <see cref="PlayerDied"/> and <see cref="PlayerHealed"/>. The ground also trembles when
/// Gorm roars (<see cref="GormRoared"/>) and, a little, when a stone lands (<see cref="StoneLanded"/>) or a cracked
/// wall breaks (<see cref="WallBroken"/>).
/// </summary>
internal sealed class DamageFeedback
{
    private const float HealSeconds = 1.2f;
    private const float JoltSeconds = 0.25f;
    private const float DeathFadeSeconds = 1f;
    private const float JoltRadians = 0.035f;
    private const float QuakeSeconds = 1.5f;
    private const float QuakeRadians = 0.02f;

    private float _jolt;
    private float _quake;
    private float _deathFade;
    private bool _isDead;
    private float _time;
    private float _heal;
    private int _healAmount;

    public void OnEvent(GameEvent gameEvent)
    {
        switch (gameEvent)
        {
            case PlayerHit:
                _jolt = 1f;
                break;
            case PlayerHealed healed:
                _heal = 1f;
                _healAmount = healed.Amount;
                break;
            case PlayerDied:
                _isDead = true;
                break;
            case GormRoared:
                _quake = 1f;
                break;
            case StoneLanded:
                _quake = MathF.Max(_quake, 0.25f);
                break;
            case WallBroken:
                _quake = MathF.Max(_quake, 0.45f);
                break;
        }
    }

    public void Update(float frameSeconds)
    {
        _time += frameSeconds;
        _jolt = MathF.Max(0f, _jolt - frameSeconds / JoltSeconds);
        _quake = MathF.Max(0f, _quake - frameSeconds / QuakeSeconds);
        _heal = MathF.Max(0f, _heal - frameSeconds / HealSeconds);
        if (_isDead)
        {
            _deathFade = MathF.Min(1f, _deathFade + frameSeconds / DeathFadeSeconds);
        }
    }

    /// <summary>
    /// The camera jolt as a small turn (x = to the right, y = up, in radians) - add it to the looking direction.
    /// A quick shake that dies down, plus the slower trembling of the ground.
    /// </summary>
    public Vector2 Jolt() =>
        (new Vector2(MathF.Sin(_time * 70f), MathF.Cos(_time * 53f) * 0.6f + 0.4f) * JoltRadians * _jolt * _jolt)
        + (new Vector2(MathF.Sin(_time * 41f), MathF.Sin(_time * 57f)) * QuakeRadians * MathF.Sqrt(_quake));

    /// <summary>Draws the heal number and the death screen. Call after the 3D picture, before the HUD.</summary>
    public void Draw()
    {
        var height = Raylib.GetScreenHeight();

        if (_heal > 0f)
        {
            // "+50" right of the health bar (bottom left), rising a little and fading out.
            var text = $"+{_healAmount}";
            var rise = (int)(18 * (1f - _heal));
            PixelText.Draw(text, 250, height - 70 - rise, 24, new Color(235, 205, 120, (int)(255 * MathF.Min(1f, _heal * 2f))));
        }

        if (_deathFade > 0f)
        {
            // Only the red: "YOU DIED" with the numbers of the run follows on the end screen.
            Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), height, new Color(90, 0, 8, (int)(170 * _deathFade)));
        }
    }
}
