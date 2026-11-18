using System.Globalization;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// The words for the boons, shared by the bonus choice and the map page (issue #347): a name, what one choice gives and
/// what several choices give together. Counted from the base value, as the boons work (decision record 0004).
/// </summary>
public static class BoonTexts
{
    /// <summary>The boon's name as shown in the game, e.g. "STRENGTH".</summary>
    public static string Name(Boon boon) => boon switch
    {
        Boon.StaminaRegen => "STAMINA",
        Boon.Health => "VITALITY",
        Boon.Damage => "STRENGTH",
        Boon.Experience => "WISDOM",
        _ => throw new ArgumentOutOfRangeException(nameof(boon), boon, "Unknown boon."),
    };

    /// <summary>What the boon changes, e.g. "damage".</summary>
    public static string What(Boon boon) => boon switch
    {
        Boon.StaminaRegen => "stamina regen",
        Boon.Health => "max health",
        Boon.Damage => "damage",
        Boon.Experience => "experience",
        _ => throw new ArgumentOutOfRangeException(nameof(boon), boon, "Unknown boon."),
    };

    /// <summary>What <paramref name="count"/> choices of the boon give together, e.g. "+10 %" or "+10" (health).</summary>
    public static string Amount(Boon boon, BoonBalance boons, int count)
    {
        ArgumentNullException.ThrowIfNull(boons);
        return boon switch
        {
            Boon.Health => string.Create(CultureInfo.InvariantCulture, $"+{boons.Health * count}"),
            Boon.StaminaRegen => Percent(boons.StaminaRegen * count),
            Boon.Damage => Percent(boons.Damage * count),
            Boon.Experience => Percent(boons.Experience * count),
            _ => throw new ArgumentOutOfRangeException(nameof(boon), boon, "Unknown boon."),
        };
    }

    /// <summary>What one choice gives, e.g. "+5 % damage".</summary>
    public static string Effect(Boon boon, BoonBalance boons) => $"{Amount(boon, boons, 1)} {What(boon)}";

    private static string Percent(float value) => string.Create(CultureInfo.InvariantCulture, $"+{value * 100f:0.#} %");
}
