using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The spiral staircase that rises in the boss room after Gorm's death. The designer's model (issue #300, see
/// <see cref="StairsLook"/>): one file per wall variant, drawn in the variant of the walls of the room it stands in. It
/// comes up out of the floor (the part below the floor is hidden by it); the lamp on top is lit only once it stands, and
/// pulses. Pure presentation.
/// <para>
/// Without a model - in the wall variant and in variant A - the placeholder stands in: a stone pole with steps winding
/// round it and a faint light from above.
/// </para>
/// </summary>
internal sealed class StairsView : IDisposable
{
    private const int Steps = 24;

    // The box of the staircase (+-Stairs.Radius, 0..Stairs.Height, 6 m) has its corners 3.45 m from the middle at half height,
    // but the model and the placeholder are round: their farthest points are 3.23 and 3.24 m away (half the height 3 m, the
    // model 1.2 m out). A little more, for safety.
    private const float BoundingRadius = 3.5f;

    private static readonly Color Stone = new(120, 114, 108, 255);
    private static readonly Color Pole = new(84, 80, 76, 255);
    private static readonly Color Light = new(210, 190, 140, 255);

    private static readonly int StonePart = Index(StairsLook.Stone);
    private static readonly int GlowPart = Index(StairsLook.Glow);

    private readonly Dictionary<TextureVariant, TexturedModel> _models;
    private readonly Matrix4x4[] _turns = new Matrix4x4[2];
    private readonly Matrix4x4[] _frames = new Matrix4x4[2];

    private StairsView(Dictionary<TextureVariant, TexturedModel> models)
    {
        _models = models;
    }

    /// <summary>
    /// Loads the staircase of every wall variant from <paramref name="directory"/> (<see cref="StairsLook.FileName"/>). A
    /// missing or broken one: a warning in the log and the fallback for it. Needs the window.
    /// </summary>
    public static StairsView Load(string directory, FileLog? log)
    {
        var models = new Dictionary<TextureVariant, TexturedModel>();
        foreach (var variant in Enum.GetValues<TextureVariant>())
        {
            var path = Path.Combine(directory, StairsLook.FileName(variant));
            if (TexturedModel.TryLoad(path, StairsLook.Parts, log) is { } model)
            {
                models[variant] = model;
            }
        }

        return new StairsView(models);
    }

    /// <summary>
    /// Draws the staircase if it has begun to rise and <paramref name="view"/> may see it. <paramref name="surfaces"/> and
    /// <paramref name="textures"/>: the wall variants of the level and the ones really loaded. Call between BeginFigures
    /// and EndFigures.
    /// </summary>
    public void Draw(Stairs? stairs, float seconds, ViewCone view, SurfaceVariants surfaces, LevelTextures textures)
    {
        if (stairs is null || stairs.RiseFraction <= 0f
            || !view.MaySee(stairs.Position + new Vector3(0f, Stairs.Height / 2f, 0f), BoundingRadius))
        {
            return;
        }

        var place = Matrix4x4.CreateTranslation(stairs.Position - new Vector3(0f, StairsLook.Sink(stairs.RiseFraction), 0f));
        var lit = StairsLook.ShowsGlow(stairs.CanInteract);
        var glow = StairsLook.GlowTint(seconds);
        if (Model(stairs, surfaces, textures) is { } model)
        {
            // Nothing of the staircase moves in its file: both parts stand where they are, the whole of it rises.
            var turns = _turns.AsSpan();
            var frames = _frames.AsSpan();
            turns.Fill(Matrix4x4.Identity);
            model.Rig.Pose(turns, place, frames);
            model.DrawPart(StonePart, frames[StonePart]);
            if (lit)
            {
                model.DrawPart(GlowPart, frames[GlowPart], glow);
            }
        }
        else
        {
            DrawPlaceholder(place, lit, glow);
        }
    }

    public void Dispose()
    {
        foreach (var model in _models.Values)
        {
            model.Dispose();
        }
    }

    private static int Index(string name)
    {
        for (var i = 0; i < StairsLook.Parts.Count; i++)
        {
            if (StairsLook.Parts[i].Name == name)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"The staircase has no part {name}.");
    }

    // The variant the level really draws on the walls there (a missing B or C falls back to A there, too), else variant
    // A of the staircase, else none.
    private TexturedModel? Model(Stairs stairs, SurfaceVariants surfaces, LevelTextures textures)
    {
        var wanted = textures.Resolve(SurfaceKind.Wall, StairsLook.Variant(surfaces, stairs.Position));
        var pick = StairsLook.Pick(wanted, _models.ContainsKey(wanted), _models.ContainsKey(TextureVariant.A));
        return pick is { } variant ? _models[variant] : null;
    }

    // The coded staircase from before the model: a pole, 16 steps winding round it, a light on top once it stands.
    private static void DrawPlaceholder(Matrix4x4 place, bool lit, float glow)
    {
        ShadedBox.Draw(Matrix4x4.CreateTranslation(0f, Stairs.Height / 2f, 0f) * place, new Vector3(0.35f, Stairs.Height, 0.35f), Pole);
        for (var step = 0; step < Steps; step++)
        {
            var angle = step * (2f * MathF.PI / 8f);
            var height = (step + 0.5f) * Stairs.Height / Steps;
            var stepPlace =
                Matrix4x4.CreateTranslation(Stairs.Radius / 2f, height, 0f) *
                Matrix4x4.CreateRotationY(angle) *
                place;
            ShadedBox.Draw(stepPlace, new Vector3(Stairs.Radius, 0.12f, 0.55f), Stone);
        }

        if (lit)
        {
            var color = new Color((byte)(Light.R * glow), (byte)(Light.G * glow), (byte)(Light.B * glow), (byte)255);
            // Under the ceiling: its top ends where the box of the staircase does (issue #727).
            ShadedBox.Draw(
                Matrix4x4.CreateTranslation(0f, StairsLook.PlaceholderLampMiddle, 0f) * place,
                new Vector3(0.5f, StairsLook.PlaceholderLampThickness, 0.5f),
                color);
        }
    }
}
