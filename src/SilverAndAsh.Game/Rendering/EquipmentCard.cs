using Raylib_cs;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The card after taking a piece of equipment (issue #317): its name in the colour of its tier and what it does, for a
/// few seconds on the left of the screen - away from the crosshair, the prompts and the displays in the corners.
/// A find is celebrated by its tier, quietly: Worn gets only the card, Forged and Royal a frame in their colour (Royal
/// with a short shine), Vampiric a pulsing frame and a short blood-red glow from the screen edges.
/// Only signal colours as accents - the card itself stays dark. Pure presentation.
/// </summary>
internal sealed class EquipmentCard
{
    /// <summary>How long the card is shown, in seconds (fading in and out included).</summary>
    public const float Duration = 4.5f;

    private const float FadeIn = 0.25f;
    private const float FadeOut = 0.6f;
    private const float FlashDuration = 0.9f;
    private const int Width = 380;
    private const int Padding = 14;
    private const int NameSize = 24;
    private const int TextSize = 18;
    private const int TierSize = 16;

    private static readonly Color Background = new(14, 11, 16, 215);
    private static readonly Color PlainFrame = new(70, 64, 74, 255);
    private static readonly Color Text = new(205, 205, 214, 255);
    private static readonly Color Note = new(140, 135, 145, 255);
    private static readonly Color VampireEdge = new(120, 0, 14, 255);

    private Equipment? _piece;

    // The piece's name with its status effect, if it carries one (issue #505: "Club - Marked").
    private string _name = string.Empty;
    private float _seconds;
    private float _flash;
    private float _time;

    /// <summary>Starts the card when <paramref name="playerId"/> took a piece - a new find replaces the one shown.</summary>
    public void OnEvent(GameEvent gameEvent, int playerId)
    {
        if (gameEvent is not EquipmentTaken taken || taken.PlayerId != playerId)
        {
            return;
        }

        _piece = taken.Piece;
        _name = taken.Label;
        _seconds = 0f;
        _flash = taken.Tier == ItemTier.Vampiric ? 1f : 0f;
    }

    public void Update(float frameSeconds)
    {
        _time += frameSeconds;
        _flash = MathF.Max(0f, _flash - frameSeconds / FlashDuration);
        if (_piece is null)
        {
            return;
        }

        _seconds += frameSeconds;
        if (_seconds >= Duration)
        {
            _piece = null;
        }
    }

    public void Draw()
    {
        DrawFlash();
        if (_piece is not { } piece)
        {
            return;
        }

        var fade = Math.Clamp(MathF.Min(_seconds / FadeIn, (Duration - _seconds) / FadeOut), 0f, 1f);
        var tier = TierColours.Of(piece.Tier, _time);
        var lines = Wrap(EquipmentCardText.Effect(piece), Width - (2 * Padding), TextSize);
        var note = EquipmentCardText.Note(piece);

        var height = Padding + NameSize + 6 + TierSize + 10 + (lines.Count * (TextSize + 4)) + (note is null ? 0 : TierSize + 6) + Padding;
        var left = 20;
        var top = (Raylib.GetScreenHeight() / 2) - 150;

        Raylib.DrawRectangle(left, top, Width, height, Faded(Background, fade));
        DrawFrame(piece.Tier, tier, left, top, height, fade);

        var y = top + Padding;
        PixelText.Draw(_name, left + Padding, y, NameSize, Faded(Readable(tier), fade));
        y += NameSize + 6;
        PixelText.Draw(EquipmentCardText.TierLine(piece),left + Padding, y, TierSize, Faded(Note, fade));
        y += TierSize + 10;
        foreach (var line in lines)
        {
            PixelText.Draw(line, left + Padding, y, TextSize, Faded(Text, fade));
            y += TextSize + 4;
        }

        if (note is not null)
        {
            PixelText.Draw(note, left + Padding, y + 2, TierSize, Faded(Note, fade));
        }
    }

    /// <summary>
    /// The prompt's text colour for <paramref name="tier"/>: the tier colour lifted towards the text colour, so even the
    /// dark wood brown of Worn reads well on a dark floor.
    /// </summary>
    public static Color Readable(Color tier) => Blend(tier, Text, 0.2f);

    /// <summary>Worn: a plain frame. Forged: its colour. Royal: its colour with a shine running once along the top. Vampiric: pulsing.</summary>
    private void DrawFrame(ItemTier tier, Color colour, int left, int top, int height, float fade)
    {
        var bounds = new Rectangle(left, top, Width, height);
        switch (tier)
        {
            case ItemTier.Worn:
                Raylib.DrawRectangleLinesEx(bounds, 1, Faded(PlainFrame, fade));
                break;
            case ItemTier.Forged:
                Raylib.DrawRectangleLinesEx(bounds, 2, Faded(colour, fade));
                break;
            case ItemTier.Royal:
                Raylib.DrawRectangleLinesEx(bounds, 2, Faded(colour, fade));
                var shine = _seconds / 1.2f;
                if (shine < 1f)
                {
                    var x = left + (int)((Width - 60) * shine);
                    Raylib.DrawRectangle(x, top, 60, 2, Faded(new Color(255, 240, 200, 255), fade * (1f - shine)));
                }

                break;
            default:
                Raylib.DrawRectangleLinesEx(bounds, 3, Faded(colour, fade));
                break;
        }
    }

    /// <summary>The Vampiric find: a short, dark blood-red glow from the screen edges - softer than being hit.</summary>
    private void DrawFlash()
    {
        if (_flash <= 0f)
        {
            return;
        }

        var width = Raylib.GetScreenWidth();
        var height = Raylib.GetScreenHeight();
        var edge = new Color(VampireEdge.R, VampireEdge.G, VampireEdge.B, (byte)(130 * _flash * _flash));
        var clear = new Color(VampireEdge.R, VampireEdge.G, VampireEdge.B, (byte)0);
        var band = Math.Max(width, height) / 7;
        Raylib.DrawRectangleGradientH(0, 0, band, height, edge, clear);
        Raylib.DrawRectangleGradientH(width - band, 0, band, height, clear, edge);
        Raylib.DrawRectangleGradientV(0, 0, width, band, edge, clear);
        Raylib.DrawRectangleGradientV(0, height - band, width, band, clear, edge);
    }

    /// <summary>Splits <paramref name="text"/> into lines no wider than <paramref name="width"/> pixels.</summary>
    private static List<string> Wrap(string text, int width, int size)
    {
        var lines = new List<string>();
        var line = string.Empty;
        foreach (var word in text.Split(' '))
        {
            var longer = line.Length == 0 ? word : $"{line} {word}";
            if (line.Length > 0 && PixelText.Measure(longer, size) > width)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = longer;
            }
        }

        lines.Add(line);
        return lines;
    }

    private static Color Faded(Color colour, float fade) => new(colour.R, colour.G, colour.B, (byte)(colour.A * fade));

    private static Color Blend(Color from, Color to, float amount) => new(
        (byte)(from.R + (to.R - from.R) * amount),
        (byte)(from.G + (to.G - from.G) * amount),
        (byte)(from.B + (to.B - from.B) * amount),
        (byte)255);
}
