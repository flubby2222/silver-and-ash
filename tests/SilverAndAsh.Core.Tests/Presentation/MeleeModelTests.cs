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

    [Theory]
    [InlineData(WeaponKind.SilverBlade)]
    [InlineData(WeaponKind.Club)]
    public void A_melee_model_matches_its_files_in_the_repository(WeaponKind kind)
    {
        var files = WeaponPose.MeleeModel(kind)!;

        var weapon = GlbScene.Read(File.ReadAllBytes(RepositoryFiles.Find($"assets/models/{files.Weapon}")));
        var hand = GlbScene.Read(File.ReadAllBytes(RepositoryFiles.Find($"assets/models/{files.Hand}")));

        Assert.Equal(files.Parts.Select(part => part.Name), weapon.Select(part => part.Name));
        Assert.Equal(WeaponPose.HandParts.Select(part => part.Name), hand.Select(part => part.Name));
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
