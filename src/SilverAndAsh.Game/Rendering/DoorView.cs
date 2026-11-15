using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Placeholder doors: a closed door fills its whole doorway cell (exactly what blocks you), an open one is gone.
/// Wooden doors are brown planks with dark bands; the boss door is almost black with bone-white skulls and a red
/// glow. The grate of an arena shows as iron bars on both faces of the doorway. Pure presentation - real models
/// come later.
/// </summary>
internal static class DoorView
{
    private static readonly Color Wood = new(104, 70, 42, 255);
    private static readonly Color Band = new(48, 40, 36, 255);
    private static readonly Color BossDoor = new(38, 22, 24, 255);
    private static readonly Color Bone = new(214, 204, 180, 255);
    private static readonly Color RedGlow = new(200, 30, 30, 255);
    private static readonly Color Iron = new(70, 72, 80, 255);

    /// <summary>Draws all closed doors. Call between BeginMode3D and EndMode3D.</summary>
    public static void Draw(IReadOnlyList<Door> doors, float seconds)
    {
        foreach (var door in doors.Where(door => door.Blocks))
        {
            var size = door.BoxMax - door.BoxMin;
            var centre = (door.BoxMin + door.BoxMax) / 2f;

            // Facing: the passage runs along Z for north/south doors, along X for east/west ones.
            var alongX = door.Side is RoomSide.East or RoomSide.West;
            var turn = alongX ? Matrix4x4.CreateRotationY(MathF.PI / 2f) : Matrix4x4.Identity;
            var width = alongX ? size.Z : size.X;
            var place = turn * Matrix4x4.CreateTranslation(centre);

            var depth = alongX ? size.X : size.Z;
            if (!door.IsOpen && door.Kind == DoorKind.Boss)
            {
                DrawBossDoor(place, width, size.Y, depth, seconds);
            }
            else if (!door.IsOpen)
            {
                DrawWoodenDoor(place, width, size.Y, depth);
            }

            if (door.IsBarred)
            {
                DrawGrate(place, width, size.Y, depth);
            }
        }
    }

    // Iron bars just in front of both faces of the doorway - where the blocking actually starts.
    private static void DrawGrate(Matrix4x4 place, float width, float height, float depth)
    {
        foreach (var face in new[] { -1f, 1f })
        {
            var z = face * (depth / 2f + 0.06f);
            for (var i = 0; i < 6; i++)
            {
                var x = -width / 2f + width * (i + 0.5f) / 6f;
                ShadedBox.Draw(Matrix4x4.CreateTranslation(x, 0f, z) * place, new Vector3(0.08f, height, 0.08f), Iron);
            }

            foreach (var y in new[] { -0.25f, 0.25f })
            {
                ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, y * height, z) * place, new Vector3(width, 0.1f, 0.1f), Iron);
            }
        }
    }

    // In local space the door's face is the X/Y plane, its thickness runs along Z.
    private static void DrawWoodenDoor(Matrix4x4 place, float width, float height, float depth)
    {
        ShadedBox.Draw(place, new Vector3(width, height, depth), Wood);
        foreach (var y in new[] { -0.3f, 0.3f })
        {
            ShadedBox.Draw(
                Matrix4x4.CreateTranslation(0f, y * height, 0f) * place,
                new Vector3(width * 0.98f, 0.18f, depth + 0.04f),
                Band);
        }
    }

    private static void DrawBossDoor(Matrix4x4 place, float width, float height, float depth, float seconds)
    {
        ShadedBox.Draw(place, new Vector3(width, height, depth), BossDoor);

        // A slowly pulsing red seam down the middle ...
        var pulse = 0.6f + 0.4f * MathF.Sin(seconds * 2f);
        var glow = new Color((byte)(RedGlow.R * pulse), (byte)(RedGlow.G * pulse), (byte)(RedGlow.B * pulse), (byte)255);
        ShadedBox.Draw(place, new Vector3(0.12f, height * 0.9f, depth + 0.06f), glow);

        // ... and three skulls above it on both faces.
        foreach (var x in new[] { -0.55f, 0f, 0.55f })
        {
            ShadedBox.Draw(
                Matrix4x4.CreateTranslation(x, height * 0.32f, 0f) * place,
                new Vector3(0.32f, 0.36f, depth + 0.12f),
                Bone);
        }
    }
}
