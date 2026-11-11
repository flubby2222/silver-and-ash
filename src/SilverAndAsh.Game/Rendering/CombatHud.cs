using System.Globalization;
using Raylib_cs;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The combat part of the HUD, drawn sharp in full window resolution on top of the 3D picture:
/// crosshair, readiness of blade and pistol next to it, the blade's charge under it, and the health at the bottom left.
/// Pure presentation - every value comes from Core.
/// </summary>
internal static class CombatHud
{
    private static readonly Color Light = new(226, 228, 238, 235);
    private static readonly Color Outline = new(12, 10, 16, 180);
    private static readonly Color Track = new(40, 35, 45, 190);
    private static readonly Color Ready = new(200, 205, 220, 255);
    private static readonly Color Cooling = new(120, 124, 140, 255);
    private static readonly Color Charged = new(232, 196, 96, 255);
    private static readonly Color Blood = new(150, 24, 34, 255);
    private static readonly Color TextColor = new(200, 200, 210, 255);

    public static void Draw(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var centreX = Raylib.GetScreenWidth() / 2;
        var centreY = Raylib.GetScreenHeight() / 2;

        DrawCrosshair(centreX, centreY);

        // Blade on the left, pistol on the right - the same sides as the weapons in the hands.
        DrawReadiness(centreX - 22, centreY, player.BladeCooldown);
        DrawReadiness(centreX + 19, centreY, player.PistolCooldown);

        if (player.BladeCharge.IsCharging)
        {
            DrawCharge(centreX, centreY + 16, player.BladeCharge.Fraction);
        }

        DrawHealth(player.Health);
    }

    /// <summary>Four short lines with a gap in the middle and a dot, with a dark outline so it shows on any wall.</summary>
    private static void DrawCrosshair(int x, int y)
    {
        const int Gap = 4;
        const int Length = 6;
        const int Thickness = 2;

        foreach (var (rx, ry, w, h) in new[]
        {
            (x - Gap - Length, y - Thickness / 2, Length, Thickness),   // left
            (x + Gap, y - Thickness / 2, Length, Thickness),            // right
            (x - Thickness / 2, y - Gap - Length, Thickness, Length),   // up
            (x - Thickness / 2, y + Gap, Thickness, Length),            // down
            (x - 1, y - 1, 2, 2),                                       // dot
        })
        {
            Raylib.DrawRectangle(rx - 1, ry - 1, w + 2, h + 2, Outline);
            Raylib.DrawRectangle(rx, ry, w, h, Light);
        }
    }

    /// <summary>A small upright bar: full when the weapon is ready, refilling from the bottom while it cools down.</summary>
    private static void DrawReadiness(int x, int centreY, Cooldown cooldown)
    {
        const int Width = 3;
        const int Height = 16;
        var top = centreY - Height / 2;
        var filled = (int)MathF.Round(Height * (1f - cooldown.RemainingFraction));

        Raylib.DrawRectangle(x - 1, top - 1, Width + 2, Height + 2, Outline);
        Raylib.DrawRectangle(x, top, Width, Height, Track);
        Raylib.DrawRectangle(x, top + Height - filled, Width, filled, cooldown.IsReady ? Ready : Cooling);
    }

    /// <summary>A thin bar under the crosshair while the blade is being charged; golden when full.</summary>
    private static void DrawCharge(int centreX, int top, float fraction)
    {
        const int Width = 40;
        const int Height = 3;
        var left = centreX - Width / 2;

        Raylib.DrawRectangle(left - 1, top - 1, Width + 2, Height + 2, Outline);
        Raylib.DrawRectangle(left, top, Width, Height, Track);
        Raylib.DrawRectangle(left, top, (int)MathF.Round(Width * fraction), Height, fraction >= 1f ? Charged : Ready);
    }

    /// <summary>Health at the bottom left, above the frame counter: a blood-red bar with the number next to it.</summary>
    private static void DrawHealth(int health)
    {
        const int Width = 180;
        const int Height = 10;
        var top = Raylib.GetScreenHeight() - 56;
        var fraction = Math.Clamp((float)health / Tuning.Combat.PlayerMaxHealth, 0f, 1f);

        Raylib.DrawText("HEALTH", 20, top - 20, 16, TextColor);
        Raylib.DrawRectangle(20, top, Width, Height, Track);
        Raylib.DrawRectangle(20, top, (int)MathF.Round(Width * fraction), Height, Blood);
        Raylib.DrawText(health.ToString(CultureInfo.InvariantCulture), 20 + Width + 10, top - 3, 16, TextColor);
    }
}
