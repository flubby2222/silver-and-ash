using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The map page (issue #347), opened and closed with Tab: on the left the player's values and chosen boons (the lines
/// come from Core, <see cref="CharacterSheet"/>), on the right the discovered rooms from above, the doors of those rooms
/// (coloured by state, a padlock on a locked one, a skull on the boss door), the unopened chests there (in their tier's
/// colour, a padlock if locked), blood vials left behind and an arrow for the player's position and view
/// direction. North is up (the first row of the level). It is see-through, so the game can be played with it open -
/// the game goes on meanwhile, and only Tab or Esc close it (issue #346). Pure presentation - what is discovered decides Core.
/// </summary>
internal static class MapView
{
    private static readonly Color Shade = new(0, 0, 0, 80);
    private static readonly Color Floor = new(120, 112, 108, 160);
    private static readonly Color CorridorFloor = new(92, 86, 84, 160);
    private static readonly Color StartFloor = new(104, 118, 104, 160);
    private static readonly Color BossFloor = new(130, 60, 62, 160);
    private static readonly Color TreasureFloor = new(150, 126, 60, 160);
    private static readonly Color SecretFloor = new(110, 84, 140, 160);
    private static readonly Color Cleared = new(150, 140, 120, 160);
    private static readonly Color Lock = new(240, 200, 80, 255);
    private static readonly Color LockDark = new(60, 44, 16, 255);
    private static readonly Color Bone = new(236, 228, 210, 255);
    private static readonly Color DoorClosed = new(150, 104, 62, 255);
    private static readonly Color DoorOpen = new(210, 200, 180, 255);
    private static readonly Color DoorBarred = new(150, 152, 165, 255);
    private static readonly Color DoorBoss = new(210, 40, 44, 255);
    private static readonly Color DoorLocked = new(220, 180, 70, 255);
    private static readonly Color PlayerColor = new(230, 190, 90, 255);
    private static readonly Color Vial = new(220, 40, 50, 255);
    private static readonly Color VialRim = new(40, 8, 12, 255);
    private static readonly Color Heading = new(214, 204, 180, 255);
    private static readonly Color Hint = new(150, 150, 160, 255);
    private static readonly Color Panel = new(16, 12, 18, 255);
    private static readonly Color Label = new(170, 166, 176, 255);
    private static readonly Color Value = new(226, 222, 232, 255);
    private static readonly Color Gold = new(232, 196, 96, 255);

    // The column of values and boons on the left edge. Opaque and only as tall as its lines, so it covers the
    // game's title behind it but leaves the level and health bars at the bottom free - even with all four boons
    // chosen at the smallest window (480 high) it ends above them.
    private const int HeadingGap = 6;
    private const int Edge = 12;
    private const int PanelTop = 12;
    private const int LineHeight = 19;
    private const int FontSize = 16;
    private const int HeadingSize = 18;
    private const int Padding = 10;
    private const int ColumnGap = 16;

    /// <summary>Draws the page: values and boons on the left, the map on the right.</summary>
    public static void Draw(GameWorld world, Player player, float seconds)
    {
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        Raylib.DrawRectangle(0, 0, width, height, Shade);
        var panelRight = DrawSheet(world, player);

        // The map fills the rest of the screen, right of the column; the hint sits under its heading, away from the HUD.
        var mapLeft = panelRight + 24;
        var mapWidth = width - mapLeft - 40;
        DrawCentred("MAP", mapLeft, mapWidth, PanelTop + 12, 36, Heading);
        DrawCentred("Tab or Esc: close", mapLeft, mapWidth, PanelTop + 52, 16, Hint);

        var discovered = world.Rooms.Where(room => room.IsDiscovered).ToList();
        if (discovered.Count == 0)
        {
            DrawCentred("This level has no map.", mapLeft, mapWidth, height / 2, 24, Hint);
            return;
        }

        // Fit the discovered part (plus a margin) into the screen below the heading.
        var cells = discovered.SelectMany(room => room.Cells).ToList();
        var minX = cells.Min(cell => cell.X) - 1;
        var minZ = cells.Min(cell => cell.Z) - 1;
        var spanX = cells.Max(cell => cell.X) - minX + 2;
        var spanZ = cells.Max(cell => cell.Z) - minZ + 2;
        var area = new Rectangle(mapLeft, 90, mapWidth, height - 140);
        var size = MathF.Min(MathF.Min(area.Width / spanX, area.Height / spanZ), 28f);
        var left = area.X + (area.Width - spanX * size) / 2f;
        var top = area.Y + (area.Height - spanZ * size) / 2f;

        Vector2 ToScreen(float cellX, float cellZ) => new(left + (cellX - minX) * size, top + (cellZ - minZ) * size);

        foreach (var room in discovered)
        {
            var color = room.Kind switch
            {
                RoomKind.Start => StartFloor,
                RoomKind.Corridor => CorridorFloor,
                RoomKind.Boss => BossFloor,
                RoomKind.Treasure => TreasureFloor,
                RoomKind.Secret => SecretFloor,
                _ when room.IsCleared => Cleared,
                _ => Floor,
            };
            foreach (var (x, z) in room.Cells)
            {
                var corner = ToScreen(x, z);
                Raylib.DrawRectangleV(corner, new Vector2(size, size), color);
            }
        }

        foreach (var door in world.Doors.Where(world.IsKnown))
        {
            var color = door.IsBarred ? DoorBarred
                : door.IsOpen ? DoorOpen
                : door.Kind == DoorKind.Boss ? DoorBoss
                : door.Kind == DoorKind.Locked ? DoorLocked
                : DoorClosed;
            var corner = ToScreen(door.X, door.Z);
            var inset = size * 0.15f;
            Raylib.DrawRectangleV(corner + new Vector2(inset, inset), new Vector2(size - 2 * inset, size - 2 * inset), color);

            var middle = corner + new Vector2(size / 2f, size / 2f);
            var icon = MathF.Max(size * 0.7f, 10f);
            if (door.Kind == DoorKind.Boss)
            {
                DrawSkull(middle, icon);
            }
            else if (door.Kind == DoorKind.Locked && !door.IsOpen)
            {
                DrawPadlock(middle, icon, Lock);
            }
        }

        // Unopened chests: a small box in the tier's colour with its band, a padlock if locked.
        foreach (var chest in world.KnownChests)
        {
            var place = ToScreen(chest.Position.X / Level.CellSize, chest.Position.Z / Level.CellSize);
            var box = new Vector2(MathF.Max(size * 0.7f, 9f), MathF.Max(size * 0.5f, 7f));
            var (body, band) = ChestView.Colours(chest.Tier, seconds);
            var corner = place - (box / 2f);
            Raylib.DrawRectangleV(corner - Vector2.One, box + (2f * Vector2.One), LockDark);
            Raylib.DrawRectangleV(corner, box, body);
            Raylib.DrawRectangleV(corner + new Vector2(0f, box.Y * 0.35f), new Vector2(box.X, MathF.Max(1f, box.Y * 0.18f)), band);
            if (chest.IsLocked)
            {
                DrawPadlock(place + new Vector2(0f, box.Y * 0.1f), box.Y * 0.9f, Lock);
            }
        }

        // Blood vials left behind: a small red drop with a dark rim.
        foreach (var vial in world.KnownBloodVials)
        {
            var place = ToScreen(vial.Position.X / Level.CellSize, vial.Position.Z / Level.CellSize);
            var radius = MathF.Max(size * 0.28f, 3f);
            Raylib.DrawCircleV(place, radius + 1f, VialRim);
            Raylib.DrawCircleV(place, radius, Vial);
        }

        DrawPlayer(player, ToScreen, size);
    }

    // The values and the boons in a dark column on the left edge, names left, values in a second column. Returns the
    // column's right edge, so the map starts beside it.
    private static int DrawSheet(GameWorld world, Player player)
    {
        var stats = CharacterSheet.Stats(world, player);
        var boons = CharacterSheet.Boons(player, world.Balance.Boons);
        IEnumerable<SheetLine> all = [.. stats, .. boons];
        var labelWidth = all.Max(line => Raylib.MeasureText(line.Label, FontSize));
        var valueWidth = all.Max(line => Raylib.MeasureText(line.Value, FontSize));
        var valueX = Edge + Padding + labelWidth + ColumnGap;
        var right = valueX + Math.Max(valueWidth, Raylib.MeasureText("No boons yet", FontSize)) + Padding;
        var lines = stats.Count + Math.Max(boons.Count, 1);
        var bottom = PanelTop + Padding + (2 * (HeadingSize + HeadingGap)) + HeadingGap + (lines * LineHeight) + Padding;
        Raylib.DrawRectangle(Edge, PanelTop, right - Edge, bottom - PanelTop, Panel);

        var y = PanelTop + Padding;
        Raylib.DrawText("HUNTER", Edge + Padding, y, HeadingSize, Heading);
        y += HeadingSize + HeadingGap;
        foreach (var line in stats)
        {
            DrawLine(line, valueX, y, Value);
            y += LineHeight;
        }

        y += HeadingGap;
        Raylib.DrawText("BOONS", Edge + Padding, y, HeadingSize, Heading);
        y += HeadingSize + HeadingGap;
        if (boons.Count == 0)
        {
            Raylib.DrawText("No boons yet", Edge + Padding, y, FontSize, Hint);
        }

        foreach (var line in boons)
        {
            DrawLine(line, valueX, y, Gold);
            y += LineHeight;
        }

        return right;
    }

    private static void DrawLine(SheetLine line, int valueX, int y, Color valueColor)
    {
        Raylib.DrawText(line.Label, Edge + Padding, y, FontSize, Label);
        Raylib.DrawText(line.Value, valueX, y, FontSize, valueColor);
    }

    private static void DrawCentred(string text, int left, int width, int y, int fontSize, Color color) =>
        Raylib.DrawText(text, left + ((width - Raylib.MeasureText(text, fontSize)) / 2), y, fontSize, color);

    // A padlock, about `size` tall: a shackle (half ring) on a square body with a keyhole.
    private static void DrawPadlock(Vector2 centre, float size, Color color)
    {
        var body = new Vector2(size * 0.7f, size * 0.5f);
        var bodyCorner = centre + new Vector2(-body.X / 2f, -size * 0.05f);
        var shackleCentre = new Vector2(centre.X, bodyCorner.Y);
        var outer = size * 0.28f;
        Raylib.DrawRing(shackleCentre, outer * 0.55f, outer, 180f, 360f, 12, LockDark);
        Raylib.DrawRing(shackleCentre, outer * 0.62f, outer * 0.92f, 180f, 360f, 12, color);
        Raylib.DrawRectangleV(bodyCorner - Vector2.One, body + (2f * Vector2.One), LockDark);
        Raylib.DrawRectangleV(bodyCorner, body, color);
        Raylib.DrawCircleV(bodyCorner + new Vector2(body.X / 2f, body.Y * 0.45f), MathF.Max(1f, size * 0.07f), LockDark);
    }

    // A skull, about `size` tall: round head, jaw, two dark eyes.
    private static void DrawSkull(Vector2 centre, float size)
    {
        var head = centre + new Vector2(0f, -size * 0.1f);
        var radius = size * 0.32f;
        var jaw = new Vector2(size * 0.36f, size * 0.2f);
        Raylib.DrawCircleV(head, radius + 1f, LockDark);
        Raylib.DrawRectangleV(new Vector2(centre.X - jaw.X / 2f - 1f, head.Y + radius * 0.5f - 1f), jaw + (2f * Vector2.One), LockDark);
        Raylib.DrawCircleV(head, radius, Bone);
        Raylib.DrawRectangleV(new Vector2(centre.X - jaw.X / 2f, head.Y + radius * 0.5f), jaw, Bone);
        var eye = MathF.Max(1f, radius * 0.3f);
        Raylib.DrawCircleV(head + new Vector2(-radius * 0.42f, radius * 0.1f), eye, LockDark);
        Raylib.DrawCircleV(head + new Vector2(radius * 0.42f, radius * 0.1f), eye, LockDark);
    }

    // A small arrow: the tip points where the player looks (the view's yaw; +Z is down on the map).
    private static void DrawPlayer(Player player, Func<float, float, Vector2> toScreen, float size)
    {
        var centre = toScreen(player.Position.X / Level.CellSize, player.Position.Z / Level.CellSize);
        var yaw = player.View.YawRadians;
        var forward = new Vector2(MathF.Sin(yaw), MathF.Cos(yaw));
        var side = new Vector2(-forward.Y, forward.X);
        var length = MathF.Max(size * 0.9f, 8f);
        var tip = centre + forward * length;
        var backLeft = centre - forward * length * 0.5f + side * length * 0.55f;
        var backRight = centre - forward * length * 0.5f - side * length * 0.55f;

        // Raylib wants the corners counter-clockwise on screen; draw both ways so the order never hides it.
        Raylib.DrawTriangle(tip, backLeft, backRight, PlayerColor);
        Raylib.DrawTriangle(tip, backRight, backLeft, PlayerColor);
    }
}
