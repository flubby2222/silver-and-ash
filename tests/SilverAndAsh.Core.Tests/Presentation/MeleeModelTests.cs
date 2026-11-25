using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Core.Tests.Presentation;

public class MeleeModelTests
{
    [Fact]
    public void The_club_gets_its_own_model_and_a_sleeve()
    {
        var files = WeaponPose.MeleeModel(WeaponKind.Club);

        Assert.NotNull(files);
        Assert.Equal("club.glb", files.Weapon);
        Assert.Equal("club_hand.glb", files.Hand);
    }

    [Fact]
    public void The_axe_gets_its_own_model_in_the_club_sleeve_bent_at_the_grip()
    {
        // Issue #776: a table entry instead of the box placeholder; the axe is held like the club.
        var files = WeaponPose.MeleeModel(WeaponKind.HuntersAxe);

        Assert.NotNull(files);
        Assert.Equal("hunters_axe.glb", files.Weapon);
        Assert.Equal("club_hand.glb", files.Hand);
        Assert.True(files.BendsAtGrip);
        Assert.False(WeaponPose.MeleeModel(WeaponKind.SilverBlade)!.BendsAtGrip);
    }

    [Fact]
    public void The_axe_swings_in_seven_tenths_of_its_cooldown_from_the_balance_file()
    {
        // Issue #776 (provisional): the heavy axe swings slower, following its cooldown; a second value checks it is
        // a share and not a fixed time.
        var cooldown = Balance.Default.HuntersAxe.Cooldown;

        Assert.Equal(0.7f * cooldown, WeaponPose.SwingSeconds(WeaponKind.HuntersAxe, cooldown), 5);
        Assert.Equal(0.7f * 2f * cooldown, WeaponPose.SwingSeconds(WeaponKind.HuntersAxe, 2f * cooldown), 5);
        Assert.True(WeaponPose.SwingSeconds(WeaponKind.HuntersAxe, cooldown) > WeaponPose.FixedSwingSeconds);
    }

    [Theory]
    [InlineData(WeaponKind.SilverBlade)]
    [InlineData(WeaponKind.Club)]
    public void Blade_and_club_keep_their_fixed_swing_whatever_the_cooldown(WeaponKind kind)
    {
        // Issue #776: only the axe follows its cooldown; the club stays as fast as before.
        Assert.Equal(WeaponPose.FixedSwingSeconds, WeaponPose.SwingSeconds(kind, Balance.Default.Melee(kind).Cooldown));
        Assert.Equal(WeaponPose.FixedSwingSeconds, WeaponPose.SwingSeconds(kind, 3f));
        Assert.Equal(0.28f, WeaponPose.FixedSwingSeconds);
    }

    [Fact]
    public void Only_the_axe_and_the_whip_are_drawn_smaller_than_their_files()
    {
        // Issue #776: the axe is scaled so its head stays within reach (ClubBendTests); blade and club stay as drawn.
        // Issue #786: the whip as well, so its long handle ends inside the cuff (MeleeHandleInCuffTests) and its loop
        // stays within reach (WhipPoseTests).
        Assert.True(WeaponPose.MeleeModel(WeaponKind.HuntersAxe)!.Scale is > 0.9f and < 1f);
        Assert.True(WeaponPose.MeleeModel(WeaponKind.Whip)!.Scale is > 0.5f and < 1f);
        Assert.Equal(1f, WeaponPose.MeleeModel(WeaponKind.Club)!.Scale);
        Assert.Equal(1f, WeaponPose.MeleeModel(WeaponKind.SilverBlade)!.Scale);
    }

    [Fact]
    public void The_whip_gets_a_rest_and_a_strike_file_in_the_club_sleeve_bent_at_the_grip()
    {
        // Issue #786: a table entry instead of the box placeholder; only the whip has a second file for the strike.
        var files = WeaponPose.MeleeModel(WeaponKind.Whip);

        Assert.NotNull(files);
        Assert.Equal("whip_rest.glb", files.Weapon);
        Assert.Equal("whip_strike.glb", files.StrikeWeapon);
        Assert.Equal("club_hand.glb", files.Hand);
        Assert.True(files.BendsAtGrip);
        Assert.Null(WeaponPose.MeleeModel(WeaponKind.HuntersAxe)!.StrikeWeapon);
    }

    [Fact]
    public void The_whip_swings_in_nine_tenths_of_its_cooldown_from_the_balance_file()
    {
        // Issue #786 (provisional): the lash rolls out slowly, following the whip's cooldown; a second value checks it
        // is a share and not a fixed time.
        var cooldown = Balance.Default.Whip.Cooldown;

        Assert.Equal(0.9f * cooldown, WeaponPose.SwingSeconds(WeaponKind.Whip, cooldown), 5);
        Assert.Equal(0.9f * 2f * cooldown, WeaponPose.SwingSeconds(WeaponKind.Whip, 2f * cooldown), 5);
    }

    [Theory]
    [InlineData(WeaponKind.SilverBlade)]
    [InlineData(WeaponKind.Club)]
    [InlineData(WeaponKind.HuntersAxe)]
    [InlineData(WeaponKind.Whip)]
    public void A_melee_model_matches_its_files_in_the_repository(WeaponKind kind)
    {
        var files = WeaponPose.MeleeModel(kind)!;

        var weapon = GlbScene.Read(File.ReadAllBytes(RepositoryFiles.Find($"assets/models/{files.Weapon}")));
        var hand = GlbScene.Read(File.ReadAllBytes(RepositoryFiles.Find($"assets/models/{files.Hand}")));

        Assert.Equal(files.Parts.Select(part => part.Name), weapon.Select(part => part.Name));
        Assert.Equal(WeaponPose.HandParts.Select(part => part.Name), hand.Select(part => part.Name));
        if (files.StrikeWeapon is { } strikeFile)
        {
            var strike = GlbScene.Read(File.ReadAllBytes(RepositoryFiles.Find($"assets/models/{strikeFile}")));
            Assert.Equal(files.Parts.Select(part => part.Name), strike.Select(part => part.Name));
        }
    }

    [Theory]
    [InlineData(WeaponKind.Pistol)]
    [InlineData(WeaponKind.Rifle)]
    [InlineData(WeaponKind.Blunderbuss)]
    public void Ranged_weapons_have_no_melee_model(WeaponKind kind)
    {
        Assert.Null(WeaponPose.MeleeModel(kind));
    }
}
