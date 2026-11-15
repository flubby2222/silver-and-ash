using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The map while M is held: the discovered rooms from above, the doors of those rooms (coloured by state) and an
/// arrow for the player's position and view direction. North is up (the first row of the level). The game goes on
/// meanwhile. Pure presentation - what is discovered decides Core.
/// </summary>
internal static class MapView
{
    private static readonly Color Shade = new(0, 0, 0, 170);
    private static readonly Color Floor = new(120, 112, 108, 230);
    private static readonly Color CorridorFloor = new(92, 86, 84, 230);
    private static readonly Color StartFloor = new(104, 118, 104, 230);
    private static readonly Color BossFloor = new(130, 60, 62, 230);
    private static readonly Color TreasureFloor = new(150, 126, 60, 230);
    private static readonly Color Cleared = new(150, 140, 120, 230);
    private static readonly Color DoorClosed = new(150, 104, 62, 255);
    private static readonly Color DoorOpen = new(210, 200, 180, 255);
    private static readonly Color DoorBarred = new(150, 152, 165, 255);
    private static readonly Color DoorBoss = new(210, 40, 44, 255);
    private static readonly Color DoorLocked = new(220, 180, 70, 255);
    private static readonly Color PlayerColor = new(230, 190, 90, 255);
    private static readonly Color Heading = new(214, 204, 180, 255);
    private static readonly Color Hint = new(150, 150, 160, 255);

    public static void Draw(GameWorld world, Player player)
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
        }

        DrawPlayer(player, ToScreen, size);
        MenuView.DrawTitle("Hold M", height - 34, 18, Hint);
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
