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
