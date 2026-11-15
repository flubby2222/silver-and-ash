using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// Placeholder doors: a closed door fills its whole doorway cell (exactly what blocks you), an open one is gone.
/// Wooden doors are brown planks with dark bands; the boss door is almost black with bone-white skulls and a red
/// glow; a locked door is dark iron with rivets and a brass padlock on both faces. The grate of an arena shows as
/// iron bars on both faces of the doorway. A cracked wall is drawn as wall by the level itself; here it only gets its
/// fine, dark cracks on both faces - easy to miss if you are not looking. Pure presentation - real models
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
    private static readonly Color DarkIron = new(52, 54, 60, 255);
    private static readonly Color Rivet = new(96, 98, 108, 255);
    private static readonly Color Brass = new(176, 140, 72, 255);
    private static readonly Color Crack = new(30, 26, 30, 255);

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
            if (!door.IsOpen && door.Kind == DoorKind.Cracked)
            {
                DrawCracks(place, size.Y, depth);
                continue;
            }

            if (!door.IsOpen && door.Kind == DoorKind.Boss)
            {
                DrawBossDoor(place, width, size.Y, depth, seconds);
            }
            else if (!door.IsOpen && door.Kind == DoorKind.Locked)
            {
                DrawLockedDoor(place, width, size.Y, depth);
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

    // A few thin dark lines just in front of both wall faces, branching from a point at chest height.
    private static void DrawCracks(Matrix4x4 place, float height, float depth)
    {
        (float X, float Y, float Length, float Angle)[] lines =
        [
            (0f, -0.1f, 0.55f, 0.35f), (0.12f, 0.2f, 0.4f, -0.5f), (-0.15f, 0.12f, 0.35f, 0.9f),
            (0.05f, -0.42f, 0.45f, -0.2f), (-0.08f, -0.7f, 0.3f, 0.6f),
        ];
        foreach (var face in new[] { -1f, 1f })
        {
            var z = face * (depth / 2f + 0.01f);
            foreach (var (x, y, length, angle) in lines)
            {
                var line = Matrix4x4.CreateRotationZ(angle) * Matrix4x4.CreateTranslation(x, y + (1.3f - height / 2f), z);
                ShadedBox.Draw(line * place, new Vector3(0.025f, length, 0.01f), Crack);
            }
        }
    }

    // Dark iron with rows of rivets, and a brass padlock at hand height on both faces.
    private static void DrawLockedDoor(Matrix4x4 place, float width, float height, float depth)
    {
        ShadedBox.Draw(place, new Vector3(width, height, depth), DarkIron);
        foreach (var y in new[] { -0.4f, 0f, 0.4f })
        {
            for (var i = 0; i < 5; i++)
            {
                var x = -width / 2f + width * (i + 0.5f) / 5f;
                ShadedBox.Draw(Matrix4x4.CreateTranslation(x, y * height, 0f) * place, new Vector3(0.07f, 0.07f, depth + 0.04f), Rivet);
            }
        }

        // At hand height (1.1 m above the floor), below the eyes - so it does not fill the view close up.
        var lockY = 1.1f - height / 2f;
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, lockY, 0f) * place, new Vector3(0.26f, 0.24f, depth + 0.14f), Brass);        // body
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, lockY + 0.19f, 0f) * place, new Vector3(0.18f, 0.14f, depth + 0.08f), Iron); // shackle
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
