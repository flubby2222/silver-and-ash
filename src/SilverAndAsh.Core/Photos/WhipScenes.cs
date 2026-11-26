using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Showroom;

namespace SilverAndAsh.Core.Photos;

/// <summary>
/// The whip in hand (issue #786): at rest, through one quick swing, while charging, in the showroom, on the HUD and in
/// the light of the test course. The swings stand before a dummy 6 m away, about as far as the whip reaches, so its
/// lash can be seen to reach it. Each swing is pressed on tick 0 and let go on tick 1; each later tick is one frame
/// of it (the whip swings in 0.9 of its 0.7 s cooldown, about 38 frames).
/// </summary>
internal static class WhipScenes
{
    // Before the dummy, about as far as the whip reaches (balance.json: 6 m).
    private const int DummyMetres = 6;

    public static PhotoScene[] All() =>
    [
        new("whip-rest", "The whip in hand at rest: the handle in the sleeve, the short loop of the lash hanging from the hand (issue #786).",
            studio => new PhotoSetup(WhipInHand(studio.Arena('D', DummyMetres)))),
        new("whip-swing-out", "A quick whip swing at 0.16 of its way: the lash rolling out from the hand (issue #786).",
            studio => Swing(studio, 7)),
        new("whip-swing-hit", "A quick whip swing at 0.4 of its way, where it hits: the lash straight out to the dummy 6 m away (issue #786).",
            studio => Swing(studio, 16)),
        new("whip-swing-back", "A quick whip swing at 0.56 of its way: the lash sagging towards the floor on the way back (issue #786).",
            studio => Swing(studio, 22)),
        new("whip-charging", "The whip held charging for two thirds of a second: its loop whirling around the handle (issue #786).",
            studio => new PhotoSetup(WhipInHand(studio.Arena('D', DummyMetres)))
            {
                Ticks = 40,
                Input = _ => PlayerInput.None with { Melee = true },
            }),
        new("whip-charged-release", "A fully charged whip swing let go, at 0.4 of its way (issue #786).",
            studio => PhotoScenes.ChargedSwing(WhipInHand(studio.Arena('D', DummyMetres)), 15)),
        new("showroom-whip", "The whip in hand before the Whip (Royal) lying in the showroom (issue #786).",
            studio => new PhotoSetup(WhipInHand(PhotoScenes.BeforeEquipment(studio, piece => piece is WeaponPiece { Weapon: WeaponKind.Whip })))),
        new("hud-whip", "Like hud-weapons, but the whip in hand: its icon, a small picture of the model with its loop (issue #786).",
            studio => new PhotoSetup(WhipInHand(PhotoScenes.AllWeapons(studio.StartRun())))),
        new("course-1-whip", "Like course-1-arms-row, the whip in hand: its model in the light of the test course (issue #786).",
            studio => CourseScenes.ArmsRowWith(studio, WhipInHand)),
    ];

    // A quick swing, stopped `ticks` ticks after it was pressed.
    private static PhotoSetup Swing(PhotoStudio studio, int ticks) => new(WhipInHand(studio.Arena('D', DummyMetres)))
    {
        Ticks = ticks,
        Input = tick => PlayerInput.None with { Melee = tick == 0 },
    };

    private static GameWorld WhipInHand(GameWorld world)
    {
        world.Player.EquipMelee(WeaponKind.Whip);
        return world;
    }
}
