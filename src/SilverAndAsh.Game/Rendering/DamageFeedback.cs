using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// What the player sees when hurt: a red flash at the screen's edges and a short jolt of the camera.
/// At death the picture turns red and "YOU DIED" appears. Pure presentation - Core reports
/// <see cref="PlayerHit"/> and <see cref="PlayerDied"/>.
/// </summary>
internal sealed class DamageFeedback
{
    private const float FlashSeconds = 0.4f;
    private const float JoltSeconds = 0.25f;
    private const float DeathFadeSeconds = 1f;
    private const float JoltRadians = 0.035f;

    private float _flash;
    private float _jolt;
    private float _deathFade;
    private bool _isDead;
    private float _time;

    public void OnEvent(GameEvent gameEvent)
    {
        switch (gameEvent)
        {
            case PlayerHit:
                _flash = 1f;
                _jolt = 1f;
                break;
            case PlayerDied:
                _isDead = true;
                break;
        }
    }

    public void Update(float frameSeconds)
    {
        _time += frameSeconds;
        _flash = MathF.Max(0f, _flash - frameSeconds / FlashSeconds);
        _jolt = MathF.Max(0f, _jolt - frameSeconds / JoltSeconds);
        if (_isDead)
        {
            _deathFade = MathF.Min(1f, _deathFade + frameSeconds / DeathFadeSeconds);
        }
    }

    /// <summary>
    /// The camera jolt as a small turn (x = to the right, y = up, in radians) - add it to the looking direction.
    /// A quick shake that dies down.
    /// </summary>
    public Vector2 Jolt() =>
        new Vector2(MathF.Sin(_time * 70f), MathF.Cos(_time * 53f) * 0.6f + 0.4f) * JoltRadians * _jolt * _jolt;

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

        if (_deathFade > 0f)
        {
            Raylib.DrawRectangle(0, 0, width, height, new Color(90, 0, 8, (int)(170 * _deathFade)));

            const string Text = "YOU DIED";
            const int Size = 64;
            var textWidth = Raylib.MeasureText(Text, Size);
            Raylib.DrawText(Text, (width - textWidth) / 2, height / 2 - Size, Size, new Color(235, 220, 220, (int)(255 * _deathFade)));
        }
    }
}
