using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Blood bolts in flight (glowing red balls, drawn between two ticks so they move smoothly) and the short splash
/// where one hits a wall. Pure presentation.
/// </summary>
internal sealed class ProjectileView
{
    private const float SplashSeconds = 0.35f;

    private static readonly Color Core = new(255, 70, 60, 255);
    private static readonly Color Shell = new(150, 10, 20, 255);
    private static readonly Color Splash = new(130, 8, 16, 255);

    private readonly List<(Vector3 Point, float Age)> _splashes = [];
    private float _time;

    public void OnEvent(GameEvent gameEvent)
    {
        if (gameEvent is ProjectileImpact impact)
        {
            _splashes.Add((impact.Point, 0f));
        }
    }

    public void Update(float frameSeconds)
    {
        _time += frameSeconds;
        for (var i = _splashes.Count - 1; i >= 0; i--)
        {
            var age = _splashes[i].Age + frameSeconds;
            if (age > SplashSeconds)
            {
                _splashes.RemoveAt(i);
            }
            else
            {
                _splashes[i] = (_splashes[i].Point, age);
            }
        }
    }

    /// <summary>Draws all bolts and splashes. <paramref name="alpha"/> blends between the last two ticks.</summary>
    public void Draw(IReadOnlyList<Projectile> projectiles, float alpha)
    {
        foreach (var projectile in projectiles)
        {
            var centre = Vector3.Lerp(projectile.PreviousPosition, projectile.Position, alpha);
            var pulse = 1f + 0.15f * MathF.Sin(_time * 30f + projectile.Id);
            var size = projectile.Radius * 2f * pulse;

            // A bright core and a slightly bigger dark-red cube spinning around it: the core's corners poke through,
            // so the ball looks like a glowing, churning blob in the low resolution.
            var spin = Matrix4x4.CreateRotationY(_time * 9f) * Matrix4x4.CreateRotationX(_time * 7f) * Matrix4x4.CreateTranslation(centre);
            ShadedBox.Draw(Matrix4x4.CreateTranslation(centre), new Vector3(size * 0.8f), Core);
            ShadedBox.Draw(spin, new Vector3(size * 0.95f), Shell);
        }

        foreach (var (point, age) in _splashes)
        {
            // A burst of four small drops flying apart and shrinking.
            var t = age / SplashSeconds;
            var size = 0.12f * (1f - t);
            for (var i = 0; i < 4; i++)
            {
                var angle = i * MathF.PI / 2f + 0.4f;
                var offset = new Vector3(MathF.Cos(angle), 0.6f - t, MathF.Sin(angle)) * (0.35f * t);
                ShadedBox.Draw(Matrix4x4.CreateTranslation(point + offset), new Vector3(size), Splash);
            }
        }
    }
}
