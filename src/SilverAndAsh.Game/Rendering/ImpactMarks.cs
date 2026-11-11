using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Bullet impacts on walls, floor and ceiling: a short bright spark, then a dark mark that disappears after a while.
/// Pure presentation - Core only reports where the shot hit (<see cref="BulletImpact"/>).
/// </summary>
internal sealed class ImpactMarks
{
    private const float SparkSeconds = 0.08f;
    private const float LifeSeconds = 6f;
    private const int MaxMarks = 40;
    private const float Size = 0.12f;
    private const float Thickness = 0.01f;

    private static readonly Color Spark = new(255, 236, 190, 255);
    private static readonly Color Mark = new(36, 30, 36, 255);

    private readonly List<(BulletImpact Impact, float Age)> _marks = [];

    public void OnEvent(GameEvent gameEvent)
    {
        if (gameEvent is BulletImpact impact)
        {
            if (_marks.Count == MaxMarks)
            {
                _marks.RemoveAt(0);   // the oldest one goes
            }

            _marks.Add((impact, 0f));
        }
    }

    public void Update(float frameSeconds)
    {
        for (var i = _marks.Count - 1; i >= 0; i--)
        {
            var age = _marks[i].Age + frameSeconds;
            if (age > LifeSeconds)
            {
                _marks.RemoveAt(i);
            }
            else
            {
                _marks[i] = (_marks[i].Impact, age);
            }
        }
    }

    /// <summary>Draws all marks. Call between BeginMode3D and EndMode3D.</summary>
    public void Draw()
    {
        foreach (var (impact, age) in _marks)
        {
            // A thin square lying flat on the surface, lifted a few millimetres so it is not swallowed by it.
            var normal = impact.Normal;
            var size = new Vector3(
                MathF.Abs(normal.X) > 0.5f ? Thickness : Size,
                MathF.Abs(normal.Y) > 0.5f ? Thickness : Size,
                MathF.Abs(normal.Z) > 0.5f ? Thickness : Size);
            var centre = impact.Point + normal * (Thickness / 2f + 0.003f);
            ShadedBox.Draw(Matrix4x4.CreateTranslation(centre), size, age < SparkSeconds ? Spark : Mark);
        }
    }
}
