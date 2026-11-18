using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// A small picture (render texture) the 3D world is drawn into instead of the screen.
/// Afterwards it is scaled up to the window WITHOUT smoothing ("nearest neighbour"),
/// which gives the hard pixel edges of the PS2 era. Its height is fixed, its width follows the shape of the window
/// (<see cref="PictureWidth"/>, issue #350) - so the picture fills the window without bars and without stretching.
/// </summary>
internal sealed class LowResolutionTarget : IDisposable
{
    private RenderTexture2D _texture;

    /// <summary>A picture <paramref name="height"/> lines high, as wide as the window's shape asks for.</summary>
    public LowResolutionTarget(int height) => Create(WidthForWindow(height, Width), height);

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>
    /// Fits the picture to the window as it is now: <paramref name="height"/> lines (picture variants, issue #234), the
    /// width from the window's shape (issue #350). Call before drawing every frame - the window can be dragged, maximised
    /// or switched to full screen at any time. Does nothing if the size stays, and while the window is minimised.
    /// </summary>
    public void FitToWindow(int height) => Resize(WidthForWindow(height, Width), height);

    private void Resize(int width, int height)
    {
        if (width == Width && height == Height)
        {
            return;
        }

        Raylib.UnloadRenderTexture(_texture);
        Create(width, height);
    }

    /// <summary>Everything drawn after this lands in the small picture instead of on the screen.</summary>
    public void BeginDrawing() => Raylib.BeginTextureMode(_texture);

    /// <summary>Back to drawing on the screen.</summary>
    public void EndDrawing() => Raylib.EndTextureMode();

    /// <summary>
    /// Draws the small picture onto the screen, as large as possible without distortion - filling the whole window
    /// unless it is narrower than 5:4 or wider than 32:9.
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

    /// <summary>Saves the small picture as it is, in game resolution, as a PNG file (photo mode, issue #224).</summary>
    public void SaveAs(string path)
    {
        var image = Raylib.LoadImageFromTexture(_texture.Texture);

        // Stored upside down, like for drawing (see DrawToScreen).
        Raylib.ImageFlipVertical(ref image);
        Raylib.ExportImage(image, path);
        Raylib.UnloadImage(image);
    }

    public void Dispose() => Raylib.UnloadRenderTexture(_texture);

    // A minimised window has no size: then the picture keeps its width (or, at the start, takes the standard shape).
    private static int WidthForWindow(int height, int currentWidth)
    {
        var screenWidth = Raylib.GetScreenWidth();
        var screenHeight = Raylib.GetScreenHeight();
        return screenWidth <= 0 || screenHeight <= 0 || Raylib.IsWindowMinimized()
            ? currentWidth > 0 ? currentWidth : PictureWidth.For(0, 0, height)
            : PictureWidth.For(screenWidth, screenHeight, height);
    }

    private void Create(int width, int height)
    {
        Width = width;
        Height = height;
        _texture = Raylib.LoadRenderTexture(width, height);

        // Point = no smoothing when scaling: every small pixel becomes a sharp block.
        Raylib.SetTextureFilter(_texture.Texture, TextureFilter.Point);
    }
}
