using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The HUD (issue #369): the approved design A3 with ornament V3, drawn from the parts picture
/// <c>assets/hud/hud_parts.png</c> where <see cref="HudLayout"/> puts them. The HUD is laid out on a canvas of 480
/// lines over the picture of the 3D world and scaled with it, so both keep their proportions in every window. Only the
/// hints under the crosshair stay in full window resolution (<see cref="HudTextLines"/>). Pure presentation - what it
/// shows comes from <see cref="HudReadout"/>.
/// </summary>
internal sealed class HudView : IDisposable
{
    /// <summary>How long the pickup toast stays, in seconds.</summary>
    public const float ToastSeconds = 3f;

    /// <summary>The consecrated time blinks in its last seconds.</summary>
    private const float BlinkFrom = 3f;

    private static readonly Color HintColor = new(200, 200, 210, 255);
    private static readonly Color HintOutline = new(12, 10, 16, 180);

    private readonly HudParts _parts;
    private readonly HudLayout _layout;
    private readonly Texture2D _texture;
    private string? _toast;
    private float _toastLeft;
    private bool _disposed;

    private HudView(HudParts parts, PixelFont font, Texture2D texture)
    {
        _parts = parts;
        _layout = new HudLayout(parts, font);
        _texture = texture;
    }

    /// <summary>
    /// Loads the parts after the window is open (the picture becomes a texture on the graphics card). Null - with a
    /// warning in the log - if a file is missing or broken: the game then runs without a HUD.
    /// </summary>
    public static HudView? Load(string hudDirectory, string fontsDirectory, FileLog? log)
    {
        try
        {
            var parts = HudParts.Parse(File.ReadAllText(Path.Combine(hudDirectory, "hud_parts.json")));
            var font = PixelFont.Parse(File.ReadAllText(Path.Combine(fontsDirectory, "kapitell.json")));
            var imagePath = Path.Combine(hudDirectory, parts.Image);
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException("HUD parts picture missing.", imagePath);
            }

            var texture = Raylib.LoadTexture(imagePath);
            if (texture.Width == 0)
            {
                throw new InvalidOperationException($"HUD parts picture '{parts.Image}' could not be read.");
            }

            Raylib.SetTextureFilter(texture, TextureFilter.Point);
            return new HudView(parts, font, texture);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or System.Text.Json.JsonException or FormatException
                                              or KeyNotFoundException or InvalidOperationException)
        {
            log?.Warning("HUD parts not loaded, the game runs without a HUD.", exception);
            return null;
        }
    }

    /// <summary>Starts the toast when <paramref name="playerId"/> took something - a new find replaces the one shown.</summary>
    public void OnEvent(GameEvent gameEvent, int playerId)
    {
        if (PickupToast.TextFor(gameEvent, playerId) is { } text)
        {
            _toast = text;
            _toastLeft = ToastSeconds;
        }
    }

    public void Update(float frameSeconds)
    {
        _toastLeft = MathF.Max(0f, _toastLeft - frameSeconds);
        if (_toastLeft == 0f)
        {
            _toast = null;
        }
    }

    /// <summary>A new run: no toast left from the last one.</summary>
    public void Reset()
    {
        _toast = null;
        _toastLeft = 0f;
    }

    /// <summary>
    /// Draws the HUD of <paramref name="player"/> over a picture of the 3D world <paramref name="pictureWidth"/> x
    /// <paramref name="pictureHeight"/> pixels, placed in the window as <see cref="LowResolutionTarget"/> places it.
    /// </summary>
    public void Draw(GameWorld world, Player player, int pictureWidth, int pictureHeight)
    {
        var readout = HudReadout.Of(world, player);

        // The consecrated time blinks in its last seconds: off every other quarter second.
        var secondsLeft = readout.ConsecratedSecondsLeft;
        if (secondsLeft > 0f && secondsLeft < BlinkFrom && (int)(secondsLeft * 4f) % 2 == 1)
        {
            readout = readout with { ConsecratedSecondsLeft = 0f };
        }

        var canvas = HudCanvas.For(pictureWidth);
        var picture = _layout.Arrange(readout, _toast, canvas);
        var screen = Letterbox.Fit(pictureWidth, pictureHeight, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        var camera = new Camera2D(new Vector2(screen.X, screen.Y), Vector2.Zero, 0f, screen.Width / canvas.Width);

        Raylib.BeginMode2D(camera);
        foreach (var part in picture.Parts)
        {
            var source = _parts[part.Name];
            Raylib.DrawTextureRec(_texture, new Rectangle(source.X, source.Y, part.Width, source.Height),
                new Vector2(part.X, part.Y), Color.White);
        }

        // The reload veil over the weapon picture (issue #370), shrinking as the reload comes on; its right column is
        // the light front edge.
        var veilColor = ColorOf(_parts.ReloadVeil, _parts.ReloadVeilAlpha);
        var veilEdge = ColorOf(_parts.ReloadVeilEdge, _parts.ReloadVeilEdgeAlpha);
        foreach (var veil in picture.ReloadVeils)
        {
            Raylib.DrawRectangle(veil.X, veil.Y, veil.Width - 1, veil.Height, veilColor);
            Raylib.DrawRectangle(veil.Right - 1, veil.Y, 1, veil.Height, veilEdge);
        }

        var shadow = ColorOf(_parts.TextShadow);
        foreach (var text in picture.Texts)
        {
            PixelText.DrawScaled(text.Text, text.X + 1, text.Top + 1, text.Scale, shadow);
            PixelText.DrawScaled(text.Text, text.X, text.Top, text.Scale, ColorOf(text.Color));
        }

        Raylib.EndMode2D();

        if (!player.IsDead && player.AmmoHint == AmmoHint.Reload)
        {
            DrawHint(HudTextLines.Of(HudText.ReloadHint, Raylib.GetScreenHeight()), "Reload [R]");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Raylib.UnloadTexture(_texture);
        _disposed = true;
    }

    /// <summary>
    /// A short hint centred under the crosshair (issue #259), in full window resolution with a dark outline, large
    /// enough to catch the eye in a fight, on light walls and in the dark alike.
    /// </summary>
    private static void DrawHint(HudTextLine line, string text)
    {
        var left = (Raylib.GetScreenWidth() / 2) - (PixelText.Measure(text, line.FontSize) / 2);
        foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
        {
            PixelText.Draw(text, left + dx, line.Top + dy, line.FontSize, HintOutline);
        }

        PixelText.Draw(text, left, line.Top, line.FontSize, HintColor);
    }

    private Color ColorOf(HudTextColor color) => ColorOf(color switch
    {
        HudTextColor.Second => _parts.TextSecond,
        HudTextColor.Pickup => _parts.TextPickup,
        HudTextColor.Hint => _parts.TextHint,
        _ => _parts.TextMain,
    });

    private static Color ColorOf(HudColor color, float alpha = 1f) =>
        new(color.R, color.G, color.B, (byte)MathF.Round(255f * Math.Clamp(alpha, 0f, 1f)));
}
