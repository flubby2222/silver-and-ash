namespace SilverAndAsh.Core.Gameplay;

/// <summary>A page of the menu a player opens during a run (issue #348): the map page (Tab) or the equipment page (G).</summary>
public enum PlayerMenuPage
{
    /// <summary>Values and boons on the left, the map on the right (Tab, issue #347).</summary>
    Map,

    /// <summary>The weapons and items carried - the inventory of decision record 0008 (G).</summary>
    Equipment,
}

/// <summary>A key of the menu, pressed in one tick (issue #348).</summary>
public enum MenuKey
{
    /// <summary>Tab: opens the map page, closes it, or turns to it from the equipment page.</summary>
    Map,

    /// <summary>G: opens the equipment page, closes it, or turns to it from the map page.</summary>
    Equipment,

    /// <summary>Esc: closes the menu, whichever page is open.</summary>
    Close,
}

/// <summary>
/// The menu of one player with its two pages (issue #348): closed, or open on <see cref="Page"/>. The key of a page opens
/// it, the same key closes it again, the key of the other page turns to that page; Esc closes. The world goes on
/// meanwhile, and only the player closes it - neither moving nor a hit does (issue #346).
/// </summary>
/// <param name="Page">The page shown, or null while the menu is closed.</param>
public readonly record struct PlayerMenu(PlayerMenuPage? Page)
{
    /// <summary>The menu closed.</summary>
    public static PlayerMenu Closed => default;

    /// <summary>True while a page is shown.</summary>
    public bool IsOpen => Page is not null;

    /// <summary>The menu after <paramref name="key"/> was pressed.</summary>
    public PlayerMenu Press(MenuKey key)
    {
        var page = key switch
        {
            MenuKey.Map => PlayerMenuPage.Map,
            MenuKey.Equipment => PlayerMenuPage.Equipment,
            _ => (PlayerMenuPage?)null,
        };
        return page is null || page == Page ? Closed : new PlayerMenu(page);
    }
}
