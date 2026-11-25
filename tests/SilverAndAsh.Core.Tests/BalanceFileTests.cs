using System.Globalization;
using System.Reflection;
using System.Text.Json;
using SilverAndAsh.Core.World;

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
    public void It_reads_the_play_round_a_column_and_refuses_a_negative_one()
    {
        // Issue #539.
        Assert.Equal(0.5f, BalanceFile.Parse("""{ "columns": { "play": 0.5 } }""").Balance.Columns.Play);

        var negative = BalanceFile.Parse("""{ "columns": { "play": -0.5 } }""");
        Assert.Equal(Balance.Default.Columns.Play, negative.Balance.Columns.Play);
        Assert.Contains(negative.Warnings, w => w.Contains("columns.play"));
    }

    [Fact]
    public void It_reads_how_many_blows_a_column_bears_and_refuses_none()
    {
        // Issue #590: a column that broke under no blow at all would give no cover - at least one.
        Assert.Equal(5, BalanceFile.Parse("""{ "columns": { "hitsToBreak": 5 } }""").Balance.Columns.HitsToBreak);

        var none = BalanceFile.Parse("""{ "columns": { "hitsToBreak": 0 } }""");
        Assert.Equal(Balance.Default.Columns.HitsToBreak, none.Balance.Columns.HitsToBreak);
        Assert.Contains(none.Warnings, w => w.Contains("columns.hitsToBreak"));
    }

    [Fact]
    public void It_reads_the_boss_rooms_column_grid_allows_none_but_refuses_a_spacing_of_nothing()
    {
        // Issue #597: no columns in the boss room is a choice; columns all on one spot are not.
        var grid = BalanceFile.Parse("""{ "columns": { "bossRoomPerSide": 3, "bossRoomSpacing": 10 } }""").Balance.Columns;
        Assert.Equal((3, 10f), (grid.BossRoomPerSide, grid.BossRoomSpacing));

        var none = BalanceFile.Parse("""{ "columns": { "bossRoomPerSide": 0 } }""");
        Assert.Equal(0, none.Balance.Columns.BossRoomPerSide);
        Assert.Empty(none.Warnings);

        var nothing = BalanceFile.Parse("""{ "columns": { "bossRoomSpacing": 0 } }""");
        Assert.Equal(Balance.Default.Columns.BossRoomSpacing, nothing.Balance.Columns.BossRoomSpacing);
        Assert.Contains(nothing.Warnings, w => w.Contains("columns.bossRoomSpacing"));
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

    [Fact]
    public void Weapons_may_carry_no_status_effect_and_never_trigger_one()
    {
        var result = BalanceFile.Parse("""
            { "statusEffects": { "weaponShare": 0,
              "weaponHitChance": { "worn": 0, "forged": 0, "royal": 0, "vampiric": 0 } } }
            """);

        var effects = result.Balance.StatusEffects;
        Assert.Equal(0f, effects.WeaponShare);
        Assert.Equal(0f, effects.WeaponHitChance.Worn);
        Assert.Equal(0f, effects.WeaponHitChance.Forged);
        Assert.Equal(0f, effects.WeaponHitChance.Royal);
        Assert.Equal(0f, effects.WeaponHitChance.Vampiric);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void A_ranged_weapon_may_pierce_nothing_and_shoot_straight()
    {
        var result = BalanceFile.Parse("""{ "pistol": { "pierce": 0, "spread": 0 }, "rifle": { "pierce": 0 }, "blunderbuss": { "pierce": 0, "spread": 0 } }""");

        Assert.Equal(0, result.Balance.Pistol.Pierce);
        Assert.Equal(0, result.Balance.Rifle.Pierce);
        Assert.Equal(0f, result.Balance.Blunderbuss.Spread);
        Assert.Empty(result.Warnings);
    }

    [Theory]
    [InlineData("rifle", "magazineSize")]
    [InlineData("rifle", "reloadTime")]
    [InlineData("blunderbuss", "pellets")]
    [InlineData("blunderbuss", "range")]
    [InlineData("blade", "cooldown")]
    [InlineData("player", "maxHealth")]
    [InlineData("player", "maxSpeed")]
    [InlineData("player", "jumpDuration")]
    [InlineData("bat", "spitInterval")]
    [InlineData("player", "staminaJumps")]
    [InlineData("pistol", "magazineSize")]
    [InlineData("pistol", "reloadTime")]
    [InlineData("hearing", "range")]
    [InlineData("hearing", "nearDistance")]
    [InlineData("hearing", "rolloff")]
    [InlineData("hearing", "maxPan")]
    [InlineData("skills", "levelsPerPoint")]
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
    [InlineData("""{ "blunderbuss": { "spread": 120 } }""", "blunderbuss.spread")]
    [InlineData("""{ "club": { "angle": 200 } }""", "club.angle")]
    [InlineData("""{ "ghoul": { "attackHalfAngle": 200 } }""", "ghoul.attackHalfAngle")]
    [InlineData("""{ "drops": { "ghoulChance": 1.5 } }""", "drops.ghoulChance")]
    [InlineData("""{ "drops": { "batChance": 2 } }""", "drops.batChance")]
    [InlineData("""{ "drops": { "consecratedAmmoShare": 1.2 } }""", "drops.consecratedAmmoShare")]
    [InlineData("""{ "silver": { "ghoulChance": 1.5 } }""", "silver.ghoulChance")]
    [InlineData("""{ "silver": { "batChance": 2 } }""", "silver.batChance")]
    [InlineData("""{ "keys": { "roomChance": 1.5 } }""", "keys.roomChance")]
    [InlineData("""{ "keys": { "enemyChance": 3 } }""", "keys.enemyChance")]
    [InlineData("""{ "loot": { "higherTierChance": 1.5 } }""", "loot.higherTierChance")]
    [InlineData("""{ "loot": { "itemShare": 2 } }""", "loot.itemShare")]
    [InlineData("""{ "layout": { "corridorChance": 1.5 } }""", "layout.corridorChance")]
    [InlineData("""{ "gorm": { "phase2Health": 1.5 } }""", "gorm.phase2Health")]
    [InlineData("""{ "gorm": { "swingArc": 400 } }""", "gorm.swingArc")]
    [InlineData("""{ "gorm": { "equipmentVampiricChance": 1.5 } }""", "gorm.equipmentVampiricChance")]
    [InlineData("""{ "gorm": { "extraSilver": -1 } }""", "gorm.extraSilver")]
    [InlineData("""{ "difficulty": { "ghoulShareNear": 1.5 } }""", "difficulty.ghoulShareNear")]
    [InlineData("""{ "difficulty": { "ghoulShareFar": 2 } }""", "difficulty.ghoulShareFar")]
    [InlineData("""{ "difficulty": { "batShareNear": 1.1 } }""", "difficulty.batShareNear")]
    [InlineData("""{ "difficulty": { "batShareFar": 3 } }""", "difficulty.batShareFar")]
    [InlineData("""{ "hearing": { "maxPan": 1.2 } }""", "hearing.maxPan")]
    [InlineData("""{ "difficulty": { "doorConeAngle": 91 } }""", "difficulty.doorConeAngle")]
    [InlineData("""{ "grates": { "blockHeight": 3.5 } }""", "grates.blockHeight")]
    [InlineData("""{ "statusEffects": { "marked": { "bonus": 1.5 } } }""", "statusEffects.marked.bonus")]
    [InlineData("""{ "statusEffects": { "marked": { "seconds": 31 } } }""", "statusEffects.marked.seconds")]
    [InlineData("""{ "skills": { "levelsPerPoint": 21 } }""", "skills.levelsPerPoint")]
    [InlineData("""{ "statusEffects": { "weaponShare": 1.5 } }""", "statusEffects.weaponShare")]
    [InlineData("""{ "statusEffects": { "weaponHitChance": { "worn": 1.1 } } }""", "statusEffects.weaponHitChance.worn")]
    [InlineData("""{ "statusEffects": { "weaponHitChance": { "forged": 2 } } }""", "statusEffects.weaponHitChance.forged")]
    [InlineData("""{ "statusEffects": { "weaponHitChance": { "royal": 1.5 } } }""", "statusEffects.weaponHitChance.royal")]
    [InlineData("""{ "statusEffects": { "weaponHitChance": { "vampiric": 3 } } }""", "statusEffects.weaponHitChance.vampiric")]
    public void Values_above_their_limit_fall_back_to_their_standard(string json, string name)
    {
        var result = BalanceFile.Parse(json);

        Assert.Equal(Balance.Default, result.Balance);
        Assert.Contains(result.Warnings, w => w.Contains(name));
    }

    [Theory]
    [InlineData("doorDistance", 0f)]
    [InlineData("doorDistance", 0.75f)]
    [InlineData("playerDistance", 0f)]
    [InlineData("playerDistance", 0.75f)]
    [InlineData("sideDoorDistance", 0f)]
    [InlineData("sideDoorDistance", 0.75f)]
    public void Door_and_player_distance_below_one_cell_fall_back_to_their_standard(string name, float cells)
    {
        // Issue #445: an enemy must never stand in a doorway nor come out on a player's cell.
        var metres = (cells * Level.CellSize).ToString(CultureInfo.InvariantCulture);
        var result = BalanceFile.Parse($$"""{ "difficulty": { "{{name}}": {{metres}} } }""");

        Assert.Equal(Balance.Default, result.Balance);
        Assert.Contains(result.Warnings, w => w.Contains($"difficulty.{name}"));
    }

    [Theory]
    [InlineData("0.00000001")]
    [InlineData("0.4")]
    public void A_top_speed_of_almost_nothing_falls_back_to_its_standard(string speed)
    {
        // Issue #706: with a tiny top speed the steps would come in floods.
        var result = BalanceFile.Parse($$"""{ "player": { "maxSpeed": {{speed}} } }""");

        Assert.Equal(Balance.Default, result.Balance);
        Assert.Contains(result.Warnings, w => w.Contains("player.maxSpeed") && w.Contains("below its limit"));
    }

    [Fact]
    public void A_top_speed_of_half_a_metre_a_second_is_allowed()
    {
        var result = BalanceFile.Parse("""{ "player": { "maxSpeed": 0.5 } }""");

        Assert.Equal(0.5f, result.Balance.Player.MaxSpeed);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Door_and_player_distance_of_one_cell_are_allowed()
    {
        var metres = Level.CellSize.ToString(CultureInfo.InvariantCulture);
        var result = BalanceFile.Parse($$"""{ "difficulty": { "doorDistance": {{metres}}, "playerDistance": {{metres}} } }""");

        Assert.Equal(Level.CellSize, result.Balance.Difficulty.DoorDistance);
        Assert.Equal(Level.CellSize, result.Balance.Difficulty.PlayerDistance);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void A_side_door_distance_below_the_door_distance_puts_both_back_to_their_standards()
    {
        // Issue #490: beside a door an enemy must keep at least as far as straight in front of it.
        var door = (Balance.Default.Difficulty.SideDoorDistance + Level.CellSize).ToString(CultureInfo.InvariantCulture);
        var side = Balance.Default.Difficulty.SideDoorDistance.ToString(CultureInfo.InvariantCulture);
        var result = BalanceFile.Parse($$"""{ "difficulty": { "doorDistance": {{door}}, "sideDoorDistance": {{side}} } }""");

        Assert.Equal(Balance.Default, result.Balance);
        Assert.Contains(result.Warnings, w => w.Contains("difficulty.doorDistance") && w.Contains("difficulty.sideDoorDistance"));
    }

    [Fact]
    public void A_door_cone_of_a_right_angle_and_a_side_distance_equal_to_the_door_distance_are_allowed()
    {
        var metres = Balance.Default.Difficulty.DoorDistance.ToString(CultureInfo.InvariantCulture);
        var result = BalanceFile.Parse($$"""{ "difficulty": { "doorConeAngle": 90, "sideDoorDistance": {{metres}} } }""");

        Assert.Equal(90f, result.Balance.Difficulty.DoorConeAngle);
        Assert.Equal(Balance.Default.Difficulty.DoorDistance, result.Balance.Difficulty.SideDoorDistance);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void A_grate_that_blocks_up_to_its_open_height_is_allowed()
    {
        var open = SilverAndAsh.Core.Presentation.GratePose.OpenHeight.ToString(CultureInfo.InvariantCulture);
        var result = BalanceFile.Parse($$"""{ "grates": { "blockHeight": {{open}} } }""");

        Assert.Equal(SilverAndAsh.Core.Presentation.GratePose.OpenHeight, result.Balance.Grates.BlockHeight);
        Assert.Empty(result.Warnings);
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
    public void A_near_distance_beyond_the_hearing_range_falls_back_to_both_standards()
    {
        var result = BalanceFile.Parse("""{ "hearing": { "nearDistance": 12, "range": 10 } }""");

        Assert.Equal(Balance.Default.Hearing, result.Balance.Hearing);
        Assert.Contains(result.Warnings, w => w.Contains("hearing.nearDistance") && w.Contains("hearing.range"));
    }

    [Fact]
    public void A_near_distance_as_far_as_the_hearing_range_is_allowed()
    {
        var result = BalanceFile.Parse("""{ "hearing": { "nearDistance": 10, "range": 10 } }""");

        Assert.Equal(10f, result.Balance.Hearing.NearDistance);
        Assert.Equal(10f, result.Balance.Hearing.Range);
        Assert.Empty(result.Warnings);
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
    [InlineData("""{ "chests": { "vampire": { "equipmentChance": 1.5 } } }""", "chests.vampire.equipmentChance")]
    [InlineData("""{ "chests": { "beggar": { "equipmentChance": -0.1 } } }""", "chests.beggar.equipmentChance")]
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
            { "chests": { "combatRoomChance": 0, "vampire": { "treasureShare": 0, "lockedTreasureShare": 0, "secretShare": 0, "lockedChance": 0, "silverMin": 0, "silverMax": 0, "bloodVialChance": 0, "equipmentChance": 0 } } }
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
    public void A_charged_knockback_shorter_than_the_plain_one_falls_back_to_both_standards()
    {
        var result = BalanceFile.Parse("""{ "club": { "knockback": 8, "chargedKnockback": 2 } }""");

        Assert.Equal(Balance.Default.Club, result.Balance.Club);
        Assert.Contains(result.Warnings, w => w.Contains("club.knockback") && w.Contains("club.chargedKnockback"));
    }

    [Fact]
    public void A_melee_weapon_may_have_no_knockback()
    {
        var result = BalanceFile.Parse("""{ "club": { "knockback": 0, "chargedKnockback": 0 } }""");

        Assert.Equal(0f, result.Balance.Club.Knockback);
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
    public void It_reads_the_level_costs_as_a_table()
    {
        // Issue #503: the rows replace the standard table as a whole.
        var result = BalanceFile.Parse("""{ "experience": { "levelCosts": [ { "fromLevel": 0, "cost": 50 }, { "fromLevel": 5, "cost": 80 } ] } }""");

        Assert.Empty(result.Warnings);
        Assert.Equal([new LevelCost(0, 50), new LevelCost(5, 80)], result.Balance.Experience.LevelCosts);
        Assert.Equal(Balance.Default.Experience.Ghoul, result.Balance.Experience.Ghoul);
    }

    [Fact]
    public void Without_level_costs_the_standard_table_stays()
    {
        var result = BalanceFile.Parse("""{ "experience": { "ghoul": 40 } }""");

        Assert.Empty(result.Warnings);
        Assert.Equal(Balance.Default.Experience.LevelCosts, result.Balance.Experience.LevelCosts);
    }

    [Fact]
    public void An_empty_table_of_level_costs_is_refused()
    {
        var result = BalanceFile.Parse("""{ "experience": { "levelCosts": [] } }""");

        Assert.Equal(Balance.Default, result.Balance);
        Assert.Contains(result.Warnings, w => w.Contains("experience.levelCosts") && w.Contains("empty"));
    }

    [Fact]
    public void A_table_of_level_costs_that_is_null_is_refused()
    {
        var result = BalanceFile.Parse("""{ "experience": { "levelCosts": null } }""");

        Assert.Equal(Balance.Default, result.Balance);
        Assert.Contains(result.Warnings, w => w.Contains("experience.levelCosts") && w.Contains("empty"));
    }

    [Fact]
    public void A_row_of_level_costs_that_is_null_is_refused()
    {
        var result = BalanceFile.Parse("""{ "experience": { "levelCosts": [ { "fromLevel": 0, "cost": 50 }, null ] } }""");

        Assert.Equal(Balance.Default, result.Balance);
        Assert.Contains(result.Warnings, w => w.Contains("experience.levelCosts") && w.Contains("empty row"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void Level_costs_that_do_not_start_at_level_zero_are_refused(int first)
    {
        // Below the first row a level would cost nothing - the player would stay on level 0 forever.
        var result = BalanceFile.Parse($$"""{ "experience": { "levelCosts": [ { "fromLevel": {{first}}, "cost": 50 }, { "fromLevel": 5, "cost": 80 } ] } }""");

        Assert.Equal(Balance.Default, result.Balance);
        Assert.Contains(result.Warnings, w => w.Contains("experience.levelCosts") && w.Contains("start at level 0"));
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(5, 5)]
    public void Level_costs_whose_thresholds_do_not_rise_are_refused(int second, int third)
    {
        var result = BalanceFile.Parse($$"""
            { "experience": { "levelCosts": [
              { "fromLevel": 0, "cost": 50 }, { "fromLevel": {{second}}, "cost": 80 }, { "fromLevel": {{third}}, "cost": 90 } ] } }
            """);

        Assert.Equal(Balance.Default, result.Balance);
        Assert.Contains(result.Warnings, w => w.Contains("experience.levelCosts") && w.Contains("rise"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Level_costs_of_nothing_or_less_are_refused(int cost)
    {
        // A level that costs nothing would never be climbed (Player.AddExperience stops there).
        var result = BalanceFile.Parse($$"""{ "experience": { "levelCosts": [ { "fromLevel": 0, "cost": 50 }, { "fromLevel": 5, "cost": {{cost}} } ] } }""");

        Assert.Equal(Balance.Default, result.Balance);
        Assert.Contains(result.Warnings, w => w.Contains("experience.levelCosts") && w.Contains("cost above 0"));
    }

    [Fact]
    public void A_refused_table_of_level_costs_keeps_the_other_experience_values()
    {
        var result = BalanceFile.Parse("""{ "experience": { "ghoul": 40, "levelCosts": [] } }""");

        Assert.Equal(40, result.Balance.Experience.Ghoul);
        Assert.Equal(Balance.Default.Experience.LevelCosts, result.Balance.Experience.LevelCosts);
    }

    [Fact]
    public void A_misspelled_name_in_a_row_of_level_costs_is_reported()
    {
        // The row then lacks its cost (0) and the table is refused as well.
        var result = BalanceFile.Parse("""{ "experience": { "levelCosts": [ { "fromLevel": 0, "cots": 50 } ] } }""");

        Assert.Contains(result.Warnings, w => w.Contains("experience.levelCosts[0].cots"));
        Assert.Equal(Balance.Default.Experience.LevelCosts, result.Balance.Experience.LevelCosts);
    }

    [Fact]
    public void Refusing_a_table_of_level_costs_leaves_the_standard_values_untouched()
    {
        // The standard table is handed over, not changed - the next file still sees the same standard.
        var before = Balance.Default.Experience.LevelCosts.ToList();

        BalanceFile.Parse("""{ "experience": { "levelCosts": [ { "fromLevel": 3, "cost": 0 } ] } }""");

        Assert.Equal(before, Balance.Default.Experience.LevelCosts);
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
