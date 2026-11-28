using System.Numerics;
using System.Text;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Photos;

/// <summary>
/// The shimmer of loot with a tier (issue #825, <see cref="LootShimmer"/>): pieces of all four tiers from 3 m and 8 m,
/// the weapons, two pieces close together, a piece next to a disc of rime, pieces at a step and at a wall, and thirty
/// pieces in one hall to measure the frame rate (<c>--bench loot-shimmer-30</c>). The pieces lie in a bare hall from the
/// start, as finds; the pictures wait until they lie still.
/// </summary>
internal static class LootShimmerScenes
{
    private const int HallWidth = 9;
    private const int PlayerColumn = HallWidth / 2;
    private const int PlayerRow = 2;

    // Ticks before a picture: the pieces lie still and are somewhere in their pulse (each at its own place by its id).
    private const int Settled = 30;

    // One item of every tier, the weakest first, and the throwing knives (Vampiric) with their own shape.
    private static readonly ItemKind[] ItemOfEachTier = [ItemKind.HuntersBoots, ItemKind.SilverRosary, ItemKind.BladeParry, ItemKind.Leech];

    public static PhotoScene[] All() =>
    [
        new("loot-shimmer-3m", "Loot of all four tiers 3 m away, an item of each (boxes) and the throwing knives: each floats without a plate, lights up, glows on the floor and throws sparks in the colour of its tier (issue #825).",
            studio => new PhotoSetup(TierRow(studio, 3f)) { Ticks = Settled, ShowsOnlyTheWorld = true }),
        new("loot-shimmer-8m", "The same loot of all four tiers 8 m away: can each piece and its tier still be told? (issue #825)",
            studio => new PhotoSetup(TierRow(studio, 8f)) { Ticks = Settled, ShowsOnlyTheWorld = true }),
        new("loot-shimmer-weapons-3m", "Weapons lying as loot 3 m away - club, rifle, hunting axe, crossbow as bars until they have models, and the throwing knives as three knives, tips together (issue #825).",
            studio => new PhotoSetup(WeaponRow(studio, 3f)) { Ticks = Settled, ShowsOnlyTheWorld = true }),
        new("loot-shimmer-knives-2m", "The throwing knives lying as loot, seen from 2 m: three knives, tips together at the bottom, leaning apart (form a2 of the trial piece, issue #825).",
            studio => new PhotoSetup(Hall(studio, 2f, 0.25f, [new ExhibitWeapon(WeaponKind.ThrowingKnives, default)], [], (_, _) => null)) { Ticks = Settled, ShowsOnlyTheWorld = true }),
        new("loot-shimmer-pair-3m", "Two pieces of loot 0.4 m apart, 3 m away: their glows on the floor overlap without flickering (issue #825).",
            studio => new PhotoSetup(Hall(studio, 3f, 0.2f, [], Row([ItemKind.BladeParry, ItemKind.SilverRosary], 0.4f), (_, _) => null)) { Ticks = Settled, ShowsOnlyTheWorld = true }),
        new("loot-shimmer-frost-4m", "A piece of loot next to a chilled ghoul 4 m away: its glow on the floor lies over the disc of rime without flickering (issue #825).",
            studio => new PhotoSetup(NextToFrost(studio)) { Ticks = 3, ShowsOnlyTheWorld = true }),
        new("loot-shimmer-step-4m", "Two pieces of loot 4 m away, the right one at a pillar, the left one in front of a step of 0.25 m: the glow is cut off at the wall and does not climb the step (issue #825).",
            studio => new PhotoSetup(AtWallAndStep(studio)) { Ticks = Settled, ShowsOnlyTheWorld = true }),
        new("loot-shimmer-30", "Thirty pieces of loot in one hall, 3 m to 8 m away - to measure the frame rate with --bench (issue #825).",
            studio => new PhotoSetup(Thirty(studio)) { Ticks = Settled, ShowsOnlyTheWorld = true }),
    ];

    /// <summary>The clips: the row of all four tiers for two turns of a piece, from 3 m and from 8 m.</summary>
    public static MotionScene[] Clips() =>
    [
        new("loot-shimmer-near", "Loot of all four tiers 3 m away for one full turn: floating, lighting up in steps, the glow on the floor pulsing, sparks rising (issue #825).",
            studio => new PhotoSetup(TierRow(studio, 3f, NearClipLook)) { Ticks = Settled + ClipTicks }, FrameEvery: 3),
        new("loot-shimmer-far", "The same loot 8 m away for one full turn (issue #825).",
            studio => new PhotoSetup(TierRow(studio, 8f)) { Ticks = Settled + ClipTicks }, FrameEvery: 3),
    ];

    // One full turn of a piece: two pulses of the floor glow, three flashes.
    private static readonly int ClipTicks = SimulationClock.SecondsToTicks(LootShimmer.TurnPeriod);

    // The clips keep the hands, as in the game; from 3 m the eye looks at a point under the floor, so the pieces stand
    // above the hands.
    private const float NearClipLook = -0.5f;

    private static GameWorld TierRow(PhotoStudio studio, float metres, float lookHeight = 0.3f) =>
        Hall(studio, metres, lookHeight, [new ExhibitWeapon(WeaponKind.ThrowingKnives, new Vector3(1.8f, 0f, 0f))],
            Row(ItemOfEachTier, 0.9f, -0.45f), (_, _) => null);

    private static GameWorld WeaponRow(PhotoStudio studio, float metres)
    {
        WeaponKind[] kinds = [WeaponKind.Club, WeaponKind.Rifle, WeaponKind.HuntersAxe, WeaponKind.StakeCrossbow, WeaponKind.ThrowingKnives];
        var weapons = kinds.Select((kind, index) => new ExhibitWeapon(kind, new Vector3((index - ((kinds.Length - 1) / 2f)) * 1.1f, 0f, 0f))).ToList();
        return Hall(studio, metres, 0.3f, weapons, [], (_, _) => null);
    }

    // A ghoul 4 m in front of the player, chilled; the piece lies so that its glow reaches onto the disc of rime.
    private static GameWorld NextToFrost(PhotoStudio studio)
    {
        var world = Hall(studio, 4f, 0.2f, [], [new ExhibitItem(ItemKind.SilverRosary, new Vector3(0.75f, 0f, -0.2f))],
            (x, z) => x == PlayerColumn && z == PlayerRow + 2 ? 'G' : null);
        world.ApplyStatus(world.Ghouls[0], StatusKind.GraveChill, world.Player);
        return world;
    }

    // Row 4 of the hall (4 m away): a pillar one cell right of the middle as the player sees it, a step up from one
    // cell left of it. Each piece lies with its body clear of the edge and its widest ring reaching over it.
    private static GameWorld AtWallAndStep(PhotoStudio studio)
    {
        var gap = (BodySizes.Pickup.Radius + LootShimmer.Rings[^1].Radius) / 2f;
        var half = Level.CellSize / 2f;
        return Hall(studio, 4f, 0.15f, [],
            [new ExhibitItem(ItemKind.BladeParry, new Vector3(half - gap, 0f, 0f)), new ExhibitItem(ItemKind.Leech, new Vector3(-half + gap, 0f, 0f))],
            (x, z) => x == PlayerColumn - 1 && z == PlayerRow + 2 ? '#' : x > PlayerColumn ? '1' : null);
    }

    // Six rows of five pieces from 3 m to 8 m, items of all tiers and weapons in turn.
    private static GameWorld Thirty(PhotoStudio studio)
    {
        WeaponKind[] weaponKinds = [WeaponKind.ThrowingKnives, WeaponKind.Club, WeaponKind.Rifle, WeaponKind.ThrowingKnives, WeaponKind.HuntersAxe];
        var weapons = new List<ExhibitWeapon>();
        var items = new List<ExhibitItem>();
        for (var row = 0; row < 6; row++)
        {
            for (var column = 0; column < 5; column++)
            {
                var at = new Vector3((column - 2) * 1.1f, 0f, row);
                if (row % 2 == 0)
                {
                    items.Add(new ExhibitItem(ItemOfEachTier[(row + column) % ItemOfEachTier.Length], at));
                }
                else
                {
                    weapons.Add(new ExhibitWeapon(weaponKinds[column], at));
                }
            }
        }

        return Hall(studio, 3f, 0.3f, weapons, items, (_, _) => null);
    }

    private static List<ExhibitItem> Row(IReadOnlyList<ItemKind> kinds, float apart, float shift = 0f) =>
        [.. kinds.Select((kind, index) => new ExhibitItem(kind, new Vector3(((index - ((kinds.Count - 1) / 2f)) * apart) + shift, 0f, 0f)))];

    // A bare hall like PhotoStudio.Arena, without a figure unless `symbolAt` puts one (or a wall, or a step) on a cell.
    // The places of the finds count from the point on the floor `metres` in front of the player (x to the player's right - he looks
    // along +z, so that is against the way the map is written -, z away from the player); the player looks at that point, `lookHeight` above the floor.
    private static GameWorld Hall(
        PhotoStudio studio, float metres, float lookHeight, IReadOnlyList<ExhibitWeapon> weapons, IReadOnlyList<ExhibitItem> items,
        Func<int, int, char?> symbolAt)
    {
        var depth = PlayerRow + 9;
        var text = new StringBuilder();
        for (var z = 0; z < depth; z++)
        {
            for (var x = 0; x < HallWidth; x++)
            {
                var wall = x == 0 || z == 0 || x == HallWidth - 1 || z == depth - 1;
                text.Append(wall ? '#' : x == PlayerColumn && z == PlayerRow ? 'P' : symbolAt(x, z) ?? '.');
            }

            text.Append('\n');
        }

        var level = LevelParser.Parse(text.ToString());
        var middle = level.CellCenter(PlayerColumn, PlayerRow) + new Vector3(0f, 0f, metres);
        var world = new GameWorld(level, PhotoStudio.Seed, studio.Balance)
        {
            Surfaces = SurfaceVariants.Plain,
            Finds = new GroundFinds(
                [], [],
                [.. weapons.Select(weapon => weapon with { Position = middle + Seen(weapon.Position) })],
                [.. items.Select(item => item with { Position = middle + Seen(item.Position) })]),
        };
        PhotoStudio.Face(world.Player, middle + new Vector3(0f, lookHeight, 0f));
        return world;
    }

    private static Vector3 Seen(Vector3 place) => place with { X = -place.X };
}
