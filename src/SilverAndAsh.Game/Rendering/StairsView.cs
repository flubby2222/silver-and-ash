using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The spiral staircase that rises in the boss room after Gorm's death: a stone pole with steps winding round it,
/// coming up out of the floor. Pure presentation.
/// </summary>
internal static class StairsView
{
    private const int Steps = 16;

    private static readonly Color Stone = new(120, 114, 108, 255);
    private static readonly Color Pole = new(84, 80, 76, 255);
    private static readonly Color Light = new(210, 190, 140, 255);

    public static void Draw(Stairs? stairs, float seconds)
    {
        if (stairs is null || stairs.RiseFraction <= 0f)
        {
            return;
        }

        // Rising: it comes up out of the floor (the part below the floor is hidden by it).
        var rise = 1f - (1f - stairs.RiseFraction) * (1f - stairs.RiseFraction);
        var place = Matrix4x4.CreateTranslation(stairs.Position - new Vector3(0f, (1f - rise) * Stairs.Height, 0f));

        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, Stairs.Height / 2f, 0f) * place, new Vector3(0.35f, Stairs.Height, 0.35f), Pole);
        for (var step = 0; step < Steps; step++)
        {
            var angle = step * (2f * MathF.PI / 8f);
            var height = (step + 0.5f) * Stairs.Height / Steps;
            var stepPlace =
                Matrix4x4.CreateTranslation(Stairs.Radius / 2f, height, 0f) *
                Matrix4x4.CreateRotationY(angle) *
                place;
            ShadedBox.Draw(stepPlace, new Vector3(Stairs.Radius, 0.12f, 0.55f), Stone);
        }

        // Once it stands, a faint light from above shows the way up.
        if (stairs.CanInteract)
        {
            var glow = 0.5f + 0.5f * MathF.Sin(seconds * 2f);
            var color = new Color((byte)(Light.R * glow), (byte)(Light.G * glow), (byte)(Light.B * glow), (byte)255);
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, Stairs.Height + 0.05f, 0f) * place, new Vector3(0.5f, 0.1f, 0.5f), color);
        }
    }
}
