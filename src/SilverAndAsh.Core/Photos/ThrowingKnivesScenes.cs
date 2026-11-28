using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Photos;

/// <summary>
/// The throwing knives (issue #821): the fan in the hand with three, two and one knife, the throw, a knife in flight,
/// a knife in a dummy and in Gorm and knives in the stone from 3 m, the reload and the HUD. A knife is thrown on tick 0;
/// the next ones follow as soon as the weapon lets them (<see cref="ThrowEvery"/>). The pictures "at rest" wait until the
/// throw's movement is over (<see cref="ThrowingKnivesPose.ThrowTicks"/>).
/// </summary>
internal static class ThrowingKnivesScenes
{
    private const int DummyMetres = 8;

    // The hall's wall behind the player is this far from them (the player's cell is the third of the hall).
    private const float WallMetres = 3f;

    // Ticks after the last throw at which a picture "at rest" is taken: the throw's 40 ticks and a few more.
    private const int Settled = ThrowingKnivesPose.ThrowTicks + 10;

    public static PhotoScene[] All() =>
    [
        new("knives-rest-3", "The throwing knives in hand, all three: a fan out of the cuff, the left one foremost (issue #821).",
            studio => new PhotoSetup(KnivesInHand(studio.Arena('D', DummyMetres)))),
        new("knives-rest-2", "The throwing knives after the first throw, at rest: the left knife is gone, two stand in the cuff (issue #821).",
            studio => Thrown(KnivesInHand(studio.Arena('D', DummyMetres)), 1, Settled)),
        new("knives-rest-1", "The throwing knives after the second throw, at rest: only the middle knife stands in the cuff (issue #821).",
            studio => Thrown(KnivesInHand(studio.Arena('D', DummyMetres)), 2, Settled)),
        new("knives-throw", "The throwing knives six ticks into a throw: the hand pulled across, the thrown knife already out of it (issue #821).",
            studio => Thrown(KnivesInHand(studio.Arena('D', DummyMetres)), 1, 6)),
        new("knives-flight", "A thrown knife in flight, the tick after it left the hand, tumbling towards a dummy 8 m away (issue #821).",
            studio => Thrown(KnivesInHand(studio.Arena('D', DummyMetres)), 1, 1)),
        new("knives-dummy-3m", "A throwing knife in a dummy, seen from 3 m, the handle towards the thrower; a second one would fell the dummy (issue #821).",
            studio => Thrown(Before(studio, 'D'), 1, Settled)),
        new("knives-gorm-3m", "A throwing knife in Gorm 3 m away, a few ticks after it hit: the knife goes with the body. A ghoul falls to one knife and takes it with it (issue #821).",
            studio => Thrown(Before(studio, 'T', RoomTemplate.RoomCeilingHeight), 1, 10)),
        new("knives-stone-3m", "Three throwing knives in the wall, seen from 3 m: the hand empty, the handles hang a little (issue #821).",
            studio => Thrown(BeforeWall(studio), ThrowingKnivesPose.Count, Settled)),
        new("knives-reload-down", "The throwing knives in the reload, a fifth of the way: the empty hand goes down out of the picture (issue #821).",
            studio => Reloading(studio, 0.2f)),
        new("knives-reload-up", "The throwing knives in the reload, four fifths of the way: the hand comes back with three knives, the fan opening (issue #821).",
            studio => Reloading(studio, 0.8f)),
        new("hud-knives", "Like hud-weapons, but the throwing knives in hand: their icon, a small picture of the knife (issue #821).",
            studio => new PhotoSetup(KnivesInHand(PhotoScenes.AllWeapons(studio.StartRun())))),
    ];

    // Ticks from one throw to the next: the weapon's cooldown and two ticks of room.
    private static int ThrowEvery(GameWorld world) => SimulationClock.SecondsToTicks(world.Balance.ThrowingKnives.Cooldown) + 2;

    // `throws` knives thrown one after the other, the view a little further to the right before each new one; the
    // picture is taken `after` ticks after the last one was pressed.
    private static PhotoSetup Thrown(GameWorld world, int throws, int after)
    {
        var every = ThrowEvery(world);
        var last = (throws - 1) * every;
        return new PhotoSetup(world)
        {
            Ticks = last + after + 1,
            Input = tick =>
            {
                // The view turns as in the game: once a frame, outside the tick (Player.Look).
                if (tick > 0 && tick <= last && tick % every == every / 2)
                {
                    world.Player.Look(PlayerInput.None with { Look = Aside });
                }

                return PlayerInput.None with { Shoot = tick <= last && tick % every == 0 };
            },
        };
    }

    // The mouse movement between two throws: about 3 degrees to the right, so the knives do not stick in one point.
    private static readonly Vector2 Aside = new(0.05f / ControlSettings.MouseSensitivity, 0f);

    // All three thrown, then R: the picture is taken when the reload has come `share` of the way.
    private static PhotoSetup Reloading(PhotoStudio studio, float share)
    {
        var world = KnivesInHand(studio.Arena('D', DummyMetres));
        var every = ThrowEvery(world);
        var reloadAt = ((ThrowingKnivesPose.Count - 1) * every) + Settled;
        var reload = SimulationClock.SecondsToTicks(world.Balance.ThrowingKnives.ReloadTime);
        return new PhotoSetup(world)
        {
            Ticks = reloadAt + 1 + (int)MathF.Round(reload * share),
            Input = tick => PlayerInput.None with { Shoot = tick < reloadAt && tick % every == 0, Reload = tick == reloadAt },
        };
    }

    // The bare hall with `figure` 3 m in front of the player: it stands 4 m away, the player steps 1 m towards it.
    private static GameWorld Before(PhotoStudio studio, char figure, float ceiling = Cell.DefaultCeilingHeight)
    {
        var world = KnivesInHand(studio.Arena(figure, 4, ceiling));
        var target = world.Targets.Single();
        var player = world.Player;
        var towards = Vector3.Normalize(target.Body.Position - player.Position) with { Y = 0f };
        player.PlaceAt(player.Position + (towards * (4f - WallMetres)));
        PhotoStudio.Face(player, target.Body.Position + new Vector3(0f, target.Body.Height * 0.6f, 0f));
        return world;
    }

    // The bare hall, the player turned round to the wall 3 m behind them, looking at it a little below eye height.
    private static GameWorld BeforeWall(PhotoStudio studio)
    {
        var world = KnivesInHand(studio.Arena('D', DummyMetres));
        var player = world.Player;
        var away = Vector3.Normalize(player.Position - world.Targets.Single().Body.Position) with { Y = 0f };
        PhotoStudio.Face(player, player.Position + (away * WallMetres) + new Vector3(-0.1f, 1.35f, 0f));
        return world;
    }

    private static GameWorld KnivesInHand(GameWorld world)
    {
        world.Player.Equip(0, WeaponKind.ThrowingKnives);
        return world;
    }
}
