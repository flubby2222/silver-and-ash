using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// A small picture (render texture) the 3D world is drawn into instead of the screen.
/// Afterwards it is scaled up to the window WITHOUT smoothing ("nearest neighbour"),
/// which gives the hard pixel edges of the PS2 era.
/// </summary>
internal sealed class LowResolutionTarget : IDisposable
{
    private readonly RenderTexture2D _texture;

    public LowResolutionTarget(int width, int height)
    {
        Width = width;
        Height = height;
        _texture = Raylib.LoadRenderTexture(width, height);

        // Point = no smoothing when scaling: every small pixel becomes a sharp block.
        Raylib.SetTextureFilter(_texture.Texture, TextureFilter.Point);
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Everything drawn after this lands in the small picture instead of on the screen.</summary>
    public void BeginDrawing() => Raylib.BeginTextureMode(_texture);

    /// <summary>Back to drawing on the screen.</summary>
    public void EndDrawing() => Raylib.EndTextureMode();

    /// <summary>
    /// Draws the small picture onto the screen, as large as possible without distortion.
    /// Must be called between <c>Raylib.BeginDrawing()</c> and <c>Raylib.EndDrawing()</c>.
    /// </summary>
    public void DrawToScreen()
    {
        var target = Letterbox.Fit(Width, Height, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());

        // Render textures are stored upside down (OpenGL convention) - the negative height flips them back.
        var source = new Rectangle(0, 0, Width, -Height);
        var destination = new Rectangle(target.X, target.Y, target.Width, target.Height);
        Raylib.DrawTexturePro(_texture.Texture, source, destination, Vector2.Zero, 0f, Color.White);
    }

    public void Dispose() => Raylib.UnloadRenderTexture(_texture);
}
