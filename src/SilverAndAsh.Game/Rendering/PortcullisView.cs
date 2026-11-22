using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The portcullises (issue #540, see <see cref="GratePose"/>): the guide ring in the arch of the door plane and the iron
/// grate, lifted by the height Core gives - in the doors of the arenas (issue #558, <see cref="Door.GrateHeight"/>) and
/// as exhibits of the showroom. The stone frame is <see cref="DoorView"/>'s: in the level it draws it with the door, for
/// an exhibit <see cref="DoorView.DrawFrame"/>. Fully open the grate is not drawn; on the way the wall above the doorway
/// hides its upper part. Pure presentation: where the grate hangs decides Core. An exhibit without the frame of a face
/// is left out (the grate would hang in a hole in the wall); without a ring or the grate model only that part.
/// </summary>
internal sealed class PortcullisView : IDisposable
{
    // The grate lies within x +-1 m and z +-0.2 m of the cell's middle and reaches 3.5 m above the open grate's tips;
    // the frame within the cell. A sphere around half the doorway's height, with room to spare.
    private const float MiddleHeight = 1.75f;
    private const float BoundingRadius = 2.5f;

    private static readonly TextureVariant[] WallVariants = [TextureVariant.A, TextureVariant.B, TextureVariant.C];

    private readonly TexturedModel? _grate;
    private readonly Dictionary<TextureVariant, TexturedModel> _guides;
    private readonly Matrix4x4[] _turns = new Matrix4x4[2];
    private readonly Matrix4x4[] _poses = new Matrix4x4[2];

    private PortcullisView(TexturedModel? grate, Dictionary<TextureVariant, TexturedModel> guides)
    {
        _grate = grate;
        _guides = guides;
    }

    /// <summary>True if the grate model is loaded - then a barred door with a grate needs no placeholder bars.</summary>
    public bool HasGrate => _grate is not null;

    /// <summary>
    /// Loads the grate (<see cref="GratePose.FileName"/>) and the guide ring of every wall variant
    /// (<see cref="GratePose.GuideFileName"/>) from <paramref name="directory"/>. A missing or broken one: a warning in
    /// the log and the fallback for it. Needs the window.
    /// </summary>
    public static PortcullisView Load(string directory, FileLog? log)
    {
        var grate = TexturedModel.TryLoad(Path.Combine(directory, GratePose.FileName), GratePose.Parts, log);
        var guides = new Dictionary<TextureVariant, TexturedModel>();
        foreach (var variant in WallVariants)
        {
            if (TexturedModel.TryLoad(Path.Combine(directory, GratePose.GuideFileName(variant)), GratePose.GuideRigParts, log) is { } guide)
            {
                guides[variant] = guide;
            }
        }

        return new PortcullisView(grate, guides);
    }

    /// <summary>
    /// Draws the portcullis of every door of <paramref name="doors"/> that has one and <paramref name="view"/> may see,
    /// its grate where Core says (<see cref="Door.GrateHeight"/>); the frame is the door's. The ring in the variant of
    /// the door's front face. Call between BeginFigures and EndFigures.
    /// </summary>
    public void Draw(IReadOnlyList<Door> doors, ViewCone view, SurfaceVariants surfaces, LevelTextures textures)
    {
        foreach (var door in doors)
        {
            if (!door.HasGrate)
            {
                continue;
            }

            var centre = (door.BoxMin + door.BoxMax) / 2f;
            var feet = new Vector3(centre.X, door.BoxMin.Y, centre.Z);
            if (view.MaySee(feet + new Vector3(0f, MiddleHeight, 0f), BoundingRadius))
            {
                var (front, _) = DoorFrameLook.Variants(surfaces, door.X, door.Z, door.Side);
                DrawPortcullis(textures.Resolve(SurfaceKind.Wall, front), door.Side, feet, door.GrateHeight);
            }
        }
    }

    /// <summary>
    /// Draws every portcullis of <paramref name="portcullises"/> that <paramref name="view"/> may see, in the frame of a
    /// normal door from <paramref name="frames"/>, its grate at <paramref name="height"/> (<see cref="GratePose.Height"/>).
    /// <paramref name="surfaces"/> and <paramref name="textures"/>: the wall variants of the level and the ones really
    /// loaded. Call between BeginFigures and EndFigures.
    /// </summary>
    public void Draw(
        IReadOnlyList<ExhibitPortcullis> portcullises, float height, ViewCone view, SurfaceVariants surfaces, LevelTextures textures, DoorView frames)
    {
        foreach (var portcullis in portcullises)
        {
            if (view.MaySee(portcullis.Position + new Vector3(0f, MiddleHeight, 0f), BoundingRadius)
                && frames.DrawFrame(DoorKind.Normal, portcullis.X, portcullis.Z, portcullis.Side, portcullis.Position, surfaces, textures))
            {
                DrawPortcullis(textures.Resolve(SurfaceKind.Wall, portcullis.Variant), portcullis.Side, portcullis.Position, height);
            }
        }
    }

    public void Dispose()
    {
        _grate?.Dispose();
        foreach (var model in _guides.Values)
        {
            model.Dispose();
        }
    }

    // Ring and grate stand on the floor in the middle of the cell, turned like a door in that wall.
    private void DrawPortcullis(TextureVariant variant, RoomSide side, Vector3 feet, float height)
    {
        var place = Matrix4x4.CreateRotationY(DoorPose.Yaw(side)) * Matrix4x4.CreateTranslation(feet);
        if (Guide(variant) is { } guide)
        {
            DrawFixed(guide, 0, place);
        }

        if (_grate is not null && GratePose.IsDrawn(height))
        {
            DrawFixed(_grate, 0, Matrix4x4.CreateTranslation(0f, height, 0f) * place);
        }
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
}