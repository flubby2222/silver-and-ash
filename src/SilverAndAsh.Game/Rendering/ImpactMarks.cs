using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Bullet impacts on walls, floor and ceiling: a short bright spark, then a dark mark that disappears after a while. The
/// stake crossbow's shot (issue #793) leaves a mark of its own: a bigger crack and the stake's end sticking out of the stone,
/// which stays longer.
/// Pure presentation - Core only reports where the shot hit (<see cref="BulletImpact"/>).
/// </summary>
internal sealed class ImpactMarks
{
    private const float SparkSeconds = 0.08f;
    private const float LifeSeconds = 6f;
    private const int MaxMarks = 40;
    private const float Size = 0.12f;
    private const float Thickness = 0.01f;
    private const float StakeLifeSeconds = 20f;
    private const float StakeCrackSize = 0.2f;
    private const float StakeStubWidth = 0.03f;
    private const float StakeStubLength = 0.09f;

    // Steps of the palette (EffectColours, issue #609).
    private static readonly Color Spark = PaletteColour.Opaque(EffectColours.ImpactSpark);
    private static readonly Color Mark = PaletteColour.Opaque(EffectColours.ImpactMark);
    private static readonly Color StakeStub = PaletteColour.Opaque(EffectColours.StakeStub);

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
            if (age > LifeOf(_marks[i].Impact))
            {
                _marks.RemoveAt(i);
            }
            else
            {
                _marks[i] = (_marks[i].Impact, age);
            }
        }
    }

    private static float LifeOf(BulletImpact impact) => impact.Weapon == WeaponKind.StakeCrossbow ? StakeLifeSeconds : LifeSeconds;

    /// <summary>Draws all marks. Call between BeginMode3D and EndMode3D.</summary>
    public void Draw()
    {
        foreach (var (impact, age) in _marks)
        {
            // A thin square lying flat on the surface, lifted a few millimetres so it is not swallowed by it.
            var normal = impact.Normal;
            var isStake = impact.Weapon == WeaponKind.StakeCrossbow;
            var size = FlatOn(normal, isStake ? StakeCrackSize : Size, Thickness);
            var centre = impact.Point + normal * (Thickness / 2f + 0.003f);
            ShadedBox.Draw(Matrix4x4.CreateTranslation(centre), size, age < SparkSeconds ? Spark : Mark);
            if (isStake)
            {
                // The stake's end, a short square beam standing out of the surface.
                var stub = impact.Point + normal * (StakeStubLength / 2f);
                ShadedBox.Draw(Matrix4x4.CreateTranslation(stub), FlatOn(normal, StakeStubWidth, StakeStubLength), StakeStub);
            }
        }
    }

    /// <summary>A box <paramref name="across"/> wide on every side but the one the surface faces (its normal), where it is <paramref name="along"/> thick.</summary>
    private static Vector3 FlatOn(Vector3 normal, float across, float along) => new(
        MathF.Abs(normal.X) > 0.5f ? along : across,
        MathF.Abs(normal.Y) > 0.5f ? along : across,
        MathF.Abs(normal.Z) > 0.5f ? along : across);
}
