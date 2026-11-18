using System.Text;
using System.Text.Json;

namespace SilverAndAsh.Core;

/// <summary>
/// Reads and writes <see cref="GameSettings"/> as a small JSON file, e.g.
/// <c>%LOCALAPPDATA%\SilverAndAsh\settings.json</c>. Like the balancing file it never stops the game: a missing or
/// broken file gives the standard settings, values outside their range are brought back into it.
/// </summary>
public static class SettingsFile
{
    private const string MouseSensitivityName = "mouseSensitivity";
    private const string MasterVolumeName = "masterVolume";
    private const string EffectsVolumeName = "effectsVolume";
    private const string AmbienceVolumeName = "ambienceVolume";
    private const string FullScreenName = "fullScreen";

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
                settings = Read(settings, entry);
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
            File.WriteAllText(path, ToJson(settings));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static GameSettings Read(GameSettings settings, JsonProperty entry)
    {
        var value = entry.Value;
        bool Is(string name) => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase);

        if (value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out var number))
        {
            return Is(MouseSensitivityName) ? settings with { MouseSensitivity = number }
                : Is(MasterVolumeName) ? settings with { MasterVolume = number }
                : Is(EffectsVolumeName) ? settings with { EffectsVolume = number }
                : Is(AmbienceVolumeName) ? settings with { AmbienceVolume = number }
                : settings;
        }

        return value.ValueKind is JsonValueKind.True or JsonValueKind.False && Is(FullScreenName)
            ? settings with { FullScreen = value.GetBoolean() }
            : settings;
    }

    private static string ToJson(GameSettings settings)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber(MouseSensitivityName, Math.Round((decimal)settings.MouseSensitivity, 2));
            writer.WriteNumber(MasterVolumeName, Math.Round((decimal)settings.MasterVolume, 2));
            writer.WriteNumber(EffectsVolumeName, Math.Round((decimal)settings.EffectsVolume, 2));
            writer.WriteNumber(AmbienceVolumeName, Math.Round((decimal)settings.AmbienceVolume, 2));
            writer.WriteBoolean(FullScreenName, settings.FullScreen);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }
}
