using System.Collections.Frozen;
using System.Globalization;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Presentation;

/// <summary>Which of the HUD colours of <see cref="HudParts"/> a text is drawn in.</summary>
public enum HudTextColor
{
    /// <summary>Numbers and names.</summary>
    Main,

    /// <summary>Quieter texts (rounds while reloading).</summary>
    Second,

    /// <summary>Gold: only the pickup toast.</summary>
    Pickup,
}

/// <summary>A rectangle of the HUD canvas in design pixels.</summary>
public readonly record struct HudRect(int X, int Y, int Width, int Height)
{
    /// <summary>The first column right of the rectangle.</summary>
    public int Right => X + Width;

    /// <summary>The first row below the rectangle.</summary>
    public int Bottom => Y + Height;

    /// <summary>Whether this rectangle and <paramref name="other"/> share at least one pixel.</summary>
    public bool Overlaps(HudRect other) =>
        X < other.Right && other.X < Right && Y < other.Bottom && other.Y < Bottom;
}

/// <summary>
/// A part placed on the HUD canvas: drawn 1:1 with its top left corner at <paramref name="X"/>, <paramref name="Y"/>,
/// only its left <paramref name="Width"/> columns (less than the part's width for a bar filled part of the way).
/// </summary>
public readonly record struct HudPlacedPart(string Name, int X, int Y, int Width);

/// <summary>
/// A text placed on the HUD canvas in the pixel font: its left edge, the top of its capitals, the whole-number
/// enlargement and the colour. The game draws a dark shadow one pixel below and right of it.
/// </summary>
public readonly record struct HudPlacedText(string Text, int X, int Top, int Scale, HudTextColor Color);

/// <summary>
/// Everything the HUD draws in one frame, in drawing order: parts first, then the reload veils, then texts, then the
/// life edge. A reload veil (issue #370) lies over the picture of a weapon being reloaded, from the left; its rightmost
/// column is its light front edge.
/// </summary>
public sealed record HudPicture(IReadOnlyList<HudPlacedPart> Parts, IReadOnlyList<HudPlacedText> Texts, HudRect? LifeEdge)
{
    /// <summary>The reload veils, one per weapon being reloaded.</summary>
    public IReadOnlyList<HudRect> ReloadVeils { get; init; } = [];
}

/// <summary>
/// The canvas the HUD is laid out on, in design pixels: as wide as the picture of the 3D world (issue #350) and
/// 480 lines high - but never narrower than <see cref="MinimumWidth"/>. A narrower picture (4:3, 5:4) gets a canvas of
/// that width and its own shape instead, so it is taller than 480 and the HUD is drawn a little smaller: the bottom
/// groups would otherwise run into each other.
/// </summary>
public readonly record struct HudCanvas(int Width, int Height)
{
    /// <summary>The narrowest canvas: 16:10. All groups keep a gap there (tested).</summary>
    public const int MinimumWidth = 768;

    /// <summary>The design height (480 lines, like the picture).</summary>
    public const int DesignHeight = 480;

    /// <summary>The canvas over a picture <paramref name="pictureWidth"/> x <see cref="DesignHeight"/>.</summary>
    public static HudCanvas For(int pictureWidth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pictureWidth);
        return pictureWidth >= MinimumWidth
            ? new HudCanvas(pictureWidth, DesignHeight)
            : new HudCanvas(MinimumWidth, (int)Math.Round(DesignHeight * (double)MinimumWidth / pictureWidth, MidpointRounding.AwayFromZero));
    }
}

/// <summary>
/// Where the HUD draws what (issue #369): the approved design A3 with ornament V3 (art bible, section "HUD"), built
/// from the parts of <see cref="HudParts"/> at their design positions. On a canvas wider or narrower than the design
/// the groups keep to their corners or the middle; the bottom groups keep to the bottom edge. Pure arithmetic - the
/// Game project draws the result.
/// </summary>
public sealed class HudLayout
{
    /// <summary>Every bottom frame ends on this line of the 854 x 480 design (the frame body, without ornament below).</summary>
    public const int BottomBaseline = 477;

    /// <summary>Height of a bottom frame body (above <see cref="BottomBaseline"/>), its dark ground inside.</summary>
    public const int BottomBodyHeight = 30;

    /// <summary>Space kept between two groups next to each other, and between a group and a text beside it.</summary>
    public const int GroupGap = 4;

    /// <summary>Right edge of the rounds inside a weapon frame, from the frame's left edge.</summary>
    public const int RoundsRight = 38;

    /// <summary>Middle row of the top frames' dark ground (level, skill point, currency).</summary>
    public const int TopMiddle = 20;

    /// <summary>The reload veil inside a weapon frame (as in the design): this far in from the frame's top left ...</summary>
    public const int VeilInset = 3;

    /// <summary>... this wide when the reload starts ...</summary>
    public const int VeilWidth = 32;

    /// <summary>... and this high.</summary>
    public const int VeilHeight = 24;

    private const int DesignWidth = 854;
    private const int DesignHeight = 480;

    private static readonly FrozenDictionary<WeaponKind, string> WeaponIcons = new Dictionary<WeaponKind, string>
    {
        [WeaponKind.SilverBlade] = "icon_blade",
        [WeaponKind.Pistol] = "icon_pistol",
        [WeaponKind.Club] = "icon_club",
        [WeaponKind.Rifle] = "icon_rifle",
        [WeaponKind.Blunderbuss] = "icon_blunder",
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<ItemKind, string> ItemIcons = new Dictionary<ItemKind, string>
    {
        [ItemKind.HuntersBoots] = "icon_boots",
        [ItemKind.IronFlask] = "icon_flask",
        [ItemKind.SilverRosary] = "icon_rosary",
        [ItemKind.QuickdrawHolster] = "icon_holster",
        [ItemKind.BladeParry] = "icon_parry",
        [ItemKind.HuntersEye] = "icon_eye",
        [ItemKind.Leech] = "icon_leech",
        [ItemKind.AshenHeart] = "icon_ashheart",
    }.ToFrozenDictionary();

    private readonly HudParts _parts;
    private readonly PixelFont _font;

    /// <summary>A layout drawing <paramref name="parts"/> and measuring its texts with <paramref name="font"/>.</summary>
    public HudLayout(HudParts parts, PixelFont font)
    {
        _parts = parts ?? throw new ArgumentNullException(nameof(parts));
        _font = font ?? throw new ArgumentNullException(nameof(font));
    }

    /// <summary>The weapons that have a picture of their own (all, tested); any other would show its initial.</summary>
    public static IReadOnlyCollection<WeaponKind> WeaponsWithIcon => WeaponIcons.Keys;

    /// <summary>The items that have a picture of their own (all, tested); any other would show its initial.</summary>
    public static IReadOnlyCollection<ItemKind> ItemsWithIcon => ItemIcons.Keys;

    /// <summary>The name of the part for weapon <paramref name="kind"/>, or null if it has none yet.</summary>
    public static string? IconOf(WeaponKind kind) => WeaponIcons.GetValueOrDefault(kind);

    /// <summary>The name of the part for item <paramref name="kind"/>, or null if it has none yet.</summary>
    public static string? IconOf(ItemKind kind) => ItemIcons.GetValueOrDefault(kind);

    /// <summary>The rectangle a placed part covers.</summary>
    public HudRect Bounds(HudPlacedPart part) => new(part.X, part.Y, part.Width, _parts[part.Name].Height);

    /// <summary>The rectangle a placed text covers, with its shadow one pixel below and right.</summary>
    public HudRect Bounds(HudPlacedText text) =>
        new(text.X, text.Top, _font.Width(text.Text, text.Scale) + 1, (_font.CapHeight * text.Scale) + 1);

    /// <summary>
    /// The HUD for <paramref name="readout"/> on <paramref name="canvas"/>, with the pickup toast
    /// <paramref name="toast"/> above the bottom left (none while null).
    /// </summary>
    public HudPicture Arrange(HudReadout readout, string? toast, HudCanvas canvas)
    {
        ArgumentNullException.ThrowIfNull(readout);
        var picture = new Builder(this, canvas);

        ArrangeItems(picture, readout.Items);
        var lifeEdge = ArrangeVitals(picture, readout);
        if (!readout.IsDead)
        {
            ArrangeWeapons(picture, readout);
            picture.Part("crosshair", Anchor.Middle, Anchor.Middle);
        }

        ArrangeLevel(picture, readout);
        if (readout.Boss is { } boss)
        {
            ArrangeBoss(picture, boss);
        }

        ArrangeCurrency(picture, readout);
        if (toast is not null)
        {
            ArrangeToast(picture, toast);
        }

        return new HudPicture(picture.Parts, picture.Texts, lifeEdge) { ReloadVeils = picture.Veils };
    }

    /// <summary>
    /// The reload veil over a weapon frame whose left edge is <paramref name="frameX"/> and whose body ends on line
    /// <paramref name="baseline"/>: inside the frame, covering the share still to go (1 - <paramref name="progress"/>)
    /// from the left, so it shrinks as the reload comes on. Null once nothing is left to cover.
    /// </summary>
    public static HudRect? ReloadVeil(int frameX, int baseline, float progress)
    {
        var width = (int)MathF.Round(VeilWidth * (1f - Math.Clamp(progress, 0f, 1f)));
        return width > 0 ? new HudRect(frameX + VeilInset, baseline - BottomBodyHeight + VeilInset, width, VeilHeight) : null;
    }

    // Bottom left: one frame per item, as many as fit before the life frame; more items end in a "+N" frame.
    private void ArrangeItems(Builder picture, IReadOnlyList<ItemKind> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        var first = _parts["frame_item_0"];
        var step = _parts["frame_item_1"].LayoutX - first.LayoutX;
        var limit = picture.X(_parts["frame_vitals"].LayoutX, Anchor.Middle) - GroupGap;
        var fit = Math.Max(1, ((limit - first.LayoutX - first.Width) / step) + 1);
        var shown = items.Count <= fit ? items.Count : fit - 1;

        for (var i = 0; i < Math.Min(items.Count, fit); i++)
        {
            var frame = picture.Part($"frame_item_{i % 2}", Anchor.Start, Anchor.End, x: first.LayoutX + (i * step));
            if (i >= shown)
            {
                picture.CentredText(string.Create(CultureInfo.InvariantCulture, $"+{items.Count - shown}"),
                    frame.X + (first.Width / 2), BottomMiddle(picture), 1, HudTextColor.Main);
            }
            else if (IconOf(items[i]) is { } icon)
            {
                var part = _parts[icon];
                picture.Part(icon, frame.X + ((first.Width - part.Width + 1) / 2), BottomBodyTop(picture) + ((BottomBodyHeight - part.Height + 1) / 2));
            }
            else
            {
                picture.CentredText(Items.Info(items[i]).Name[..1], frame.X + (first.Width / 2), BottomMiddle(picture),
                    PixelFont.NumberScale, HudTextColor.Main);
            }
        }
    }

    // Bottom middle: the life bar and five stamina segments, both without a number.
    private HudRect? ArrangeVitals(Builder picture, HudReadout readout)
    {
        picture.Part("frame_vitals", Anchor.Middle, Anchor.End);
        var life = picture.Part("life_empty", Anchor.Middle, Anchor.End);
        var full = _parts["life_full"];
        var filled = (int)MathF.Round(full.Width * readout.Life);
        HudRect? edge = null;
        if (filled > 0)
        {
            picture.Part("life_full", life.X, life.Y, filled);
            if (filled < full.Width)
            {
                edge = new HudRect(life.X + filled - 1, life.Y, 1, full.Height);
            }
        }

        const int Segments = 5;
        for (var i = 0; i < Segments; i++)
        {
            var segment = picture.Part($"stamina_{i}_empty", Anchor.Middle, Anchor.End);
            var width = _parts[$"stamina_{i}_full"].Width;
            var share = Math.Clamp((readout.Stamina * Segments) - i, 0f, 1f);
            var segmentFilled = (int)MathF.Round(width * share);
            if (segmentFilled > 0)
            {
                picture.Part($"stamina_{i}_full", segment.X, segment.Y, segmentFilled);
            }
        }

        return edge;
    }

    // Bottom right: the melee hand and the three ranged places; the chosen one lit, each ranged weapon with its rounds.
    private void ArrangeWeapons(Builder picture, HudReadout readout)
    {
        for (var i = 0; i < readout.Weapons.Count; i++)
        {
            var weapon = readout.Weapons[i];
            var frameName = weapon.IsSelected ? $"frame_weapon_{i}_active" : $"frame_weapon_{i}";
            var frame = picture.Part(frameName, Anchor.End, Anchor.End);
            var frameWidth = _parts[frameName].Width;
            if (weapon.Kind is not { } kind)
            {
                continue;
            }

            var iconRight = frame.X;
            if (IconOf(kind) is { } icon)
            {
                var name = weapon.IsSelected ? icon + "_active" : icon;
                var part = _parts[name];
                var (offsetX, offsetY) = OffsetInWeaponFrame(_parts[icon]);
                var placed = picture.Part(name, frame.X + offsetX, frame.Y + offsetY);
                iconRight = placed.X + part.Width;
            }
            else
            {
                var initial = Weapons.Info(kind).Name[..1];
                var text = picture.CentredText(initial, frame.X + (frameWidth / 2), BottomMiddle(picture), PixelFont.NumberScale,
                    HudTextColor.Main);
                iconRight = text.X + _font.Width(initial, PixelFont.NumberScale);
            }

            if (weapon.IsReloading && ReloadVeil(frame.X, picture.Y(BottomBaseline, Anchor.End), weapon.ReloadProgress) is { } veil)
            {
                picture.Veils.Add(veil);
            }

            if (weapon.Rounds is { } rounds)
            {
                var number = rounds.ToString(CultureInfo.InvariantCulture);
                // Small, as in the design: the picture of the weapon is what the eye looks for.
                var left = Math.Max(iconRight + 1, frame.X + RoundsRight - _font.Width(number));
                picture.Text(number, left, BottomMiddle(picture) - (_font.CapHeight / 2), 1,
                    weapon.IsReloading ? HudTextColor.Second : HudTextColor.Main);
            }
        }

        if (readout.ConsecratedSecondsLeft > 0f)
        {
            var seconds = (int)MathF.Ceiling(readout.ConsecratedSecondsLeft);
            var text = string.Create(CultureInfo.InvariantCulture, $"Consecrated {seconds}s");
            var last = _parts["frame_weapon_3"];
            var right = picture.X(last.LayoutX + last.Width, Anchor.End) - GroupGap;
            var top = picture.Y(last.LayoutY, Anchor.End) - GroupGap - _font.CapHeight - 1;
            picture.Text(text, right - _font.Width(text), top, 1, HudTextColor.Main);
        }
    }

    // Where a weapon picture sits inside its frame: as in the design, in whichever frame the design shows it.
    private (int X, int Y) OffsetInWeaponFrame(HudPart icon)
    {
        for (var i = 0; i < 1 + Player.RangedSlotCount; i++)
        {
            var frame = _parts[$"frame_weapon_{i}"];
            if (icon.LayoutX >= frame.LayoutX && icon.LayoutX < frame.LayoutX + frame.Width)
            {
                return (icon.LayoutX - frame.LayoutX, icon.LayoutY - frame.LayoutY);
            }
        }

        throw new InvalidOperationException($"The design shows '{icon.Name}' in no weapon frame.");
    }

    // Top left: the level with its number, the experience bar, and while a boon waits the skill point with its key.
    private void ArrangeLevel(Builder picture, HudReadout readout)
    {
        var level = picture.Part("frame_level", Anchor.Start, Anchor.Start);
        var levelPart = _parts["frame_level"];
        var number = readout.Level.ToString(CultureInfo.InvariantCulture);
        var scale = _font.Width(number, PixelFont.NumberScale) <= levelPart.Width - 12 ? PixelFont.NumberScale : 1;
        picture.CentredText(number, level.X + (levelPart.Width / 2), TopMiddle - 2, scale, HudTextColor.Main);

        picture.Part("frame_xp", Anchor.Start, Anchor.Start);
        var bar = picture.Part("xp_empty", Anchor.Start, Anchor.Start);
        var filled = (int)MathF.Round(_parts["xp_full"].Width * readout.Experience);
        if (filled > 0)
        {
            picture.Part("xp_full", bar.X, bar.Y, filled);
        }

        if (readout.PendingBoons <= 0)
        {
            return;
        }

        var skill = picture.Part("frame_skill_0", Anchor.Start, Anchor.Start);
        var skillPart = _parts["frame_skill_0"];
        var star = _parts["icon_skillpoint"];
        picture.Part("icon_skillpoint", skill.X + ((skillPart.Width - star.Width + 1) / 2), star.LayoutY);

        var key = _parts["frame_prompt"];
        var keyTop = TopMiddle - (key.Height / 2);
        var cap = picture.Part("frame_prompt", skill.X + skillPart.Width + GroupGap - 1, keyTop);
        picture.CentredText("X", cap.X + (key.Width / 2), keyTop + (key.Height / 2), 1, HudTextColor.Main);
    }

    // Top middle, only in a boss fight: the boss's name and a thin bar of his health.
    private void ArrangeBoss(Builder picture, HudBoss boss)
    {
        var frame = picture.Part("frame_boss", Anchor.Middle, Anchor.Start);
        var framePart = _parts["frame_boss"];
        var bar = picture.Part("boss_empty", Anchor.Middle, Anchor.Start);
        var filled = (int)MathF.Round(_parts["boss_full"].Width * boss.Health);
        if (filled > 0)
        {
            picture.Part("boss_full", bar.X, bar.Y, filled);
        }

        var nameTop = bar.Y - GroupGap - _font.CapHeight - 1;
        picture.Text(boss.Name, frame.X + ((framePart.Width - _font.Width(boss.Name)) / 2), nameTop, 1, HudTextColor.Main);
    }

    // Top right: silver behind its coin, keys at the right end with the key in front of the number.
    private void ArrangeCurrency(Builder picture, HudReadout readout)
    {
        var frame = picture.Part("frame_currency", Anchor.End, Anchor.Start);
        var framePart = _parts["frame_currency"];
        var coin = _parts["icon_coin"];
        var key = _parts["icon_key"];
        var coinPlaced = picture.Part("icon_coin", Anchor.End, Anchor.Start);

        var keys = readout.Keys.ToString(CultureInfo.InvariantCulture);
        var keysRight = frame.X + framePart.Width - 14;
        var keysScale = _font.Width(keys, PixelFont.NumberScale) <= 12 ? PixelFont.NumberScale : 1;
        var keysLeft = keysRight - _font.Width(keys, keysScale);
        picture.Text(keys, keysLeft, TopMiddle - (_font.CapHeight * keysScale / 2), keysScale, HudTextColor.Main);
        var keyX = keysLeft - 7 - key.Width;
        picture.Part("icon_key", keyX, key.LayoutY);

        var silver = readout.Silver.ToString(CultureInfo.InvariantCulture);
        var silverLeft = coinPlaced.X + coin.Width + GroupGap + 1;
        var silverScale = silverLeft + _font.Width(silver, PixelFont.NumberScale) <= keyX - GroupGap ? PixelFont.NumberScale : 1;
        picture.Text(silver, silverLeft, TopMiddle - (_font.CapHeight * silverScale / 2), silverScale, HudTextColor.Main);
    }

    // Above the bottom left: the gold toast of what was just picked up.
    private void ArrangeToast(Builder picture, string toast)
    {
        var frame = picture.Part("frame_toast", Anchor.Start, Anchor.End);
        var framePart = _parts["frame_toast"];
        var eye = picture.Part("icon_toast_eye", Anchor.Start, Anchor.End);
        var top = frame.Y + ((framePart.Height - _font.CapHeight + 1) / 2);
        picture.Text(toast, eye.X + _parts["icon_toast_eye"].Width + 7, top, 1, HudTextColor.Pickup);
    }

    private static int BottomBodyTop(Builder picture) => picture.Y(BottomBaseline - BottomBodyHeight, Anchor.End);

    private static int BottomMiddle(Builder picture) => picture.Y(BottomBaseline - (BottomBodyHeight / 2), Anchor.End);

    private enum Anchor
    {
        Start,
        Middle,
        End,
    }

    // Collects the placed parts and texts; moves design positions to their corner or the middle of the canvas.
    private sealed class Builder(HudLayout layout, HudCanvas canvas)
    {
        public List<HudPlacedPart> Parts { get; } = [];

        public List<HudPlacedText> Texts { get; } = [];

        public List<HudRect> Veils { get; } = [];

        public int X(int designX, Anchor anchor) => designX + Shift(anchor, canvas.Width, DesignWidth);

        public int Y(int designY, Anchor anchor) => designY + Shift(anchor, canvas.Height, DesignHeight);

        // The part at its design position, moved with its group.
        public HudPlacedPart Part(string name, Anchor horizontal, Anchor vertical, int? x = null)
        {
            var part = layout._parts[name];
            return Part(name, X(x ?? part.LayoutX, horizontal), Y(part.LayoutY, vertical));
        }

        // The part at a place already on the canvas.
        public HudPlacedPart Part(string name, int x, int y, int? width = null)
        {
            var placed = new HudPlacedPart(name, x, y, width ?? layout._parts[name].Width);
            Parts.Add(placed);
            return placed;
        }

        public HudPlacedText Text(string text, int x, int top, int scale, HudTextColor color)
        {
            var placed = new HudPlacedText(text, x, top, scale, color);
            Texts.Add(placed);
            return placed;
        }

        // A text centred on a point of the canvas.
        public HudPlacedText CentredText(string text, int centreX, int centreY, int scale, HudTextColor color) =>
            Text(text, centreX - (layout._font.Width(text, scale) / 2), centreY - (layout._font.CapHeight * scale / 2), scale, color);

        private static int Shift(Anchor anchor, int size, int design) => anchor switch
        {
            Anchor.Start => 0,
            Anchor.Middle => (size - design) / 2,
            _ => size - design,
        };
    }
}
