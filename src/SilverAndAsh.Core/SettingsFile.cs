using System.Globalization;
using System.Text.Json;

namespace SilverAndAsh.Core;

/// <summary>
/// Reads and writes <see cref="GameSettings"/> as a small JSON file, e.g.
/// <c>%LOCALAPPDATA%\SilverAndAsh\settings.json</c>. Like the balancing file it never stops the game: a missing or
/// broken file gives the standard settings, values outside their range are brought back into it.
/// </summary>
public static class SettingsFile
{
    /// <summary>The settings in the file at <paramref name="path"/>, or the standard settings if there are none.</summary>
    public static GameSettings Load(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return GameSettings.Default;
            }

            // A wrong or unknown entry is skipped on its own - it does not reset the other settings.
            var settings = GameSettings.Default;
            foreach (var entry in document.RootElement.EnumerateObject())
            {
                if (string.Equals(entry.Name, "mouseSensitivity", StringComparison.OrdinalIgnoreCase)
                    && entry.Value.ValueKind == JsonValueKind.Number)
                {
                    settings = settings with { MouseSensitivity = entry.Value.GetSingle() };
                }
            }

            return settings.InRange();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return GameSettings.Default;
        }
    }

    /// <summary>
    /// Writes <paramref name="settings"/> to <paramref name="path"/> (creating the folder if needed).
    /// False if that was not possible - the game goes on, the settings just are not kept.
    /// </summary>
    public static bool Save(GameSettings settings, string path)
    {
        ArgumentNullException.ThrowIfNull(settings);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var value = settings.MouseSensitivity.ToString("0.00", CultureInfo.InvariantCulture);
            File.WriteAllText(path, $"{{\n  \"mouseSensitivity\": {value}\n}}\n");
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
