using System.Numerics;
using Raylib_cs;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The shader for the level: lantern light and fog, computed by the graphics card for every pixel.
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
        SetVector3("fogColor", ToVector3(LookSettings.FogColor));
        SetFloat("fogStart", LookSettings.FogStart);
        SetFloat("fogEnd", LookSettings.FogEnd);
        SetFloat("lanternMinimum", LookSettings.LanternMinimum);
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

    private void SetVector3(string name, Vector3 value) =>
        Raylib.SetShaderValue(Shader, Raylib.GetShaderLocation(Shader, name), value, ShaderUniformDataType.Vec3);

    private static Vector3 ToVector3(Color color) => new(color.R / 255f, color.G / 255f, color.B / 255f);
}
