using System.Globalization;
using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The window after a level up: the picture darkens, one box per boon of the offer Core drew from the pool.
/// Choose with 1 / 2 / 3 or a click. It only reads and shows - the choice goes to Core as input
/// (<see cref="Core.Input.PlayerInput.ChooseBoon"/>).
/// </summary>
internal static class BoonWindow
{
    /// <summary>
    /// Seconds after the window opens before a click counts. The cursor appears in the middle - right over a box -
    /// and attack clicks just after the killing blow must not choose a bonus by accident (found while testing).
    /// </summary>
    public const float ClickDelaySeconds = 0.6f;

    private const int BoxWidth = 220;
    private const int BoxHeight = 120;
    private const int Gap = 24;

    private static readonly Color Shade = new(10, 6, 12, 170);
    private static readonly Color Box = new(32, 26, 36, 235);
    private static readonly Color BoxHovered = new(58, 44, 52, 245);
    private static readonly Color Gold = new(232, 196, 96, 255);
    private static readonly Color Text = new(220, 220, 230, 255);
    private static readonly Color Muted = new(150, 150, 165, 255);

    private static readonly KeyboardKey[] Keys = [KeyboardKey.One, KeyboardKey.Two, KeyboardKey.Three, KeyboardKey.Four];
    private static readonly KeyboardKey[] PadKeys = [KeyboardKey.Kp1, KeyboardKey.Kp2, KeyboardKey.Kp3, KeyboardKey.Kp4];

    /// <summary>
    /// The boon chosen in this frame from <paramref name="offer"/> (key 1, 2, 3 ..., or a click on a box once the window
    /// has been open for <see cref="ClickDelaySeconds"/> - <paramref name="openSeconds"/>), or null.
    /// </summary>
    public static Boon? Pick(IReadOnlyList<Boon> offer, float openSeconds)
    {
        ArgumentNullException.ThrowIfNull(offer);
        for (var i = 0; i < offer.Count && i < Keys.Length; i++)
        {
            if (Raylib.IsKeyPressed(Keys[i]) || Raylib.IsKeyPressed(PadKeys[i]))
            {
                return offer[i];
            }
        }

        return openSeconds >= ClickDelaySeconds && Raylib.IsMouseButtonPressed(MouseButton.Left) ? Hovered(offer) : null;
    }

    /// <summary>Draws the window for <paramref name="player"/>'s waiting offer, with the values of <paramref name="boons"/>.</summary>
    public static void Draw(Player player, BoonBalance boons)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(boons);
        var offer = player.CurrentOffer;
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        Raylib.DrawRectangle(0, 0, width, height, Shade);

        var pending = player.PendingBoons;
        var title = pending > 1 ? $"LEVEL UP - choose a boon ({pending} left)" : "LEVEL UP - choose a boon";
        var titleWidth = Raylib.MeasureText(title, 32);
        Raylib.DrawText(title, (width - titleWidth) / 2, height / 2 - BoxHeight / 2 - 70, 32, Gold);

        var hovered = Hovered(offer);
        for (var i = 0; i < offer.Count; i++)
        {
            var box = BoxAt(i, offer.Count);
            var boon = offer[i];
            Raylib.DrawRectangleRec(box, boon == hovered ? BoxHovered : Box);
            Raylib.DrawRectangleLinesEx(box, 2f, boon == hovered ? Gold : Muted);

            var x = (int)box.X + 16;
            var y = (int)box.Y + 14;
            Raylib.DrawText($"[{i + 1}]", x, y, 20, Muted);
            Raylib.DrawText(Name(boon), x + 40, y, 20, Gold);
            Raylib.DrawText(Effect(boon, boons), x, y + 44, 18, Text);
            Raylib.DrawText(SoFar(boon, boons, player.BoonCount(boon)), x, y + 72, 16, Muted);
        }
    }

    private static string Name(Boon boon) => boon switch
    {
        Boon.StaminaRegen => "STAMINA",
        Boon.Health => "VITALITY",
        Boon.Damage => "STRENGTH",
        _ => "WISDOM",
    };

    private static string Effect(Boon boon, BoonBalance boons) => boon switch
    {
        Boon.StaminaRegen => Percent(boons.StaminaRegen, "stamina regen"),
        Boon.Health => string.Create(CultureInfo.InvariantCulture, $"+{boons.Health} max health"),
        Boon.Damage => Percent(boons.Damage, "damage"),
        _ => Percent(boons.Experience, "experience"),
    };

    /// <summary>What the player has of this boon so far, e.g. "now +10 %" - so stacking is visible.</summary>
    private static string SoFar(Boon boon, BoonBalance boons, int count) => count == 0 ? "not chosen yet" : boon switch
    {
        Boon.Health => string.Create(CultureInfo.InvariantCulture, $"now +{boons.Health * count}"),
        Boon.StaminaRegen => Total(boons.StaminaRegen, count),
        Boon.Damage => Total(boons.Damage, count),
        _ => Total(boons.Experience, count),
    };

    private static string Percent(float value, string what) =>
        string.Create(CultureInfo.InvariantCulture, $"+{value * 100f:0.#} % {what}");

    private static string Total(float value, int count) =>
        string.Create(CultureInfo.InvariantCulture, $"now +{value * count * 100f:0.#} %");

    /// <summary>The boon whose box is under the mouse cursor, or null.</summary>
    private static Boon? Hovered(IReadOnlyList<Boon> offer)
    {
        var mouse = Raylib.GetMousePosition();
        for (var i = 0; i < offer.Count; i++)
        {
            if (Raylib.CheckCollisionPointRec(mouse, BoxAt(i, offer.Count)))
            {
                return offer[i];
            }
        }

        return null;
    }

    /// <summary>Box <paramref name="index"/> of <paramref name="count"/>, side by side in the middle of the window.</summary>
    private static Rectangle BoxAt(int index, int count)
    {
        var total = count * BoxWidth + (count - 1) * Gap;
        var left = (Raylib.GetScreenWidth() - total) / 2 + index * (BoxWidth + Gap);
        var top = Raylib.GetScreenHeight() / 2 - BoxHeight / 2;
        return new Rectangle(new Vector2(left, top), new Vector2(BoxWidth, BoxHeight));
    }
}
