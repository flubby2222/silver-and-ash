using Raylib_cs;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// "[F] Open" under the crosshair while the player looks at something they can use - or, if F would do nothing for
/// them right now, only the reason in a dimmer colour ("Locked - needs a key"). Equipment is named in the colour of its
/// tier ("[F] Take Rifle (Forged)", issue #317).
/// </summary>
internal static class InteractionPrompt
{
    private const int FontSize = 20;
    private static readonly Color Key = new(230, 190, 90, 255);
    private static readonly Color Text = new(220, 220, 228, 255);
    private static readonly Color NotPossible = new(160, 150, 150, 255);

    public static void Draw(IInteractable? target, Player player)
    {
        if (target is null)
        {
            return;
        }

        var offer = target.OfferFor(player);
        var key = offer.IsPossible ? "[F] " : string.Empty;
        var keyWidth = Raylib.MeasureText(key, FontSize);
        var total = keyWidth + Raylib.MeasureText(offer.Text, FontSize);
        var x = (Raylib.GetScreenWidth() - total) / 2;
        var y = Raylib.GetScreenHeight() / 2 + 40;
        Raylib.DrawText(key, x, y, FontSize, Key);
        var colour = !offer.IsPossible ? NotPossible
            : offer.Tier is { } tier ? EquipmentCard.Readable(TierColours.Of(tier, (float)Raylib.GetTime()))
            : Text;
        Raylib.DrawText(offer.Text, x + keyWidth, y, FontSize, colour);
    }
}
