using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Consumables lying in the level: a small blood vial that floats a hand's width above the floor, turns slowly and
/// pulses red - easy to spot in the dark. Drawn between two ticks so a falling vial moves smoothly. Pure presentation.
/// </summary>
internal sealed class PickupView
{
    private static readonly Color Glass = new(120, 14, 26, 255);
    private static readonly Color Blood = new(230, 40, 50, 255);
    private static readonly Color Neck = new(180, 170, 175, 255);
    private static readonly Color Cork = new(110, 74, 46, 255);

    private float _time;

    public void Update(float frameSeconds) => _time += frameSeconds;

    /// <summary>Draws all pickups. <paramref name="alpha"/> blends between the last two ticks.</summary>
    public void Draw(IReadOnlyList<Pickup> pickups, float alpha)
    {
        foreach (var pickup in pickups)
        {
            var bottom = Vector3.Lerp(pickup.PreviousPosition, pickup.Position, alpha);
            var phase = _time * 2.5f + pickup.Id;
            var hover = 0.12f + MathF.Sin(phase) * 0.04f;
            var place =
                Matrix4x4.CreateRotationY(_time * 1.8f + pickup.Id) *
                Matrix4x4.CreateTranslation(bottom + new Vector3(0f, hover, 0f));

            // The blood inside pulses: dark glass around a glowing core.
            var pulse = 0.5f + 0.5f * MathF.Sin(phase * 2f);
            var core = Blend(Glass, Blood, 0.4f + 0.6f * pulse);

            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.08f, 0f) * place, new Vector3(0.14f, 0.16f, 0.14f), Glass);   // body
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.07f, 0f) * place, new Vector3(0.1f, 0.12f, 0.15f), core);     // blood
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.19f, 0f) * place, new Vector3(0.06f, 0.06f, 0.06f), Neck);    // neck
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, 0.235f, 0f) * place, new Vector3(0.075f, 0.035f, 0.075f), Cork); // cork
        }
    }

    private static Color Blend(Color from, Color to, float amount) => new(
        (byte)(from.R + (to.R - from.R) * amount),
        (byte)(from.G + (to.G - from.G) * amount),
        (byte)(from.B + (to.B - from.B) * amount),
        (byte)255);
}
