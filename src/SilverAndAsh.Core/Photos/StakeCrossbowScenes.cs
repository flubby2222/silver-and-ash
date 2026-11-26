using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Showroom;

namespace SilverAndAsh.Core.Photos;

/// <summary>
/// The stake crossbow in hand (issue #793): loaded and relaxed, in the kick of a shot, through the reload, in the
/// showroom and on the HUD. The shots stand before a dummy 8 m away. A shot is pressed on tick 0 and its reload on
/// tick <see cref="ReloadAt"/>; each later tick is one frame of the reload (3 s in balance.json, about 180 frames).
/// </summary>
internal static class StakeCrossbowScenes
{
    private const int DummyMetres = 8;

    // The tick R is pressed after the shot: the kick has settled, the string hangs relaxed.
    private const int ReloadAt = 30;

    public static PhotoScene[] All() =>
    [
        new("crossbow-loaded", "The stake crossbow in hand, loaded: the bow bent back, the string drawn, the stake in the groove (issue #793).",
            studio => new PhotoSetup(CrossbowInHand(studio.Arena('D', DummyMetres)))),
        new("crossbow-shot", "The stake crossbow three ticks after a shot: the string already forward, no stake in the groove, the muzzle kicked up (issue #793).",
            studio => Shot(studio, 4)),
        new("crossbow-relaxed", "The stake crossbow half a second after a shot: relaxed, the string straight across the bow's tips, no stake (issue #793).",
            studio => Shot(studio, ReloadAt)),
        new("crossbow-reload-pull", "The stake crossbow in the reload, half way: the muzzle dipped, the crank turning (issue #793).",
            studio => Reloading(studio, 0.5f)),
        new("crossbow-reload-catch", "The stake crossbow in the reload just after the string catches: the bow bent again, the stake not yet laid in (issue #793).",
            studio => Reloading(studio, 0.83f)),
        new("crossbow-reload-end", "The stake crossbow in the reload, the stake laid in just before its end (issue #793).",
            studio => Reloading(studio, 0.97f)),
        new("showroom-crossbow", "The stake crossbow in hand before the Crossbow (Vampiric) lying in the showroom (issue #793).",
            studio => new PhotoSetup(CrossbowInHand(PhotoScenes.BeforeEquipment(studio, piece => piece is WeaponPiece { Weapon: WeaponKind.StakeCrossbow })))),
        new("hud-crossbow", "Like hud-weapons, but the stake crossbow in hand: its icon, a small picture of the model (issue #793).",
            studio => new PhotoSetup(CrossbowInHand(PhotoScenes.AllWeapons(studio.StartRun())))),
    ];

    // One shot at the dummy, stopped `ticks` ticks after it was pressed.
    private static PhotoSetup Shot(PhotoStudio studio, int ticks) => new(CrossbowInHand(studio.Arena('D', DummyMetres)))
    {
        Ticks = ticks,
        Input = tick => PlayerInput.None with { Shoot = tick == 0 },
    };

    // A shot, then R: the picture is taken when the reload has come `share` of the way.
    private static PhotoSetup Reloading(PhotoStudio studio, float share)
    {
        var world = CrossbowInHand(studio.Arena('D', DummyMetres));
        var reload = SimulationClock.SecondsToTicks(world.Balance.StakeCrossbow.ReloadTime);
        return new PhotoSetup(world)
        {
            Ticks = ReloadAt + 1 + (int)MathF.Round(reload * share),
            Input = tick => PlayerInput.None with { Shoot = tick == 0, Reload = tick == ReloadAt },
        };
    }

    private static GameWorld CrossbowInHand(GameWorld world)
    {
        world.Player.Equip(0, WeaponKind.StakeCrossbow);
        return world;
    }
}
