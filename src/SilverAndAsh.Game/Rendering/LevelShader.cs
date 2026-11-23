using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The shader for the whole 3D world (level, figures, loot, effects): lantern light and fog - and in the picture variants
/// of issue #234 the dot pattern -, computed by the graphics card for every pixel.
/// The shader programs live in assets/shaders/level.vs and level.fs.
/// </summary>
internal sealed class LevelShader : IDisposable
{
    private readonly int _viewPositionLocation;
    private readonly int _flashLocation;
    private readonly int _glowLocation;
    private readonly int _seeThroughLocation;

    public LevelShader(string shaderDirectory)
    {
        Shader = Raylib.LoadShader(
            Path.Combine(shaderDirectory, "level.vs"),
            Path.Combine(shaderDirectory, "level.fs"));

        // A shader that does not compile is not a crash in raylib - it silently falls back to the default.
        // We want to know about it.
        if (!Raylib.IsShaderValid(Shader))
        {
            throw new InvalidOperationException($"The level shader in '{shaderDirectory}' could not be loaded.");
        }

        // "Locations" are the slots of the uniform variables in the compiled shader program.
        _viewPositionLocation = Raylib.GetShaderLocation(Shader, "viewPos");
        _flashLocation = Raylib.GetShaderLocation(Shader, "flash");
        _glowLocation = Raylib.GetShaderLocation(Shader, "glow");
        _seeThroughLocation = Raylib.GetShaderLocation(Shader, "seeThrough");
        SetFloat("glow", 0f);
        SetInt("seeThrough", 0);

        // These values do not change while playing - set them once.
        SetFloat("fogStart", LookSettings.FogStart);
        SetFloat("fogEnd", LookSettings.FogEnd);
        SetFloat("lanternMinimum", LookSettings.LanternMinimum);
        SetFloat("fadeStart", LookVariants.FadeStart);
        SetFloat("fadeEnd", LookVariants.FadeEnd);
        Apply(LookVariants.Default);
    }

    /// <summary>Switches the picture variant (issue #234): 15-bit pattern, fade after the pattern, fog colour.</summary>
    public void Apply(LookVariant look)
    {
        // With the pattern the fog colour lies exactly on a 5-bit step, so fully fogged pixels get no pattern of their
        // own and match the background (Dithering.SnapToStep).
        var fog = ToVector3(LookSettings.FogColor);
        if (look.Quantize)
        {
            fog = new Vector3(Dithering.SnapToStep(fog.X), Dithering.SnapToStep(fog.Y), Dithering.SnapToStep(fog.Z));
        }

        SetVector3("fogColor", fog);
        SetInt("quantize", look.Quantize ? 1 : 0);
        SetInt("patternFade", look.PatternFade ? 1 : 0);
    }

    public Shader Shader { get; }

    /// <summary>Tells the shader where the camera is. Call once per frame before drawing.</summary>
    public void SetCameraPosition(Vector3 position) =>
        Raylib.SetShaderValue(Shader, _viewPositionLocation, position, ShaderUniformDataType.Vec3);

    /// <summary>
    /// Starts drawing figures (boxes from <see cref="ShadedBox"/>) with lantern light and fog, like the level.
    /// Their corners are already in world positions, so the model matrix is set to "no change" (identity).
    /// </summary>
    public void BeginFigures()
    {
        Raylib.BeginShaderMode(Shader);
        Raylib.SetShaderValueMatrix(Shader, Raylib.GetShaderLocation(Shader, "matModel"), Matrix4x4.Identity);
        Raylib.SetShaderValue(Shader, Raylib.GetShaderLocation(Shader, "colDiffuse"), Vector4.One, ShaderUniformDataType.Vec4);
    }

    /// <summary>
    /// Lets what is drawn next flash: its surface is blended toward <paramref name="color"/> by
    /// <paramref name="amount"/> (0..1, 0 = no flash) before shading, lantern and fog. Used by models, whose colour comes
    /// from a texture and cannot simply be swapped like the colour of a box.
    /// </summary>
    public void SetFlash(Color color, float amount)
    {
        // Raylib collects triangles and sends them to the graphics card later; a value set now would also apply to
        // triangles collected before. So draw those first.
        Rlgl.DrawRenderBatchActive();
        var value = new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, Math.Clamp(amount, 0f, 1f));
        Raylib.SetShaderValue(Shader, _flashLocation, value, ShaderUniformDataType.Vec4);
    }

    /// <summary>
    /// Lets what is drawn next shine by itself (issue #480): the lantern never makes it darker than
    /// <paramref name="amount"/> (0..1, 0 = lit as usual); the fog still covers it far away.
    /// </summary>
    public void SetGlow(float amount)
    {
        // As with the flash: triangles collected before must not get the new value.
        Rlgl.DrawRenderBatchActive();
        Raylib.SetShaderValue(Shader, _glowLocation, Math.Clamp(amount, 0f, 1f), ShaderUniformDataType.Float);
    }

    /// <summary>
    /// Draws what comes next see-through (issue #609), still lit, fogged and in the picture variant like everything else:
    /// the shader keeps the alpha of the colour, the colour is blended over what lies behind, the picture's alpha stays
    /// (<see cref="KeepPictureAlpha"/>), and nothing writes the depth, so soft things do not cut holes into one another.
    /// Call between BeginFigures and EndFigures, after everything solid; end with <see cref="EndSeeThrough"/>.
    /// </summary>
    public void BeginSeeThrough()
    {
        // As with the flash: triangles collected before must stay solid.
        Rlgl.DrawRenderBatchActive();
        Raylib.SetShaderValue(Shader, _seeThroughLocation, 1, ShaderUniformDataType.Int);
        KeepPictureAlpha.Begin();
        Rlgl.DisableDepthMask();
    }

    /// <summary>Back to solid drawing.</summary>
    public void EndSeeThrough()
    {
        Rlgl.DrawRenderBatchActive();
        Rlgl.EnableDepthMask();
        KeepPictureAlpha.End();
        Raylib.SetShaderValue(Shader, _seeThroughLocation, 0, ShaderUniformDataType.Int);
    }

    /// <summary>Ends drawing figures.</summary>
    public void EndFigures() => Raylib.EndShaderMode();

    public void Dispose() => Raylib.UnloadShader(Shader);

    private void SetFloat(string name, float value) =>
        Raylib.SetShaderValue(Shader, Raylib.GetShaderLocation(Shader, name), value, ShaderUniformDataType.Float);

    private void SetInt(string name, int value) =>
        Raylib.SetShaderValue(Shader, Raylib.GetShaderLocation(Shader, name), value, ShaderUniformDataType.Int);

    private void SetVector3(string name, Vector3 value) =>
        Raylib.SetShaderValue(Shader, Raylib.GetShaderLocation(Shader, name), value, ShaderUniformDataType.Vec3);

    private static Vector3 ToVector3(Color color) => new(color.R / 255f, color.G / 255f, color.B / 255f);
}
