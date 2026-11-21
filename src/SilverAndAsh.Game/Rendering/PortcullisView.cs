using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The portcullises of the showroom (issue #540, see <see cref="GratePose"/>): the stone frame of a normal door (each
/// face in the variant the level draws there, as <see cref="DoorView"/> does), the guide ring in the arch of the door
/// plane and the iron grate, lifted by the height Core gives. Fully open the grate is not drawn; on the way the wall
/// above the doorway hides its upper part. Pure presentation: where the grate hangs decides Core. Without the frame of
/// a face the whole portcullis is left out (the grate would hang in a hole in the wall); without a ring or the grate
/// model only that part.
/// </summary>
internal sealed class PortcullisView : IDisposable
{
    // The grate lies within x +-1 m and z +-0.2 m of the cell's middle and reaches 3.5 m above the open grate's tips;
    // the frame within the cell. A sphere around half the doorway's height, with room to spare.
    private const float MiddleHeight = 1.75f;
    private const float BoundingRadius = 2.5f;

    private static readonly int FrameFront = Index(DoorFrameLook.Parts, DoorFrameLook.Front);
    private static readonly int FrameBack = Index(DoorFrameLook.Parts, DoorFrameLook.Back);
    private static readonly TextureVariant[] WallVariants = [TextureVariant.A, TextureVariant.B, TextureVariant.C];

    private readonly TexturedModel? _grate;
    private readonly Dictionary<TextureVariant, TexturedModel> _guides;
    private readonly Dictionary<TextureVariant, TexturedModel> _frames;
    private readonly Matrix4x4[] _turns = new Matrix4x4[2];
    private readonly Matrix4x4[] _poses = new Matrix4x4[2];

    private PortcullisView(TexturedModel? grate, Dictionary<TextureVariant, TexturedModel> guides, Dictionary<TextureVariant, TexturedModel> frames)
    {
        _grate = grate;
        _guides = guides;
        _frames = frames;
    }

    /// <summary>
    /// Loads the grate (<see cref="GratePose.FileName"/>), the guide ring of every wall variant
    /// (<see cref="GratePose.GuideFileName"/>) and the normal door frame of every wall variant from
    /// <paramref name="directory"/>. A missing or broken one: a warning in the log and the fallback for it. Needs the window.
    /// </summary>
    public static PortcullisView Load(string directory, FileLog? log)
    {
        var grate = TexturedModel.TryLoad(Path.Combine(directory, GratePose.FileName), GratePose.Parts, log);
        var guides = new Dictionary<TextureVariant, TexturedModel>();
        var frames = new Dictionary<TextureVariant, TexturedModel>();
        foreach (var variant in WallVariants)
        {
            if (TexturedModel.TryLoad(Path.Combine(directory, GratePose.GuideFileName(variant)), GratePose.GuideRigParts, log) is { } guide)
            {
                guides[variant] = guide;
            }

            if (TexturedModel.TryLoad(Path.Combine(directory, DoorFrameLook.FileName(DoorKind.Normal, variant)), DoorFrameLook.Parts, log) is { } frame)
            {
                frames[variant] = frame;
            }
        }

        return new PortcullisView(grate, guides, frames);
    }

    /// <summary>
    /// Draws every portcullis of <paramref name="portcullises"/> that <paramref name="view"/> may see, its grate at
    /// <paramref name="height"/> (<see cref="GratePose.Height"/>). <paramref name="surfaces"/> and
    /// <paramref name="textures"/>: the wall variants of the level and the ones really loaded. Call between BeginFigures
    /// and EndFigures.
    /// </summary>
    public void Draw(IReadOnlyList<ExhibitPortcullis> portcullises, float height, ViewCone view, SurfaceVariants surfaces, LevelTextures textures)
    {
        foreach (var portcullis in portcullises)
        {
            if (!view.MaySee(portcullis.Position + new Vector3(0f, MiddleHeight, 0f), BoundingRadius))
            {
                continue;
            }

            var (frontWanted, backWanted) = DoorFrameLook.Variants(surfaces, portcullis.X, portcullis.Z, portcullis.Side);
            var front = Frame(textures.Resolve(SurfaceKind.Wall, frontWanted));
            var back = Frame(textures.Resolve(SurfaceKind.Wall, backWanted));
            if (front is null || back is null)
            {
                continue;
            }

            // Frame, ring and grate stand on the floor in the middle of the cell, turned like a door in that wall.
            var place = Matrix4x4.CreateRotationY(DoorPose.Yaw(portcullis.Side)) * Matrix4x4.CreateTranslation(portcullis.Position);
            DrawFixed(front, FrameFront, place);
            DrawFixed(back, FrameBack, place);
            if (Guide(textures.Resolve(SurfaceKind.Wall, portcullis.Variant)) is { } guide)
            {
                DrawFixed(guide, 0, place);
            }

            if (_grate is not null && GratePose.IsDrawn(height))
            {
                DrawFixed(_grate, 0, Matrix4x4.CreateTranslation(0f, height, 0f) * place);
            }
        }
    }

    public void Dispose()
    {
        _grate?.Dispose();
        foreach (var model in _guides.Values.Concat(_frames.Values))
        {
            model.Dispose();
        }
    }

    private TexturedModel? Frame(TextureVariant wanted)
    {
        var pick = DoorFrameLook.Pick(wanted, _frames.ContainsKey(wanted), _frames.ContainsKey(TextureVariant.A));
        return pick is { } variant ? _frames[variant] : null;
    }

    private TexturedModel? Guide(TextureVariant wanted)
    {
        var pick = GratePose.Pick(wanted, _guides.ContainsKey(wanted), _guides.ContainsKey(TextureVariant.A));
        return pick is { } variant ? _guides[variant] : null;
    }

    // Nothing in these files moves: every part stands where it is in its file, the whole set at place.
    private void DrawFixed(TexturedModel model, int part, Matrix4x4 place)
    {
        var count = model.Rig.Count;
        var turns = _turns.AsSpan(0, count);
        var poses = _poses.AsSpan(0, count);
        turns.Fill(Matrix4x4.Identity);
        model.Rig.Pose(turns, place, poses);
        model.DrawPart(part, poses[part]);
    }

    private static int Index(IReadOnlyList<RigPart> parts, string name)
    {
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].Name == name)
            {
                return i;
            }
        }

        throw new ArgumentException($"The door frame has no part '{name}'.", nameof(name));
    }
}
