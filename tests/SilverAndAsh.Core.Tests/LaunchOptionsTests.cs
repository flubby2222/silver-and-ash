using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Tests;

public class LaunchOptionsTests
{
    [Fact]
    public void Without_options_a_generated_level_0_is_played()
    {
        var options = LaunchOptions.Parse([]);

        Assert.Equal("generated", options.LevelName);
        Assert.True(options.IsGenerated);
    }

    [Fact]
    public void The_old_test_level_can_still_be_chosen()
    {
        Assert.False(LaunchOptions.Parse(["--level", "test"]).IsGenerated);
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
    public void The_generated_level_is_chosen_by_name_and_takes_a_seed()
    {
        var options = LaunchOptions.Parse(["--level", "generated", "--seed", "123"]);

        Assert.True(options.IsGenerated);
        Assert.Equal(123, options.Seed);
        Assert.False(LaunchOptions.Parse(["--level", "heights"]).IsGenerated);
    }

    [Fact]
    public void Without_seed_there_is_none_chosen()
    {
        Assert.Null(LaunchOptions.Parse(["--level", "generated"]).Seed);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("99999999999")]
    public void A_seed_must_be_a_whole_number(string seed)
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--seed", seed]));

        Assert.Contains("seed", error.Message);
    }

    [Fact]
    public void Seed_option_without_a_number_is_rejected()
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--seed"]));

        Assert.Contains("needs a number", error.Message);
    }

    [Fact]
    public void Unknown_options_are_rejected()
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--levle", "heights"]));

        Assert.Contains("--levle", error.Message);
    }

    [Fact]
    public void Without_give_no_weapon_is_given()
    {
        Assert.Empty(LaunchOptions.Parse([]).Give!);
    }

    [Fact]
    public void Give_names_weapons_in_any_case_and_as_often_as_wanted()
    {
        var options = LaunchOptions.Parse(["--give", "club", "--seed", "5", "--give", "PISTOL"]);

        Assert.Equal([WeaponKind.Club, WeaponKind.Pistol], options.Give!);
        Assert.Equal(5, options.Seed);
    }

    [Theory]
    [InlineData("sword")]
    [InlineData("1")]
    [InlineData("")]
    public void An_unknown_weapon_is_rejected_with_the_known_ones(string name)
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--give", name]));

        Assert.Contains("club", error.Message);
    }

    [Fact]
    public void Give_without_a_weapon_is_rejected()
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--give"]));

        Assert.Contains("needs a weapon", error.Message);
    }
}
