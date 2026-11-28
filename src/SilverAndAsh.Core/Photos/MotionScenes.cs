using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.Showroom;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Photos;

/// <summary>
/// Every motion scene in one place (issue #286). A new clip is one more entry here: a name, what it shows and how the
/// <see cref="PhotoStudio"/> builds it - the same building blocks as the photo scenes, with a fixed input for every
/// tick. See docs/foto-skript.md.
/// </summary>
public static class MotionScenes
{
    // Ticks of stillness before something happens and after it is over: the clip starts and ends at rest.
    private const int Rest = 12;

    /// <summary>All clips, in the order <c>--clip all</c> takes them.</summary>
    public static IReadOnlyList<MotionScene> All { get; } =
    [
        new("blade-charge", "The silver blade held until fully charged, trembling a while, let go: the full swing and back to rest.",
            studio =>
            {
                // Held from tick 12 until a third of a second past the full charge, then let go.
                var held = SimulationClock.SecondsToTicks(studio.Balance.Blade.ChargeTime) + 20;
                return new PhotoSetup(studio.Arena('D', 10))
                {
                    Ticks = Rest + held + 50,
                    Input = tick => PlayerInput.None with { Melee = tick >= Rest && tick < Rest + held },
                };
            }),
        new("blade-quick", "A quick blade swing: pressed for one tick, the swing out and back to rest.",
            studio => new PhotoSetup(studio.Arena('D', 10))
            {
                Ticks = Rest + 50,
                Input = tick => PlayerInput.None with { Melee = tick == Rest },
            }),
        new("pistol-shot", "One pistol shot: the kick, hammer and muzzle flash, and the pistol settling again.",
            studio => new PhotoSetup(studio.Arena('D', 10))
            {
                Ticks = Rest + 50,
                Input = tick => PlayerInput.None with { Shoot = tick == Rest },
            }),
        new("pistol-reload", "The pistol reloaded (R) with one round gone, from the key to the full magazine.",
            studio =>
            {
                var reload = SimulationClock.SecondsToTicks(studio.Balance.Pistol.ReloadTime);
                return new PhotoSetup(OneRoundFired(studio.Arena('D', 10)))
                {
                    Ticks = Rest + reload + 30,
                    Input = tick => PlayerInput.None with { Reload = tick == Rest },
                };
            }),
        new("pistol-shot-in-swing", "A pistol shot while the blade swings: both hands move at once.",
            studio => new PhotoSetup(studio.Arena('D', 10))
            {
                Ticks = Rest + 50,
                Input = tick => PlayerInput.None with { Melee = tick == Rest, Shoot = tick == Rest + 4 },
            }),
        new("pistol-reload-in-swing", "The pistol reloaded (R) and the blade swung in the middle of it.",
            studio =>
            {
                var reload = SimulationClock.SecondsToTicks(studio.Balance.Pistol.ReloadTime);
                return new PhotoSetup(OneRoundFired(studio.Arena('D', 10)))
                {
                    Ticks = Rest + reload + 30,
                    Input = tick => PlayerInput.None with { Reload = tick == Rest, Melee = tick == Rest + 30 },
                };
            }),
        new("pistol-reload-cancelled", "The pistol reloaded (R), the rifle chosen (2) half way through (the reload stops), the pistol chosen again (1).",
            studio =>
            {
                var world = OneRoundFired(studio.Arena('D', 10));
                world.Player.Equip(1, WeaponKind.Rifle);
                return new PhotoSetup(world)
                {
                    Ticks = Rest + 100,
                    Input = tick => PlayerInput.None with
                    {
                        Reload = tick == Rest,
                        SelectSlot = tick == Rest + 40 ? 1 : tick == Rest + 70 ? 0 : null,
                    },
                };
            }),
        new("rifle-reload-to-pistol", "The rifle chosen (2) and fired, reloaded (R), the pistol chosen (1) half way through: the pistol must not show the reload.",
            studio =>
            {
                var world = studio.Arena('D', 10);
                world.Player.Equip(1, WeaponKind.Rifle);
                world.Tick(PlayerInput.None with { SelectSlot = 1 });
                OneRoundFired(world);
                return new PhotoSetup(world)
                {
                    Ticks = Rest + 90,
                    Input = tick => PlayerInput.None with
                    {
                        Reload = tick == Rest,
                        SelectSlot = tick == Rest + 40 ? 0 : null,
                    },
                };
            }),
        Slam("gorm-slam", 8, TextureVariant.A, "close"),
        Slam("gorm-slam-far", 14, TextureVariant.A, "from afar, just outside its reach"),
        Slam("gorm-slam-far-b", 14, TextureVariant.B, "from afar on floor B"),
        Slam("gorm-slam-far-c", 14, TextureVariant.C, "from afar on floor C, the cobbles (in the game never in a boss room)"),
        DoorOpening("door-normal-opening", DoorKind.Normal, "A wooden double door 6 m away, seen from the front"),
        DoorOpening("door-boss-opening", DoorKind.Boss, "The boss door 6 m away, seen from the front"),
        new("stairs-rising", "The spiral staircase rising in the boss room after Gorm's death, seen from 6 m: the whole rise until it stands and its lamp is lit (issue #300).",
            studio => new PhotoSetup(studio.AfterGorm(6f))
            {
                Ticks = SimulationClock.SecondsToTicks(studio.Balance.Gorm.StairsRiseTime) + 40,
            }),
        new("column-breaking", "The breaking column of the showroom from 6 m: it stands at Z2, breaks, falls apart in its dust and lies as Z3 (issue #601).",
            BeforeBreakingColumn, FrameEvery: 3),
        ThrowingKnivesScenes.Ricochet(),
    ];

    /// <summary>The clip called <paramref name="name"/>, or null.</summary>
    public static MotionScene? Find(string name) => All.FirstOrDefault(scene => scene.Name == name);

    // Gorm's hammer blow and its shock wave (issue #299) in a hall as high as his boss room: Gorm left to fight until
    // he has nearly wound up a hammer blow, then the player put `metres` away from him (behind him if the hall has room
    // there, otherwise in front), looking at him. The clip shows the blow and the wave running out over the floor.
    private static MotionScene Slam(string name, int metres, TextureVariant floor, string what) =>
        new(name, $"Gorm's hammer blow and the shock wave over the floor, {what} ({metres} m away).",
            studio =>
            {
                var world = studio.Arena('T', metres, RoomTemplate.RoomCeilingHeight, floor);
                for (var tick = 0; tick < 3000 && !(world.Gorm is { State: GormState.WindingUp, Attack: GormAttack.Hammer, StateProgress: >= 0.85f }); tick++)
                {
                    world.Tick(PlayerInput.None);
                }

                var gorm = world.Gorm!;
                var behind = gorm.Position.Z - metres >= 2f;
                world.Player.PlaceAt(new Vector3(gorm.Position.X, 0f, behind ? gorm.Position.Z - metres : gorm.Position.Z + metres));
                PhotoStudio.Face(world.Player, gorm.Position + new Vector3(0f, 1.5f, 0f));
                return new PhotoSetup(world) { Ticks = 120 };
            });

    // The showroom's breaking column (issue #601) from 6 m, from inside the hall north of the line it falls along (the
    // passage in the south has a lower ceiling that would cut off its top), past the hall's middle column; in full
    // height, without hands or titles. From a few ticks before the break of its loop (ShowroomColumnCycle; a scene's
    // world starts at tick 0) until its dust has settled.
    private static PhotoSetup BeforeBreakingColumn(PhotoStudio studio)
    {
        var layout = ShowroomLayout.Build();
        var column = layout.Exhibits.Columns.First(column => column.Stage == ColumnStage.Falling);
        var target = column.Position + new Vector3(2f, 2.6f, 0f);
        var (x, z) = Level.ToGrid(column.Position.X, column.Position.Z);
        var world = layout.NewWorldAt(x, z, target, studio.Balance);
        world.Player.PlaceAt(column.Position + new Vector3(2.5f, 0f, 5.5f));
        PhotoStudio.Face(world.Player, target);
        return new PhotoSetup(world) { Ticks = ShowroomColumnCycle.StandTicks - 6 + ColumnDust.Ticks, ShowsOnlyTheWorld = true };
    }

    // The door of `kind` opened by the player (F, as in the game, before the first tick - see PhotoStudio.DoorHall):
    // its whole swing and a little rest after it.
    private static MotionScene DoorOpening(string name, DoorKind kind, string what) =>
        new(name, $"{what}, opened (F): its whole swing until it stands open.",
            studio => new PhotoSetup(studio.DoorHall(kind, open: true))
            {
                Ticks = SimulationClock.SecondsToTicks(studio.Balance.Doors.OpenTime(kind)) + 30,
            });

    // One shot fired and the half second after it gone by before the clip begins: the magazine has room for a round,
    // so R reloads, and the pistol stands still again. The game never sees this shot - it happened before the scene.
    private static GameWorld OneRoundFired(GameWorld world)
    {
        world.Tick(PlayerInput.None with { Shoot = true });
        for (var tick = 0; tick < 30; tick++)
        {
            world.Tick(PlayerInput.None);
        }

        if (world.Player.Ranged.Magazine.Rounds >= world.Player.Ranged.Magazine.Size)
        {
            throw new InvalidOperationException("The pistol did not fire before the reload clip.");
        }

        return world;
    }
}
