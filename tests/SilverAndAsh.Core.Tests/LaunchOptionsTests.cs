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
    public void Without_photo_the_game_is_played()
    {
        var options = LaunchOptions.Parse([]);

        Assert.Null(options.Photo);
        Assert.Null(options.PhotoFolder);
    }

    [Theory]
    [InlineData("ghoul-4m")]
    [InlineData("all")]
    [InlineData("list")]
    [InlineData("compare")]
    public void Photo_takes_a_scene_all_or_list(string photo)
    {
        Assert.Equal(photo, LaunchOptions.Parse(["--photo", photo]).Photo);
    }

    [Fact]
    public void Bench_takes_one_photo_scene()
    {
        var options = LaunchOptions.Parse(["--bench", "room-combat"]);

        Assert.Equal("room-combat", options.Bench);
        Assert.Null(options.Photo);
        Assert.Null(LaunchOptions.Parse([]).Bench);
    }

    [Theory]
    [InlineData]
    [InlineData("all")]
    [InlineData("ghoul-5m")]
    public void Bench_without_a_known_scene_is_rejected_with_the_known_ones(params string[] scene)
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--bench", .. scene]));

        Assert.Contains("room-combat", error.Message);
    }

    [Fact]
    public void Bench_and_photo_cannot_be_combined()
    {
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--bench", "room-combat", "--photo", "all"]));
    }

    [Fact]
    public void Photo_with_out_names_the_folder()
    {
        var options = LaunchOptions.Parse(["--photo", "all", "--out", "some folder"]);

        Assert.Equal("some folder", options.PhotoFolder);
    }

    [Fact]
    public void An_unknown_photo_scene_is_rejected_with_the_known_ones()
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--photo", "ghoul-5m"]));

        Assert.Contains("ghoul-5m", error.Message);
        Assert.Contains("ghoul-4m", error.Message);
    }

    [Fact]
    public void Photo_without_a_scene_is_rejected()
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--photo"]));

        Assert.Contains("needs a scene", error.Message);
    }

    [Theory]
    [InlineData]
    [InlineData("  ")]
    public void Out_without_a_folder_is_rejected(params string[] folder)
    {
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--photo", "all", "--out", .. folder]));
    }

    [Fact]
    public void Out_without_photo_is_rejected()
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--out", "photos"]));

        Assert.Contains("only goes with '--photo'", error.Message);
    }

    [Theory]
    [InlineData("blade-charge")]
    [InlineData("all")]
    [InlineData("list")]
    public void Clip_takes_a_motion_scene_all_or_list(string clip)
    {
        var options = LaunchOptions.Parse(["--clip", clip]);

        Assert.Equal(clip, options.Clip);
        Assert.Null(options.Photo);
        Assert.Null(LaunchOptions.Parse([]).Clip);
    }

    [Fact]
    public void Clip_with_out_names_the_folder()
    {
        Assert.Equal("clips", LaunchOptions.Parse(["--clip", "all", "--out", "clips"]).PhotoFolder);
    }

    [Theory]
    [InlineData]
    [InlineData("ghoul-4m")]
    [InlineData("compare")]
    public void Clip_without_a_known_motion_scene_is_rejected_with_the_known_ones(params string[] clip)
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--clip", .. clip]));

        Assert.Contains("blade-charge", error.Message);
    }

    [Theory]
    [InlineData("--photo", "ghoul-4m")]
    [InlineData("--bench", "room-combat")]
    public void Clip_cannot_be_combined_with_photo_or_bench(string option, string scene)
    {
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--clip", "all", option, scene]));
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

    [Fact]
    public void Without_look_the_game_keeps_its_picture()
    {
        Assert.Null(LaunchOptions.Parse([]).Look);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("4", 4)]
    public void Look_chooses_a_picture_variant(string text, int expected)
    {
        Assert.Equal(expected, LaunchOptions.Parse(["--look", text]).Look);
        Assert.Equal(expected, LaunchOptions.Parse(["--photo", "ghoul-4m", "--look", text]).Look);
    }

    [Theory]
    [InlineData]
    [InlineData("0")]
    [InlineData("5")]
    [InlineData("-1")]
    [InlineData("two")]
    public void Look_without_a_variant_from_1_to_4_is_rejected(params string[] variant)
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--look", .. variant]));

        Assert.Contains("from 1 to 4", error.Message);
    }

    [Fact]
    public void Compare_takes_every_variant_itself()
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--photo", "compare", "--look", "2"]));

        Assert.Contains("leave out '--look'", error.Message);
    }

    [Fact]
    public void Without_window_the_standard_size_is_used()
    {
        Assert.Null(LaunchOptions.Parse([]).Window);
    }

    [Fact]
    public void Window_chooses_the_window_size()
    {
        Assert.Equal(new WindowSize(2520, 1080), LaunchOptions.Parse(["--window", "2520x1080"]).Window);

        var options = LaunchOptions.Parse(["--photo", "room-combat", "--window", "1024x768"]);
        Assert.Equal(new WindowSize(1024, 768), options.Window);
        Assert.Equal("room-combat", options.Photo);
    }

    [Theory]
    [InlineData]
    [InlineData("1280")]
    [InlineData("1280x")]
    [InlineData("x720")]
    [InlineData("1280*720")]
    [InlineData("1280x720x3")]
    [InlineData("-1280x720")]
    [InlineData("199x720")]
    [InlineData("1280x7681")]
    [InlineData("99999x720")]
    public void Window_without_a_sensible_size_is_rejected(params string[] size)
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--window", .. size]));

        Assert.Contains("'--window' needs a size", error.Message);
    }

    [Fact]
    public void Without_showroom_the_game_is_played()
    {
        Assert.False(LaunchOptions.Parse([]).Showroom);
    }

    [Fact]
    public void Showroom_opens_the_showroom_and_goes_with_look()
    {
        Assert.True(LaunchOptions.Parse(["--showroom"]).Showroom);

        var options = LaunchOptions.Parse(["--showroom", "--look", "3"]);
        Assert.True(options.Showroom);
        Assert.Equal(3, options.Look);
    }

    [Theory]
    [InlineData("--level", "heights")]
    [InlineData("--seed", "123")]
    [InlineData("--give", "club")]
    [InlineData("--photo", "ghoul-4m")]
    [InlineData("--bench", "ghoul-4m")]
    [InlineData("--clip", "all")]
    public void Showroom_cannot_be_combined_with_a_level_weapons_or_pictures(string option, string value)
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--showroom", option, value]));

        Assert.Contains("'--showroom'", error.Message);
    }

    [Fact]
    public void The_unknown_option_message_names_showroom()
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--showrom"]));

        Assert.Contains("--showroom", error.Message);
    }

    [Fact]
    public void Course_opens_the_test_course_and_goes_with_look()
    {
        Assert.False(LaunchOptions.Parse([]).Course);
        Assert.True(LaunchOptions.Parse(["--course"]).Course);

        var options = LaunchOptions.Parse(["--look", "2", "--course"]);
        Assert.True(options.Course);
        Assert.Equal(2, options.Look);
    }

    [Theory]
    [InlineData("--level", "heights")]
    [InlineData("--seed", "123")]
    [InlineData("--give", "club")]
    [InlineData("--photo", "ghoul-4m")]
    [InlineData("--bench", "ghoul-4m")]
    [InlineData("--clip", "all")]
    [InlineData("--showroom")]
    [InlineData("--window", "854x480")]
    public void Course_cannot_be_combined_with_anything_but_look(params string[] other)
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--course", .. other]));

        Assert.Contains("'--course'", error.Message);
    }

    [Fact]
    public void Course_may_come_only_once()
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--course", "--course"]));

        Assert.Contains("only once", error.Message);
    }

    [Fact]
    public void The_unknown_option_message_names_course()
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--corse"]));

        Assert.Contains("--course", error.Message);
    }

    [Fact]
    public void Without_room_option_there_is_no_room()
    {
        Assert.Null(LaunchOptions.Parse([]).Room);
    }

    [Fact]
    public void Room_option_chooses_a_template_with_enemies()
    {
        var options = LaunchOptions.Parse(["--room", "assets/rooms/combat/l-hall.txt"]);

        Assert.Equal(new RoomOptions("assets/rooms/combat/l-hall.txt", WithEnemies: true), options.Room);
        Assert.True(options.IsGenerated);
    }

    [Theory]
    [InlineData("--room", "assets/rooms/combat/l-hall.txt", "--no-enemies")]
    [InlineData("--no-enemies", "--room", "assets/rooms/combat/l-hall.txt")]
    public void No_enemies_leaves_them_out_wherever_it_stands(string first, string second, string third)
    {
        var options = LaunchOptions.Parse([first, second, third]);

        Assert.Equal(new RoomOptions("assets/rooms/combat/l-hall.txt", WithEnemies: false), options.Room);
    }

    [Fact]
    public void Room_goes_with_seed_weapons_and_the_entrance_photo()
    {
        var options = LaunchOptions.Parse(
            ["--room", "assets/rooms/combat/l-hall.txt", "--seed", "7", "--give", "club", "--photo", "entrance", "--out", "pictures"]);

        Assert.Equal(7, options.Seed);
        Assert.Equal([WeaponKind.Club], options.Give);
        Assert.Equal(RoomOptions.EntrancePhoto, options.Photo);
        Assert.Equal("pictures", options.PhotoFolder);
        Assert.NotNull(options.Room);
    }

    [Theory]
    [InlineData("--room")]
    [InlineData("--room", "--seed", "3")]
    [InlineData("--room", " ")]
    public void Room_without_a_file_is_an_error(params string[] args)
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(args));

        Assert.Contains("'--room' needs a room template file", error.Message);
    }

    [Fact]
    public void Room_may_come_only_once()
    {
        var error = Assert.Throws<ArgumentException>(
            () => LaunchOptions.Parse(["--room", "assets/rooms/combat/l-hall.txt", "--room", "assets/rooms/boss/great-hall.txt"]));

        Assert.Contains("only once", error.Message);
    }

    [Fact]
    public void No_enemies_without_a_room_is_an_error()
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--no-enemies"]));

        Assert.Contains("'--no-enemies' only goes with '--room <file>'", error.Message);
    }

    [Theory]
    [InlineData("--level", "heights")]
    [InlineData("--showroom")]
    [InlineData("--clip", "all")]
    [InlineData("--bench", "room-combat")]
    public void Room_cannot_be_combined_with_another_place(params string[] other)
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--room", "assets/rooms/combat/l-hall.txt", .. other]));

        Assert.Contains("'--room'", error.Message);
    }

    [Theory]
    [InlineData("all")]
    [InlineData("room-combat")]
    public void With_a_room_the_only_photo_is_the_entrance(string scene)
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--room", "assets/rooms/combat/l-hall.txt", "--photo", scene]));

        Assert.Contains("only photo is '--photo entrance'", error.Message);
    }

    [Fact]
    public void The_entrance_photo_needs_a_room()
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--photo", "entrance"]));

        Assert.Contains("needs a room", error.Message);
    }

    [Fact]
    public void The_unknown_option_message_names_room()
    {
        var error = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--rooms"]));

        Assert.Contains("--room <file>", error.Message);
        Assert.Contains("--no-enemies", error.Message);
    }
}
