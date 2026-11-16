using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// What the player sees when hurt: a red flash at the screen's edges and a short jolt of the camera.
/// At death the picture turns red (the end screen with "YOU DIED" follows). When healed, "+50" rises next to the health bar.
/// (A level up shows the bonus window instead - <see cref="BoonWindow"/>.) Pure presentation - Core reports
/// <see cref="PlayerHit"/>, <see cref="PlayerDied"/> and <see cref="PlayerHealed"/>. The ground also trembles when
/// Gorm roars (<see cref="GormRoared"/>) and, a little, when a stone lands (<see cref="StoneLanded"/>) or a cracked
/// wall breaks (<see cref="WallBroken"/>).
/// </summary>
internal sealed class DamageFeedback
{
    private const float HealSeconds = 1.2f;
    private const float FlashSeconds = 0.4f;
    private const float JoltSeconds = 0.25f;
    private const float DeathFadeSeconds = 1f;
    private const float JoltRadians = 0.035f;
    private const float QuakeSeconds = 1.5f;
    private const float QuakeRadians = 0.02f;

    private float _flash;
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
                _flash = 1f;
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
        _flash = MathF.Max(0f, _flash - frameSeconds / FlashSeconds);
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

    /// <summary>Draws the red edges and the death screen. Call after the 3D picture, before the HUD.</summary>
    public void Draw()
    {
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();

        if (_flash > 0f)
        {
            // Red from each edge towards the middle, fading out - a "vignette".
            var edge = new Color(170, 0, 10, (int)(170 * _flash));
            var clear = new Color(170, 0, 10, 0);
            var band = Math.Max(width, height) / 5;
            Raylib.DrawRectangleGradientH(0, 0, band, height, edge, clear);
            Raylib.DrawRectangleGradientH(width - band, 0, band, height, clear, edge);
            Raylib.DrawRectangleGradientV(0, 0, width, band, edge, clear);
            Raylib.DrawRectangleGradientV(0, height - band, width, band, clear, edge);
        }

        if (_heal > 0f)
        {
            // "+50" right of the health bar (bottom left), rising a little and fading out.
            var text = $"+{_healAmount}";
            var rise = (int)(18 * (1f - _heal));
            Raylib.DrawText(text, 250, height - 70 - rise, 24, new Color(235, 205, 120, (int)(255 * MathF.Min(1f, _heal * 2f))));
        }

        if (_deathFade > 0f)
        {
            // Only the red: "YOU DIED" with the numbers of the run follows on the end screen.
            Raylib.DrawRectangle(0, 0, width, height, new Color(90, 0, 8, (int)(170 * _deathFade)));
        }
    }
}
