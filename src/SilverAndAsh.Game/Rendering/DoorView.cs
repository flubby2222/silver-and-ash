using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The doors. The designer's models (assets/models/door_*.glb, issue #245, see <see cref="DoorPose"/>): a wooden double
/// door, a locked door with one iron-bound leaf and a padlock, a boss door with two heavy leaves, and the fine cracks of a
/// cracked wall. The portal (head panel, bands) always stands, in its stone frame (issue #303, see <see cref="DoorFrameLook"/>:
/// each face of the frame from the file of the wall variant of the room it faces); the leaves swing open away from the player
/// who opened the door, as far as <see cref="Door.OpenFraction"/> says, starting and stopping slowly. The padlock is
/// gone once the door is unlocked. A cracked wall is drawn as wall by the level itself; here it gets its plates (issue
/// #246, see <see cref="CrackedWallLook"/>): on each face the wall texture of that face's variant with the cracks painted
/// in, and a little debris at its foot - and once broken nothing. If a plate cannot be loaded, the fine lines of
/// door_cracked.glb stand in, and without those the placeholder (<see cref="CrackedWallLook.Choose"/>).
/// <para>
/// A kind whose model - or whose frame, in the variant of a face and in variant A - is missing or broken gets the placeholder: a closed door fills its whole doorway cell (exactly what
/// blocks you), an open one is gone. Wooden doors are brown planks with dark bands; the boss door is almost black with
/// bone-white skulls and a red glow; a locked door is dark iron with rivets and a brass padlock on both faces; the
/// cracks are a few dark lines. The grate of an arena shows as iron bars on both faces of the doorway, with or
/// without a model.
/// </para>
/// </summary>
internal sealed class DoorView : IDisposable
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

    // Room for the parts of the largest model (the cracked wall: two plates, two heaps of debris, a chunk).
    private const int MostParts = 5;

    // Every door model, open leaves and arena grate included, lies within x +-1 m and z +-1.125 m of the cell's middle
    // (the plates of the frame stand 1/8 m out of the wall) and 0..3.5 m above its floor: its farthest corner is 2.31 m
    // from the middle at half height. A little more, for safety.
    private const float PortalHeight = 3.5f;
    private const float BoundingRadius = 2.5f;

    private static readonly int PanelFront = Index(CrackedWallLook.PanelFront);
    private static readonly int PanelBack = Index(CrackedWallLook.PanelBack);
    private static readonly int DebrisFront = Index(CrackedWallLook.DebrisFront);
    private static readonly int DebrisBack = Index(CrackedWallLook.DebrisBack);
    private static readonly int ChunkPart = Index(CrackedWallLook.Chunk);
    private static readonly int FrameFront = FrameIndex(DoorFrameLook.Front);
    private static readonly int FrameBack = FrameIndex(DoorFrameLook.Back);

    private readonly Dictionary<DoorKind, TexturedModel> _models;
    private readonly Dictionary<TextureVariant, TexturedModel> _crackedWalls;
    private readonly Dictionary<(DoorKind Kind, TextureVariant Variant), TexturedModel> _doorFrames;
    private readonly Matrix4x4[] _turns = new Matrix4x4[MostParts];
    private readonly Matrix4x4[] _frames = new Matrix4x4[MostParts];

    private DoorView(
        Dictionary<DoorKind, TexturedModel> models,
        Dictionary<TextureVariant, TexturedModel> crackedWalls,
        Dictionary<(DoorKind Kind, TextureVariant Variant), TexturedModel> doorFrames)
    {
        _models = models;
        _crackedWalls = crackedWalls;
        _doorFrames = doorFrames;
    }

    /// <summary>
    /// Loads one model per kind from <paramref name="directory"/> (<see cref="DoorPose.FileName"/>), and the cracked
    /// wall and the door frame of every wall variant (<see cref="CrackedWallLook.FileName"/>,
    /// <see cref="DoorFrameLook.FileName"/>). A missing or broken one: a warning in the log
    /// and the fallback for it. Needs the window.
    /// </summary>
    public static DoorView Load(string directory, FileLog? log)
    {
        var models = new Dictionary<DoorKind, TexturedModel>();
        foreach (var kind in Enum.GetValues<DoorKind>())
        {
            var path = Path.Combine(directory, DoorPose.FileName(kind));
            if (TexturedModel.TryLoad(path, DoorPose.Parts(kind), log) is { } model)
            {
                models[kind] = model;
            }
        }

        var crackedWalls = new Dictionary<TextureVariant, TexturedModel>();
        foreach (var variant in Enum.GetValues<TextureVariant>())
        {
            var path = Path.Combine(directory, CrackedWallLook.FileName(variant));
            if (TexturedModel.TryLoad(path, CrackedWallLook.Parts, log) is { } model)
            {
                crackedWalls[variant] = model;
            }
        }

        var doorFrames = new Dictionary<(DoorKind Kind, TextureVariant Variant), TexturedModel>();
        foreach (var kind in Enum.GetValues<DoorKind>().Where(DoorFrameLook.HasFrame))
        {
            foreach (var variant in Enum.GetValues<TextureVariant>())
            {
                var path = Path.Combine(directory, DoorFrameLook.FileName(kind, variant));
                if (TexturedModel.TryLoad(path, DoorFrameLook.Parts, log) is { } model)
                {
                    doorFrames[(kind, variant)] = model;
                }
            }
        }

        return new DoorView(models, crackedWalls, doorFrames);
    }

    /// <summary>
    /// Draws one stone of the cracked wall of <paramref name="variant"/> (the chunk of <see cref="CrackedWallLook"/>,
    /// about <see cref="CrackedWallLook.ChunkSize"/> around its origin) at <paramref name="frame"/>, <paramref name="tint"/>
    /// darker or brighter. False if that model is not loaded: draw something else. Call between BeginFigures and EndFigures.
    /// </summary>
    public bool DrawChunk(TextureVariant variant, Matrix4x4 frame, float tint)
    {
        if (!_crackedWalls.TryGetValue(variant, out var model))
        {
            return false;
        }

        model.DrawPart(ChunkPart, frame, tint);
        return true;
    }

    /// <summary>
    /// Draws the doors <paramref name="view"/> may see; the others cost nothing (issue #245: all 25 doors of a level
    /// every frame took 5.8 ms). <paramref name="surfaces"/> and <paramref name="textures"/>: the wall variants of the
    /// level and the ones really loaded - a cracked wall shows on each face the variant the level shows there. Call
    /// between BeginFigures and EndFigures.
    /// </summary>
    public void Draw(IReadOnlyList<Door> doors, float seconds, ViewCone view, SurfaceVariants surfaces, LevelTextures textures)
    {
        foreach (var door in doors)
        {
            var size = door.BoxMax - door.BoxMin;
            var centre = (door.BoxMin + door.BoxMax) / 2f;
            if (!view.MaySee(new Vector3(centre.X, door.BoxMin.Y + (PortalHeight / 2f), centre.Z), BoundingRadius))
            {
                continue;
            }

            // Facing: the passage runs along Z for north/south doors, along X for east/west ones.
            var alongX = door.Side is RoomSide.East or RoomSide.West;
            var turn = Matrix4x4.CreateRotationY(DoorPose.Yaw(door.Side));
            var width = alongX ? size.Z : size.X;
            var depth = alongX ? size.X : size.Z;

            if (door.Kind == DoorKind.Cracked)
            {
                DrawCrackedWall(door, turn, centre, size.Y, depth, surfaces, textures);
            }
            else if (_models.TryGetValue(door.Kind, out var model) && Frames(door, surfaces, textures) is ({ } front, { } back))
            {
                // The model stands on the floor in the middle of the cell, in its frame.
                var place = turn * Matrix4x4.CreateTranslation(centre.X, door.BoxMin.Y, centre.Z);
                DrawModel(model, door, place);
                DrawFrame(front, back, place);
            }
            else
            {
                DrawPlaceholder(door, turn * Matrix4x4.CreateTranslation(centre), width, size.Y, depth, seconds);
            }

            if (door.IsBarred)
            {
                DrawGrate(turn * Matrix4x4.CreateTranslation(centre), width, size.Y, depth);
            }
        }
    }

    public void Dispose()
    {
        foreach (var model in _models.Values)
        {
            model.Dispose();
        }

        foreach (var model in _doorFrames.Values)
        {
            model.Dispose();
        }
    }

    // The frame model for each face: the variant the level really draws there (a missing B or C falls back to A there,
    // too), else variant A of the frame. Null for a face without any: then the whole door is the placeholder.
    private (TexturedModel? Front, TexturedModel? Back) Frames(Door door, SurfaceVariants surfaces, LevelTextures textures)
    {
        var (front, back) = DoorFrameLook.Variants(surfaces, door.X, door.Z, door.Side);
        return (Frame(door.Kind, textures.Resolve(SurfaceKind.Wall, front)), Frame(door.Kind, textures.Resolve(SurfaceKind.Wall, back)));
    }

    private TexturedModel? Frame(DoorKind kind, TextureVariant wanted)
    {
        var pick = DoorFrameLook.Pick(wanted, _doorFrames.ContainsKey((kind, wanted)), _doorFrames.ContainsKey((kind, TextureVariant.A)));
        return pick is { } variant ? _doorFrames[(kind, variant)] : null;
    }

    // Nothing of the frame moves: both faces stand where they are in their files.
    private void DrawFrame(TexturedModel front, TexturedModel back, Matrix4x4 place)
    {
        var turns = _turns.AsSpan(0, DoorFrameLook.Parts.Count);
        var frames = _frames.AsSpan(0, DoorFrameLook.Parts.Count);
        turns.Fill(Matrix4x4.Identity);
        front.Rig.Pose(turns, place, frames);
        front.DrawPart(FrameFront, frames[FrameFront]);
        back.Rig.Pose(turns, place, frames);
        back.DrawPart(FrameBack, frames[FrameBack]);
    }

    // A broken cracked wall leaves nothing behind. A standing one: the plates, else the lines, else the placeholder.
    private void DrawCrackedWall(
        Door door, Matrix4x4 turn, Vector3 centre, float height, float depth, SurfaceVariants surfaces, LevelTextures textures)
    {
        if (door.IsOpen)
        {
            return;
        }

        // The variant the level really draws on each face (a missing B or C falls back to A there, too).
        var (front, back) = CrackedWallLook.Variants(surfaces, door.X, door.Z, door.Side);
        front = textures.Resolve(SurfaceKind.Wall, front);
        back = textures.Resolve(SurfaceKind.Wall, back);
        var drawing = CrackedWallLook.Choose(
            _crackedWalls.ContainsKey(front),
            _crackedWalls.ContainsKey(back),
            textures.For(SurfaceKind.Wall, front) is not null && textures.For(SurfaceKind.Wall, back) is not null,
            _models.ContainsKey(DoorKind.Cracked));
        var place = turn * Matrix4x4.CreateTranslation(centre.X, door.BoxMin.Y, centre.Z);
        switch (drawing)
        {
            case CrackedWallDrawing.Panels:
                DrawPlate(_crackedWalls[front], PanelFront, DebrisFront, place);
                DrawPlate(_crackedWalls[back], PanelBack, DebrisBack, place);
                break;
            case CrackedWallDrawing.Lines:
                DrawModel(_models[DoorKind.Cracked], door, place);
                break;
            default:
                DrawCracks(turn * Matrix4x4.CreateTranslation(centre), height, depth);
                break;
        }
    }

    // Nothing of the cracked wall moves: every part stands where it is in the file.
    private void DrawPlate(TexturedModel model, int panel, int debris, Matrix4x4 place)
    {
        var turns = _turns.AsSpan(0, CrackedWallLook.Parts.Count);
        var frames = _frames.AsSpan(0, CrackedWallLook.Parts.Count);
        turns.Fill(Matrix4x4.Identity);
        model.Rig.Pose(turns, place, frames);
        model.DrawPart(panel, frames[panel]);
        model.DrawPart(debris, frames[debris]);
    }

    private static int Index(string part)
    {
        for (var i = 0; i < CrackedWallLook.Parts.Count; i++)
        {
            if (CrackedWallLook.Parts[i].Name == part)
            {
                return i;
            }
        }

        throw new ArgumentException($"The cracked wall has no part '{part}'.", nameof(part));
    }

    private static int FrameIndex(string part)
    {
        for (var i = 0; i < DoorFrameLook.Parts.Count; i++)
        {
            if (DoorFrameLook.Parts[i].Name == part)
            {
                return i;
            }
        }

        throw new ArgumentException($"The door frame has no part '{part}'.", nameof(part));
    }

    private void DrawModel(TexturedModel model, Door door, Matrix4x4 place)
    {
        var parts = DoorPose.Parts(door.Kind);
        var turns = _turns.AsSpan(0, parts.Count);
        var frames = _frames.AsSpan(0, parts.Count);
        DoorPose.Turns(door.Kind, door.OpenFraction, door.OpenedFromLowSide, turns);
        model.Rig.Pose(turns, place, frames);
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].Name == DoorPose.Lock && !DoorPose.ShowsLock(door.IsOpen))
            {
                continue;
            }

            model.DrawPart(i, frames[i]);
        }
    }

    private static void DrawPlaceholder(Door door, Matrix4x4 place, float width, float height, float depth, float seconds)
    {
        if (door.IsOpen)
        {
            return;
        }

        // A cracked wall never comes here: DrawCrackedWall has its own fallbacks.
        switch (door.Kind)
        {
            case DoorKind.Boss:
                DrawBossDoor(place, width, height, depth, seconds);
                break;
            case DoorKind.Locked:
                DrawLockedDoor(place, width, height, depth);
                break;
            default:
                DrawWoodenDoor(place, width, height, depth);
                break;
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
