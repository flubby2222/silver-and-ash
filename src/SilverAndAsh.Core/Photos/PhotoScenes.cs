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
        new("well-ready", "The Well of Sirius 4 m away, ready: water in its three bowls.",
            studio => new PhotoSetup(studio.Well(spent: false))),
        new("well-spent", "The Well of Sirius 4 m away, spent: dry.",
            studio => new PhotoSetup(studio.Well(spent: true))),
        new("teleporter-locked-3m", "A teleporter not found yet, 3 m away: dark crystal (issue #480).",
            studio => new PhotoSetup(studio.TeleporterHall(3f, unlocked: false))),
        new("teleporter-unlocked-3m", "A found teleporter 3 m away: its crystal shines by itself (issue #480).",
            studio => new PhotoSetup(studio.TeleporterHall(3f, unlocked: true))),
        new("teleporter-locked-10m", "A teleporter not found yet, 10 m away (issue #480).",
            studio => new PhotoSetup(studio.TeleporterHall(10f, unlocked: false))),
        new("teleporter-unlocked-10m", "A found teleporter 10 m away: the crystal still shines through the dark (issue #480).",
            studio => new PhotoSetup(studio.TeleporterHall(10f, unlocked: true))),
        new("teleporter-hint", "On the start-hall teleporter of level 0 with every teleporter found: the hint [T] Travel (issue #480).",
            studio => new PhotoSetup(studio.AtStartTeleporter(sealBossGate: false))),
        new("menu-travel", "The destination choice on the start-hall teleporter: map, marks, Boss Gate chosen (issue #480).",
            studio => new PhotoSetup(studio.AtStartTeleporter(sealBossGate: false)) { Menu = PhotoMenu.Travel }),
        new("menu-travel-sealed", "The destination choice with the boss-gate room sealed: its mark grey, no Travel (issue #480).",
            studio => new PhotoSetup(studio.AtStartTeleporter(sealBossGate: true)) { Menu = PhotoMenu.Travel }),
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
        new("club-rest", "The club in hand at rest, held like the blade (issue #403).",
            studio => new PhotoSetup(ClubInHand(studio.Arena('G', 10)))),
        new("club-swing-half", "A quick club swing halfway out, the same movement as the blade's (issue #403).",
            studio => new PhotoSetup(ClubInHand(studio.Arena('G', 10)))
            {
                Ticks = 3,
                Input = tick => PlayerInput.None with { Melee = tick == 0 },
            }),
        new("club-charged", "The club fully wound up: the bend at the grip eases to 20 degrees, the head turns in (issue #417).",
            studio => new PhotoSetup(ClubInHand(studio.Arena('G', 10)))
            {
                Ticks = 600,
                Input = _ => PlayerInput.None with { Melee = true },
                StopWhen = world => world.Player.MeleeCharge is { IsCharging: true, Fraction: >= 1f },
            }),
        new("club-swing-full", "A quick club swing at its height, where it hits: the club straight in the sleeve (issue #417).",
            studio => new PhotoSetup(ClubInHand(studio.Arena('G', 10)))
            {
                Ticks = 8,
                Input = tick => PlayerInput.None with { Melee = tick == 0 },
            }),
        new("club-charged-release", "A fully charged club swing let go, at its height: the club straight in the sleeve although the wind-up is still easing off (issue #417).",
            studio => ChargedClubSwing(studio)),
        new("showroom-club", "The club in hand before the club (Worn) lying in the showroom (issue #403).",
            studio => new PhotoSetup(ClubInHand(BeforeEquipment(studio, piece => piece is WeaponPiece { Weapon: WeaponKind.Club })))),
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
        new("hud-blunderbuss", "Like hud-weapons, but the blunderbuss chosen (3): its one round large above the weapons (issue #388).",
            studio => new PhotoSetup(AllWeapons(studio.StartRun()))
            {
                Ticks = 30,
                Input = tick => PlayerInput.None with { SelectSlot = tick == 0 ? 2 : null },
            }),
        new("hud-melee", "Like hud-weapons, the club held charging: the large rounds number gives way (issue #388).",
            studio => new PhotoSetup(AllWeapons(studio.StartRun()))
            {
                Ticks = 20,
                Input = _ => PlayerInput.None with { Melee = true },
            }),
        new("hud-reload","The pistol fired empty in the start room: \"Reload [R]\" under the crosshair (issue #259).",
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
        new("hud-reload-mid", "The pistol half way through its reload: the veil has shrunk to the right half of the frame (issues #370, #389).",
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
        new("ghoul-shotbody-front", "The showroom's ghoul from 1.8 m in front, with what a shot can hit drawn over it as wires (issue #433).",
            studio => new PhotoSetup(BesideTheGhoul(studio, ahead: 1.8f, aside: 0f)) { ShowsShotBodies = true }),
        new("ghoul-shotbody-side", "The showroom's ghoul from 1.8 m to its side, with what a shot can hit drawn over it as wires (issue #433).",
            studio => new PhotoSetup(BesideTheGhoul(studio, ahead: 0f, aside: 1.8f)) { ShowsShotBodies = true }),
        new("crossbow-skeleton-rest-4m", "The showroom's crossbow skeleton at rest, 4 m in front of it: crossbow lowered, eyes dark (issue #497).",
            studio => new PhotoSetup(BeforeCrossbowSkeleton(studio, CrossbowSkeletonStance.Rest, 4f))),
        new("crossbow-skeleton-announce-4m", "The showroom's crossbow skeleton announcing a shot, 4 m in front of it: crossbow raised, eyes red (issue #497).",
            studio => new PhotoSetup(BeforeCrossbowSkeleton(studio, CrossbowSkeletonStance.Announce, 4f))),
        new("crossbow-skeleton-announce-2m", "The showroom's crossbow skeleton announcing a shot, 2 m in front of it - the red eyes up close (issue #497).",
            studio => new PhotoSetup(BeforeCrossbowSkeleton(studio, CrossbowSkeletonStance.Announce, 2f))),
        new("crypt-warden-rest-4m", "The showroom's crypt warden at rest, 4 m in front of it: pole propped up, eye dull (issue #530).",
            studio => new PhotoSetup(BeforeCryptWarden(studio, CryptWardenStance.Rest, 4f))),
        new("crypt-warden-announce-4m", "The showroom's crypt warden announcing its blow, 4 m in front of it: pole raised, eye red (issue #530).",
            studio => new PhotoSetup(BeforeCryptWarden(studio, CryptWardenStance.Announce, 4f))),
        new("crypt-warden-announce-2m", "The showroom's crypt warden announcing its blow, 2 m in front of it - the red eye up close (issue #530).",
            studio => new PhotoSetup(BeforeCryptWarden(studio, CryptWardenStance.Announce, 2f))),
        new("model-probe-4m", "One target on a bare floor, 4 m away - stands in for a model under game conditions.",
            studio => new PhotoSetup(studio.Arena('G', 4)) { IsModelProbe = true }),
        new("model-probe-10m", "One target on a bare floor, 10 m away - stands in for a model under game conditions.",
            studio => new PhotoSetup(studio.Arena('G', 10)) { IsModelProbe = true }),
        .. ShowroomScenes(),
        .. WallPillarScenes(),
        .. CeilingArchScenes(),
        .. WallNicheScenes(),
        .. PortcullisScenes(),
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

    // The wall pillars of the showroom (issue #536): for every wall variant and part, from the south side of the part a
    // little east of its second pillar, looking at it at the height of the eye - the pillar beside the bare wall.
    private static IEnumerable<PhotoScene> WallPillarScenes()
    {
        foreach (var section in ShowroomLayout.Build().Sections)
        {
            foreach (var part in Enum.GetValues<ShowroomPart>())
            {
                var height = part == ShowroomPart.Corridor ? "3.5 m" : "6 m";
                yield return new(
                    $"wall-pillar-{section.Variant.ToString().ToLowerInvariant()}-{part.ToString().ToLowerInvariant()}",
                    $"The wall pillars of the showroom against wall variant {section.Variant} under a ceiling of {height} (issue #536).",
                    studio => new PhotoSetup(BeforeWallPillar(studio, section.Index, part)));
            }
        }
    }

    // The ceiling arches of the showroom (issue #537): for every wall variant the arch nearest the corridor's mouth from
    // 2.5 m east of it, looking up at it - arch, pillars and the wall behind; then down the arch corridor of section A from
    // its mouth, the sequence of three one behind the other.
    private static IEnumerable<PhotoScene> CeilingArchScenes()
    {
        foreach (var section in ShowroomLayout.Build().Sections)
        {
            yield return new(
                $"ceiling-arch-{section.Variant.ToString().ToLowerInvariant()}",
                $"A ceiling arch of the showroom across a corridor 2 m wide in wall variant {section.Variant}, from 2.5 m (issue #537).",
                studio => new PhotoSetup(BeforeCeilingArch(studio, section.Index)));
        }

        yield return new(
            "ceiling-arch-sequence",
            "Down the showroom's arch corridor of wall variant A from its mouth: three ceiling arches, 4 m apart (issue #537).",
            studio => new PhotoSetup(AlongCeilingArches(studio)));
    }

    private static GameWorld BeforeCeilingArch(PhotoStudio studio, int sectionIndex)
    {
        var layout = ShowroomLayout.Build();
        var arch = ArchesOf(layout, sectionIndex)[0];
        var (x, z) = Level.ToGrid(arch.Position.X, arch.Position.Z);
        var world = layout.NewWorldAt(x + 1, z, arch.Position, studio.Balance);
        world.Player.PlaceAt(arch.Position + new Vector3(2.5f, 0f, 0f));
        PhotoStudio.Face(world.Player, arch.Position + new Vector3(0f, 2.4f, 0f));
        return world;
    }

    private static GameWorld AlongCeilingArches(PhotoStudio studio)
    {
        var layout = ShowroomLayout.Build();
        var arches = ArchesOf(layout, 0);
        var (x, z) = Level.ToGrid(arches[0].Position.X, arches[0].Position.Z);
        var world = layout.NewWorldAt(x + 2, z, arches[^1].Position, studio.Balance);
        PhotoStudio.Face(world.Player, arches[^1].Position + new Vector3(0f, 1.9f, 0f));
        return world;
    }

    // The ceiling arches of a section, from the corridor's mouth (east) inward.
    private static List<ExhibitCeilingArch> ArchesOf(ShowroomLayout layout, int sectionIndex)
    {
        var variant = layout.Sections[sectionIndex].Variant;
        return [.. layout.Exhibits.CeilingArches.Where(arch => arch.Variant == variant).OrderByDescending(arch => arch.Position.X)];
    }

    // The wall niches of the showroom (issue #538): for every wall variant and part, 3 m in front of the part's second niche
    // and a little east of it, looking at it at the height of its sill and back - frame, plate and the wall around it.
    private static IEnumerable<PhotoScene> WallNicheScenes()
    {
        foreach (var section in ShowroomLayout.Build().Sections)
        {
            foreach (var part in Enum.GetValues<ShowroomPart>())
            {
                var height = part == ShowroomPart.Corridor ? "3.5 m" : "6 m";
                yield return new(
                    $"wall-niche-{section.Variant.ToString().ToLowerInvariant()}-{part.ToString().ToLowerInvariant()}",
                    $"A wall niche of the showroom in wall variant {section.Variant} under a ceiling of {height}, from 3 m (issue #538).",
                    studio => new PhotoSetup(BeforeWallNiche(studio, section.Index, part, WallNicheForm.Flat)));
            }
        }

        // The deep niches (issue #553): the same view of the part's first deep niche without and with a shelf.
        foreach (var section in ShowroomLayout.Build().Sections)
        {
            foreach (var part in Enum.GetValues<ShowroomPart>())
            {
                foreach (var (form, name, shown) in new[] { (WallNicheForm.Deep, "deep", "deep wall niche"), (WallNicheForm.DeepWithShelf, "shelf", "deep wall niche with a shelf") })
                {
                    var height = part == ShowroomPart.Corridor ? "3.5 m" : "6 m";
                    yield return new(
                        $"wall-niche-{name}-{section.Variant.ToString().ToLowerInvariant()}-{part.ToString().ToLowerInvariant()}",
                        $"A {shown} of the showroom in wall variant {section.Variant} under a ceiling of {height}, from 3 m (issue #553).",
                        studio => new PhotoSetup(BeforeWallNiche(studio, section.Index, part, form)));
                }
            }
        }
    }

    // The second flat niche of the part, or the first deep one of its form.
    private static GameWorld BeforeWallNiche(PhotoStudio studio, int sectionIndex, ShowroomPart part, WallNicheForm form)
    {
        var layout = ShowroomLayout.Build();
        var section = layout.Sections[sectionIndex];
        var size = part == ShowroomPart.Corridor ? WallNicheSize.Corridor : WallNicheSize.Room;
        var niches = layout.Exhibits.WallNiches
            .Where(niche => niche.Variant == section.Variant && niche.Size == size && niche.Form == form)
            .OrderBy(niche => niche.Position.X)
            .ToList();
        var niche = niches[form == WallNicheForm.Flat ? Math.Min(1, niches.Count - 1) : 0];
        var (x, _) = Level.ToGrid(niche.Position.X, niche.Position.Z);
        var world = layout.NewWorldAt(x, section.NorthRow, niche.Position, studio.Balance);
        world.Player.PlaceAt(niche.Position + (niche.Facing * 3f) + new Vector3(0.8f, 0f, 0f));
        PhotoStudio.Face(world.Player, niche.Position + new Vector3(0f, 1.3f, 0f));
        return world;
    }

    // The portcullis of section A (issue #540): shut, half lifted and open, from the room 3 m in front of it, from its
    // closet behind it and from 6 m back in the room. The world's ticks pick the point of the showroom's loop
    // (ShowroomGrateCycle); a scene's world starts at tick 0.
    private static IEnumerable<PhotoScene> PortcullisScenes()
    {
        foreach (var (state, ticks) in PortcullisStates())
        {
            foreach (var (where, back) in new[] { ("room", 3f), ("closet", -3f), ("far", 6f) })
            {
                yield return new(
                    $"portcullis-{state}-{where}",
                    $"The portcullis of the showroom {state}, seen from {where} (issue #540).",
                    studio => new PhotoSetup(BeforePortcullis(studio, back)) { Ticks = ticks(studio.Balance.Grates) });
            }
        }
    }

    // Shut: 30 ticks into the shut hold. Half: 55 % into the lift, the grate about 1.7 m up (the lift stretches with
    // its length, so the share holds for any LiftTicks). Open: in the open hold.
    private static IEnumerable<(string State, Func<GratesBalance, int> Ticks)> PortcullisStates()
    {
        const int hold = ShowroomGrateCycle.HoldTicks;
        yield return ("closed", timing => hold + timing.FallTicks + 30);
        yield return ("half", timing => (2 * hold) + timing.FallTicks + (int)MathF.Round(timing.LiftTicks * PortcullisHalfLiftShare));
        yield return ("open", _ => 10);
    }

    // The share of the lift at which the half-lifted portcullis scenes are taken (issue #540).
    private const float PortcullisHalfLiftShare = 0.55f;

    // back: metres south of the door plane (into the room); negative: north, into the closet. A little to the side, so
    // the depth of the frame shows, looking at the middle of the doorway.
    private static GameWorld BeforePortcullis(PhotoStudio studio, float back)
    {
        var layout = ShowroomLayout.Build();
        var portcullis = layout.Exhibits.Portcullises[0];
        var world = layout.NewWorldAt(portcullis.X, portcullis.Z, portcullis.Position, studio.Balance);
        world.Player.PlaceAt(portcullis.Position + new Vector3(0.6f, 0f, back));
        PhotoStudio.Face(world.Player, portcullis.Position + new Vector3(0f, 1.6f, 0f));
        return world;
    }

    private static GameWorld BeforeWallPillar(PhotoStudio studio, int sectionIndex, ShowroomPart part)
    {
        var layout = ShowroomLayout.Build();
        var section = layout.Sections[sectionIndex];
        var ceiling = ShowroomSection.CeilingOf(part);
        var pillar = layout.Exhibits.WallPillars
            .Where(pillar => pillar.Variant == section.Variant && pillar.CeilingHeight == ceiling)
            .OrderBy(pillar => pillar.Position.X)
            .Skip(1)
            .First();
        var (x, z) = Level.ToGrid(pillar.Position.X, pillar.Position.Z + 0.01f);
        var world = layout.NewWorldAt(x, section.SouthRowOf(part), pillar.Position, studio.Balance);
        var south = ((section.SouthRowOf(part) + 1) * Level.CellSize) - 0.5f;
        world.Player.PlaceAt(new Vector3(pillar.Position.X + 1.5f, layout.Level.CellCenter(x, z).Y, south));
        PhotoStudio.Face(world.Player, pillar.Position + new Vector3(0f, Math.Min(ceiling * 0.45f, 2f), 0f));
        return world;
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

    // The showroom with a player `ahead` m in front of its ghoul and `aside` m to its side (by the way it looks - it
    // never moves there), looking at the middle of its shot body (issue #433).
    private static GameWorld BesideTheGhoul(PhotoStudio studio, float ahead, float aside)
    {
        var world = BeforeEquipment(studio, piece => piece is EnemyPiece { Enemy: ShowroomEnemy.Ghoul });
        var ghoul = world.Ghouls.First(ghoul => Vector3.Distance(ghoul.Position, world.Player.Position) < 1f);
        var side = new Vector3(ghoul.Facing.Z, 0f, -ghoul.Facing.X);
        world.Player.PlaceAt(ghoul.Position + (ghoul.Facing * ahead) + (side * aside));
        PhotoStudio.Face(world.Player, ghoul.Position + (ghoul.Facing * BodySizes.Ghoul.HeadForward / 2f) + new Vector3(0f, BodySizes.Ghoul.TorsoHeight / 2f, 0f));
        return world;
    }

    // The showroom with a player `distance` m in front of its crossbow skeleton in `stance` (it looks south, along +Z),
    // looking at its chest (issue #497).
    private static GameWorld BeforeCrossbowSkeleton(PhotoStudio studio, CrossbowSkeletonStance stance, float distance)
    {
        var world = BeforeEquipment(studio, piece => piece is CrossbowSkeletonPiece shown && shown.Stance == stance);
        var skeleton = world.Showroom!.CrossbowSkeletons.First(exhibit => exhibit.Stance == stance);
        world.Player.PlaceAt(skeleton.Position + (skeleton.Facing * distance));
        PhotoStudio.Face(world.Player, skeleton.Position + new Vector3(0f, CrossbowSkeletonLook.Height * 0.6f, 0f));
        return world;
    }

    // The showroom with a player `distance` m in front of its crypt warden in `stance` (it looks south, along +Z),
    // looking at its chest (issue #530).
    private static GameWorld BeforeCryptWarden(PhotoStudio studio, CryptWardenStance stance, float distance)
    {
        var world = BeforeEquipment(studio, piece => piece is CryptWardenPiece shown && shown.Stance == stance);
        var warden = world.Showroom!.CryptWardens.First(exhibit => exhibit.Stance == stance);
        world.Player.PlaceAt(warden.Position + (warden.Facing * distance));
        PhotoStudio.Face(world.Player, warden.Position + new Vector3(0f, CryptWardenLook.Height * 0.6f, 0f));
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

    // The club held until fully charged, then let go; stops at the height of the swing, as many ticks after it
    // started as the quick swing in club-swing-full.
    private static PhotoSetup ChargedClubSwing(PhotoStudio studio)
    {
        var world = ClubInHand(studio.Arena('G', 10));
        var released = false;
        return new PhotoSetup(world)
        {
            Ticks = 600,
            Input = _ =>
            {
                released |= world.Player.MeleeCharge is { IsCharging: true, Fraction: >= 1f };
                return PlayerInput.None with { Melee = !released };
            },
            StopWhen = TicksAfter<MeleeSwung>(7),
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

    private static GameWorld ClubInHand(GameWorld world)
    {
        world.Player.EquipMelee(WeaponKind.Club);
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
