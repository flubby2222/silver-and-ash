using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Photos;

/// <summary>
/// Every photo scene in one place (issue #224). A new scene is one more line here: a name, what it shows and how the
/// <see cref="PhotoStudio"/> builds it. See docs/foto-skript.md.
/// </summary>
public static class PhotoScenes
{
    /// <summary>All scenes, in the order <c>--photo all</c> takes them.</summary>
    public static IReadOnlyList<PhotoScene> All { get; } =
    [
        new("start-room", "Level 0 as a run begins: start room, level name, standard weapons.",
            studio => new PhotoSetup(studio.StartRun())),
        new("room-combat", "A combat room from its far corner, just entered: its enemies wake up.",
            studio => new PhotoSetup(studio.InRoom(RoomKind.Combat))),
        new("room-corridor", "A corridor from one end.",
            studio => new PhotoSetup(studio.InRoom(RoomKind.Corridor))),
        new("room-treasure", "A treasure room from its far corner, its chests in view.",
            studio => new PhotoSetup(studio.InRoom(RoomKind.Treasure))),
        new("room-secret", "A secret room (behind a cracked wall) from its far corner.",
            studio => new PhotoSetup(studio.InRoom(RoomKind.Secret))),
        new("room-boss", "The boss room from 12 m, Gorm waiting.",
            studio => new PhotoSetup(studio.BeforeGorm(12f))),
        new("chests-4m", "The chests of all four tiers side by side 4 m away, closed and locked, and an open one.",
            studio => new PhotoSetup(studio.ChestRow())),
        new("ghoul-4m", "A ghoul 4 m away in a bare hall.",
            studio => new PhotoSetup(studio.Arena('G', 4))),
        new("ghoul-10m", "A ghoul 10 m away in a bare hall.",
            studio => new PhotoSetup(studio.Arena('G', 10))),
        new("ghoul-windup", "A ghoul drawing back for a blow, halfway through.",
            studio => new PhotoSetup(studio.Arena('G', 4))
            {
                Ticks = 600,
                StopWhen = world => world.Ghouls[0] is { State: GhoulState.WindingUp, WindUpProgress: >= 0.5f },
            }),
        new("pistol-recoil", "The pistol right after a shot: kicked up, hammer down, muzzle flash (issue #249).",
            studio => new PhotoSetup(studio.Arena('G', 10))
            {
                Ticks = 2,
                Input = tick => PlayerInput.None with { Shoot = tick == 0 },
            }),
        new("blade-charged", "The silver blade fully charged, drawn back and trembling (issue #249).",
            studio => new PhotoSetup(studio.Arena('G', 10))
            {
                Ticks = 600,
                Input = _ => PlayerInput.None with { Melee = true },
                StopWhen = world => world.Player.MeleeCharge is { IsCharging: true, Fraction: >= 1f },
            }),
        new("bat-4m", "A bat 4 m away in a bare hall.",
            studio => new PhotoSetup(studio.Arena('B', 4))),
        new("bat-10m", "A bat 10 m away in a bare hall.",
            studio => new PhotoSetup(studio.Arena('B', 10))),
        new("bat-screech", "A bat screeching, the warning before its dive, halfway through (issue #249).",
            studio => new PhotoSetup(studio.Arena('B', 4))
            {
                Ticks = 1200,
                // No spit in the air: it would hide the bat.
                StopWhen = world => world.Bats[0] is { State: BatState.Screeching, ScreechProgress: >= 0.5f } && world.Projectiles.Count == 0,
            }),
        new("bat-dive", "A bat diving at the player, within 4 m of its feet (issue #249).",
            studio => new PhotoSetup(studio.Arena('B', 4))
            {
                Ticks = 1200,
                // Within 4 m of the player's feet: well into the dive, coming closer.
                StopWhen = world => world.Bats[0].State == BatState.Diving
                    && System.Numerics.Vector3.Distance(world.Bats[0].Position, world.Player.Position) < 4f,
            }),
        new("gorm", "Gorm, the Chained, 6 m away in his boss room.",
            studio => new PhotoSetup(studio.BeforeGorm(6f))),
        new("hud-weapons", "The HUD with every weapon place taken: club in hand, pistol, rifle and blunderbuss.",
            studio => new PhotoSetup(AllWeapons(studio.StartRun()))),
        new("hud-rifle", "Like hud-weapons, but the rifle chosen (2): a placeholder next to the club (issue #249).",
            studio => new PhotoSetup(AllWeapons(studio.StartRun()))
            {
                Ticks = 30,
                Input = tick => PlayerInput.None with { SelectSlot = tick == 0 ? 1 : null },
            }),
        new("inventory", "The inventory window (G) open, every place of the bag taken.",
            studio => new PhotoSetup(FullBag(AllWeapons(studio.StartRun())))
            {
                Input = tick => PlayerInput.None with { ToggleInventory = tick == 0 },
            }),
        new("map", "The map kept open (M) at the start of a run.",
            studio => new PhotoSetup(studio.StartRun()) { MapOpen = true }),
        new("model-probe-4m", "One target on a bare floor, 4 m away - stands in for a model under game conditions.",
            studio => new PhotoSetup(studio.Arena('G', 4)) { IsModelProbe = true }),
        new("model-probe-10m", "One target on a bare floor, 10 m away - stands in for a model under game conditions.",
            studio => new PhotoSetup(studio.Arena('G', 10)) { IsModelProbe = true }),
    ];

    /// <summary>
    /// The scenes <c>--photo compare</c> takes in every picture variant (issue #234): corridor, combat room, a ghoul from
    /// near and far and drawing back, a bat from far, Gorm.
    /// </summary>
    public static IReadOnlyList<PhotoScene> LookComparison { get; } =
        new[] { "room-corridor", "room-combat", "ghoul-4m", "ghoul-10m", "ghoul-windup", "bat-10m", "gorm" }
            .Select(name => All.First(scene => scene.Name == name)).ToList();

    /// <summary>The scene called <paramref name="name"/>, or null.</summary>
    public static PhotoScene? Find(string name) => All.FirstOrDefault(scene => scene.Name == name);

    private static GameWorld AllWeapons(GameWorld world)
    {
        world.Player.EquipMelee(WeaponKind.Club);
        world.Player.Equip(1, WeaponKind.Rifle);
        world.Player.Equip(2, WeaponKind.Blunderbuss);
        return world;
    }

    private static GameWorld FullBag(GameWorld world)
    {
        WeaponKind[] kinds = [WeaponKind.SilverBlade, WeaponKind.Pistol, WeaponKind.Rifle, WeaponKind.Blunderbuss, WeaponKind.Club, WeaponKind.Rifle];
        foreach (var kind in kinds)
        {
            world.Player.Stow(kind);
        }

        return world;
    }
}
