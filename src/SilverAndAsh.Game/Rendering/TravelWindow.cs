using System.Numerics;
using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Game.Input;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The destination choice of the teleporters (issue #480, design A of #477): T or the Y button at an unlocked teleporter
/// opens it, the map shows a mark per unlocked teleporter. Arrows or the D-pad choose, Enter, the A button, a double-click
/// or the button travel; T, Esc or the B button close it. Pure presentation: which destination is free or sealed comes
/// from Core (<see cref="GameWorld.TravelChoices"/>), and the travel goes to Core as input
/// (<see cref="Core.Input.PlayerInput.TeleportTo"/>).
/// </summary>
internal sealed class TravelWindow
{
    /// <summary>The key that opens and closes the window.</summary>
    public const KeyboardKey Key = KeyboardKey.T;

    /// <summary>The hint at an unlocked teleporter.</summary>
    public const string Hint = "Travel";

    private const string SealedHint = "Sealed: wait for the fight to end";

    // The cursor appears in the middle of the window when it opens; see BoonWindow.ClickDelaySeconds.
    private const float ClickDelaySeconds = BoonWindow.ClickDelaySeconds;
    private const double DoubleClickSeconds = 0.4;

    private static readonly Color Veil = new(8, 6, 12, 150);
    private static readonly Color MapGround = new(14, 12, 18, 220);
    private static readonly Color RoomFloor = new(58, 56, 62, 255);
    private static readonly Color RoomEdge = new(84, 82, 90, 255);
    private static readonly Color Hatch = new(30, 28, 34, 255);
    private static readonly Color Pale = new(214, 210, 200, 255);
    private static readonly Color Grey = new(110, 108, 114, 255);
    private static readonly Color Dark = new(10, 8, 12, 255);
    private static readonly Color Gold = new(232, 196, 96, 255);
    private static readonly Color KeyColour = new(230, 190, 90, 255);
    private static readonly Color HintText = new(220, 220, 228, 255);

    private TravelLayout? _layout;
    private float _openSeconds;
    private int? _lastClicked;
    private double _lastClickTime = double.NegativeInfinity;

    /// <summary>True while the window is open.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>The destination shown on the right (a teleporter number), or null.</summary>
    public int? Selected { get; private set; }

    /// <summary>
    /// The window's own keys in this frame: T or Y opens it when <paramref name="choices"/> has entries (the player stands
    /// on an unlocked teleporter), T or B closes it. Returns true if it opened or closed.
    /// </summary>
    public bool Toggle(IReadOnlyList<TravelChoice> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        var toggle = Raylib.IsKeyPressed(Key) || Pressed(GamepadButton.RightFaceUp);
        if (IsOpen && (toggle || Pressed(GamepadButton.RightFaceRight)))
        {
            Close();
            return true;
        }

        return !IsOpen && toggle && Open(choices);
    }

    /// <summary>Opens the window with the first free destination selected; not if <paramref name="choices"/> is empty.</summary>
    public bool Open(IReadOnlyList<TravelChoice> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        if (choices.Count == 0)
        {
            return false;
        }

        IsOpen = true;
        _openSeconds = 0f;
        _lastClicked = null;
        Selected = TravelCursor.First(choices);
        return true;
    }

    /// <summary>Closes the window (Esc, or the player left the teleporter).</summary>
    public void Close()
    {
        IsOpen = false;
        Selected = null;
    }

    /// <summary>
    /// The window's frame: closes it when the player can no longer travel (<paramref name="choices"/> empty: they left the
    /// teleporter, died, or its room was sealed), moves the selection and returns the destination the player travels to
    /// in this frame (and closes the window), or null.
    /// </summary>
    public int? Update(GameWorld world, IReadOnlyList<TravelChoice> choices, float frameSeconds)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(choices);
        if (!IsOpen)
        {
            return null;
        }

        if (choices.Count == 0)
        {
            Close();
            return null;
        }

        _openSeconds += frameSeconds;
        Selected = TravelCursor.Keep(choices, Selected);
        if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.Right)
            || Pressed(GamepadButton.LeftFaceDown) || Pressed(GamepadButton.LeftFaceRight))
        {
            Selected = TravelCursor.Step(choices, Selected, +1);
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.Left)
            || Pressed(GamepadButton.LeftFaceUp) || Pressed(GamepadButton.LeftFaceLeft))
        {
            Selected = TravelCursor.Step(choices, Selected, -1);
        }

        var travel = ((Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter)) && !FullScreenToggle.AltHeld)
            || Pressed(GamepadButton.RightFaceDown);
        if (_openSeconds >= ClickDelaySeconds && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            travel |= Click(world, choices);
        }

        var chosen = choices.FirstOrDefault(choice => choice.Teleporter.Id == Selected);
        if (!travel || chosen is not { CanTravel: true })
        {
            return null;
        }

        Close();
        return chosen.Teleporter.Id;
    }

    /// <summary>Draws the window over the game; <paramref name="world"/> gives the rooms of the map.</summary>
    public void Draw(GameWorld world, IReadOnlyList<TravelChoice> choices)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(choices);
        var canvas = MenuArt.Canvas;
        var page = Page(world, choices, canvas);
        MenuArt.Shade(Veil);
        MenuArt.Draw(page.Frame, canvas);

        MenuArt.Begin(canvas);
        Raylib.DrawRectangleRec(ToRectangle(page.MapArea), MapGround);
        Raylib.DrawRectangleLinesEx(ToRectangle(page.MapArea), 1f, RoomEdge);
        Raylib.DrawRectangleLinesEx(ToRectangle(page.Detail), 1f, RoomEdge);
        DrawRooms(world, page.Map);

        var mouse = canvas.ToCanvas(Raylib.GetMousePosition());
        var hovered = TravelLayout.SpotAt(page, mouse);
        foreach (var spot in page.Spots)
        {
            DrawMark(TravelLayout.MarkOf(spot.Choice, Selected), spot.Centre);
            MenuArt.DrawText(spot.Name with { Color = spot.Choice.Teleporter.Id == Selected ? MenuTextColor.Selected : MenuTextColor.Second });
        }

        foreach (var (mark, centre) in page.Legend)
        {
            DrawMark(mark, centre);
        }

        if (page.Spots.Any(spot => spot.Choice.Teleporter.Id == hovered && spot.Choice.State == TravelState.Sealed))
        {
            MenuArt.DrawText(new MenuText(SealedHint, (int)mouse.X + 10, (int)mouse.Y + 12, 1, MenuTextColor.Main));
        }

        MenuArt.End();
    }

    /// <summary>"[T] Travel" on the line of the interaction hint, when nothing else is offered there.</summary>
    public static void DrawHint()
    {
        const string key = "[T] ";
        var line = HudTextLines.Of(HudText.InteractionPrompt, Raylib.GetScreenHeight());
        var keyWidth = PixelText.Measure(key, line.FontSize);
        var x = (Raylib.GetScreenWidth() - keyWidth - PixelText.Measure(Hint, line.FontSize)) / 2;
        PixelText.Draw(key, x, line.Top, line.FontSize, KeyColour);
        PixelText.Draw(Hint, x + keyWidth, line.Top, line.FontSize, HintText);
    }

    private TravelPage Page(GameWorld world, IReadOnlyList<TravelChoice> choices, MenuCanvas canvas)
    {
        _layout ??= new TravelLayout(PixelText.MeasureScaled, PixelText.CapHeight(1));
        var rooms = world.Rooms.Where(room => room.IsDiscovered).SelectMany(room => room.Cells).ToList();
        return _layout.Page(canvas, choices, Selected, rooms);
    }

    // A click on a free mark chooses it; a second click on it soon after, or a click on the button, travels.
    private bool Click(GameWorld world, IReadOnlyList<TravelChoice> choices)
    {
        var canvas = MenuArt.Canvas;
        var page = Page(world, choices, canvas);
        var mouse = canvas.ToCanvas(Raylib.GetMousePosition());
        if (page.Button.Contains(mouse))
        {
            return true;
        }

        if (TravelLayout.SpotAt(page, mouse) is not { } target)
        {
            return false;
        }

        var now = Raylib.GetTime();
        var twice = target == _lastClicked && now - _lastClickTime <= DoubleClickSeconds;
        Selected = TravelCursor.Click(choices, Selected, target);
        _lastClicked = target;
        _lastClickTime = now;
        return twice && Selected == target;
    }

    private static void DrawRooms(GameWorld world, TravelMap map)
    {
        foreach (var room in world.Rooms.Where(room => room.IsDiscovered))
        {
            foreach (var (x, z) in room.Cells)
            {
                var corner = map.ToCanvas(x, z);
                Raylib.DrawRectangleV(corner, new Vector2(map.Size, map.Size), RoomFloor);
                if (room.IsSealed)
                {
                    // Diagonal stripes: the room is sealed while the fight goes on.
                    for (var s = 0f; s < map.Size * 2f; s += 4f)
                    {
                        var from = corner + new Vector2(MathF.Min(s, map.Size), MathF.Max(0f, s - map.Size));
                        var to = corner + new Vector2(MathF.Max(0f, s - map.Size), MathF.Min(s, map.Size));
                        Raylib.DrawLineV(from, to, Hatch);
                    }
                }
            }
        }
    }

    private static void DrawMark(TravelMark mark, Vector2 centre)
    {
        const float r = 6f;
        var fill = mark switch
        {
            TravelMark.Chosen => Gold,
            TravelMark.Sealed => Grey,
            _ => Pale,
        };
        Diamond(centre, r + 1f, Dark);
        Diamond(centre, r, fill);
        switch (mark)
        {
            case TravelMark.Here:
                Raylib.DrawTriangle(centre + new Vector2(0f, -r - 3f), centre + new Vector2(5f, -r - 10f), centre + new Vector2(-5f, -r - 10f), Pale);
                break;
            case TravelMark.Chosen:
                Corners(centre, r + 4f, Gold);
                break;
            case TravelMark.Sealed:
                Raylib.DrawRectangleV(centre + new Vector2(-r, -1f), new Vector2(2f * r, 2f), Dark);
                break;
        }
    }

    private static void Diamond(Vector2 c, float r, Color colour)
    {
        // Counter-clockwise, as raylib wants it.
        Raylib.DrawTriangle(c + new Vector2(0f, -r), c + new Vector2(-r, 0f), c + new Vector2(0f, r), colour);
        Raylib.DrawTriangle(c + new Vector2(0f, -r), c + new Vector2(0f, r), c + new Vector2(r, 0f), colour);
    }

    private static void Corners(Vector2 c, float r, Color colour)
    {
        const float l = 4f;
        foreach (var (sx, sy) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
        {
            var corner = c + new Vector2(sx * r, sy * r);
            Raylib.DrawLineV(corner, corner - new Vector2(sx * l, 0f), colour);
            Raylib.DrawLineV(corner, corner - new Vector2(0f, sy * l), colour);
        }
    }

    // The first gamepad, if one is plugged in: Y opens, the D-pad chooses, A travels, B closes.
    private static bool Pressed(GamepadButton button) =>
        Raylib.IsGamepadAvailable(Gamepad) && Raylib.IsGamepadButtonPressed(Gamepad, button);

    private const int Gamepad = 0;

    private static Rectangle ToRectangle(MenuRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
