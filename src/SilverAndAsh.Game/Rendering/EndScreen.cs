using System.Globalization;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// "YOU DIED" or "VICTORY" with the numbers of the run, and the choice: a new run or back to the title.
/// The world behind it stands still.
/// </summary>
internal static class EndScreen
{
    public const int NewRun = 0;
    public const int Title = 1;

    private const int LineHeight = 32;
    private const int FontSize = 24;

    private static readonly string[] Items = ["New run", "Title"];
    private static readonly Color Shade = new(0, 0, 0, 190);
    private static readonly Color Died = new(190, 36, 44, 255);
    private static readonly Color Victory = new(230, 190, 90, 255);
    private static readonly Color Label = new(160, 156, 166, 255);
    private static readonly Color Value = new(225, 222, 230, 255);

    private static int Heading => Raylib.GetScreenHeight() / 8;

    private static int StatsTop => Heading + 100;

    private static int MenuTop(RunSummary summary) => StatsTop + Lines(summary).Count * LineHeight + 30;

    /// <summary>The chosen button this frame (<see cref="NewRun"/>, <see cref="Title"/>) or -1.</summary>
    public static int Update(MenuView menu, RunSummary summary) => menu.Update(Items, MenuTop(summary), out _);

    public static void Draw(MenuView menu, RunSummary summary)
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Shade);
        var won = summary.Outcome == RunOutcome.Won;
        MenuView.DrawTitle(won ? "VICTORY" : "YOU DIED", Heading, 72, won ? Victory : Died);

        var centre = Raylib.GetScreenWidth() / 2;
        var y = StatsTop;
        foreach (var (label, value) in Lines(summary))
        {
            Raylib.DrawText(label, centre - 200, y, FontSize, Label);
            Raylib.DrawText(value, centre + 200 - Raylib.MeasureText(value, FontSize), y, FontSize, Value);
            y += LineHeight;
        }

        menu.Draw(Items, MenuTop(summary));
    }

    private static List<(string Label, string Value)> Lines(RunSummary summary)
    {
        static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        var lines = new List<(string, string)>
        {
            ("Time", summary.Time),
            ("Ghouls slain", Number(summary.GhoulsKilled)),
            ("Bats slain", Number(summary.BatsKilled)),
            ("Damage dealt", Number(summary.DamageDealt)),
            ("Damage taken", Number(summary.DamageTaken)),
            ("Level reached", Number(summary.Level)),
            ("Silver collected", Number(summary.Silver)),
        };
        if (summary.ArenaRooms > 0)
        {
            lines.Add(("Rooms cleared", $"{Number(summary.RoomsCleared)}/{Number(summary.ArenaRooms)}"));
        }

        lines.Add(("Seed", Number(summary.Seed)));
        return lines;
    }
}
