using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// The menu with two pages (issue #348): Tab opens the map page, G the equipment page; the same key closes, the other
/// turns the page; Esc closes.
/// </summary>
public class PlayerMenuTests
{
    private static Player NewPlayer() => new(LevelParser.Parse("P"));

    private static PlayerInput Press(MenuKey key) => PlayerInput.None with { PressMenu = key };

    // Every page (null: closed) and every key, with the page after it.
    [Theory]
    [InlineData(null, MenuKey.Map, PlayerMenuPage.Map)]
    [InlineData(null, MenuKey.Equipment, PlayerMenuPage.Equipment)]
    [InlineData(null, MenuKey.Close, null)]
    [InlineData(PlayerMenuPage.Map, MenuKey.Map, null)]
    [InlineData(PlayerMenuPage.Map, MenuKey.Equipment, PlayerMenuPage.Equipment)]
    [InlineData(PlayerMenuPage.Map, MenuKey.Close, null)]
    [InlineData(PlayerMenuPage.Equipment, MenuKey.Map, PlayerMenuPage.Map)]
    [InlineData(PlayerMenuPage.Equipment, MenuKey.Equipment, null)]
    [InlineData(PlayerMenuPage.Equipment, MenuKey.Close, null)]
    public void A_key_turns_the_menu_to_its_page(PlayerMenuPage? page, MenuKey key, PlayerMenuPage? after)
    {
        var menu = new PlayerMenu(page).Press(key);

        Assert.Equal(after, menu.Page);
        Assert.Equal(after is not null, menu.IsOpen);
    }

    [Fact]
    public void A_new_player_has_the_menu_closed()
    {
        Assert.Equal(PlayerMenu.Closed, NewPlayer().Menu);
        Assert.False(NewPlayer().Menu.IsOpen);
    }

    [Fact]
    public void Tab_and_G_turn_the_pages_both_ways_and_the_key_of_the_open_page_closes()
    {
        var player = NewPlayer();

        player.Tick(Press(MenuKey.Map));
        Assert.Equal(PlayerMenuPage.Map, player.Menu.Page);
        Assert.False(player.IsInventoryOpen);

        player.Tick(Press(MenuKey.Equipment));
        Assert.Equal(PlayerMenuPage.Equipment, player.Menu.Page);
        Assert.True(player.IsInventoryOpen);

        player.Tick(Press(MenuKey.Map));
        Assert.Equal(PlayerMenuPage.Map, player.Menu.Page);

        player.Tick(PlayerInput.None);
        Assert.Equal(PlayerMenuPage.Map, player.Menu.Page);

        player.Tick(Press(MenuKey.Map));
        Assert.False(player.Menu.IsOpen);
    }

    [Theory]
    [InlineData(MenuKey.Map)]
    [InlineData(MenuKey.Equipment)]
    public void Esc_closes_either_page(MenuKey open)
    {
        var player = NewPlayer();
        player.Tick(Press(open));
        Assert.True(player.Menu.IsOpen);

        player.Tick(Press(MenuKey.Close));

        Assert.False(player.Menu.IsOpen);
    }

    [Fact]
    public void A_click_does_nothing_on_the_map_page()
    {
        // The click goes to the equipment page only: on the map page the bag stays as it is.
        var player = NewPlayer();
        player.Stow(WeaponKind.Rifle);
        player.Tick(Press(MenuKey.Map));

        player.Tick(PlayerInput.None with { InventoryClick = new InventoryClick(InventoryPlace.Bag, 0, Drop: true) });

        Assert.Equal(WeaponKind.Rifle, player.Bag[0]);
        Assert.DoesNotContain(player.Events, e => e is WeaponDropped);
    }

    [Fact]
    public void A_dead_player_has_the_menu_closed_and_cannot_open_it()
    {
        var player = NewPlayer();
        player.Tick(Press(MenuKey.Map));
        player.TakeDamage(player.MaxHealth, []);

        player.Tick(PlayerInput.None);
        Assert.False(player.Menu.IsOpen);

        player.Tick(Press(MenuKey.Map));
        Assert.False(player.Menu.IsOpen);
    }
}
