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
        Assert.Equal(GameSettings.MaxMouseSensitivity, (GameSettings.Default with { MouseSensitivity = 2.9f }).Changed(+5).MouseSensitivity);
        Assert.Equal(GameSettings.MinMouseSensitivity, GameSettings.Default.Changed(-50).MouseSensitivity);
        Assert.Equal(1.1f, GameSettings.Default.Changed(+1).MouseSensitivity, 0.0001f);
        Assert.Equal(0.9f, GameSettings.Default.Changed(-1).MouseSensitivity, 0.0001f);
    }

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
