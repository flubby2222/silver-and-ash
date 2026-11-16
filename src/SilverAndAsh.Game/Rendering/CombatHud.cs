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
    private static readonly Color Consecrated = new(220, 235, 255, 255);
    private static readonly Color SilverCoin = new(196, 202, 214, 255);
    private static readonly Color SilverRim = new(110, 114, 128, 255);

    public static void Draw(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var centreX = Raylib.GetScreenWidth() / 2;
        var centreY = Raylib.GetScreenHeight() / 2;

        // Dead: no aiming any more - only the (empty) health stays.
        if (!player.IsDead)
        {
            DrawCrosshair(centreX, centreY);

            // Blade on the left, pistol on the right - the same sides as the weapons in the hands.
            DrawReadiness(centreX - 22, centreY, player.MeleeCooldown);
            DrawReadiness(centreX + 19, centreY, player.Ranged.Cooldown);
            DrawAmmo(player.Ranged.Magazine);
            DrawWeapons(player);
        }

        if (player.MeleeCharge.IsCharging)
        {
            DrawCharge(centreX, centreY + 16, player.MeleeCharge.Fraction);
        }

        DrawHealth(player.Health, player.MaxHealth);
        DrawLevel(player.Level, player.Experience, player.ExperienceToNextLevel);
        DrawSilver(player.Silver);
        DrawKeys(player.Keys);

        if (player.IsConsecrated)
        {
            DrawConsecrated(player.ConsecratedSecondsLeft);
        }
    }

    /// <summary>The level and a thin golden bar of the experience towards the next one, above the health.</summary>
    private static void DrawLevel(int level, int experience, int toNext)
    {
        const int Width = 180;
        const int Height = 4;
        var top = Raylib.GetScreenHeight() - 136;
        var fraction = toNext > 0 ? Math.Clamp((float)experience / toNext, 0f, 1f) : 0f;

        Raylib.DrawText(string.Create(CultureInfo.InvariantCulture, $"LV {level}"), 20, top, 16, Charged);
        var barTop = top + 20;
        Raylib.DrawRectangle(19, barTop - 1, Width + 2, Height + 2, Outline);
        Raylib.DrawRectangle(20, barTop, Width, Height, Track);
        Raylib.DrawRectangle(20, barTop, (int)MathF.Round(Width * fraction), Height, Charged);
    }

    /// <summary>The silver of this run at the top right: a small coin and "SILVER 42".</summary>
    private static void DrawSilver(int silver)
    {
        const int FontSize = 20;
        const int Top = 20;
        var text = string.Create(CultureInfo.InvariantCulture, $"SILVER {silver}");
        var right = Raylib.GetScreenWidth() - 20;
        var left = right - Raylib.MeasureText(text, FontSize);
        Raylib.DrawText(text, left, Top, FontSize, SilverCoin);

        var coinX = left - 14;
        var coinY = Top + FontSize / 2;
        Raylib.DrawCircle(coinX, coinY, 8f, SilverRim);
        Raylib.DrawCircle(coinX, coinY, 6f, SilverCoin);
        Raylib.DrawCircle(coinX - 2, coinY - 2, 2f, Light);
    }

    /// <summary>The keys in hand under the silver at the top right: "KEYS 2", golden.</summary>
    private static void DrawKeys(int keys)
    {
        const int FontSize = 20;
        var text = string.Create(CultureInfo.InvariantCulture, $"KEYS {keys}");
        var left = Raylib.GetScreenWidth() - 20 - Raylib.MeasureText(text, FontSize);
        Raylib.DrawText(text, left, 48, FontSize, keys > 0 ? Charged : Cooling);
    }

    /// <summary>Consecrated ammunition: its time left, above the health - pale and holy, blinking in the last 3 s.</summary>
    private static void DrawConsecrated(float secondsLeft)
    {
        var blinkOff = secondsLeft < 3f && (int)(secondsLeft * 4f) % 2 == 1;
        if (blinkOff)
        {
            return;
        }

        var text = string.Create(CultureInfo.InvariantCulture, $"CONSECRATED {MathF.Ceiling(secondsLeft):0}s");
        Raylib.DrawText(text, 20, Raylib.GetScreenHeight() - 104, 16, Consecrated);
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

    /// <summary>
    /// The weapons carried, bottom right above the rounds: the melee weapon ([LMB]) and the three ranged places
    /// ([1] [2] [3]), each framed in its tier's colour; the ranged weapon in the hand is lit up, empty places are dim.
    /// </summary>
    private static void DrawWeapons(Player player)
    {
        const int Width = 96;
        const int Height = 30;
        const int Gap = 6;
        var seconds = (float)Raylib.GetTime();
        var right = Raylib.GetScreenWidth() - 20;
        var top = Raylib.GetScreenHeight() - 122;
        var left = right - (4 * Width) - (3 * Gap);

        DrawWeaponBox(left, top, Width, Height, "LMB", Weapons.Info(player.Melee), selected: true, seconds);
        for (var slot = 0; slot < Player.RangedSlotCount; slot++)
        {
            var x = left + ((slot + 1) * (Width + Gap));
            var key = string.Create(CultureInfo.InvariantCulture, $"{slot + 1}");
            DrawWeaponBox(x, top, Width, Height, key, player.RangedSlots[slot]?.Info, slot == player.SelectedSlot, seconds);
        }
    }

    private static void DrawWeaponBox(int x, int y, int width, int height, string key, WeaponInfo? weapon, bool selected, float seconds)
    {
        Raylib.DrawRectangle(x, y, width, height, selected ? new Color(40, 34, 40, 220) : new Color(20, 16, 22, 160));
        if (weapon is not { } info)
        {
            var empty = new Color(90, 88, 98, 255);
            Raylib.DrawRectangleLines(x, y, width, height, empty);
            Raylib.DrawText(key, x + 4, y + 3, 10, empty);
            Raylib.DrawText("-", x + 4, y + 15, 10, empty);
            return;
        }

        var frame = TierColours.Of(info.Tier, seconds);
        Raylib.DrawRectangleLinesEx(new Rectangle(x, y, width, height), selected ? 2f : 1f, frame);
        Raylib.DrawText(key, x + 4, y + 3, 10, selected ? TextColor : Track);
        Raylib.DrawText(info.Name, x + 4, y + 15, 10, selected ? TextColor : new Color(150, 150, 160, 255));
    }

    /// <summary>
    /// The pistol's rounds at the bottom right, above the version: "5 / 6"; red "0 / 6 [R]" when empty; while
    /// reloading "Reloading..." with a bar filling up underneath.
    /// </summary>
    private static void DrawAmmo(Magazine magazine)
    {
        const int Size = 24;
        const int BarWidth = 120;
        const int BarHeight = 4;
        var right = Raylib.GetScreenWidth() - 20;
        var top = Raylib.GetScreenHeight() - 78;

        var text = magazine.IsReloading ? "Reloading..."
            : magazine.Rounds == 0 ? string.Create(CultureInfo.InvariantCulture, $"0 / {magazine.Size} [R]")
            : string.Create(CultureInfo.InvariantCulture, $"{magazine.Rounds} / {magazine.Size}");
        var color = magazine.Rounds == 0 && !magazine.IsReloading ? Blood : TextColor;
        Raylib.DrawText(text, right - Raylib.MeasureText(text, Size), top, Size, color);

        if (magazine.IsReloading)
        {
            var left = right - BarWidth;
            var barTop = top + Size + 6;
            Raylib.DrawRectangle(left - 1, barTop - 1, BarWidth + 2, BarHeight + 2, Outline);
            Raylib.DrawRectangle(left, barTop, BarWidth, BarHeight, Track);
            Raylib.DrawRectangle(left, barTop, (int)MathF.Round(BarWidth * magazine.ReloadProgress), BarHeight, TextColor);
        }
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
    private static void DrawHealth(int health, int maxHealth)
    {
        const int Width = 180;
        const int Height = 10;
        var top = Raylib.GetScreenHeight() - 56;
        var fraction = Math.Clamp((float)health / maxHealth, 0f, 1f);

        Raylib.DrawText("HEALTH", 20, top - 20, 16, TextColor);
        Raylib.DrawRectangle(20, top, Width, Height, Track);
        Raylib.DrawRectangle(20, top, (int)MathF.Round(Width * fraction), Height, Blood);
        Raylib.DrawText(health.ToString(CultureInfo.InvariantCulture), 20 + Width + 10, top - 3, 16, TextColor);
    }
}
