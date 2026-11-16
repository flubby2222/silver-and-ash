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
        new("bat-4m", "A bat 4 m away in a bare hall.",
            studio => new PhotoSetup(studio.Arena('B', 4))),
        new("bat-10m", "A bat 10 m away in a bare hall.",
            studio => new PhotoSetup(studio.Arena('B', 10))),
        new("gorm", "Gorm, the Chained, 6 m away in his boss room.",
            studio => new PhotoSetup(studio.BeforeGorm(6f))),
        new("hud-weapons", "The HUD with every weapon place taken: club in hand, pistol, rifle and blunderbuss.",
            studio => new PhotoSetup(AllWeapons(studio.StartRun()))),
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
