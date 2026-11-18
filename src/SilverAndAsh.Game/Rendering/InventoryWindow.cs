using System.Globalization;
using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The equipment page of the menu (G, decision record 0008, issues #193, #348), Tab turns to the map page: the weapons in hand on top (melee and the three ranged places),
/// the six places of the bag below, the items with their effect on the right. The game goes on behind it; only G or Esc
/// close it, neither moving nor a hit (issue #346). A left click equips, a right click drops - every frame is in the colour of the weapon's
/// tier. It only reads and shows - the click goes to Core as input (<see cref="Core.Input.PlayerInput.InventoryClick"/>).
/// </summary>
internal static class InventoryWindow
{
    /// <summary>Seconds after opening before a click counts - like the bonus choice, so a click meant for the blade does not land here.</summary>
    public const float ClickDelaySeconds = BoonWindow.ClickDelaySeconds;

    private const int BoxWidth = 150;
    private const int BoxHeight = 54;
    private const int Gap = 12;
    private const int Columns = 4;
    private const int ItemsWidth = 260;
    private const int Padding = 24;

    private static readonly Color Shade = new(10, 6, 12, 60);
    private static readonly Color Panel = new(22, 18, 26, 225);
    private static readonly Color Box = new(32, 26, 36, 230);
    private static readonly Color BoxHovered = new(58, 44, 52, 245);
    private static readonly Color Gold = new(232, 196, 96, 255);
    private static readonly Color Text = new(220, 220, 230, 255);
    private static readonly Color Muted = new(150, 150, 165, 255);
    private static readonly Color Empty = new(90, 88, 98, 255);

    /// <summary>
    /// The click of this frame (left: equip, right: drop) on the place under the cursor, once the window has been open for
    /// <see cref="ClickDelaySeconds"/> - <paramref name="openSeconds"/>; null without a click or off the places.
    /// </summary>
    public static InventoryClick? Pick(float openSeconds)
    {
        if (openSeconds < ClickDelaySeconds)
        {
            return null;
        }

        var drop = Raylib.IsMouseButtonPressed(MouseButton.Right);
        if (!drop && !Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            return null;
        }

        return Hovered() is { } place ? place with { Drop = drop } : null;
    }

    /// <summary>Draws the window for <paramref name="player"/>.</summary>
    public static void Draw(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var seconds = (float)Raylib.GetTime();
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), Shade);

        var (left, top, width, height) = PanelRect();
        Raylib.DrawRectangle(left, top, width, height, Panel);
        Raylib.DrawRectangleLines(left, top, width, height, Muted);
        MenuTabs.Draw(PlayerMenuPage.Equipment, Raylib.GetScreenWidth() / 2);
        PixelText.Draw("EQUIPMENT", left + Padding, top + 14, 28, Gold);

        var hovered = Hovered();
        var equipTop = BoxAt(new InventoryClick(InventoryPlace.Melee, 0)).Y;
        PixelText.Draw("IN HAND", left + Padding, (int)equipTop - 22, 16, Muted);
        DrawBox(new InventoryClick(InventoryPlace.Melee, 0), "LMB", Weapons.Info(player.Melee), selected: true, hovered, seconds);
        for (var slot = 0; slot < Player.RangedSlotCount; slot++)
        {
            var key = string.Create(CultureInfo.InvariantCulture, $"{slot + 1}");
            DrawBox(new InventoryClick(InventoryPlace.Ranged, slot), key, player.RangedSlots[slot]?.Info, slot == player.SelectedSlot, hovered, seconds);
        }

        var bagTop = BoxAt(new InventoryClick(InventoryPlace.Bag, 0)).Y;
        var used = player.Bag.Count(place => place is not null);
        PixelText.Draw(string.Create(CultureInfo.InvariantCulture, $"BAG  {used} / {Player.InventorySlotCount}"), left + Padding, (int)bagTop - 22, 16, Muted);
        for (var index = 0; index < Player.InventorySlotCount; index++)
        {
            WeaponInfo? info = player.Bag[index] is { } kind ? Weapons.Info(kind) : null;
            DrawBox(new InventoryClick(InventoryPlace.Bag, index), string.Empty, info, selected: false, hovered, seconds);
        }

        // The items (decision record 0008) come with their own issue; until then the list is empty.
        var itemsLeft = left + Padding + (Columns * BoxWidth) + ((Columns - 1) * Gap) + Padding;
        Raylib.DrawLine(itemsLeft - (Padding / 2), (int)equipTop - 24, itemsLeft - (Padding / 2), top + height - 48, Empty);
        PixelText.Draw("ITEMS", itemsLeft, (int)equipTop - 22, 16, Muted);
        PixelText.Draw("No items yet", itemsLeft, (int)equipTop, 16, Empty);

        const string Help = "Left click: equip   Right click: drop   [G] or [Esc] close   [Tab] map";
        PixelText.Draw(Help, left + ((width - PixelText.Measure(Help, 16)) / 2), top + height - 32, 16, Muted);

        if (hovered is { Place: InventoryPlace.Ranged } && player.RangedSlots.Count(weapon => weapon is not null) == 1
            && player.RangedSlots[hovered.Value.Index] is not null)
        {
            const string Last = "Your last ranged weapon stays";
            PixelText.Draw(Last, left + ((width - PixelText.Measure(Last, 16)) / 2), top + height - 54, 16, Gold);
        }
    }

    private static void DrawBox(InventoryClick place, string key, WeaponInfo? weapon, bool selected, InventoryClick? hovered, float seconds)
    {
        var box = BoxAt(place);
        var isHovered = hovered is { } h && h.Place == place.Place && h.Index == place.Index;
        Raylib.DrawRectangleRec(box, isHovered ? BoxHovered : Box);
        var x = (int)box.X + 8;
        var y = (int)box.Y + 6;
        if (weapon is not { } info)
        {
            Raylib.DrawRectangleLinesEx(box, 1f, Empty);
            PixelText.Draw(key, x, y, 12, Empty);
            PixelText.Draw("-", x, y + 20, 18, Empty);
            return;
        }

        var frame = TierColours.Of(info.Tier, seconds);
        Raylib.DrawRectangleLinesEx(box, selected || isHovered ? 3f : 2f, frame);
        PixelText.Draw(key, x, y, 12, Muted);
        PixelText.Draw(info.Name, x, y + 16, 18, selected ? Gold : Text);
        var kind = $"{(info.Hand == WeaponHand.Melee ? "Melee" : "Ranged")} - {info.Tier}";
        PixelText.Draw(kind, x, y + 36, 10, frame);
    }

    /// <summary>The place under the mouse cursor, or null.</summary>
    private static InventoryClick? Hovered()
    {
        var mouse = Raylib.GetMousePosition();
        foreach (var place in Places())
        {
            if (Raylib.CheckCollisionPointRec(mouse, BoxAt(place)))
            {
                return place;
            }
        }

        return null;
    }

    private static IEnumerable<InventoryClick> Places()
    {
        yield return new InventoryClick(InventoryPlace.Melee, 0);
        for (var slot = 0; slot < Player.RangedSlotCount; slot++)
        {
            yield return new InventoryClick(InventoryPlace.Ranged, slot);
        }

        for (var index = 0; index < Player.InventorySlotCount; index++)
        {
            yield return new InventoryClick(InventoryPlace.Bag, index);
        }
    }

    /// <summary>The window in the middle of the screen.</summary>
    private static (int Left, int Top, int Width, int Height) PanelRect()
    {
        var bagRows = (Player.InventorySlotCount + Columns - 1) / Columns;
        var width = Padding + (Columns * BoxWidth) + ((Columns - 1) * Gap) + Padding + ItemsWidth + Padding;
        var height = 60 + 26 + BoxHeight + 40 + (bagRows * BoxHeight) + ((bagRows - 1) * Gap) + 70;
        return ((Raylib.GetScreenWidth() - width) / 2, (Raylib.GetScreenHeight() - height) / 2, width, height);
    }

    /// <summary>The box of <paramref name="place"/>: the hands in one row, the bag in rows of four below.</summary>
    private static Rectangle BoxAt(InventoryClick place)
    {
        var (left, top, _, _) = PanelRect();
        var handsTop = top + 60 + 26;
        var (column, y) = place.Place switch
        {
            InventoryPlace.Melee => (0, handsTop),
            InventoryPlace.Ranged => (place.Index + 1, handsTop),
            _ => (place.Index % Columns, handsTop + BoxHeight + 40 + ((place.Index / Columns) * (BoxHeight + Gap))),
        };
        var x = left + Padding + (column * (BoxWidth + Gap));
        return new Rectangle(new Vector2(x, y), new Vector2(BoxWidth, BoxHeight));
    }
}
