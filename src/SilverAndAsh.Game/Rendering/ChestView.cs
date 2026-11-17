using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The chests, one look per tier so it shows from afar whether a key is worth it: Beggar's = brown wood, Guard's = steel
/// blue with a handle, King's = gold with a crown, Vampire's = blood red with spikes and a dark glow. The designer's
/// models (assets/models/chest_*.glb, issue #249); a tier whose model is missing or broken gets the placeholder boxes.
/// <para>
/// Both looks follow the chest's plan (issue #209): the front with the lock looks towards the middle of the room, the lid
/// opens over the back edge. A locked chest shows its brass lock, an unlocked one none. An open chest stands with its
/// lid up and its dark inside showing (it is looted when it opens). A combat room's chest rises out of the floor.
/// </para>
/// </summary>
internal sealed class ChestView : IDisposable
{
    private static readonly Color Brass = new(176, 140, 72, 255);
    private static readonly Color Inside = new(18, 14, 14, 255);

    // The Vampire's chest in the file has the middle of its pulse (175, 25, 35); the pulse runs 150 .. 200.
    private const float VampireMiddle = 175f;

    private readonly Dictionary<ChestTier, TexturedModel> _models;
    private readonly Matrix4x4[] _turns = new Matrix4x4[ChestPose.Parts.Count];
    private readonly Matrix4x4[] _frames = new Matrix4x4[ChestPose.Parts.Count];

    private ChestView(Dictionary<ChestTier, TexturedModel> models) => _models = models;

    /// <summary>
    /// Loads one model per tier from <paramref name="directory"/> (chest_beggar.glb ...). A missing or broken one: a
    /// warning in the log and the placeholder for that tier. Needs the window.
    /// </summary>
    public static ChestView Load(string directory, FileLog? log)
    {
        var models = new Dictionary<ChestTier, TexturedModel>();
        foreach (var tier in Enum.GetValues<ChestTier>())
        {
            var path = Path.Combine(directory, $"chest_{tier.ToString().ToLowerInvariant()}.glb");
            if (TexturedModel.TryLoad(path, ChestPose.Parts, log) is { } model)
            {
                models[tier] = model;
            }
        }

        return new ChestView(models);
    }

    /// <summary>Draws all chests that are (partly) above the floor. Call between BeginFigures and EndFigures.</summary>
    public void Draw(IReadOnlyList<Chest> chests, float seconds)
    {
        foreach (var chest in chests.Where(chest => chest.RiseFraction > 0f))
        {
            // Rising: it comes up out of the floor (the part below the floor is hidden by it). Turned so the front looks
            // where the plan says.
            var sink = (1f - EaseOut(chest.RiseFraction)) * Chest.Height;
            var place = Matrix4x4.CreateRotationY(ChestPose.Yaw(chest.Plan.Front))
                * Matrix4x4.CreateTranslation(chest.Position - new Vector3(0f, sink, 0f));

            if (_models.TryGetValue(chest.Tier, out var model))
            {
                DrawModel(model, chest, place, seconds);
            }
            else
            {
                DrawPlaceholder(chest, place, seconds);
            }
        }
    }

    public void Dispose()
    {
        foreach (var model in _models.Values)
        {
            model.Dispose();
        }
    }

    internal static (Color Body, Color Band) Colours(ChestTier tier, float seconds) => tier switch
    {
        ChestTier.Beggar => (new Color(110, 76, 46, 255), new Color(56, 44, 36, 255)),
        ChestTier.Guard => (new Color(92, 112, 140, 255), new Color(52, 58, 70, 255)),
        ChestTier.King => (new Color(214, 170, 64, 255), new Color(140, 100, 36, 255)),
        ChestTier.Vampire => (Pulse(new Color(150, 20, 30, 255), new Color(200, 30, 40, 255), seconds), new Color(40, 10, 14, 255)),
        _ => (Color.Magenta, Color.Magenta),
    };

    private void DrawModel(TexturedModel model, Chest chest, Matrix4x4 place, float seconds)
    {
        _turns[0] = Matrix4x4.Identity;
        _turns[1] = Rig.Lift(ChestPose.LidDegrees(chest.IsOpen));
        _turns[2] = Matrix4x4.Identity;
        model.Rig.Pose(_turns, place, _frames);

        // The Vampire's chest glows like the placeholder (and the HUD and the map, TierColours): brighter and darker.
        var tint = chest.Tier == ChestTier.Vampire ? Colours(ChestTier.Vampire, seconds).Body.R / VampireMiddle : 1f;
        model.DrawPart(0, _frames[0], tint);
        model.DrawPart(1, _frames[1], tint);
        if (chest.IsLocked)
        {
            model.DrawPart(2, _frames[2]);
        }
    }

    private static void DrawPlaceholder(Chest chest, Matrix4x4 place, float seconds)
    {
        var (body, band) = Colours(chest.Tier, seconds);

        const float BaseHeight = Chest.Height * 0.65f;
        const float LidHeight = Chest.Height - BaseHeight;
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, BaseHeight / 2f, 0f) * place, new Vector3(Chest.Width, BaseHeight, Chest.Depth), body);

        if (chest.IsOpen)
        {
            // Dark inside, and the lid tipped over the back edge (its own -z).
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, BaseHeight + 0.005f, 0f) * place, new Vector3(Chest.Width * 0.9f, 0.01f, Chest.Depth * 0.85f), Inside);
            var lid = Matrix4x4.CreateTranslation(0f, LidHeight / 2f, Chest.Depth / 2f)
                * Matrix4x4.CreateRotationX(-1.9f)
                * Matrix4x4.CreateTranslation(0f, BaseHeight, -Chest.Depth / 2f);
            ShadedBox.Draw(lid * place, new Vector3(Chest.Width, LidHeight, Chest.Depth), body);
        }
        else
        {
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, BaseHeight + LidHeight / 2f, 0f) * place, new Vector3(Chest.Width, LidHeight, Chest.Depth), body);
        }

        // Two bands round the chest, a little proud of it.
        foreach (var x in new[] { -0.3f, 0.3f })
        {
            ShadedBox.Draw(Matrix4x4.CreateTranslation(x, BaseHeight / 2f, 0f) * place, new Vector3(0.08f, BaseHeight + 0.02f, Chest.Depth + 0.02f), band);
        }

        if (chest.IsLocked)
        {
            // A padlock on the front.
            ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, BaseHeight - 0.08f, Chest.Depth / 2f + 0.04f) * place, new Vector3(0.16f, 0.16f, 0.06f), Brass);
        }
    }

    private static Color Pulse(Color from, Color to, float seconds)
    {
        var amount = 0.5f + 0.5f * MathF.Sin(seconds * 2f);
        return new Color(
            (byte)(from.R + (to.R - from.R) * amount),
            (byte)(from.G + (to.G - from.G) * amount),
            (byte)(from.B + (to.B - from.B) * amount),
            (byte)255);
    }

    // Fast at first, slowing down at the top.
    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}
