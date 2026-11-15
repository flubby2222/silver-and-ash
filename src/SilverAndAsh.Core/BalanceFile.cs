using System.Globalization;
using System.Reflection;
using System.Text.Json;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core;

/// <summary>The balancing values read from a file, and everything that was wrong with the file.</summary>
public sealed record BalanceLoadResult(Balance Balance, IReadOnlyList<string> Warnings);

/// <summary>
/// Reads the balancing file (JSON, e.g. <c>assets/balance.json</c>) into a <see cref="Balance"/>.
/// <para>
/// It never stops the game: a missing or broken file gives the standard values, a missing value its standard value,
/// a misspelled name or a value that would break the game (negative health, a jump higher than the room ...) its
/// standard value - and every such case becomes a warning for the log.
/// </para>
/// </summary>
public static class BalanceFile
{
    /// <summary>Names as in JavaScript ("maxHealth"), comments and a comma after the last entry allowed.</summary>
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Values that may be 0 (a bonus of nothing, no damage, a free move ...). All others must be above 0.</summary>
    private static readonly HashSet<string> ZeroAllowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "player.airControl", "player.perfectJumpSpeedBonus", "player.exhaustedJumpSpeedPenalty",
        "player.landingDipPerSpeed", "player.landingDipMax", "player.landingTiltPerMetre", "player.dodgeStaminaSegments",
        "blade.damage", "blade.chargeStaminaSegments", "blade.coneStartBehind",
        "pistol.damage", "ghoul.attackDamage", "bat.biteDamage", "bat.spitAimBelowEyes", "bloodBolt.damage",
        "drops.ghoulChance", "drops.batChance", "drops.consecratedAmmoShare", "bloodVial.heal",
        "silver.ghoulMin", "silver.ghoulMax", "silver.batMin", "silver.batMax",
        "experience.ghoul", "experience.bat",
        "boons.staminaRegen", "boons.health", "boons.damage", "boons.experience",
        "layout.corridorChance", "layout.maxLoops",
        "difficulty.ghoulShareNear", "difficulty.ghoulShareFar", "difficulty.batShareNear", "difficulty.batShareFar",
    };

    /// <summary>Highest sensible values. A jump may at most bring the head up to the ceiling.</summary>
    private static readonly Dictionary<string, double> Maximum = new(StringComparer.OrdinalIgnoreCase)
    {
        ["player.airControl"] = 1,
        ["player.jumpHeight"] = Cell.DefaultCeilingHeight - BodySizes.Player.Height,
        ["blade.angle"] = 180,
        ["ghoul.attackHalfAngle"] = 180,
        ["drops.ghoulChance"] = 1,
        ["drops.batChance"] = 1,
        ["drops.consecratedAmmoShare"] = 1,
        ["layout.corridorChance"] = 1,
        ["difficulty.ghoulShareNear"] = 1,
        ["difficulty.ghoulShareFar"] = 1,
        ["difficulty.batShareNear"] = 1,
        ["difficulty.batShareFar"] = 1,
    };

    /// <summary>Values that must stay below a limit (a penalty of 100 % would mean standing still).</summary>
    private static readonly Dictionary<string, double> Below = new(StringComparer.OrdinalIgnoreCase)
    {
        ["player.exhaustedJumpSpeedPenalty"] = 1,
    };

    /// <summary>Pairs where the first may not be above the second.</summary>
    private static readonly (string Section, string Min, string Max)[] Ranges =
    [
        ("bat", "circleRadiusMin", "circleRadiusMax"),
        ("bat", "turnRoundMin", "turnRoundMax"),
        ("bat", "circleHeightMin", "circleHeightMax"),
        ("layout", "combatRoomsMin", "combatRoomsMax"),
        ("silver", "ghoulMin", "ghoulMax"),
        ("silver", "batMin", "batMax"),
    ];

    /// <summary>Reads the file at <paramref name="path"/>. A missing or unreadable file gives the standard values.</summary>
    public static BalanceLoadResult Load(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(Balance.Default, [$"Balancing file '{path}' could not be read ({exception.Message}) - using the standard values."]);
        }

        return Parse(text);
    }

    /// <summary>Reads balancing values from the JSON text <paramref name="json"/>.</summary>
    public static BalanceLoadResult Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var warnings = new List<string>();
        Balance? balance;
        try
        {
            using var document = JsonDocument.Parse(json, DocumentOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new(Balance.Default, ["The balancing file must contain one { ... } block - using the standard values."]);
            }

            ReportUnknownNames(document.RootElement, typeof(Balance), string.Empty, warnings);
            balance = document.RootElement.Deserialize<Balance>(Options);
        }
        catch (JsonException exception)
        {
            return new(Balance.Default, [$"The balancing file is broken ({exception.Message}) - using the standard values."]);
        }

        // The deserializer created fresh objects, so fixing values on them never touches Balance.Default.
        balance ??= new Balance();
        CheckValues(balance, new Balance(), string.Empty, warnings);
        CheckRanges(balance, warnings);
        return new(balance, warnings);
    }

    /// <summary>Warns about every name in the file that is not a value of <paramref name="type"/> (typos, derived values).</summary>
    private static void ReportUnknownNames(JsonElement element, Type type, string prefix, List<string> warnings)
    {
        foreach (var entry in element.EnumerateObject())
        {
            var property = SettableProperties(type)
                .FirstOrDefault(p => string.Equals(JsonName(p), entry.Name, StringComparison.OrdinalIgnoreCase));
            var path = prefix + entry.Name;
            if (property is null)
            {
                warnings.Add($"Unknown name '{path}' in the balancing file - ignored (a typo, or a value that cannot be set).");
            }
            else if (IsSection(property.PropertyType) && entry.Value.ValueKind == JsonValueKind.Object)
            {
                ReportUnknownNames(entry.Value, property.PropertyType, path + ".", warnings);
            }
        }
    }

    /// <summary>Puts every value that would break the game back to its standard (taken from <paramref name="defaults"/>).</summary>
    private static void CheckValues(object values, object defaults, string prefix, List<string> warnings)
    {
        foreach (var property in SettableProperties(values.GetType()))
        {
            var path = prefix + JsonName(property);
            var standard = property.GetValue(defaults);
            if (IsSection(property.PropertyType))
            {
                var section = property.GetValue(values);
                if (section is null)
                {
                    warnings.Add($"'{path}' is empty in the balancing file - using its standard values.");
                    property.SetValue(values, standard);
                }
                else
                {
                    CheckValues(section, standard!, path + ".", warnings);
                }

                continue;
            }

            var value = Convert.ToDouble(property.GetValue(values), CultureInfo.InvariantCulture);
            var problem = Problem(path, value);
            if (problem is not null)
            {
                warnings.Add($"'{path}' = {value.ToString(CultureInfo.InvariantCulture)} {problem} - using the standard {Convert.ToString(standard, CultureInfo.InvariantCulture)}.");
                property.SetValue(values, standard);
            }
        }
    }

    /// <summary>What is wrong with <paramref name="value"/>, or null if nothing.</summary>
    private static string? Problem(string path, double value)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            return "is negative or not a number";
        }

        if (value == 0 && !ZeroAllowed.Contains(path))
        {
            return "must be above 0";
        }

        if (Maximum.TryGetValue(path, out var maximum) && value > maximum)
        {
            return $"is above its limit of {maximum.ToString(CultureInfo.InvariantCulture)}";
        }

        if (Below.TryGetValue(path, out var limit) && value >= limit)
        {
            return $"must stay below {limit.ToString(CultureInfo.InvariantCulture)}";
        }

        return null;
    }

    /// <summary>A minimum above its maximum: both go back to their standards.</summary>
    private static void CheckRanges(Balance balance, List<string> warnings)
    {
        foreach (var (sectionName, minName, maxName) in Ranges)
        {
            var sectionProperty = SettableProperties(typeof(Balance)).First(p => JsonName(p) == sectionName);
            var section = sectionProperty.GetValue(balance)!;
            var standard = sectionProperty.GetValue(Balance.Default)!;
            var min = SettableProperties(section.GetType()).First(p => JsonName(p) == minName);
            var max = SettableProperties(section.GetType()).First(p => JsonName(p) == maxName);
            if (Convert.ToDouble(min.GetValue(section), CultureInfo.InvariantCulture) > Convert.ToDouble(max.GetValue(section), CultureInfo.InvariantCulture))
            {
                warnings.Add($"'{sectionName}.{minName}' is above '{sectionName}.{maxName}' - using the standards for both.");
                min.SetValue(section, min.GetValue(standard));
                max.SetValue(section, max.GetValue(standard));
            }
        }
    }

    /// <summary>The values that can be set (derived ones like gravity cannot).</summary>
    private static IEnumerable<PropertyInfo> SettableProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.SetMethod is { IsPublic: true });

    /// <summary>One of the sections of <see cref="Balance"/> (player, blade, ghoul ...).</summary>
    private static bool IsSection(Type type) => type.IsClass && type.Namespace == typeof(Balance).Namespace;

    private static string JsonName(PropertyInfo property) => JsonNamingPolicy.CamelCase.ConvertName(property.Name);
}
