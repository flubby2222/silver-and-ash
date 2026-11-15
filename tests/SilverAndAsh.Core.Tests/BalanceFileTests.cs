using System.Reflection;
using System.Text.Json;

namespace SilverAndAsh.Core.Tests;

public class BalanceFileTests
{
    [Fact]
    public void It_reads_the_values_of_the_file()
    {
        var result = BalanceFile.Parse("""{ "ghoul": { "speed": 6, "maxHealth": 120 }, "restartDelay": 3 }""");

        Assert.Equal(6f, result.Balance.Ghoul.Speed);
        Assert.Equal(120, result.Balance.Ghoul.MaxHealth);
        Assert.Equal(3f, result.Balance.RestartDelay);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Missing_values_keep_their_standard_values()
    {
        var result = BalanceFile.Parse("""{ "ghoul": { "speed": 6 } }""");

        Assert.Equal(Balance.Default.Ghoul.AttackDamage, result.Balance.Ghoul.AttackDamage);
        Assert.Equal(Balance.Default.Player, result.Balance.Player);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void An_empty_file_gives_the_standard_values()
    {
        var result = BalanceFile.Parse("{}");

        Assert.Equal(Balance.Default, result.Balance);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void A_misspelled_name_is_reported_and_ignored()
    {
        var result = BalanceFile.Parse("""{ "ghoul": { "sped": 6 }, "ghouls": { } }""");

        Assert.Equal(Balance.Default, result.Balance);
        Assert.Contains(result.Warnings, w => w.Contains("ghoul.sped"));
        Assert.Contains(result.Warnings, w => w.Contains("ghouls"));
    }

    [Fact]
    public void Derived_values_cannot_be_set()
    {
        // Gravity comes from jump height and duration - writing it in the file is a mistake worth a warning.
        var result = BalanceFile.Parse("""{ "player": { "gravity": 9.81 } }""");

        Assert.Equal(Balance.Default.Player.Gravity, result.Balance.Player.Gravity);
        Assert.Contains(result.Warnings, w => w.Contains("player.gravity"));
    }

    [Theory]
    [InlineData("{ \"ghoul\": ")]
    [InlineData("{ \"ghoul\": { \"maxHealth\": \"a lot\" } }")]
    [InlineData("{ \"ghoul\": { \"maxHealth\": 12.5 } }")]
    [InlineData("[1, 2, 3]")]
    public void A_broken_file_gives_the_standard_values_and_a_warning(string json)
    {
        var result = BalanceFile.Parse(json);

        Assert.Equal(Balance.Default, result.Balance);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void A_negative_value_falls_back_to_its_standard_the_others_stay()
    {
        var result = BalanceFile.Parse("""{ "ghoul": { "speed": -1, "maxHealth": 120 } }""");

        Assert.Equal(Balance.Default.Ghoul.Speed, result.Balance.Ghoul.Speed);
        Assert.Equal(120, result.Balance.Ghoul.MaxHealth);
        Assert.Contains(result.Warnings, w => w.Contains("ghoul.speed"));
    }

    [Fact]
    public void Zero_is_fine_where_it_makes_sense()
    {
        var result = BalanceFile.Parse("""{ "player": { "airControl": 0, "perfectJumpSpeedBonus": 0 }, "blade": { "coneStartBehind": 0 }, "drops": { "ghoulChance": 0 } }""");

        Assert.Equal(0f, result.Balance.Drops.GhoulChance);
        Assert.Equal(0f, result.Balance.Player.AirControl);
        Assert.Equal(0f, result.Balance.Player.PerfectJumpSpeedBonus);
        Assert.Equal(0f, result.Balance.Blade.ConeStartBehind);
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("blade", "cooldown")]
    [InlineData("player", "maxHealth")]
    [InlineData("player", "jumpDuration")]
    [InlineData("bat", "spitInterval")]
    [InlineData("player", "staminaJumps")]
    [InlineData("experience", "firstLevel")]
    public void Zero_is_refused_where_it_would_break_the_game(string section, string name)
    {
        var result = BalanceFile.Parse($$"""{ "{{section}}": { "{{name}}": 0 } }""");

        Assert.Equal(Balance.Default, result.Balance);
        Assert.Contains(result.Warnings, w => w.Contains($"{section}.{name}"));
    }

    [Theory]
    [InlineData("""{ "player": { "airControl": 1.5 } }""", "player.airControl")]
    [InlineData("""{ "player": { "exhaustedJumpSpeedPenalty": 1 } }""", "player.exhaustedJumpSpeedPenalty")]
    [InlineData("""{ "blade": { "angle": 200 } }""", "blade.angle")]
    [InlineData("""{ "ghoul": { "attackHalfAngle": 200 } }""", "ghoul.attackHalfAngle")]
    [InlineData("""{ "drops": { "ghoulChance": 1.5 } }""", "drops.ghoulChance")]
    [InlineData("""{ "drops": { "batChance": 2 } }""", "drops.batChance")]
    [InlineData("""{ "drops": { "consecratedAmmoShare": 1.2 } }""", "drops.consecratedAmmoShare")]
    [InlineData("""{ "layout": { "corridorChance": 1.5 } }""", "layout.corridorChance")]
    public void Values_above_their_limit_fall_back_to_their_standard(string json, string name)
    {
        var result = BalanceFile.Parse(json);

        Assert.Equal(Balance.Default, result.Balance);
        Assert.Contains(result.Warnings, w => w.Contains(name));
    }

    [Fact]
    public void A_jump_higher_than_the_room_falls_back_to_its_standard()
    {
        // Rooms are 3.5 m high, the player 1.8 m: a jump of 2 m would hit the head on the ceiling every time.
        var result = BalanceFile.Parse("""{ "player": { "jumpHeight": 2 } }""");

        Assert.Equal(Balance.Default.Player.JumpHeight, result.Balance.Player.JumpHeight);
        Assert.Contains(result.Warnings, w => w.Contains("player.jumpHeight"));
    }

    [Theory]
    [InlineData("circleRadiusMin", 7, "circleRadiusMax", 5)]
    [InlineData("turnRoundMin", 5, "turnRoundMax", 3)]
    [InlineData("circleHeightMin", 2.6, "circleHeightMax", 2.2)]
    public void A_minimum_above_its_maximum_falls_back_to_both_standards(string min, double minValue, string max, double maxValue)
    {
        var result = BalanceFile.Parse($$"""{ "bat": { "{{min}}": {{minValue.ToString(System.Globalization.CultureInfo.InvariantCulture)}}, "{{max}}": {{maxValue.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } }""");

        Assert.Equal(Balance.Default.Bat, result.Balance.Bat);
        Assert.Contains(result.Warnings, w => w.Contains($"bat.{min}") && w.Contains($"bat.{max}"));
    }

    [Fact]
    public void More_minimum_than_maximum_combat_rooms_falls_back_to_both_standards()
    {
        var result = BalanceFile.Parse("""{ "layout": { "combatRoomsMin": 9, "combatRoomsMax": 4 } }""");

        Assert.Equal(Balance.Default.Layout, result.Balance.Layout);
        Assert.Contains(result.Warnings, w => w.Contains("layout.combatRoomsMin") && w.Contains("layout.combatRoomsMax"));
    }

    [Fact]
    public void Comments_and_trailing_commas_are_allowed()
    {
        var result = BalanceFile.Parse("""
            {
              // Faster ghouls for a test.
              "ghoul": { "speed": 6, },
            }
            """);

        Assert.Equal(6f, result.Balance.Ghoul.Speed);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void A_missing_file_gives_the_standard_values_and_a_warning()
    {
        var result = BalanceFile.Load(Path.Combine(Path.GetTempPath(), "silver-and-ash-no-such-file.json"));

        Assert.Equal(Balance.Default, result.Balance);
        Assert.Contains(result.Warnings, w => w.Contains("silver-and-ash-no-such-file.json"));
    }

    [Fact]
    public void The_shipped_file_holds_exactly_the_standard_values()
    {
        // Otherwise the game would play differently from what the tests check.
        var result = BalanceFile.Load(RepositoryFiles.Find("assets/balance.json"));

        Assert.Empty(result.Warnings);
        Assert.Equal(Balance.Default, result.Balance);
    }

    [Fact]
    public void The_shipped_file_lists_every_value()
    {
        // A value that is missing from the file still works (standard), but nobody would know it can be changed.
        var text = File.ReadAllText(RepositoryFiles.Find("assets/balance.json"));
        using var file = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        var missing = SettableNames(typeof(Balance), string.Empty)
            .Where(path => !Has(file.RootElement, path.Split('.')))
            .ToList();

        Assert.Empty(missing);
    }

    private static IEnumerable<string> SettableNames(Type type, string prefix)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.SetMethod is not null))
        {
            var name = prefix + JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            if (property.PropertyType.Namespace == typeof(Balance).Namespace && property.PropertyType.IsClass)
            {
                foreach (var inner in SettableNames(property.PropertyType, name + "."))
                {
                    yield return inner;
                }
            }
            else
            {
                yield return name;
            }
        }
    }

    private static bool Has(JsonElement element, string[] path) =>
        path.Length == 0 || (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(path[0], out var child) && Has(child, path[1..]));
}
