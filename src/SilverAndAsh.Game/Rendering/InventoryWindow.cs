using System.Globalization;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The equipment page of the menu (G, decision record 0008, issues #193, #348, #390), Tab or a click on the map tab turns
/// to the map page: the frame of values on the left, the weapons in hand on top (melee and the three ranged places), the
/// six places of the bag below, the items under them. The game goes on behind it; only G or Esc
/// close it, neither moving nor a hit (issue #346). A left click equips, a right click drops - every frame is in the colour of the weapon's
/// tier. It only reads and shows - the click goes to Core as input (<see cref="Core.Input.PlayerInput.InventoryClick"/>).
/// Drawn in the design "C2 Slim" on the menu canvas (issue #371), so it fits every window; where what sits decides
/// <see cref="MenuLayout.Equipment"/>.
/// </summary>
internal static class InventoryWindow
{
    /// <summary>Seconds after opening before a click counts - like the bonus choice, so a click meant for the blade does not land here.</summary>
    public const float ClickDelaySeconds = BoonWindow.ClickDelaySeconds;

    private static readonly Color Shade = new(10, 6, 12, 60);
    private static readonly Color Box = new(12, 10, 16, 150);
    private static readonly Color BoxHovered = new(58, 44, 52, 200);
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

        var canvas = MenuArt.Canvas;
        return Hovered(canvas, MenuArt.Layout.Equipment(canvas, 0)) is { } place ? place with { Drop = drop } : null;
    }

    /// <summary>Draws the window for <paramref name="player"/>, with the frame of values both pages share (issue #390).</summary>
    /// <param name="world">The world whose values the frame shows.</param>
    /// <param name="player">The player whose equipment the window shows.</param>
    /// <param name="seconds">The clock of the views (in photos: the ticks run so far), for the pulse of Vampiric - not the
    /// time since the window opened, so two photos of the same scene are the same picture (issue #651).</param>
    public static void Draw(GameWorld world, Player player, float seconds)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);
        MenuArt.Shade(Shade);
        MenuTabs.DrawSheet(world, player);

        var canvas = MenuArt.Canvas;
        var layout = MenuArt.Layout;
        var used = player.Bag.Count(place => place is not null);
        var page = layout.Equipment(canvas, used);
        var hovered = Hovered(canvas, page);

        MenuArt.Draw(page.Frame, canvas);
        MenuArt.Begin(canvas);
        var divider = page.Divider;
        Raylib.DrawRectangle((int)divider.X, (int)divider.Y, (int)divider.Width, (int)divider.Height, Empty);

        var effects = world.WeaponEffects;
        DrawBox(page, new InventoryClick(InventoryPlace.Melee, 0), "LMB", player.Melee, effects, selected: true, hovered, seconds);
        for (var slot = 0; slot < Player.RangedSlotCount; slot++)
        {
            var key = string.Create(CultureInfo.InvariantCulture, $"{slot + 1}");
            DrawBox(page, new InventoryClick(InventoryPlace.Ranged, slot), key, player.RangedSlots[slot]?.Kind, effects, slot == player.SelectedSlot, hovered, seconds);
        }

        for (var index = 0; index < Player.InventorySlotCount; index++)
        {
            DrawBox(page, new InventoryClick(InventoryPlace.Bag, index), string.Empty, player.Bag[index], effects, selected: false, hovered, seconds);
        }

        if (hovered is { Place: InventoryPlace.Ranged } && player.RangedSlots.Count(weapon => weapon is not null) == 1
            && player.RangedSlots[hovered.Value.Index] is not null)
        {
            MenuArt.DrawText(page.LastWeaponNote);
        }

        MenuArt.End();
    }

    private static void DrawBox(
        EquipmentPage page, InventoryClick place, string key, WeaponKind? weapon, WeaponEffects effects, bool selected, InventoryClick? hovered, float seconds)
    {
        var box = BoxAt(page, place);
        var rect = new Rectangle(box.X, box.Y, box.Width, box.Height);
        var isHovered = hovered is { } h && h.Place == place.Place && h.Index == place.Index;
        Raylib.DrawRectangleRec(rect, isHovered ? BoxHovered : Box);
        if (weapon is not { } held)
        {
            Raylib.DrawRectangleLinesEx(rect, 1f, Empty);
            var empty = MenuArt.Layout.Box(box, key, null, null);
            MenuArt.DrawText(empty.Key);
            MenuArt.DrawText(empty.Name);
            return;
        }

        var info = Weapons.Info(held);
        var frame = TierColours.Of(info.Tier, seconds);
        Raylib.DrawRectangleLinesEx(rect, selected || isHovered ? 2f : 1f, frame);
        var kind = $"{(info.Hand == WeaponHand.Melee ? "Melee" : "Ranged")} - {info.Tier}";

        // Issue #505: a weapon with a status effect shows it after its name ("Club - Marked").
        var texts = MenuArt.Layout.Box(box, key, WeaponEffects.Label(info.Name, effects.Of(held)), kind);
        MenuArt.DrawText(texts.Key);
        MenuArt.DrawText(texts.Name with { Color = selected ? MenuTextColor.Selected : MenuTextColor.Main });
        MenuArt.DrawText(texts.Kind!.Value, frame);
    }

    /// <summary>The place under the mouse cursor, or null.</summary>
    private static InventoryClick? Hovered(MenuCanvas canvas, EquipmentPage page)
    {
        var mouse = canvas.ToCanvas(Raylib.GetMousePosition());
        foreach (var place in Places())
        {
            if (BoxAt(page, place).Contains(mouse))
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

    /// <summary>The box of <paramref name="place"/>: the hands in one row, the bag in rows of four below.</summary>
    private static MenuRect BoxAt(EquipmentPage page, InventoryClick place) => place.Place switch
    {
        InventoryPlace.Melee => page.Hands[0],
        InventoryPlace.Ranged => page.Hands[place.Index + 1],
        _ => page.Bag[place.Index],
    };
}
