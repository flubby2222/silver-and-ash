using Raylib_cs;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Gorm's health bar: wide, at the top in the middle, with his name above it - shown while the boss fight is on
/// (boss room sealed, Gorm alive). Pure presentation.
/// </summary>
internal static class BossBar
{
    private const int Width = 520;
    private const int Height = 14;
    private const int Top = 64;
    private const int FontSize = 20;

    private static readonly Color Name = new(214, 204, 180, 255);
    private static readonly Color Outline = new(12, 10, 16, 200);
    private static readonly Color Track = new(40, 30, 32, 210);
    private static readonly Color Blood = new(160, 28, 36, 255);

    public static void Draw(GameWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.IsBossFightOn || world.Gorm is not { } gorm)
        {
            return;
        }

        var left = (Raylib.GetScreenWidth() - Width) / 2;
        var name = Gorm.Name;
        Raylib.DrawText(name, (Raylib.GetScreenWidth() - Raylib.MeasureText(name, FontSize)) / 2, Top - FontSize - 6, FontSize, Name);

        var fraction = Math.Clamp((float)gorm.Health / gorm.MaxHealth, 0f, 1f);
        Raylib.DrawRectangle(left - 2, Top - 2, Width + 4, Height + 4, Outline);
        Raylib.DrawRectangle(left, Top, Width, Height, Track);
        Raylib.DrawRectangle(left, Top, (int)MathF.Round(Width * fraction), Height, Blood);
    }
}
