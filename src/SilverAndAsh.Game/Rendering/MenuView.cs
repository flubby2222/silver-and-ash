using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Game.Input;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// A column of buttons in the middle of the screen, for the title screen, the Esc menu and the settings. Works with the mouse
/// (point, click) and the keyboard (Up/Down, Enter or Space; Left/Right for a value row). Pure presentation and
/// input - what a button does decides the caller.
/// </summary>
internal sealed class MenuView
{
    private const int ButtonWidth = 380;
    private const int FontSize = 26;

    private readonly int _buttonHeight;
    private readonly int _gap;

    private static readonly Color Panel = new(24, 20, 24, 230);
    private static readonly Color Border = new(90, 84, 92, 255);
    private static readonly Color Highlight = new(230, 190, 90, 255);
    private static readonly Color Text = new(220, 220, 228, 255);

    private Vector2 _lastMouse = new(float.NaN, float.NaN);
    private int? _held;

    /// <summary>
    /// A menu with buttons <paramref name="buttonHeight"/> pixels high, <paramref name="gap"/> pixels apart - lower
    /// for menus with many rows (the settings), so they still fit into a window 480 pixels high.
    /// </summary>
    public MenuView(int buttonHeight = 50, int gap = 14)
    {
        _buttonHeight = buttonHeight;
        _gap = gap;
    }

    /// <summary>The height of <paramref name="count"/> buttons from the top of the first to the bottom of the last.</summary>
    public int Height(int count) => count * (_buttonHeight + _gap) - _gap;

    /// <summary>The button the keyboard (or the mouse pointer) is on.</summary>
    public int Selected { get; private set; }

    /// <summary>
    /// True for the frame in which the selection moved, a value row was nudged or a button was chosen - the menu click
    /// sounds then (issue #291).
    /// </summary>
    public bool Clicked { get; private set; }

    /// <summary>
    /// Keeps the selection on <paramref name="index"/> - on the slider being dragged (issue #426) -, so the pointer does
    /// not move it to the rows it passes; null lets the pointer select again.
    /// </summary>
    public void Hold(int? index)
    {
        _held = index;
        Selected = index ?? Selected;
    }

    /// <summary>
    /// Call when the menu appears: the selection starts at the top, and wherever the pointer happens to rest counts
    /// as "not moved" - so it does not snatch the selection before the player touches the mouse.
    /// </summary>
    public void Open()
    {
        Selected = 0;
        _held = null;
        _lastMouse = Raylib.GetMousePosition();
    }

    /// <summary>
    /// Reads mouse and keyboard for buttons <paramref name="items"/> starting at height <paramref name="top"/>.
    /// Returns the button that was chosen this frame (-1 for none). <paramref name="step"/> is -1 / +1 when the
    /// selected button was nudged left or right with the arrow keys - for value rows.
    /// </summary>
    public int Update(IReadOnlyList<string> items, int top, out int step) =>
        Update(items.Select((_, i) => ButtonRect(i, top)).ToArray(), out step);

    /// <summary>
    /// As above, for the entries <paramref name="rows"/> of a menu laid out on <paramref name="canvas"/>
    /// (<see cref="MenuLayout"/>, issue #371) - the pointer is tested against them where the canvas shows them.
    /// </summary>
    public int Update(IReadOnlyList<MenuRect> rows, MenuCanvas canvas, out int step) =>
        Update(rows.Select(row => new Rectangle(canvas.Screen.X + (row.X * canvas.Scale), canvas.ToWindowY(row.Y),
            row.Width * canvas.Scale, row.Height * canvas.Scale)).ToArray(), out step);

    private int Update(Rectangle[] buttons, out int step)
    {
        Selected = Math.Clamp(Selected, 0, buttons.Length - 1);
        var before = Selected;
        var chosen = Read(buttons, out step);
        Clicked = Selected != before || step != 0 || chosen >= 0;
        return chosen;
    }

    private int Read(Rectangle[] buttons, out int step)
    {
        step = 0;

        if (Raylib.IsKeyPressed(KeyboardKey.Down))
        {
            Selected = (Selected + 1) % buttons.Length;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Up))
        {
            Selected = (Selected + buttons.Length - 1) % buttons.Length;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Left))
        {
            step = -1;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Right))
        {
            step = 1;
        }

        // The pointer only takes over the selection when it moves - so the keyboard is not fought by a resting mouse.
        var mouse = Raylib.GetMousePosition();
        var hovered = -1;
        for (var i = 0; i < buttons.Length; i++)
        {
            if (Raylib.CheckCollisionPointRec(mouse, buttons[i]))
            {
                hovered = i;
            }
        }

        if (hovered >= 0 && mouse != _lastMouse && _held is null)
        {
            Selected = hovered;
        }

        _lastMouse = mouse;

        // A click only chooses its button; the sliders of the settings follow the mouse on their own (issue #426).
        if (hovered >= 0 && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            Selected = hovered;
            return hovered;
        }

        // Alt + Enter switches to full screen (issue #350) and must not also press the selected button.
        var enter = Raylib.IsKeyPressed(KeyboardKey.Enter) && !FullScreenToggle.AltHeld;
        return enter || Raylib.IsKeyPressed(KeyboardKey.Space) ? Selected : -1;
    }

    /// <summary>Draws the buttons; rows in <paramref name="valueRows"/> get small arrows left and right.</summary>
    public void Draw(IReadOnlyList<string> items, int top, params int[] valueRows)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var rect = ButtonRect(i, top);
            Raylib.DrawRectangleRec(rect, Panel);
            Raylib.DrawRectangleLinesEx(rect, i == Selected ? 3f : 1f, i == Selected ? Highlight : Border);

            var width = PixelText.Measure(items[i], FontSize);
            var textY = (int)(rect.Y + (_buttonHeight - FontSize) / 2f);
            PixelText.Draw(items[i], (int)(rect.X + (rect.Width - width) / 2f), textY, FontSize, i == Selected ? Highlight : Text);

            if (valueRows.Contains(i))
            {
                PixelText.Draw("<", (int)rect.X + 16, textY, FontSize, Text);
                PixelText.Draw(">", (int)(rect.X + rect.Width) - 30, textY, FontSize, Text);
            }
        }
    }

    /// <summary>Big centred text, e.g. the game's name or "PAUSED".</summary>
    public static void DrawTitle(string text, int y, int fontSize, Color color)
    {
        var width = PixelText.Measure(text, fontSize);
        PixelText.Draw(text, (Raylib.GetScreenWidth() - width) / 2, y, fontSize, color);
    }

    private Rectangle ButtonRect(int index, int top) => new(
        (Raylib.GetScreenWidth() - ButtonWidth) / 2f, top + index * (_buttonHeight + _gap), ButtonWidth, _buttonHeight);
}
