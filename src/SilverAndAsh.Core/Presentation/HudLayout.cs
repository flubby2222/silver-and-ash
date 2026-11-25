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

    /// <summary>Yellow: only the hint to level up (issue #387).</summary>
    Hint,
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

    /// <summary>This rectangle made smaller by <paramref name="inset"/> on every side.</summary>
    public HudRect Inset(int inset) => new(X + inset, Y + inset, Width - (2 * inset), Height - (2 * inset));
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
/// Everything the HUD draws in one frame, in drawing order: parts first, then the reload veils, then texts. A reload
/// veil (issue #370) lies over the picture of a weapon being reloaded, only inside the frame's rim and on the part still
/// to go at the right (issue #389); its leftmost column is its light front edge, travelling from left to right.
/// </summary>
public sealed record HudPicture(IReadOnlyList<HudPlacedPart> Parts, IReadOnlyList<HudPlacedText> Texts)
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
/// Where the HUD draws what (issues #369, #387): the frames of the approved design A3 with ornament V3 (art bible,
/// section "HUD"), placed by one rule of spacing. Every frame body (its silver rim, without rivets and gable) keeps
/// <see cref="Spacing"/> to the frame next to it and to the edge of the canvas; what stands above or below a row of
/// frames keeps <see cref="Spacing"/> to their ornament. Inside a frame its contents share the dark ground with equal
/// gaps to its edge and to each other; a sign and its number stand <see cref="SignGap"/> apart. Pure arithmetic - the
/// Game project draws the result.
/// </summary>
public sealed class HudLayout
{
    /// <summary>The one gap of the HUD: between neighbouring frame bodies and between a frame body and the canvas edge.</summary>
    public const int Spacing = 6;

    /// <summary>Width of the silver rim of a frame body; the dark ground lies inside it.</summary>
    public const int Rim = 3;

    /// <summary>Between a sign and the number or text it stands for (coin and silver, key and keys, eye and toast).</summary>
    public const int SignGap = 3;

    /// <summary>Every bottom frame body ends on this line of a canvas 480 lines high, <see cref="Spacing"/> above its edge.</summary>
    public const int BottomBaseline = HudCanvas.DesignHeight - Spacing;

    /// <summary>Height of a bottom frame body (above <see cref="BottomBaseline"/>), its dark ground inside.</summary>
    public const int BottomBodyHeight = 30;

    /// <summary>Starts the small magazine size beside the large rounds number above the weapons ("/ 7", issue #388).</summary>
    public const string MagazineMark = "/ ";

    /// <summary>The hint under the level while a boon waits to be chosen.</summary>
    public const string LevelUpHint = "[X] to level up";

    private static readonly FrozenDictionary<WeaponKind, string> WeaponIcons = new Dictionary<WeaponKind, string>
    {
        [WeaponKind.SilverBlade] = "icon_blade",
        [WeaponKind.Pistol] = "icon_pistol",
        [WeaponKind.Club] = "icon_club",
        [WeaponKind.Rifle] = "icon_rifle",
        [WeaponKind.Blunderbuss] = "icon_blunder",

        // A small rendering of its 3D model (issue #776, tools/hud/hud_axt.py), the size and middle of the club's sign.
        [WeaponKind.HuntersAxe] = "icon_axe",
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

    /// <summary>
    /// The line under the top left group (issues #387, #435): level, life, experience and skill frames and, while a boon
    /// waits, the yellow hint under them with its shadow. The same on every canvas - the group keeps to the top left corner. The
    /// menus start their frame of values <see cref="Spacing"/> below it (<see cref="MenuLayout.SheetTopUnder"/>).
    /// </summary>
    public int TopLeftBottom => LevelHintTop() + _font.CapHeight + 1;

    /// <summary>The rectangle a placed part covers.</summary>
    public HudRect Bounds(HudPlacedPart part) => new(part.X, part.Y, part.Width, _parts[part.Name].Height);

    /// <summary>
    /// The body of a placed frame on the canvas: its silver rim, without the ornament around. A frame cut from the right
    /// (the life frame, issue #435) has a body shorter by as many columns.
    /// </summary>
    public HudRect BodyOf(HudPlacedPart part)
    {
        var whole = _parts[part.Name];
        var body = whole.Body;
        return new HudRect(part.X + body.X, part.Y + body.Y, body.Width - (whole.Width - part.Width), body.Height);
    }

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
        var picture = new Builder(this);

        ArrangeItems(picture, readout.Items, canvas);
        ArrangeStamina(picture, readout, canvas);
        if (!readout.IsDead)
        {
            ArrangeWeapons(picture, readout, canvas);
            var crosshair = _parts["crosshair"];
            picture.Part("crosshair", (canvas.Width / 2) - (crosshair.Width / 2), (canvas.Height / 2) - (crosshair.Height / 2));
        }

        var topLeftRight = ArrangeLevel(picture, readout);
        if (readout.Boss is { } boss)
        {
            ArrangeBoss(picture, boss, canvas, topLeftRight + Spacing);
        }

        ArrangeCurrency(picture, readout, canvas);
        if (toast is not null)
        {
            ArrangeToast(picture, toast, canvas);
        }

        return new HudPicture(picture.Parts, picture.Texts) { ReloadVeils = picture.Veils };
    }

    /// <summary>
    /// The reload veil over the dark ground <paramref name="ground"/> of a weapon frame (issue #389): only inside the
    /// silver rim, covering the share still to go (1 - <paramref name="progress"/>) at the right, so its front edge
    /// travels from left to right as the reload comes on. Null once nothing is left to cover.
    /// </summary>
    public static HudRect? ReloadVeil(HudRect ground, float progress)
    {
        var width = (int)MathF.Round(ground.Width * (1f - Math.Clamp(progress, 0f, 1f)));
        return width > 0 ? ground with { X = ground.Right - width, Width = width } : null;
    }

    /// <summary>
    /// Where things of the lengths <paramref name="sizes"/> start when they share the stretch from
    /// <paramref name="start"/>, <paramref name="length"/> long, in order and with equal gaps before, between and after
    /// them (the gaps differ by at most one pixel; they turn negative if the things are longer than the stretch). One
    /// thing alone is centred.
    /// </summary>
    public static int[] Spread(int start, int length, IReadOnlyList<int> sizes)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        var free = length - sizes.Sum();
        var places = new int[sizes.Count];
        var at = start;
        for (var i = 0; i < sizes.Count; i++)
        {
            at += FloorDiv((i + 1) * free, sizes.Count + 1) - FloorDiv(i * free, sizes.Count + 1);
            places[i] = at;
            at += sizes[i];
        }

        return places;
    }

    // Bottom left: one frame per item, as many as fit before the stamina frame; more items end in a "+N" frame.
    private void ArrangeItems(Builder picture, IReadOnlyList<ItemKind> items, HudCanvas canvas)
    {
        if (items.Count == 0)
        {
            return;
        }

        var body = _parts["frame_item_0"].Body;
        var step = body.Width + Spacing;
        var fit = Math.Max(1, (StaminaBodyX(canvas) - Spacing) / step);
        var shown = items.Count <= fit ? items.Count : fit - 1;

        for (var i = 0; i < Math.Min(items.Count, fit); i++)
        {
            var ground = picture.Frame($"frame_item_{i % 2}", Spacing + (i * step), BottomBodyTop(canvas)).Inset(Rim);
            if (i >= shown)
            {
                picture.CentredText(string.Create(CultureInfo.InvariantCulture, $"+{items.Count - shown}"), ground, 1, HudTextColor.Main);
            }
            else if (IconOf(items[i]) is { } icon)
            {
                picture.CentredPart(icon, ground);
            }
            else
            {
                picture.CentredText(Items.Info(items[i]).Name[..1], ground, PixelFont.NumberScale, HudTextColor.Main);
            }
        }
    }

    // Bottom middle: the five stamina segments without a number (the life bar went to the top left, issue #435).
    private void ArrangeStamina(Builder picture, HudReadout readout, HudCanvas canvas)
    {
        var body = _parts["frame_stamina"].Body;
        var ground = picture.Frame("frame_stamina", StaminaBodyX(canvas), canvas.Height - Spacing - body.Height).Inset(Rim);

        const int Segments = 5;
        var segments = Enumerable.Range(0, Segments).Select(i => _parts[$"stamina_{i}_empty"]).ToList();
        var rowLeft = segments.Min(segment => segment.LayoutX);
        var rowTop = segments.Min(segment => segment.LayoutY);
        var rowWidth = segments.Max(segment => segment.LayoutX + segment.Width) - rowLeft;
        var rowHeight = segments.Max(segment => segment.LayoutY + segment.Height) - rowTop;

        var rowX = Spread(ground.X, ground.Width, [rowWidth])[0];
        var rowY = Spread(ground.Y, ground.Height, [rowHeight])[0];
        for (var i = 0; i < Segments; i++)
        {
            var x = rowX + (segments[i].LayoutX - rowLeft);
            var y = rowY + (segments[i].LayoutY - rowTop);
            picture.Part($"stamina_{i}_empty", x, y);
            var width = _parts[$"stamina_{i}_full"].Width;
            var share = Math.Clamp((readout.Stamina * Segments) - i, 0f, 1f);
            var segmentFilled = (int)MathF.Round(width * share);
            if (segmentFilled > 0)
            {
                picture.Part($"stamina_{i}_full", x, y, segmentFilled);
            }
        }
    }

    // Bottom right: the melee hand and the three ranged places, the chosen one lit, each only with its picture; above
    // the row the rounds of the chosen ranged weapon (issue #388), above them the consecrated time.
    private void ArrangeWeapons(Builder picture, HudReadout readout, HudCanvas canvas)
    {
        var count = readout.Weapons.Count;
        var bodyWidth = _parts["frame_weapon_0"].Body.Width;
        var rowX = canvas.Width - Spacing - (count * bodyWidth) - ((count - 1) * Spacing);
        for (var i = 0; i < count; i++)
        {
            var weapon = readout.Weapons[i];
            var frameName = weapon.IsSelected ? $"frame_weapon_{i}_active" : $"frame_weapon_{i}";
            var bodyX = canvas.Width - Spacing - ((count - i) * bodyWidth) - ((count - 1 - i) * Spacing);
            var body = picture.Frame(frameName, bodyX, BottomBodyTop(canvas));
            var ground = body.Inset(Rim);
            if (weapon.IsReloading && ReloadVeil(ground, weapon.ReloadProgress) is { } veil)
            {
                picture.Veils.Add(veil);
            }

            if (weapon.Kind is not { } kind)
            {
                continue;
            }

            // Only the picture, in the middle of the ground: the rounds stand above the row since issue #388.
            if (IconOf(kind) is { } plain)
            {
                picture.CentredPart(weapon.IsSelected ? plain + "_active" : plain, ground);
            }
            else
            {
                picture.CentredText(Weapons.Info(kind).Name[..1], ground, PixelFont.NumberScale, HudTextColor.Main);
            }
        }

        // What stands above the row ends this line: Spacing above the ornament of the weapon frames.
        var above = BottomBodyTop(canvas) - _parts["frame_weapon_0"].Body.Y - Spacing;
        if (ArrangeRounds(picture, readout, rowX, canvas.Width - Spacing - rowX, above) is { } roundsTop)
        {
            above = roundsTop - Spacing;
        }

        if (readout.ConsecratedSecondsLeft > 0f)
        {
            var seconds = (int)MathF.Ceiling(readout.ConsecratedSecondsLeft);
            var text = string.Create(CultureInfo.InvariantCulture, $"Consecrated {seconds}s");
            var size = TextSize(text, 1);
            picture.Text(text, canvas.Width - Spacing - size.Width, above - size.Height, 1, HudTextColor.Main);
        }
    }

    // In the middle above the weapon row (issue #388), ending on line `bottom`: the rounds of the chosen ranged weapon
    // large, the size of its magazine small beside them ("7 / 7" - the supply itself is unlimited). Nothing while the
    // melee weapon is in use or no weapon with rounds is chosen. Returns the top of the large number, else null.
    private int? ArrangeRounds(Builder picture, HudReadout readout, int rowX, int rowWidth, int bottom)
    {
        if (readout.IsMeleeInUse || readout.Weapons.FirstOrDefault(weapon => weapon.IsSelected) is not { Rounds: { } rounds } chosen)
        {
            return null;
        }

        var large = rounds.ToString(CultureInfo.InvariantCulture);
        var largeSize = TextSize(large, PixelFont.NumberScale);
        var small = chosen.MagazineSize is { } magazine ? string.Create(CultureInfo.InvariantCulture, $"{MagazineMark}{magazine}") : null;
        var smallSize = small is null ? (Width: 0, Height: 0) : TextSize(small, 1);
        var x = Spread(rowX, rowWidth, [largeSize.Width + (small is null ? 0 : SignGap + smallSize.Width)])[0];

        var top = bottom - largeSize.Height;
        picture.Text(large, x, top, PixelFont.NumberScale, chosen.IsReloading ? HudTextColor.Second : HudTextColor.Main);
        if (small is not null)
        {
            picture.Text(small, x + largeSize.Width + SignGap, bottom - smallSize.Height, 1, HudTextColor.Second);
        }

        return top;
    }

    // Top left (issue #435, layout B): the life bar above the experience bar, the level frame with its number left of
    // them in the middle of the stack, and while a boon waits the skill point in a second row with the hint under it.
    // Returns the rightmost column of the group's top rows (the life frame or the skill points beside the experience bar).
    private int ArrangeLevel(Builder picture, HudReadout readout)
    {
        var lines = TopLeft();
        var level = picture.Frame("frame_level", Spacing, lines.LevelY);
        var levelGround = level.Inset(Rim);
        var number = readout.Level.ToString(CultureInfo.InvariantCulture);
        var scale = TextSize(number, PixelFont.NumberScale).Width <= levelGround.Width ? PixelFont.NumberScale : 1;
        picture.CentredText(number, levelGround, scale, HudTextColor.Main);

        var life = ArrangeLife(picture, readout, lines.StackX, lines.LifeY);

        var xp = picture.Frame("frame_xp", lines.StackX, lines.ExperienceY);
        var bar = picture.CentredPart("xp_empty", xp.Inset(Rim));
        var filled = (int)MathF.Round(_parts["xp_full"].Width * readout.Experience);
        if (filled > 0)
        {
            picture.Part("xp_full", bar.X, bar.Y, filled);
        }

        if (readout.PendingBoons > 0)
        {
            var skill = picture.Frame("frame_skill_0", Spacing, lines.SkillY);
            picture.CentredPart("icon_skillpoint", skill.Inset(Rim));
            picture.Text(LevelUpHint, Spacing, LevelHintTop(), 1, HudTextColor.Hint);
        }

        var skillPoints = ArrangeSkillPoints(picture, readout.SkillPoints, xp);
        return Math.Max(life.Right, skillPoints?.Right ?? 0);
    }

    /// <summary>The placeholder text of the skill points (issue #502): "1 skill point", "2 skill points" ...</summary>
    public static string SkillPointsText(int points) =>
        string.Create(CultureInfo.InvariantCulture, $"{points} skill point{(points == 1 ? string.Empty : "s")}");

    // Beside the experience bar, the spacing right of its body and in the middle of its height: a placeholder text of
    // the skill points while there are any (issue #502; the skill screen will get its own picture). Returns what it covers.
    private HudRect? ArrangeSkillPoints(Builder picture, int points, HudRect xp)
    {
        if (points <= 0)
        {
            return null;
        }

        var text = SkillPointsText(points);
        var size = TextSize(text, 1);
        var placed = picture.Text(text, xp.Right + Spacing, Spread(xp.Y, xp.Height, [size.Height])[0], 1, HudTextColor.Main);
        return Bounds(placed);
    }

    // The life bar (issue #435): as long as the highest health (LifeBar.Shape), in a frame cut to its length and closed by
    // its end cap, filled from the left, with a tick mark per health boon. Returns the frame's body.
    private HudRect ArrangeLife(Builder picture, HudReadout readout, int bodyX, int bodyY)
    {
        var bar = _parts["life_bar_empty"];
        var cap = _parts["life_cap_empty"];
        var shape = LifeBar.Shape(readout.MaxLife, readout.Life, readout.LifePerTick, cap.Width, bar.Width + cap.Width);

        var (frame, body) = picture.CutFrame("frame_life", bodyX, bodyY, shape.Length + (2 * Rim));
        picture.Part("life_frame_end", frame.X + frame.Width, frame.Y);

        var x = body.X + Rim;
        var y = Spread(body.Y, body.Height, [bar.Height])[0];
        var capX = x + shape.Length - cap.Width;
        picture.Part("life_bar_empty", x, y, shape.Length - cap.Width);
        picture.Part("life_cap_empty", capX, y);
        if (shape.Filled > 0)
        {
            picture.Part("life_bar_full", x, y, shape.Filled);
        }

        if (shape.IsFull)
        {
            picture.Part("life_cap_full", capX, y);
        }

        foreach (var tick in shape.Ticks)
        {
            var name = (tick.IsLong ? "life_tick_long_" : "life_tick_") + (tick.IsOverFill ? "full" : "empty");
            picture.Part(name, x + tick.X, y + (_parts[name].LayoutY - bar.LayoutY));
        }

        return body;
    }

    // The lines of the top left group, the same on every canvas: the life body on line Spacing, the experience body
    // Spacing under it, both from column StackX; the level body in the middle of their height left of them; the skill
    // body Spacing under the stack.
    private (int StackX, int LifeY, int ExperienceY, int LevelY, int SkillY) TopLeft()
    {
        var level = _parts["frame_level"].Body;
        var experienceY = Spacing + _parts["frame_life"].Body.Height + Spacing;
        var stackBottom = experienceY + _parts["frame_xp"].Body.Height;
        var levelY = Spread(Spacing, stackBottom - Spacing, [level.Height])[0];
        return (Spacing + level.Width + Spacing, Spacing, experienceY, levelY, stackBottom + Spacing);
    }

    // Where the hint to level up starts: Spacing under the ornament of the skill frame, placed as ArrangeLevel places it
    // (the lowest frame of the group).
    private int LevelHintTop()
    {
        var skill = _parts["frame_skill_0"];
        return TopLeft().SkillY - skill.Body.Y + skill.Height + Spacing;
    }

    // Top middle, only in a boss fight: the boss's name above a thin bar of his health. Centred, but never left of
    // `leftmost` - a long life bar pushes it right on a narrow canvas (issue #435).
    private void ArrangeBoss(Builder picture, HudBoss boss, HudCanvas canvas, int leftmost)
    {
        var body = _parts["frame_boss"].Body;
        var ground = picture.Frame("frame_boss", Math.Max((canvas.Width - body.Width) / 2, leftmost), Spacing).Inset(Rim);
        var bar = _parts["boss_empty"];
        var name = TextSize(boss.Name, 1);
        var rows = Spread(ground.Y, ground.Height, [name.Height, bar.Height]);

        picture.Text(boss.Name, Spread(ground.X, ground.Width, [name.Width])[0], rows[0], 1, HudTextColor.Main);
        var barX = Spread(ground.X, ground.Width, [bar.Width])[0];
        picture.Part("boss_empty", barX, rows[1]);
        var filled = (int)MathF.Round(_parts["boss_full"].Width * boss.Health);
        if (filled > 0)
        {
            picture.Part("boss_full", barX, rows[1], filled);
        }
    }

    // Top right: silver behind its coin, keys behind the key - two signs with their numbers, spread over the ground.
    private void ArrangeCurrency(Builder picture, HudReadout readout, HudCanvas canvas)
    {
        var body = _parts["frame_currency"].Body;
        var ground = picture.Frame("frame_currency", canvas.Width - Spacing - body.Width, Spacing).Inset(Rim);
        var coin = _parts["icon_coin"];
        var key = _parts["icon_key"];
        var silver = readout.Silver.ToString(CultureInfo.InvariantCulture);
        var keys = readout.Keys.ToString(CultureInfo.InvariantCulture);

        // Large numbers while both fit with gaps of at least one sign gap; else the silver small first, then both.
        var (silverScale, keysScale) = new[] { (PixelFont.NumberScale, PixelFont.NumberScale), (1, PixelFont.NumberScale), (1, 1) }
            .FirstOrDefault(scales => coin.Width + key.Width + (2 * SignGap) + TextSize(silver, scales.Item1).Width
                + TextSize(keys, scales.Item2).Width + (3 * SignGap) <= ground.Width, (1, 1));

        var silverWidth = coin.Width + SignGap + TextSize(silver, silverScale).Width;
        var keysWidth = key.Width + SignGap + TextSize(keys, keysScale).Width;
        var xs = Spread(ground.X, ground.Width, [silverWidth, keysWidth]);

        picture.Part("icon_coin", xs[0], Spread(ground.Y, ground.Height, [coin.Height])[0]);
        picture.Text(silver, xs[0] + coin.Width + SignGap, Spread(ground.Y, ground.Height, [TextSize(silver, silverScale).Height])[0],
            silverScale, HudTextColor.Main);
        picture.Part("icon_key", xs[1], Spread(ground.Y, ground.Height, [key.Height])[0]);
        picture.Text(keys, xs[1] + key.Width + SignGap, Spread(ground.Y, ground.Height, [TextSize(keys, keysScale).Height])[0],
            keysScale, HudTextColor.Main);
    }

    // Above the bottom left, clear of the item frames' ornament: the gold toast of what was just picked up.
    private void ArrangeToast(Builder picture, string toast, HudCanvas canvas)
    {
        var body = _parts["frame_toast"].Body;
        var itemOrnamentTop = BottomBodyTop(canvas) - _parts["frame_item_0"].Body.Y;
        var ground = picture.Frame("frame_toast", Spacing, itemOrnamentTop - Spacing - body.Height).Inset(Rim);
        var eye = _parts["icon_toast_eye"];
        var text = TextSize(toast, 1);
        var x = Spread(ground.X, ground.Width, [eye.Width + SignGap + text.Width])[0];
        picture.Part("icon_toast_eye", x, Spread(ground.Y, ground.Height, [eye.Height])[0]);
        picture.Text(toast, x + eye.Width + SignGap, Spread(ground.Y, ground.Height, [text.Height])[0], 1, HudTextColor.Pickup);
    }

    private int StaminaBodyX(HudCanvas canvas) => (canvas.Width - _parts["frame_stamina"].Body.Width) / 2;

    private static int BottomBodyTop(HudCanvas canvas) => canvas.Height - Spacing - BottomBodyHeight;

    // The rectangle a text covers with its shadow.
    private (int Width, int Height) TextSize(string text, int scale) =>
        (_font.Width(text, scale) + 1, (_font.CapHeight * scale) + 1);

    private static int FloorDiv(int a, int b) => (int)Math.Floor((double)a / b);

    // Collects the placed parts and texts.
    private sealed class Builder(HudLayout layout)
    {
        public List<HudPlacedPart> Parts { get; } = [];

        public List<HudPlacedText> Texts { get; } = [];

        public List<HudRect> Veils { get; } = [];

        // The part with its top left corner at a place of the canvas.
        public HudPlacedPart Part(string name, int x, int y, int? width = null)
        {
            var placed = new HudPlacedPart(name, x, y, width ?? layout._parts[name].Width);
            Parts.Add(placed);
            return placed;
        }

        // A frame whose body has its top left corner at a place of the canvas; returns the body.
        public HudRect Frame(string name, int bodyX, int bodyY)
        {
            var body = layout._parts[name].Body;
            Part(name, bodyX - body.X, bodyY - body.Y);
            return body with { X = bodyX, Y = bodyY };
        }

        // A frame whose body has its top left corner at a place of the canvas and is `bodyWidth` wide: the part cut from
        // the right by what the body is shorter than in the table. Returns the placed part and the body.
        public (HudPlacedPart Frame, HudRect Body) CutFrame(string name, int bodyX, int bodyY, int bodyWidth)
        {
            var whole = layout._parts[name];
            var cut = whole.Body.Width - bodyWidth;
            if (cut < 0 || cut >= whole.Width)
            {
                throw new ArgumentOutOfRangeException(nameof(bodyWidth), bodyWidth, $"{name} cannot be cut to this body.");
            }

            var placed = Part(name, bodyX - whole.Body.X, bodyY - whole.Body.Y, whole.Width - cut);
            return (placed, whole.Body with { X = bodyX, Y = bodyY, Width = bodyWidth });
        }

        // A part centred in a rectangle of the canvas.
        public HudPlacedPart CentredPart(string name, HudRect area)
        {
            var part = layout._parts[name];
            return Part(name, Spread(area.X, area.Width, [part.Width])[0], Spread(area.Y, area.Height, [part.Height])[0]);
        }

        public HudPlacedText Text(string text, int x, int top, int scale, HudTextColor color)
        {
            var placed = new HudPlacedText(text, x, top, scale, color);
            Texts.Add(placed);
            return placed;
        }

        // A text (with its shadow) centred in a rectangle of the canvas.
        public HudPlacedText CentredText(string text, HudRect area, int scale, HudTextColor color)
        {
            var size = layout.TextSize(text, scale);
            return Text(text, Spread(area.X, area.Width, [size.Width])[0], Spread(area.Y, area.Height, [size.Height])[0], scale, color);
        }
    }
}
