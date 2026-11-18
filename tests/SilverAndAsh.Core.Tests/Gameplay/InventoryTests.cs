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
    public void Moving_keeps_the_inventory_open(string move)
    {
        // Only the player closes it (issue #346): neither walking, jumping nor Shift does.
        var player = OpenPlayer();

        player.Tick(move switch
        {
            "walk" => PlayerInput.None with { Move = new Vector2(0f, 1f) },
            "jump" => PlayerInput.None with { Jump = true },
            _ => PlayerInput.None with { Dash = true },
        });

        Assert.True(player.IsInventoryOpen);
    }

    [Fact]
    public void A_hit_keeps_the_inventory_open()
    {
        // Only the player closes it (issue #346): a hit does not - also not in the tick after it.
        var player = OpenPlayer();

        player.TakeDamage(1, []);
        Assert.True(player.IsInventoryOpen);

        player.Tick(PlayerInput.None);
        Assert.True(player.IsInventoryOpen);
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
        Assert.Contains(events, gameEvent => gameEvent is WeaponDropped { Kind: WeaponKind.Pistol } dropped
            && dropped.PlayerId == player.Id && dropped.Position == player.Position);
        Assert.Contains(new WeaponSwitched(player.Id, 2, WeaponKind.Rifle), events);
    }

    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };

    [Fact]
    public void Opening_while_running_keeps_the_inventory_open()
    {
        var player = NewPlayer();

        player.Tick(Forward with { ToggleInventory = true });
        for (var tick = 0; tick < 30; tick++)
        {
            player.Tick(Forward);
        }

        Assert.True(player.IsInventoryOpen);
    }

    [Fact]
    public void A_held_move_let_go_and_pressed_anew_keeps_the_inventory_open()
    {
        // Only the player closes it (issue #346): a move pressed anew after opening does not.
        var player = NewPlayer();
        player.Tick(Forward with { ToggleInventory = true });
        player.Tick(Forward);

        player.Tick(PlayerInput.None);
        Assert.True(player.IsInventoryOpen);

        player.Tick(Forward);
        Assert.True(player.IsInventoryOpen);
    }

    [Fact]
    public void Pressing_another_direction_while_running_keeps_the_inventory_open()
    {
        // Only the player closes it (issue #346): a new direction while running does not.
        var player = NewPlayer();
        player.Tick(Forward with { ToggleInventory = true });
        player.Tick(Forward);

        player.Tick(Forward with { Move = new Vector2(1f, 1f) });

        Assert.True(player.IsInventoryOpen);
    }

    [Fact]
    public void Jump_or_Shift_pressed_anew_keep_the_inventory_open()
    {
        // Only the player closes it (issue #346): Jump or Shift let go and pressed anew do not.
        var player = NewPlayer();
        player.Tick(PlayerInput.None with { Jump = true, Dash = true, ToggleInventory = true });
        player.Tick(PlayerInput.None with { Jump = true, Dash = true });
        Assert.True(player.IsInventoryOpen);

        player.Tick(PlayerInput.None with { Dash = true });
        Assert.True(player.IsInventoryOpen);

        player.Tick(PlayerInput.None with { Jump = true, Dash = true });
        Assert.True(player.IsInventoryOpen);

        player.Tick(Toggle);
        Assert.False(player.IsInventoryOpen);
    }

    /// <summary>All three ranged places taken (pistol in the hand), the rifle in the bag.</summary>
    private static Player FullHandsWithRifleInTheBag()
    {
        var player = OpenPlayer();
        player.Equip(1, WeaponKind.Blunderbuss);
        player.Equip(2, WeaponKind.Blunderbuss);
        player.Stow(WeaponKind.Rifle);
        return player;
    }

    [Fact]
    public void A_ranged_weapon_keeps_its_magazine_in_the_bag()
    {
        var player = FullHandsWithRifleInTheBag();
        var pistol = player.Ranged;
        while (pistol.Magazine.TryTake())
        {
        }

        Click(player, InventoryPlace.Bag, 0);
        Assert.Same(pistol, player.BagRanged(0));
        Assert.Equal(WeaponKind.Rifle, player.Ranged.Kind);

        Click(player, InventoryPlace.Bag, 0);

        Assert.Same(pistol, player.Ranged);
        Assert.Equal(0, player.Ranged.Magazine.Rounds);
        Assert.Equal(WeaponKind.Rifle, player.Bag[0]);
    }

    [Fact]
    public void Putting_a_weapon_into_the_bag_breaks_its_reload_off()
    {
        var player = FullHandsWithRifleInTheBag();
        var pistol = player.Ranged;
        pistol.Magazine.TryTake();
        var rounds = pistol.Magazine.Rounds;
        Assert.True(pistol.Magazine.StartReload());

        Click(player, InventoryPlace.Bag, 0);
        for (var tick = 0; tick < SimulationClock.SecondsToTicks(Balance.Default.Pistol.ReloadTime) + 10; tick++)
        {
            player.Tick(PlayerInput.None);
        }

        Assert.False(pistol.Magazine.IsReloading);
        Assert.Equal(rounds, player.BagRanged(0)!.Magazine.Rounds);
    }

    [Fact]
    public void A_dropped_ranged_weapon_keeps_its_magazine()
    {
        var world = new GameWorld(LevelParser.Parse("P"), 1);
        world.Player.Equip(1, WeaponKind.Rifle);
        var rifle = world.Player.RangedSlots[1]!;
        rifle.Magazine.TryTake();
        world.Tick(Toggle);

        world.Tick(PlayerInput.None with { InventoryClick = new InventoryClick(InventoryPlace.Ranged, 1, Drop: true) });

        var lying = Assert.Single(world.WeaponDrops);
        Assert.Same(rifle, lying.Ranged);
        Assert.Equal(rifle.Magazine.Size - 1, lying.Ranged!.Magazine.Rounds);
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
