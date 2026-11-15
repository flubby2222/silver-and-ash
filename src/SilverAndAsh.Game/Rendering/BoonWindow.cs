using System.Globalization;
using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The window after a level up: the picture darkens, three boxes offer the bonuses. Choose with 1 / 2 / 3 or a click.
/// It only reads and shows - the choice goes to Core as input (<see cref="Core.Input.PlayerInput.ChooseBoon"/>).
/// </summary>
internal static class BoonWindow
{
    private const int BoxWidth = 220;
    private const int BoxHeight = 120;
    private const int Gap = 24;

    private static readonly Color Shade = new(10, 6, 12, 170);
    private static readonly Color Box = new(32, 26, 36, 235);
    private static readonly Color BoxHovered = new(58, 44, 52, 245);
    private static readonly Color Gold = new(232, 196, 96, 255);
    private static readonly Color Text = new(220, 220, 230, 255);
    private static readonly Color Muted = new(150, 150, 165, 255);

    private static readonly Boon[] Boons = [Boon.Vitality, Boon.Strength, Boon.Endurance];

    /// <summary>
    /// Seconds after the window opens before a click counts. The cursor appears in the middle - right over a box -
    /// and attack clicks just after the killing blow must not choose a bonus by accident (found while testing).
    /// </summary>
    public const float ClickDelaySeconds = 0.6f;

    /// <summary>
    /// The boon chosen in this frame (key 1/2/3, or a click on a box once the window has been open for
    /// <see cref="ClickDelaySeconds"/> - <paramref name="openSeconds"/>), or null.
    /// </summary>
    public static Boon? Pick(float openSeconds)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.One) || Raylib.IsKeyPressed(KeyboardKey.Kp1))
        {
            return Boon.Vitality;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Two) || Raylib.IsKeyPressed(KeyboardKey.Kp2))
        {
            return Boon.Strength;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Three) || Raylib.IsKeyPressed(KeyboardKey.Kp3))
        {
            return Boon.Endurance;
        }

        return openSeconds >= ClickDelaySeconds && Raylib.IsMouseButtonPressed(MouseButton.Left) ? Hovered() : null;
    }

    /// <summary>Draws the window for <paramref name="pending"/> waiting choices with the values of <paramref name="boons"/>.</summary>
    public static void Draw(int pending, BoonBalance boons)
    {
        ArgumentNullException.ThrowIfNull(boons);
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        Raylib.DrawRectangle(0, 0, width, height, Shade);

        var title = pending > 1 ? $"LEVEL UP - choose a boon ({pending} left)" : "LEVEL UP - choose a boon";
        var titleWidth = Raylib.MeasureText(title, 32);
        Raylib.DrawText(title, (width - titleWidth) / 2, height / 2 - BoxHeight / 2 - 70, 32, Gold);

        var hovered = Hovered();
        for (var i = 0; i < Boons.Length; i++)
        {
            var box = BoxAt(i);
            var boon = Boons[i];
            Raylib.DrawRectangleRec(box, boon == hovered ? BoxHovered : Box);
            Raylib.DrawRectangleLinesEx(box, 2f, boon == hovered ? Gold : Muted);

            var x = (int)box.X + 16;
            var y = (int)box.Y + 14;
            Raylib.DrawText($"[{i + 1}]", x, y, 20, Muted);
            Raylib.DrawText(Name(boon), x + 40, y, 20, Gold);
            Raylib.DrawText(Effect(boon, boons), x, y + 44, 18, Text);
        }
    }

    private static string Name(Boon boon) => boon switch
    {
        Boon.Vitality => "VITALITY",
        Boon.Strength => "STRENGTH",
        _ => "ENDURANCE",
    };

    private static string Effect(Boon boon, BoonBalance boons) => boon switch
    {
        Boon.Vitality => string.Create(CultureInfo.InvariantCulture, $"+{boons.VitalityHealth} max health"),
        Boon.Strength => string.Create(CultureInfo.InvariantCulture, $"+{boons.StrengthDamage * 100f:0} % damage"),
        _ => string.Create(CultureInfo.InvariantCulture, $"+{boons.EnduranceSegments} stamina segment"),
    };

    /// <summary>The box under the mouse cursor, or null.</summary>
    private static Boon? Hovered()
    {
        var mouse = Raylib.GetMousePosition();
        for (var i = 0; i < Boons.Length; i++)
        {
            if (Raylib.CheckCollisionPointRec(mouse, BoxAt(i)))
            {
                return Boons[i];
            }
        }

        return null;
    }

    /// <summary>Box <paramref name="index"/> of three, side by side in the middle of the window.</summary>
    private static Rectangle BoxAt(int index)
    {
        var total = Boons.Length * BoxWidth + (Boons.Length - 1) * Gap;
        var left = (Raylib.GetScreenWidth() - total) / 2 + index * (BoxWidth + Gap);
        var top = Raylib.GetScreenHeight() / 2 - BoxHeight / 2;
        return new Rectangle(new Vector2(left, top), new Vector2(BoxWidth, BoxHeight));
    }
}
