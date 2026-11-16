using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>The inventory (G): six places, equipping, swapping, dropping, closing (decision record 0008, issue #193).</summary>
public class InventoryTests
{
    private static Player NewPlayer() => new(LevelParser.Parse("P"));

    private static PlayerInput Toggle => PlayerInput.None with { ToggleInventory = true };

    private static List<GameEvent> Click(Player player, InventoryPlace place, int index, bool drop = false)
    {
        player.Tick(PlayerInput.None with { InventoryClick = new InventoryClick(place, index, drop) });
        return [.. player.Events];
    }

    private static Player OpenPlayer()
    {
        var player = NewPlayer();
        player.Tick(Toggle);
        return player;
    }

    [Fact]
    public void G_opens_and_closes_the_inventory()
    {
        var player = NewPlayer();
        Assert.False(player.IsInventoryOpen);

        player.Tick(Toggle);
        Assert.True(player.IsInventoryOpen);

        player.Tick(PlayerInput.None);
        Assert.True(player.IsInventoryOpen);

        player.Tick(Toggle);
        Assert.False(player.IsInventoryOpen);
    }

    [Theory]
    [InlineData("walk")]
    [InlineData("jump")]
    [InlineData("shift")]
    public void Moving_closes_the_inventory(string move)
    {
        var player = OpenPlayer();

        player.Tick(move switch
        {
            "walk" => PlayerInput.None with { Move = new Vector2(0f, 1f) },
            "jump" => PlayerInput.None with { Jump = true },
            _ => PlayerInput.None with { Dash = true },
        });

        Assert.False(player.IsInventoryOpen);
    }

    [Fact]
    public void A_hit_closes_the_inventory()
    {
        var player = OpenPlayer();

        player.TakeDamage(1, []);

        Assert.False(player.IsInventoryOpen);
    }

    [Fact]
    public void The_world_goes_on_while_the_inventory_is_open()
    {
        var world = new GameWorld(LevelParser.Parse("P"), 1);
        world.Tick(Toggle);
        var ticks = world.Statistics.Ticks;

        world.Tick(PlayerInput.None);

        Assert.True(world.Player.IsInventoryOpen);
        Assert.Equal(ticks + 1, world.Statistics.Ticks);
    }

    [Fact]
    public void A_click_does_nothing_while_the_inventory_is_closed()
    {
        var player = NewPlayer();
        player.Stow(WeaponKind.Club);

        Click(player, InventoryPlace.Bag, 0);

        Assert.Equal(WeaponKind.SilverBlade, player.Melee);
        Assert.Equal(WeaponKind.Club, player.Bag[0]);
    }

    [Fact]
    public void The_bag_has_six_places_and_takes_no_seventh_weapon()
    {
        var player = NewPlayer();
        Assert.Equal(Player.InventorySlotCount, player.Bag.Count);
        Assert.All(player.Bag, place => Assert.Null(place));

        for (var i = 0; i < Player.InventorySlotCount; i++)
        {
            Assert.True(player.Stow(WeaponKind.Rifle));
        }

        Assert.False(player.Stow(WeaponKind.Club));
        Assert.Equal(Player.InventorySlotCount, player.Bag.Count);
        Assert.DoesNotContain(WeaponKind.Club, player.Bag);
    }

    [Fact]
    public void A_left_click_on_a_melee_weapon_swaps_it_with_the_one_in_the_hand()
    {
        var player = OpenPlayer();
        player.Stow(WeaponKind.Club);

        Click(player, InventoryPlace.Bag, 0);

        Assert.Equal(WeaponKind.Club, player.Melee);
        Assert.Equal(WeaponKind.SilverBlade, player.Bag[0]);
    }

    [Fact]
    public void A_left_click_on_a_ranged_weapon_puts_it_into_the_first_free_ranged_place()
    {
        var player = OpenPlayer();
        player.Stow(WeaponKind.Rifle);

        Click(player, InventoryPlace.Bag, 0);

        Assert.Equal(WeaponKind.Rifle, player.RangedSlots[1]!.Kind);
        Assert.Null(player.Bag[0]);
        Assert.Equal(WeaponKind.Pistol, player.Ranged.Kind);
    }

    [Fact]
    public void With_all_ranged_places_taken_a_left_click_swaps_with_the_weapon_in_the_hand()
    {
        var player = OpenPlayer();
        player.Equip(1, WeaponKind.Rifle);
        player.Equip(2, WeaponKind.Blunderbuss);
        player.Tick(PlayerInput.None with { SelectSlot = 1 });
        player.Stow(WeaponKind.Pistol);

        var events = Click(player, InventoryPlace.Bag, 0);

        Assert.Equal(1, player.SelectedSlot);
        Assert.Equal(WeaponKind.Pistol, player.Ranged.Kind);
        Assert.Equal(WeaponKind.Rifle, player.Bag[0]);
        Assert.Equal(WeaponKind.Pistol, player.RangedSlots[0]!.Kind);
        Assert.Equal(WeaponKind.Blunderbuss, player.RangedSlots[2]!.Kind);
        Assert.Contains(new WeaponSwitched(player.Id, 1, WeaponKind.Pistol), events);
    }

    [Fact]
    public void A_right_click_drops_a_weapon_from_the_bag_and_it_lies_on_the_floor()
    {
        var world = new GameWorld(LevelParser.Parse("P"), 1);
        world.Player.Stow(WeaponKind.Club);
        world.Tick(Toggle);

        world.Tick(PlayerInput.None with { InventoryClick = new InventoryClick(InventoryPlace.Bag, 0, Drop: true) });

        Assert.Null(world.Player.Bag[0]);
        Assert.Contains(world.Events, gameEvent => gameEvent is WeaponDropped { Kind: WeaponKind.Club });
        var lying = Assert.Single(world.WeaponDrops);
        Assert.Equal(WeaponKind.Club, lying.Kind);

        // It falls to the floor and stays there; walking over it does not take it (that is F, with #194).
        for (var tick = 0; tick < 120; tick++)
        {
            world.Tick(PlayerInput.None);
        }

        Assert.Single(world.WeaponDrops);
        Assert.Equal(world.Level[0, 0].FloorHeight, world.WeaponDrops[0].Position.Y, 3);
        Assert.Null(world.Player.Bag[0]);
    }

    [Fact]
    public void A_right_click_drops_an_equipped_ranged_weapon_and_the_next_one_comes_into_the_hand()
    {
        var player = OpenPlayer();
        player.Equip(2, WeaponKind.Rifle);

        var events = Click(player, InventoryPlace.Ranged, 0, drop: true);

        Assert.Null(player.RangedSlots[0]);
        Assert.Equal(2, player.SelectedSlot);
        Assert.Equal(WeaponKind.Rifle, player.Ranged.Kind);
        Assert.Contains(new WeaponDropped(player.Id, WeaponKind.Pistol, player.Position), events);
        Assert.Contains(new WeaponSwitched(player.Id, 2, WeaponKind.Rifle), events);
    }

    [Fact]
    public void The_last_ranged_weapon_cannot_be_dropped()
    {
        var player = OpenPlayer();

        var events = Click(player, InventoryPlace.Ranged, 0, drop: true);

        Assert.Equal(WeaponKind.Pistol, player.Ranged.Kind);
        Assert.Same(player.RangedSlots[0], player.Ranged);
        Assert.DoesNotContain(events, gameEvent => gameEvent is WeaponDropped);
    }

    [Fact]
    public void The_melee_hand_is_never_empty()
    {
        var player = OpenPlayer();

        var events = Click(player, InventoryPlace.Melee, 0, drop: true);

        Assert.Equal(WeaponKind.SilverBlade, player.Melee);
        Assert.DoesNotContain(events, gameEvent => gameEvent is WeaponDropped);
    }

    [Fact]
    public void Clicks_on_empty_or_unknown_places_do_nothing()
    {
        var player = OpenPlayer();

        Click(player, InventoryPlace.Bag, 0);
        Click(player, InventoryPlace.Bag, 0, drop: true);
        Click(player, InventoryPlace.Bag, Player.InventorySlotCount);
        Click(player, InventoryPlace.Ranged, -1, drop: true);
        Click(player, InventoryPlace.Ranged, 1, drop: true);

        Assert.All(player.Bag, place => Assert.Null(place));
        Assert.Equal(WeaponKind.Pistol, player.Ranged.Kind);
        Assert.True(player.IsInventoryOpen);
    }

    [Fact]
    public void A_dead_player_has_no_open_inventory()
    {
        var player = OpenPlayer();
        player.TakeDamage(player.MaxHealth, []);
        player.Tick(Toggle);

        Assert.False(player.IsInventoryOpen);
    }
}
