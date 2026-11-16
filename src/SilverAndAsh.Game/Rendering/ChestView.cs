using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Placeholder chests, one look per tier so it shows from afar whether a key is worth it: Beggar's = brown wood,
/// Guard's = steel blue metal, King's = gold, Vampire's = blood red with a dark glow. All have darker bands; a locked
/// one carries a brass padlock in front. An open chest stands with its lid tipped back. A combat room's chest rises
/// out of the floor. Pure presentation - real models come later.
/// </summary>
internal static class ChestView
{
    private static readonly Color Brass = new(176, 140, 72, 255);
    private static readonly Color Inside = new(18, 14, 14, 255);

    /// <summary>Draws all chests that are (partly) above the floor. Call between BeginMode3D and EndMode3D.</summary>
    public static void Draw(IReadOnlyList<Chest> chests, float seconds, Vector3 eye)
    {
        foreach (var chest in chests.Where(chest => chest.RiseFraction > 0f))
        {
            var (body, band) = Colours(chest.Tier, seconds);

            // Rising: it comes up out of the floor (the part below the floor is hidden by it).
            var sink = (1f - EaseOut(chest.RiseFraction)) * Chest.Height;
            var place = Matrix4x4.CreateTranslation(chest.Position - new Vector3(0f, sink, 0f));

            const float BaseHeight = Chest.Height * 0.65f;
            const float LidHeight = Chest.Height - BaseHeight;
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, BaseHeight / 2f, 0f) * place, new Vector3(Chest.Width, BaseHeight, Chest.Depth), body);

            if (chest.IsOpen)
            {
                // Dark inside, and the lid tipped back over the edge away from the viewer (so it never hides the chest).
                ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, BaseHeight + 0.005f, 0f) * place, new Vector3(Chest.Width * 0.9f, 0.01f, Chest.Depth * 0.85f), Inside);
                var away = chest.Position.Z >= eye.Z ? 1f : -1f;
                var lid = Matrix4x4.CreateTranslation(0f, LidHeight / 2f, -away * Chest.Depth / 2f)
                    * Matrix4x4.CreateRotationX(away * 1.9f)
                    * Matrix4x4.CreateTranslation(0f, BaseHeight, away * Chest.Depth / 2f);
                ShadedBox.Draw(lid * place, new Vector3(Chest.Width, LidHeight, Chest.Depth), body);
            }
            else
            {
                ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, BaseHeight + LidHeight / 2f, 0f) * place, new Vector3(Chest.Width, LidHeight, Chest.Depth), body);
            }

            // Two bands round the chest, a little proud of it.
            foreach (var x in new[] { -0.3f, 0.3f })
            {
                ShadedBox.Draw(Matrix4x4.CreateTranslation(x, BaseHeight / 2f, 0f) * place, new Vector3(0.08f, BaseHeight + 0.02f, Chest.Depth + 0.02f), band);
            }

            if (chest.IsLocked)
            {
                // A padlock on both long faces, so it shows from either side.
                foreach (var z in new[] { -1f, 1f })
                {
                    var front = z * (Chest.Depth / 2f + 0.04f);
                    ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, BaseHeight - 0.08f, front) * place, new Vector3(0.16f, 0.16f, 0.06f), Brass);
                }
            }
        }
    }

    internal static (Color Body, Color Band) Colours(ChestTier tier, float seconds) => tier switch
    {
        ChestTier.Beggar => (new Color(110, 76, 46, 255), new Color(56, 44, 36, 255)),
        ChestTier.Guard => (new Color(92, 112, 140, 255), new Color(52, 58, 70, 255)),
        ChestTier.King => (new Color(214, 170, 64, 255), new Color(140, 100, 36, 255)),
        ChestTier.Vampire => (Pulse(new Color(150, 20, 30, 255), new Color(200, 30, 40, 255), seconds), new Color(40, 10, 14, 255)),
        _ => (Color.Magenta, Color.Magenta),
    };

    private static Color Pulse(Color from, Color to, float seconds)
    {
        var amount = 0.5f + 0.5f * MathF.Sin(seconds * 2f);
        return new Color(
            (byte)(from.R + (to.R - from.R) * amount),
            (byte)(from.G + (to.G - from.G) * amount),
            (byte)(from.B + (to.B - from.B) * amount),
            (byte)255);
    }

    // Fast at first, slowing down at the top.
    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}
