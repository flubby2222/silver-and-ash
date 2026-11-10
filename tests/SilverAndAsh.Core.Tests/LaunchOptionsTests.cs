namespace SilverAndAsh.Core.Tests;

public class LaunchOptionsTests
{
    [Fact]
    public void Without_options_the_test_level_is_loaded()
    {
        Assert.Equal("test", LaunchOptions.Parse([]).LevelName);
    }

    [Fact]
    public void Level_option_chooses_the_level()
    {
        Assert.Equal("heights", LaunchOptions.Parse(["--level", "heights"]).LevelName);
    }

    [Fact]
    public void Level_path_is_inside_the_level_directory()
    {
        var path = LaunchOptions.Parse(["--level", "heights"]).LevelPath(Path.Combine("assets", "levels"));

        Assert.Equal(Path.Combine("assets", "levels", "heights.txt"), path);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../secret")]
    [InlineData("C:\\Windows\\win")]
    [InlineData("Heights")]
    [InlineData("")]
    [InlineData("heights\n")]
    [InlineData("con")]
    [InlineData("nul")]
    [InlineData("com1")]
    public void Unsafe_or_odd_level_names_are_rejected(string name)
    {
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--level", name]));
    }

    [Fact]
    public void Level_option_without_a_name_is_rejected()
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--level"]));

        Assert.Contains("needs a level name", error.Message);
    }

    [Fact]
    public void Unknown_options_are_rejected()
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--levle", "heights"]));

        Assert.Contains("--levle", error.Message);
    }
}
