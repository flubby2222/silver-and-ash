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
    [InlineData("""{ "silver": { "ghoulChance": 1.5 } }""", "silver.ghoulChance")]
    [InlineData("""{ "silver": { "batChance": 2 } }""", "silver.batChance")]
    [InlineData("""{ "keys": { "roomChance": 1.5 } }""", "keys.roomChance")]
    [InlineData("""{ "keys": { "enemyChance": 3 } }""", "keys.enemyChance")]
    [InlineData("""{ "layout": { "corridorChance": 1.5 } }""", "layout.corridorChance")]
    [InlineData("""{ "gorm": { "phase2Health": 1.5 } }""", "gorm.phase2Health")]
    [InlineData("""{ "difficulty": { "ghoulShareNear": 1.5 } }""", "difficulty.ghoulShareNear")]
    [InlineData("""{ "difficulty": { "ghoulShareFar": 2 } }""", "difficulty.ghoulShareFar")]
    [InlineData("""{ "difficulty": { "batShareNear": 1.1 } }""", "difficulty.batShareNear")]
    [InlineData("""{ "difficulty": { "batShareFar": 3 } }""", "difficulty.batShareFar")]
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

    [Theory]
    [InlineData("ghoulMin", "ghoulMax")]
    [InlineData("batMin", "batMax")]
    public void More_minimum_than_maximum_silver_falls_back_to_both_standards(string min, string max)
    {
        var result = BalanceFile.Parse($$"""{ "silver": { "{{min}}": 5, "{{max}}": 2 } }""");

        Assert.Equal(Balance.Default.Silver, result.Balance.Silver);
        Assert.Contains(result.Warnings, w => w.Contains($"silver.{min}") && w.Contains($"silver.{max}"));
    }

    [Fact]
    public void Negative_silver_falls_back_to_its_standard_and_no_silver_is_fine()
    {
        var result = BalanceFile.Parse("""{ "silver": { "ghoulMin": -1, "batMin": 0, "batMax": 0, "batChance": 0 } }""");

        Assert.Equal(Balance.Default.Silver.GhoulMin, result.Balance.Silver.GhoulMin);
        Assert.Contains(result.Warnings, w => w.Contains("silver.ghoulMin"));
        Assert.Equal(0, result.Balance.Silver.BatMin);
        Assert.Equal(0, result.Balance.Silver.BatMax);
        Assert.Equal(0f, result.Balance.Silver.BatChance);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("silver.bat"));
    }

    [Fact]
    public void A_chest_tier_written_in_part_keeps_the_other_standards_of_its_own_tier()
    {
        var result = BalanceFile.Parse("""{ "chests": { "guard": { "lockedChance": 0.5 } } }""");

        Assert.Equal(Balance.Default.Chests.Guard with { LockedChance = 0.5f }, result.Balance.Chests.Guard);
        Assert.Equal(Balance.Default.Chests.King, result.Balance.Chests.King);
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("""{ "chests": { "combatRoomChance": 1.5 } }""", "chests.combatRoomChance")]
    [InlineData("""{ "chests": { "king": { "lockedChance": 2 } } }""", "chests.king.lockedChance")]
    [InlineData("""{ "chests": { "beggar": { "bloodVialChance": 1.1 } } }""", "chests.beggar.bloodVialChance")]
    [InlineData("""{ "chests": { "guard": { "silverMin": -5 } } }""", "chests.guard.silverMin")]
    public void Wrong_chest_values_fall_back_to_their_tiers_standard(string json, string name)
    {
        var result = BalanceFile.Parse(json);

        Assert.Equal(Balance.Default.Chests, result.Balance.Chests);
        Assert.Contains(result.Warnings, w => w.Contains(name));
    }

    [Fact]
    public void More_minimum_than_maximum_chest_silver_falls_back_to_both_standards_of_that_tier()
    {
        var result = BalanceFile.Parse("""{ "chests": { "king": { "silverMin": 90, "silverMax": 50, "lockedChance": 0.2 } } }""");

        Assert.Equal(Balance.Default.Chests.King with { LockedChance = 0.2f }, result.Balance.Chests.King);
        Assert.Contains(result.Warnings, w => w.Contains("chests.king.silverMin") && w.Contains("chests.king.silverMax"));
    }

    [Fact]
    public void Chests_may_hold_nothing_and_never_stand_in_combat_rooms()
    {
        var result = BalanceFile.Parse("""
            { "chests": { "combatRoomChance": 0, "vampire": { "treasureShare": 0, "lockedTreasureShare": 0, "secretShare": 0, "lockedChance": 0, "silverMin": 0, "silverMax": 0, "bloodVialChance": 0 } } }
            """);

        Assert.Equal(0f, result.Balance.Chests.CombatRoomChance);
        Assert.Equal(new ChestTierBalance(), result.Balance.Chests.Vampire);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Levels_may_have_no_treasure_rooms_and_treasure_rooms_no_chests()
    {
        var result = BalanceFile.Parse("""
            { "layout": { "treasureRooms": 0, "lockedTreasureRooms": 0 }, "chests": { "treasureChestsMin": 0, "treasureChestsMax": 0 } }
            """);

        Assert.Equal(0, result.Balance.Layout.TreasureRooms + result.Balance.Layout.LockedTreasureRooms);
        Assert.Equal(0, result.Balance.Chests.TreasureChestsMax);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Levels_may_have_no_secret_room()
    {
        var result = BalanceFile.Parse("""{ "layout": { "secretRooms": 0 } }""");

        Assert.Equal(0, result.Balance.Layout.SecretRooms);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void More_minimum_than_maximum_treasure_chests_falls_back_to_both_standards()
    {
        var result = BalanceFile.Parse("""{ "chests": { "treasureChestsMin": 3, "treasureChestsMax": 1 } }""");

        Assert.Equal(Balance.Default.Chests, result.Balance.Chests);
        Assert.Contains(result.Warnings, w => w.Contains("chests.treasureChestsMin") && w.Contains("chests.treasureChestsMax"));
    }

    [Fact]
    public void Keys_may_never_drop()
    {
        var result = BalanceFile.Parse("""{ "keys": { "roomChance": 0, "enemyChance": 0 } }""");

        Assert.Equal(new KeysBalance { RoomChance = 0f, EnemyChance = 0f }, result.Balance.Keys);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Gorm_s_stones_may_be_switched_off()
    {
        var result = BalanceFile.Parse("""{ "gorm": { "stoneCount": 0, "stoneDamage": 0 } }""");

        Assert.Equal(Balance.Default.Gorm with { StoneCount = 0, StoneDamage = 0 }, result.Balance.Gorm);
        Assert.Empty(result.Warnings);
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
