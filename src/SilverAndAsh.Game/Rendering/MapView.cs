using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The map while Tab is held, or kept open with M: the discovered rooms from above, the doors of those rooms
/// (coloured by state, a padlock on a locked one, a skull on the boss door), the unopened chests there (in their tier's
/// colour, a padlock if locked), blood vials left behind and an arrow for the player's position and view
/// direction. North is up (the first row of the level). It is see-through, so the game can be played with it open -
/// the game goes on meanwhile. Pure presentation - what is discovered decides Core.
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

    /// <summary>Draws the map. <paramref name="keptOpen"/>: opened with M (the hint then says how to close it).</summary>
    public static void Draw(GameWorld world, Player player, bool keptOpen, float seconds)
    {
        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        Raylib.DrawRectangle(0, 0, width, height, Shade);
        MenuView.DrawTitle("MAP", 24, 36, Heading);

        var discovered = world.Rooms.Where(room => room.IsDiscovered).ToList();
        if (discovered.Count == 0)
        {
            MenuView.DrawTitle("This level has no map.", height / 2, 24, Hint);
            return;
        }

        // Fit the discovered part (plus a margin) into the screen below the heading.
        var cells = discovered.SelectMany(room => room.Cells).ToList();
        var minX = cells.Min(cell => cell.X) - 1;
        var minZ = cells.Min(cell => cell.Z) - 1;
        var spanX = cells.Max(cell => cell.X) - minX + 2;
        var spanZ = cells.Max(cell => cell.Z) - minZ + 2;
        var area = new Rectangle(40, 80, width - 80, height - 130);
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
        MenuView.DrawTitle(keptOpen ? "M: close map" : "Hold Tab  -  M: keep open", height - 34, 18, Hint);
    }

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
