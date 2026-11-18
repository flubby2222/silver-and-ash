namespace SilverAndAsh.Core.Tests;

public sealed class SettingsFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"settings-{Guid.NewGuid():N}");

    private string FilePath => Path.Combine(_folder, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Without_a_file_the_standard_settings_are_used()
    {
        Assert.Equal(GameSettings.Default, SettingsFile.Load(FilePath));
        Assert.Equal(1f, GameSettings.Default.MouseSensitivity);
    }

    [Fact]
    public void Saved_settings_are_read_back()
    {
        SettingsFile.Save(new GameSettings { MouseSensitivity = 1.7f }, FilePath);

        Assert.Equal(1.7f, SettingsFile.Load(FilePath).MouseSensitivity, 0.0001f);
    }

    [Theory]
    [InlineData("this is no json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("""{ "mouseSensitivity": "fast" }""")]
    [InlineData("")]
    public void A_broken_file_gives_the_standard_settings(string text)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, text);

        Assert.Equal(GameSettings.Default, SettingsFile.Load(FilePath));
    }

    [Theory]
    [InlineData(50f, GameSettings.MaxMouseSensitivity)]
    [InlineData(0f, GameSettings.MinMouseSensitivity)]
    [InlineData(-3f, GameSettings.MinMouseSensitivity)]
    public void Values_outside_the_range_are_brought_back_into_it(float written, float expected)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, $$"""{ "mouseSensitivity": {{written.ToString(System.Globalization.CultureInfo.InvariantCulture)}} }""");

        Assert.Equal(expected, SettingsFile.Load(FilePath).MouseSensitivity);
    }

    [Fact]
    public void Unknown_entries_are_ignored()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, """{ "mouseSensitivity": 0.8, "somethingElse": true }""");

        Assert.Equal(0.8f, SettingsFile.Load(FilePath).MouseSensitivity, 0.0001f);
    }

    [Fact]
    public void Changing_the_sensitivity_stays_within_the_range()
    {
        Assert.Equal(GameSettings.MaxMouseSensitivity, (GameSettings.Default with { MouseSensitivity = 2.9f }).ChangedSensitivity(+5).MouseSensitivity);
        Assert.Equal(GameSettings.MinMouseSensitivity, GameSettings.Default.ChangedSensitivity(-50).MouseSensitivity);
        Assert.Equal(1.1f, GameSettings.Default.ChangedSensitivity(+1).MouseSensitivity, 0.0001f);
        Assert.Equal(0.9f, GameSettings.Default.ChangedSensitivity(-1).MouseSensitivity, 0.0001f);
    }

    [Fact]
    public void The_standard_is_full_volume_in_a_window()
    {
        Assert.Equal(1f, GameSettings.Default.MasterVolume);
        Assert.Equal(1f, GameSettings.Default.EffectsVolume);
        Assert.Equal(1f, GameSettings.Default.AmbienceVolume);
        Assert.False(GameSettings.Default.FullScreen);
    }

    [Fact]
    public void Every_setting_is_read_back()
    {
        // Every value away from the standard, so a value that is not written (or not read) shows.
        var settings = new GameSettings
        {
            MouseSensitivity = 1.7f,
            MasterVolume = 0.3f,
            EffectsVolume = 0.6f,
            AmbienceVolume = 0f,
            FullScreen = true,
        };

        Assert.True(SettingsFile.Save(settings, FilePath));

        Assert.Equal(settings, SettingsFile.Load(FilePath));
    }

    [Fact]
    public void Saving_what_was_read_gives_the_same_file()
    {
        SettingsFile.Save(GameSettings.Default.ChangedMasterVolume(-3).ChangedEffectsVolume(-1) with { FullScreen = true }, FilePath);
        var first = File.ReadAllText(FilePath);

        SettingsFile.Save(SettingsFile.Load(FilePath), FilePath);

        Assert.Equal(first, File.ReadAllText(FilePath));
        Assert.Contains("\"masterVolume\": 0.7", first, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_from_before_the_volumes_keeps_its_sensitivity_and_gets_the_standard_for_the_rest()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, "{\n  \"mouseSensitivity\": 0.80\n}\n");

        Assert.Equal(GameSettings.Default with { MouseSensitivity = 0.8f }, SettingsFile.Load(FilePath));
    }

    [Fact]
    public void A_wrong_entry_is_skipped_on_its_own()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(
            FilePath,
            """{ "masterVolume": "loud", "effectsVolume": 0.4, "ambienceVolume": null, "fullScreen": "yes", "mouseSensitivity": 2 }""");

        Assert.Equal(
            GameSettings.Default with { EffectsVolume = 0.4f, MouseSensitivity = 2f },
            SettingsFile.Load(FilePath));
    }

    [Theory]
    [InlineData("masterVolume")]
    [InlineData("effectsVolume")]
    [InlineData("ambienceVolume")]
    public void Volumes_outside_0_to_1_are_brought_back_into_it(string name)
    {
        Directory.CreateDirectory(_folder);

        File.WriteAllText(FilePath, $$"""{ "{{name}}": 5 }""");
        Assert.Equal(GameSettings.Default, SettingsFile.Load(FilePath));

        File.WriteAllText(FilePath, $$"""{ "{{name}}": -2 }""");
        var silent = SettingsFile.Load(FilePath);
        Assert.Equal(0f, Volume(silent, name));

        File.WriteAllText(FilePath, $$"""{ "{{name}}": 0.5 }""");
        Assert.Equal(0.5f, Volume(SettingsFile.Load(FilePath), name));
    }

    [Fact]
    public void Full_screen_is_read_from_the_file()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, """{ "fullScreen": true }""");

        Assert.Equal(GameSettings.Default with { FullScreen = true }, SettingsFile.Load(FilePath));
    }

    [Fact]
    public void Changing_a_volume_stays_between_silent_and_full()
    {
        var full = GameSettings.Default;

        Assert.Equal(1f, full.ChangedMasterVolume(+1).MasterVolume);
        Assert.Equal(0.9f, full.ChangedMasterVolume(-1).MasterVolume);
        Assert.Equal(0f, full.ChangedMasterVolume(-50).MasterVolume);
        Assert.Equal(0.1f, full.ChangedMasterVolume(-50).ChangedMasterVolume(+1).MasterVolume);

        Assert.Equal(1f, full.ChangedEffectsVolume(+1).EffectsVolume);
        Assert.Equal(0.8f, full.ChangedEffectsVolume(-2).EffectsVolume);
        Assert.Equal(0f, full.ChangedEffectsVolume(-11).EffectsVolume);

        Assert.Equal(1f, full.ChangedAmbienceVolume(+3).AmbienceVolume);
        Assert.Equal(0.7f, full.ChangedAmbienceVolume(-3).AmbienceVolume);
        Assert.Equal(0f, full.ChangedAmbienceVolume(-10).AmbienceVolume);
    }

    [Fact]
    public void Changing_one_value_leaves_the_others_alone()
    {
        var start = new GameSettings { MouseSensitivity = 1.5f, MasterVolume = 0.5f, EffectsVolume = 0.4f, AmbienceVolume = 0.3f, FullScreen = true };

        Assert.Equal(start with { MouseSensitivity = 1.6f }, start.ChangedSensitivity(+1));
        Assert.Equal(start with { MasterVolume = 0.6f }, start.ChangedMasterVolume(+1));
        Assert.Equal(start with { EffectsVolume = 0.5f }, start.ChangedEffectsVolume(+1));
        Assert.Equal(start with { AmbienceVolume = 0.4f }, start.ChangedAmbienceVolume(+1));
    }

    private static float Volume(GameSettings settings, string name) => name switch
    {
        "masterVolume" => settings.MasterVolume,
        "effectsVolume" => settings.EffectsVolume,
        _ => settings.AmbienceVolume,
    };

    [Fact]
    public void Saving_into_a_folder_that_cannot_be_written_does_not_crash()
    {
        // A file where the folder should be: the folder cannot be created.
        Directory.CreateDirectory(Path.GetDirectoryName(_folder + "-x")!);
        File.WriteAllText(_folder + "-blocker", "x");
        try
        {
            Assert.False(SettingsFile.Save(GameSettings.Default, Path.Combine(_folder + "-blocker", "settings.json")));
        }
        finally
        {
            File.Delete(_folder + "-blocker");
        }
    }
}
