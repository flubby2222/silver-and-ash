using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests;

/// <summary>
/// The test option <c>--give</c> (issue #791): weapons and items by name, and where each lands with the player.
/// </summary>
public class GiveOptionsTests
{
    private static readonly Level Corridor = LevelParser.Parse("P\n.\n.");

    [Fact]
    public void Every_weapon_and_item_can_be_found_by_its_name_in_any_case()
    {
        Assert.All(Equipment.All, piece =>
        {
            var name = piece.Weapon?.ToString() ?? piece.Item!.Value.ToString();
            Assert.Equal(piece, GiveOptions.Find(name.ToLowerInvariant()));
            Assert.Equal(piece, GiveOptions.Find(name.ToUpperInvariant()));
        });
    }

    [Theory]
    [InlineData("sword")]
    [InlineData("0")]
    [InlineData(" club")]
    public void An_unknown_name_is_rejected_with_the_known_ones(string name)
    {
        var error = Assert.Throws<ArgumentException>(() => GiveOptions.Find(name));

        Assert.Equal($"Unknown weapon '{name}'. Known: {GiveOptions.Known}.", error.Message);
    }

    [Fact]
    public void The_known_names_are_every_weapon_then_every_item_in_lower_case()
    {
        var names = Enum.GetNames<WeaponKind>().Concat(Enum.GetNames<ItemKind>()).Select(name => name.ToLowerInvariant());

        Assert.Equal(string.Join(", ", names), GiveOptions.Known);
    }

    [Fact]
    public void A_given_item_is_carried_from_the_start()
    {
        var world = new GameWorld(Corridor);

        GiveOptions.GiveTo(world, [Equipment.Of(ItemKind.Leech), Equipment.Of(ItemKind.AshenHeart)]);

        Assert.Equal([ItemKind.Leech, ItemKind.AshenHeart], world.Player.Items);
    }

    [Fact]
    public void A_given_melee_weapon_goes_into_the_hand_and_the_one_before_into_the_bag()
    {
        var world = new GameWorld(Corridor);
        var before = world.Player.Melee;

        GiveOptions.GiveTo(world, [Equipment.Of(WeaponKind.Club)]);

        Assert.Equal(WeaponKind.Club, world.Player.Melee);
        Assert.Contains(before, world.Player.Bag);
        Assert.Empty(world.Player.Items);
    }

    [Fact]
    public void A_given_ranged_weapon_goes_into_the_first_free_place()
    {
        var world = new GameWorld(Corridor);
        var free = world.Player.RangedSlots.ToList().FindIndex(weapon => weapon is null);

        GiveOptions.GiveTo(world, [Equipment.Of(WeaponKind.Rifle)]);

        Assert.Equal(WeaponKind.Rifle, world.Player.RangedSlots[free]!.Kind);
    }
}
