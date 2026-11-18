using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The menus' look (issue #371): the approved design "C2 Slim", drawn from the parts picture
/// <c>assets/menu/menu_parts.png</c> where <see cref="MenuLayout"/> puts them. Like the HUD the menus are laid out on a
/// canvas of 480 lines over the picture of the 3D world and scaled with it (<see cref="MenuCanvas"/>), so they keep
/// their proportions in every window. Loaded once per window and held here, like the font; without the parts (missing
/// or broken files, with a warning in the log) panels and cursors are drawn as plain frames - the menus keep working.
/// </summary>
internal static class MenuArt
{
    // Without the parts: the colours of the design (art bible, section "Menus").
    private static readonly Color PlainPanel = new(26, 24, 36, 235);
    private static readonly Color PlainEdge = new(200, 200, 205, 255);
    private static readonly Color PlainGold = new(224, 172, 64, 255);
    private static readonly Color PlainTrough = new(10, 8, 12, 255);

    // Panels and tabs let the game shine through a little (issue #390): 70 % opaque - texts, cursors and sliders stay whole.
    private static readonly Color PanelTint = new(255, 255, 255, 179);

    // The frame of values has no ground, only this edge (issue #390).
    private static readonly Color SheetEdge = new(163, 175, 187, 170);

    private static MenuParts? _parts;
    private static Texture2D _texture;
    private static MenuLayout? _layout;

    // Where the frame of values starts: under the HUD's top left group once the HUD is loaded (issue #387).
    private static int _sheetTop = MenuLayout.SheetLeft;

    /// <summary>Puts the frame of values under the top left group of <paramref name="hud"/> (at the top without a HUD).</summary>
    public static void PlaceUnder(HudView? hud)
    {
        _sheetTop = hud is null ? MenuLayout.SheetLeft : MenuLayout.SheetTopUnder(hud.Layout);
        _layout = null;
    }

    /// <summary>Loads the parts after the window is open (the picture becomes a texture on the graphics card).</summary>
    public static void Load(string menuDirectory, FileLog? log)
    {
        try
        {
            var parts = MenuParts.Parse(File.ReadAllText(Path.Combine(menuDirectory, "menu_parts.json")));
            var imagePath = Path.Combine(menuDirectory, parts.Image);
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException("Menu parts picture missing.", imagePath);
            }

            var texture = Raylib.LoadTexture(imagePath);
            if (texture.Width == 0)
            {
                throw new InvalidOperationException($"Menu parts picture '{parts.Image}' could not be read.");
            }

            Raylib.SetTextureFilter(texture, TextureFilter.Point);
            _texture = texture;
            _parts = parts;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or System.Text.Json.JsonException or FormatException
                                              or KeyNotFoundException or InvalidOperationException)
        {
            log?.Warning("Menu parts not loaded, the menus are drawn as plain frames.", exception);
        }

        _layout = null;
    }

    /// <summary>Frees the texture before the window closes.</summary>
    public static void Unload()
    {
        if (_parts is null)
        {
            return;
        }

        Raylib.UnloadTexture(_texture);
        _parts = null;
    }

    /// <summary>The layout, measuring with the font the texts are drawn in.</summary>
    public static MenuLayout Layout => _layout ??= new MenuLayout(PixelText.MeasureScaled, PixelText.CapHeight(1), _sheetTop);

    /// <summary>The canvas of the window as it is now.</summary>
    public static MenuCanvas Canvas => MenuCanvas.For(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());

    /// <summary>A dark veil over the whole window, under a menu over the game.</summary>
    public static void Shade(Color color) =>
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), color);

    /// <summary>Starts drawing in canvas pixels; end with <see cref="End"/>.</summary>
    public static void Begin(MenuCanvas canvas) =>
        Raylib.BeginMode2D(new Camera2D(new Vector2(canvas.Screen.X, canvas.Screen.Y), Vector2.Zero, 0f, canvas.Scale));

    /// <summary>Back to window pixels.</summary>
    public static void End() => Raylib.EndMode2D();

    /// <summary>Draws <paramref name="picture"/> on <paramref name="canvas"/>: panels, cursors, sliders, tabs, then the texts.</summary>
    public static void Draw(MenuPicture picture, MenuCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(picture);
        Begin(canvas);
        foreach (var panel in picture.Panels)
        {
            DrawPanel(panel);
        }

        foreach (var tab in picture.Tabs)
        {
            DrawTab(tab);
        }

        foreach (var cursor in picture.Cursors)
        {
            DrawCursor(cursor);
        }

        foreach (var slider in picture.Sliders)
        {
            DrawSlider(slider);
        }

        foreach (var text in picture.Texts)
        {
            DrawText(text);
        }

        End();
    }

    /// <summary>
    /// The frame of values (issue #390) on <paramref name="canvas"/>: only its edge and the texts with their shadow - no
    /// dark ground, the game shows through.
    /// </summary>
    public static void DrawSheet(ValuesSheet sheet, MenuCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        Begin(canvas);
        Raylib.DrawRectangleLinesEx(ToRectangle(sheet.Frame), 1f, SheetEdge);
        foreach (var text in sheet.Texts)
        {
            DrawText(text);
        }

        End();
    }

    /// <summary>A text with its shadow one pixel below and right, in canvas pixels.</summary>
    public static void DrawText(MenuText text)
    {
        PixelText.DrawScaled(text.Text, text.X + 1, text.Top + 1, text.Scale, ColorOf(_parts?.TextShadow ?? new MenuColor(8, 6, 10)));
        PixelText.DrawScaled(text.Text, text.X, text.Top, text.Scale, ColorOf(text.Color));
    }

    /// <summary>A text in another colour than the menu's own (e.g. a weapon's tier), with its shadow.</summary>
    public static void DrawText(MenuText text, Color color)
    {
        PixelText.DrawScaled(text.Text, text.X + 1, text.Top + 1, text.Scale, ColorOf(_parts?.TextShadow ?? new MenuColor(8, 6, 10)));
        PixelText.DrawScaled(text.Text, text.X, text.Top, text.Scale, color);
    }

    /// <summary>The colour of a menu text.</summary>
    public static Color ColorOf(MenuTextColor color)
    {
        if (_parts is not { } parts)
        {
            return color switch
            {
                MenuTextColor.Selected => PlainGold,
                MenuTextColor.Quiet => new Color(120, 120, 132, 255),
                MenuTextColor.Second => new Color(190, 186, 176, 255),
                _ => new Color(226, 220, 204, 255),
            };
        }

        return ColorOf(color switch
        {
            MenuTextColor.Selected => parts.TextSelected,
            MenuTextColor.Quiet => parts.TextQuiet,
            MenuTextColor.Second => parts.TextSecond,
            _ => parts.TextMain,
        });
    }

    private static void DrawPanel(MenuPanel panel)
    {
        if (_parts is not { } parts)
        {
            Raylib.DrawRectangleRec(ToRectangle(panel.Rect), PlainPanel with { A = PanelTint.A });
            Raylib.DrawRectangleLinesEx(ToRectangle(panel.Rect), 1f, PlainEdge);
            return;
        }

        DrawPieces(parts.Panel(panel.Rect), PanelTint);
        if (panel.Gable)
        {
            DrawPiece(parts.Gable(panel.Rect), PanelTint);
        }
    }

    private static void DrawCursor(MenuCursor cursor)
    {
        if (_parts is not { } parts)
        {
            Raylib.DrawRectangleLinesEx(ToRectangle(cursor.Rect), cursor.Small ? 1f : 2f, PlainGold);
            return;
        }

        DrawPieces(parts.Cursor(cursor.Rect, cursor.Small));
    }

    private static void DrawSlider(MenuSlider slider)
    {
        if (_parts is not { } parts)
        {
            var trough = new Rectangle(slider.Left, slider.CentreY - 3, MenuLayout.SliderRight - MenuLayout.SliderLeft, 6);
            Raylib.DrawRectangleRec(trough, PlainTrough);
            Raylib.DrawRectangleRec(trough with { Width = trough.Width * slider.Fraction }, PlainGold);
            Raylib.DrawRectangleLinesEx(trough, 1f, PlainEdge);
            return;
        }

        var pieces = parts.Slider(slider.Left, slider.CentreY, slider.Fraction);
        DrawPiece(pieces.Empty);
        if (pieces.Full is { } full)
        {
            DrawPiece(full);
        }

        DrawPiece(pieces.Knob);
    }

    private static void DrawTab(MenuTab tab)
    {
        if (_parts is not { } parts)
        {
            var outline = MenuLayout.TabOutline(tab.Index, tab.Open, tab.Left, tab.Top);
            Raylib.DrawRectangleRec(ToRectangle(outline), PlainPanel with { A = PanelTint.A });
            Raylib.DrawRectangleLinesEx(ToRectangle(outline), 1f, tab.Open ? PlainGold : PlainEdge);
            return;
        }

        DrawPiece(parts.Tab(tab.Index, tab.Open, tab.Left, tab.Top), PanelTint);
    }

    private static void DrawPieces(IReadOnlyList<MenuPiece> pieces, Color? tint = null)
    {
        foreach (var piece in pieces)
        {
            DrawPiece(piece, tint);
        }
    }

    private static void DrawPiece(MenuPiece piece, Color? tint = null)
    {
        if (piece.Target.Width <= 0 || piece.Target.Height <= 0)
        {
            return;
        }

        Raylib.DrawTexturePro(_texture, ToRectangle(piece.Source), ToRectangle(piece.Target), Vector2.Zero, 0f, tint ?? Color.White);
    }

    private static Rectangle ToRectangle(MenuRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    private static Color ColorOf(MenuColor color) => new(color.R, color.G, color.B, (byte)255);
}
