using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.Showroom;
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
        .. DoorScenes(DoorKind.Normal, "wooden double door"),
        .. DoorScenes(DoorKind.Locked, "locked door"),
        .. DoorScenes(DoorKind.Boss, "boss door"),
        new("door-cracked", "A cracked wall 6 m away in a bare hall: only its fine cracks give it away (issue #245).",
            studio => new PhotoSetup(studio.DoorHall(DoorKind.Cracked))),
        .. CrackedWallScenes(),
        new("door-sideways-half", "A wooden double door in a wall running north/south, 6 m away, half open (issue #245).",
            studio => new PhotoSetup(studio.DoorHall(DoorKind.Normal, sideways: true, open: true)) { Ticks = 600, StopWhen = Swung(0.5f) }),
        new("door-level-seed1", "The first wooden door of level 0 with seed 1, from 4 to 6 m, closed (issue #245).",
            studio => new PhotoSetup(studio.BeforeDoor(1, DoorKind.Normal))),
        new("door-level-seed2-boss", "The boss door of level 0 with seed 2, from 4 to 6 m, closed (issue #245).",
            studio => new PhotoSetup(studio.BeforeDoor(2, DoorKind.Boss))),
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
        new("blade-swing-half", "A quick blade swing halfway out: the forearm turns towards the shoulder (issue #265).",
            studio => new PhotoSetup(studio.Arena('G', 10))
            {
                // Pressed on tick 0, released on tick 1 - the swing starts; two frames later it is halfway out.
                Ticks = 3,
                Input = tick => PlayerInput.None with { Melee = tick == 0 },
            }),
        new("blade-swing-full", "A quick blade swing at its height: the forearm turns towards the shoulder (issue #265).",
            studio => new PhotoSetup(studio.Arena('G', 10))
            {
                // Seven frames after the release the blade is fully out, just before it swings back.
                Ticks = 8,
                Input = tick => PlayerInput.None with { Melee = tick == 0 },
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
        new("gorm-swing-windup", "Gorm whirling his chain above his head, the warning before the swing, the chain out to his right; in a hall as high as his boss room (issues #249, #254).",
            studio => new PhotoSetup(studio.Arena('T', 8, RoomTemplate.RoomCeilingHeight))
            {
                Ticks = 3000,
                StopWhen = world => world.Gorm is { State: GormState.WindingUp, Attack: GormAttack.Swing, StateProgress: >= 0.82f },
            }),
        new("gorm-swing", "Gorm sweeping his chain from his left to his right, a third of the way; in a hall as high as his boss room (issues #249, #254).",
            studio => new PhotoSetup(studio.Arena('T', 8, RoomTemplate.RoomCeilingHeight))
            {
                Ticks = 3000,
                StopWhen = world => world.Gorm is { State: GormState.Swinging, StateProgress: >= 0.3f },
            }),
        new("gorm-throw-windup", "Gorm about to throw his hook, which glows red: the warning, halfway through; in a hall as high as his boss room (issues #249, #254).",
            studio => new PhotoSetup(studio.Arena('T', 10, RoomTemplate.RoomCeilingHeight))
            {
                Ticks = 3000,
                StopWhen = world => world.Gorm is { State: GormState.WindingUp, Attack: GormAttack.Throw, StateProgress: >= 0.5f },
            }),
        new("gorm-hammer-windup", "Gorm with his club raised high, the warning before the hammer, near its top; in a hall as high as his boss room (issue #254).",
            studio => new PhotoSetup(studio.Arena('T', 8, RoomTemplate.RoomCeilingHeight))
            {
                Ticks = 3000,
                StopWhen = world => world.Gorm is { State: GormState.WindingUp, Attack: GormAttack.Hammer, StateProgress: >= 0.8f },
            }),
        new("gorm-shockwave", "Gorm's shock wave half way out (6 m), seen from 14 m, just outside its reach; for --bench with the wave in view (issue #299).",
            studio => MotionScenes.Find("gorm-slam-far")!.Build(studio) with
            {
                StopWhen = world => world.Gorm?.Shockwave is { Radius: >= 6f },
            }),
        new("gorm-phase2", "Gorm in phase 2 (half his health gone, red marks), 10 m away in his boss room, at the end of his roar (issue #249).",
            studio => new PhotoSetup(HalfHealth(studio.BeforeGorm(10f)))
            {
                Ticks = 600,
                StopWhen = world => world.Gorm is { IsInPhase2: true, State: GormState.Roaring, StateProgress: >= 0.95f },
            }),
        new("stairs-4m", "The spiral staircase of stone, standing in the boss room after Gorm's death, seen from 4 m, its lamp lit (issue #300).",
            studio => new PhotoSetup(studio.AfterGorm(4f))
            {
                Ticks = 600,
                StopWhen = world => world.Stairs is { CanInteract: true },
            }),
        new("stairs-10m", "Like stairs-4m, from 10 m: what is left of its outline at that distance (issue #300).",
            studio => new PhotoSetup(studio.AfterGorm(10f))
            {
                Ticks = 600,
                StopWhen = world => world.Stairs is { CanInteract: true },
            }),
        new("hud-weapons", "The HUD with every weapon place taken: club in hand, pistol, rifle and blunderbuss.",
            studio => new PhotoSetup(AllWeapons(studio.StartRun()))),
        new("hud-rifle", "Like hud-weapons, but the rifle chosen (2): a placeholder next to the club (issue #249).",
            studio => new PhotoSetup(AllWeapons(studio.StartRun()))
            {
                Ticks = 30,
                Input = tick => PlayerInput.None with { SelectSlot = tick == 0 ? 1 : null },
            }),
        new("hud-reload", "The pistol fired empty in the start room: \"Reload [R]\" under the crosshair (issue #259).",
            studio => new PhotoSetup(studio.StartRun())
            {
                Ticks = 1200,
                // Pull the trigger every few ticks; stop as the last round is gone (another pull would reload).
                Input = tick => PlayerInput.None with { Shoot = tick % 4 == 0 },
                StopWhen = world => world.Player.AmmoHint == AmmoHint.Reload,
            }),
        new("hud-texts", "Every HUD text at once (issues #340, #369): \"Reload [R]\" and \"[F] Take Rifle (Forged)\" under the crosshair, the skill point at the top left and the consecrated time above the weapons.",
            studio => new PhotoSetup(AllHudTexts(studio))
            {
                Ticks = 1200,
                Input = tick => PlayerInput.None with { Shoot = tick % 4 == 0 },
                StopWhen = world => world.Player.AmmoHint == AmmoHint.Reload,
            }),
        new("hud-calm", "The HUD in a calm moment (issue #369): three items, every weapon place taken, a quarter of the life gone, silver and a key.",
            studio => new PhotoSetup(Equipped(AllWeapons(studio.StartRun())))),
        new("hud-items", "The HUD with the club in hand and the six items drawn last (issue #381): boots, rosary, holster, parry, leech, ashen heart.",
            studio => new PhotoSetup(NewItems(AllWeapons(studio.StartRun())))),
        new("hud-boss", "The HUD in the boss fight (issue #369): Gorm's name and bar at the top, a quarter of his health gone.",
            studio => new PhotoSetup(Equipped(QuarterOfGorm(studio.BeforeGorm(10f))))
            {
                Ticks = 120,
                StopWhen = world => world.IsBossFightOn,
            }),
        new("hud-skillpoint", "The HUD while a boon waits (issue #369): the skill point with its key [X] at the top left.",
            studio => new PhotoSetup(LevelUp(Equipped(studio.StartRun())))),
        new("hud-pickup", "The HUD just after Hunter's Eye is taken with F (issue #369): the gold toast above the items.",
            studio => new PhotoSetup(Equipped(BeforeEquipment(studio, piece => piece is ItemPiece { Item: ItemKind.HuntersEye })))
            {
                Ticks = 45,
                Input = tick => PlayerInput.None with { Interact = tick == 0 },
            }),
        new("hud-reload-start", "The pistol just started reloading (R) with one round gone: the veil covers its picture (issue #370).",
            studio => ReloadingPistol(studio, 0.1f)),
        new("hud-reload-mid", "The pistol half way through its reload: the veil has shrunk to half (issue #370).",
            studio => ReloadingPistol(studio, 0.5f)),
        new("hud-hit", "A ghoul's blow has just landed: the picture darkened for a moment, the ghoul still visible (issue #370).",
            studio => new PhotoSetup(studio.Arena('G', 4))
            {
                Ticks = 1200,
                StopWhen = TicksAfter<PlayerHit>(3),
            }),
        new("hud-lowlife", "Little life left: the dark red edge at the height of its slow pulse (issue #370).",
            studio => new PhotoSetup(LowLife(studio.Arena('G', 10)))
            {
                Ticks = SimulationClock.SecondsToTicks(ScreenEffects.LowLifePulseSeconds) / 2,
            }),
        new("hud-skillpoint-glow", "A ghoul shot dead brings a level up: the short gold shine at the edge (issue #370).",
            studio => new PhotoSetup(OneShortOfLevel(studio.Arena('G', 10)))
            {
                Ticks = 1200,
                Input = tick => PlayerInput.None with { Shoot = tick % 4 == 0 },
                StopWhen = TicksAfter<LevelUp>(4),
            }),
        new("inventory", "The equipment page of the menu (G) open, every place of the bag taken (issue #348).",
            studio => new PhotoSetup(FullBag(AllWeapons(studio.StartRun())))
            {
                Input = tick => PlayerInput.None with { PressMenu = tick == 0 ? MenuKey.Equipment : null },
            }),
        new("map", "The map page of the menu (Tab) early in a run: values and five chosen boons on the left, the map on the right (issues #347, #348).",
            studio => new PhotoSetup(WithBoons(studio.StartRun()))
            {
                Input = tick => PlayerInput.None with { PressMenu = tick == 0 ? MenuKey.Map : null },
            }),
        new("map-skillpoint", "The map page of the menu (Tab) while a boon waits: the yellow \"[X] to level up\" above the values (issue #387).",
            studio => new PhotoSetup(LevelUp(Equipped(studio.StartRun())))
            {
                Input = tick => PlayerInput.None with { PressMenu = tick == 0 ? MenuKey.Map : null },
            }),
        new("menu-title", "The title screen: Start, Settings, Quit (issue #349).",
            studio => new PhotoSetup(studio.StartRun()) { Menu = PhotoMenu.Title }),
        new("menu-pause", "The pause menu (Esc) over a run: Resume, Settings, Back to title, Quit (issue #349).",
            studio => new PhotoSetup(studio.StartRun()) { Menu = PhotoMenu.Pause }),
        new("menu-settings", "The settings opened from the pause menu: sensitivity, three volumes, window or full screen (issue #349).",
            studio => new PhotoSetup(studio.StartRun()) { Menu = PhotoMenu.Settings }),
        new("menu-quit", "The question before quitting, asked from the pause menu: Cancel, Quit (issues #349, #371).",
            studio => new PhotoSetup(studio.StartRun()) { Menu = PhotoMenu.Quit }),
        new("model-probe-4m", "One target on a bare floor, 4 m away - stands in for a model under game conditions.",
            studio => new PhotoSetup(studio.Arena('G', 4)) { IsModelProbe = true }),
        new("model-probe-10m", "One target on a bare floor, 10 m away - stands in for a model under game conditions.",
            studio => new PhotoSetup(studio.Arena('G', 10)) { IsModelProbe = true }),
        .. ShowroomScenes(),
        new("equipment-rifle", "The rifle (Forged) lying in the showroom, 0.8 m away: \"[F] Take Rifle (Forged)\" in steel blue (issue #317).",
            studio => new PhotoSetup(BeforeEquipment(studio, piece => piece is WeaponPiece { Weapon: WeaponKind.Rifle }))),
        .. EquipmentTierScenes(),
        new("equipment-taken-royal", "Hunter's Eye (Royal) just taken with F: its card on the left, framed in gold (issue #317).",
            studio => new PhotoSetup(BeforeEquipment(studio, piece => piece is ItemPiece { Item: ItemKind.HuntersEye }))
            {
                Ticks = 45,
                Input = tick => PlayerInput.None with { Interact = tick == 0 },
            }),
        new("equipment-taken-vampiric", "The Leech (Vampiric) just taken with F: its card and the short blood-red glow from the edges (issue #317).",
            studio => new PhotoSetup(BeforeEquipment(studio, piece => piece is ItemPiece { Item: ItemKind.Leech }))
            {
                Ticks = 15,
                Input = tick => PlayerInput.None with { Interact = tick == 0 },
            }),
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

    // The showroom (issue #283): every part of every section from its west end, across to its north wall and pieces.
    private static IEnumerable<PhotoScene> ShowroomScenes()
    {
        foreach (var section in ShowroomLayout.Build().Sections)
        {
            foreach (var part in Enum.GetValues<ShowroomPart>())
            {
                var shown = part == ShowroomPart.Corridor
                    ? "the corridor (3.5 m) with its doors and cracked wall"
                    : "the room (6 m) with its pieces";
                yield return new(
                    $"showroom-{section.Variant.ToString().ToLowerInvariant()}-{part.ToString().ToLowerInvariant()}",
                    $"The showroom, wall variant {section.Variant}: {shown}.",
                    studio => new PhotoSetup(ShowroomLayout.Build().ViewOf(section.Index, part, studio.Balance)));
            }
        }
    }

    // One item of each tier lying in the showroom, 0.8 m away: its glow and "[F] Take ..." in its colour (issue #317).
    private static IEnumerable<PhotoScene> EquipmentTierScenes()
    {
        foreach (var tier in Enum.GetValues<ItemTier>())
        {
            var item = ShowroomPieces.All.OfType<ItemPiece>().First(piece => Items.Info(piece.Item).Tier == tier).Item;
            yield return new(
                $"equipment-{tier.ToString().ToLowerInvariant()}",
                $"{Items.Info(item).Name} ({tier}) lying in the showroom, 0.8 m away: its glow and \"[F] Take ...\" in the colour of its tier (issue #317).",
                studio => new PhotoSetup(BeforeEquipment(studio, piece => piece is ItemPiece shown && shown.Item == item)));
        }
    }

    // The showroom with a player 0.8 m south of the first piece that is `isPiece`, looking at it - close enough for F
    // (issue #317).
    private static GameWorld BeforeEquipment(PhotoStudio studio, Func<ShowroomPiece, bool> isPiece)
    {
        var layout = ShowroomLayout.Build();
        var placed = layout.Placed.First(place => isPiece(place.Piece));
        var spot = layout.Level.CellCenter(placed.X, placed.Z);
        var world = layout.NewWorldAt(placed.X, placed.Z + 1, spot, studio.Balance);
        world.Player.PlaceAt(spot + new Vector3(0f, 0f, 0.8f));
        PhotoStudio.Face(world.Player, spot + new Vector3(0f, EquipmentDrop.BoxHeight / 2f, 0f));
        return world;
    }

    // Closed, half open and fully open, 6 m away in a bare hall (issue #245).
    private static PhotoScene[] DoorScenes(DoorKind kind, string name)
    {
        var file = $"door-{kind.ToString().ToLowerInvariant()}";
        return
        [
            new(file, $"A {name} 6 m away in a bare hall, closed (issue #245).",
                studio => new PhotoSetup(studio.DoorHall(kind))),
            new($"{file}-half", $"A {name} 6 m away, opened by the player, halfway through its swing (issue #245).",
                studio => new PhotoSetup(studio.DoorHall(kind, open: true)) { Ticks = 600, StopWhen = Swung(0.5f) }),
            new($"{file}-open", $"A {name} 6 m away, opened by the player and fully swung open (issue #245).",
                studio => new PhotoSetup(studio.DoorHall(kind, open: true)) { Ticks = 600, StopWhen = Swung(1f) }),
            new($"{file}-10m", $"A {name} 10 m away in a bare hall, closed: can its kind still be told apart (issue #245)?",
                studio => new PhotoSetup(studio.DoorHall(kind, cells: 5))),
        ];
    }

    // The cracked wall in the level, its plates in the stone of the walls around (issue #246): does it sit in the wall
    // without a seam, from near and far, at a flat angle, in every variant, turned, from inside - and how it breaks.
    private static PhotoScene[] CrackedWallScenes() =>
    [
        new("cracked-wall-2m", "The first cracked wall of level 0 from 2 m, outside the secret room (issue #246).",
            studio => new PhotoSetup(studio.BeforeCrackedWall(1))),
        new("cracked-wall-4m", "The first cracked wall of level 0 from 4 m (issue #246).",
            studio => new PhotoSetup(studio.BeforeCrackedWall(2))),
        new("cracked-wall-10m", "A cracked wall of level 0 from 10 m: does it still sit in the wall (issue #246)?",
            studio => new PhotoSetup(studio.BeforeCrackedWall(5))),
        new("cracked-wall-flat", "A cracked wall from 2 m out and 6 m along: its plate at a flat angle (issue #246).",
            studio => new PhotoSetup(studio.BeforeCrackedWall(1, along: 3))),
        new("cracked-wall-a-4m", "A cracked wall in wall variant A from 4 m (issue #246).",
            studio => new PhotoSetup(studio.BeforeCrackedWall(2, variant: TextureVariant.A))),
        new("cracked-wall-b-4m", "A cracked wall in wall variant B from 4 m (issue #246).",
            studio => new PhotoSetup(studio.BeforeCrackedWall(2, variant: TextureVariant.B))),
        new("cracked-wall-c-4m", "A cracked wall in wall variant C from 4 m (issue #246).",
            studio => new PhotoSetup(studio.BeforeCrackedWall(2, variant: TextureVariant.C))),
        new("cracked-wall-sideways-4m", "A cracked wall of an East/West door from 4 m: the plates turned a quarter (issue #246).",
            studio => new PhotoSetup(studio.BeforeCrackedWall(2, sideways: true))),
        new("cracked-wall-secret-4m", "A cracked wall from inside its secret room, 4 m away (issue #246).",
            studio => new PhotoSetup(studio.BeforeCrackedWall(2, fromSecretRoom: true))),
        new("cracked-wall-break", "A cracked wall 2 m away, broken from inside its secret room: the stone chunks in flight, 8 ticks after the blow (issue #246).",
            studio => Broken(studio, 8)),
        new("cracked-wall-break-after", "The same wall 100 ticks after the blow: the dust gone, the stone chunks on the floor (issue #246).",
            studio => Broken(studio, 100)),
    ];

    // The cracked wall 2 m before the player inside its secret room (no enemy gets in there to disturb the picture),
    // struck with a fully charged blade - held until charged, then let go - and `ticks` more ticks after it broke.
    private static PhotoSetup Broken(PhotoStudio studio, int ticks)
    {
        var world = studio.BeforeCrackedWall(1, fromSecretRoom: true);
        var released = false;
        var afterBreak = 0;
        return new PhotoSetup(world)
        {
            Ticks = 600,
            Input = _ =>
            {
                released |= world.Player.MeleeCharge is { IsCharging: true, Fraction: >= 1f };
                return PlayerInput.None with { Melee = !released };
            },
            StopWhen = run => run.Doors.Any(door => door.Kind == DoorKind.Cracked && door.IsOpen) && ++afterBreak >= ticks,
        };
    }

    private static Func<GameWorld, bool> Swung(float fraction) => world => world.Doors[0].OpenFraction >= fraction;

    // The start room, one pistol round fired, then R: the picture is taken when the reload has come <paramref name="share"/>
    // of the way (issue #370).
    private static PhotoSetup ReloadingPistol(PhotoStudio studio, float share)
    {
        const int ReloadAt = 10;
        var world = studio.StartRun();
        var reload = SimulationClock.SecondsToTicks(world.Balance.Pistol.ReloadTime);
        return new PhotoSetup(world)
        {
            Ticks = ReloadAt + 1 + (int)MathF.Round(reload * share),
            Input = tick => PlayerInput.None with { Shoot = tick == 0, Reload = tick == ReloadAt },
        };
    }

    // Stops <paramref name="ticks"/> ticks after the first event of type T (issue #370: the edge effects need the
    // event during the ticks, not before). One counter per built scene.
    private static Func<GameWorld, bool> TicksAfter<T>(int ticks)
        where T : GameEvent
    {
        int? seen = null;
        return world =>
        {
            if (seen is null && world.Events.OfType<T>().Any())
            {
                seen = 0;
            }
            else if (seen is not null)
            {
                seen++;
            }

            return seen >= ticks;
        };
    }

    // Just under the share of the life at which the dark red edge shows (issue #370).
    private static GameWorld LowLife(GameWorld world)
    {
        var player = world.Player;
        var left = Math.Max(1, (int)(player.MaxHealth * ScreenEffects.LowLifeShare) - 1);
        player.TakeDamage(player.MaxHealth - left, new List<GameEvent>());
        return world;
    }

    // One experience point short of the next level: the next ghoul shot dead brings the level up (issue #370).
    private static GameWorld OneShortOfLevel(GameWorld world)
    {
        world.Player.AddExperience(world.Player.ExperienceToNextLevel - world.Player.Experience - 1, new List<GameEvent>());
        return world;
    }

    // In front of the rifle in the showroom (the "[F] Take" prompt), consecrated for a minute, one level up whose boon
    // waits: with the pistol fired empty, every HUD text shows at the same time (issue #340).
    private static GameWorld AllHudTexts(PhotoStudio studio)
    {
        var world = BeforeEquipment(studio, piece => piece is WeaponPiece { Weapon: WeaponKind.Rifle });
        world.Player.Consecrate(60f);
        world.Player.AddExperience(world.Player.ExperienceToNextLevel, new List<GameEvent>());
        return world;
    }

    // Five level ups, each time the offered boon chosen least so far (the same seed, the same boons), then a few hits taken - so
    // the map page shows changed values and several boons (issue #347).
    private static GameWorld WithBoons(GameWorld world)
    {
        var player = world.Player;
        for (var i = 0; i < 5; i++)
        {
            player.AddExperience(player.ExperienceToNextLevel - player.Experience, new List<GameEvent>());
            player.Choose(player.CurrentOffer.MinBy(player.BoonCount), new List<GameEvent>());
        }

        player.TakeDamage(player.MaxHealth / 3, new List<GameEvent>());
        return world;
    }

    // A run some rooms in (issue #369): three items, silver and a key, a quarter of the life gone.
    private static GameWorld Equipped(GameWorld world)
    {
        var player = world.Player;
        player.Take(ItemKind.IronFlask);
        player.Take(ItemKind.HuntersEye);
        player.Take(ItemKind.Leech);
        player.AddSilver(85);
        player.AddKeys(1);
        player.TakeDamage(player.MaxHealth / 4, new List<GameEvent>());
        return world;
    }

    // The six items whose pictures came last (issue #381) - as many as fit beside the life frame at 854 x 480.
    private static GameWorld NewItems(GameWorld world)
    {
        ItemKind[] items = [ItemKind.HuntersBoots, ItemKind.SilverRosary, ItemKind.QuickdrawHolster, ItemKind.BladeParry, ItemKind.Leech, ItemKind.AshenHeart];
        foreach (var item in items)
        {
            world.Player.Take(item);
        }

        return world;
    }

    // One level up whose boon waits (issue #369).
    private static GameWorld LevelUp(GameWorld world)
    {
        world.Player.AddExperience(world.Player.ExperienceToNextLevel, new List<GameEvent>());
        return world;
    }

    // A quarter of his health gone, still in phase 1.
    private static GameWorld QuarterOfGorm(GameWorld world)
    {
        var gorm = world.Gorm!;
        gorm.TakeDamage(gorm.MaxHealth / 4, new List<GameEvent>());
        return world;
    }

    private static GameWorld AllWeapons(GameWorld world)
    {
        world.Player.EquipMelee(WeaponKind.Club);
        world.Player.Equip(1, WeaponKind.Rifle);
        world.Player.Equip(2, WeaponKind.Blunderbuss);
        return world;
    }

    // Half his health gone (as by the player's hits): Gorm roars into phase 2.
    private static GameWorld HalfHealth(GameWorld world)
    {
        var gorm = world.Gorm!;
        gorm.TakeDamage(gorm.MaxHealth - (int)(gorm.MaxHealth * world.Balance.Gorm.Phase2Health), new List<GameEvent>());
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
