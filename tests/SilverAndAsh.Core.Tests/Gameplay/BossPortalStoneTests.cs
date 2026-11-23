using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// The stone of the boss portal in a running world (issue #598): the jambs leave a passage only as wide as the opening
/// (2 m at the door plane), the plinths stand out of the wall beside it, and a shot at the stone ends there.
/// </summary>
public class BossPortalStoneTests
{
    // Room 0 (start) in row 0, the boss door in cells (2, 1) and (3, 1) - its portal's middle at x 6 m, its door plane
    // at z 3 m - and room 1 (boss room) in rows 2-9. The player starts at the middle of cell (2, 0): x 5 m, a metre
    // beside the middle of the opening, right in front of the left plinth.
    private const string Map =
        "#.P..#\n##..##\n#....#\n#....#\n#....#\n#....#\n#....#\n#....#\n#....#\n#...G#\n######";

    private const float MiddleX = 6f;
    private const float DoorPlaneZ = 3f;
    private const float Radius = BodySizes.Player.Radius;

    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };
    private static readonly PlayerInput Forward = new(new Vector2(0f, 1f), Vector2.Zero, false);

    // Move.X goes to the view's right: looking along +Z that is world -X, towards the left jamb.
    private static readonly PlayerInput StrafeToTheLeftJamb = new(new Vector2(1f, 0f), Vector2.Zero, false);
    private static readonly PlayerInput Shoot = PlayerInput.None with { Shoot = true };

    [Fact]
    public void The_world_frames_its_boss_door_with_a_portal_at_the_middle_of_both_cells()
    {
        var world = World();

        var portal = Assert.Single(world.Level.Portals);
        Assert.Equal(new BossPortal(MiddleX, DoorPlaneZ, 0f, PassesAlongZ: true), portal);
    }

    [Fact]
    public void A_player_who_walks_straight_at_the_plinth_stops_in_front_of_it()
    {
        // The plinth stands 0.125 m out of the wall (z 1.875 m) and 0.5 m high - higher than a step (0.4 m).
        var world = World();
        Run(world, PressF, 1);

        Run(world, Forward, 120);

        var at = world.Player.Position;
        var plinth = BossPortalShape.Blocks[BossPortalShape.Runs];
        Assert.Equal(DoorPlaneZ - plinth.AlongTo - Radius, at.Z, 0.01f);
        Assert.Equal(5f, at.X, 0.01f);
        Assert.False(CharacterCollision.Overlaps(world.Level, at, Radius));
        Assert.False(world.Rooms[1].IsSealed);
    }

    [Fact]
    public void A_player_who_strafes_at_a_jamb_in_the_doorway_stops_at_the_opening()
    {
        var world = World();
        StandInTheDoorway(world);
        Assert.InRange(world.Player.Position.Z, DoorPlaneZ - BossPortalShape.RunStart(2), DoorPlaneZ + BossPortalShape.RunStart(2));

        Run(world, StrafeToTheLeftJamb, 120);

        // At the door plane the innermost run leaves 1 m to either side of the middle.
        var at = world.Player.Position;
        Assert.Equal(MiddleX - (BossPortalShape.OpeningHalfWidth - Radius), at.X, 0.01f);
        Assert.False(CharacterCollision.Overlaps(world.Level, at, Radius));
        Run(world, StrafeToTheLeftJamb, 30);
        Assert.Equal(at.X, world.Player.Position.X, 0.001f);
    }

    [Fact]
    public void A_player_who_walks_through_the_opening_gets_into_the_boss_room_without_touching_the_stone()
    {
        var world = World();
        Run(world, PressF, 1);

        for (var tick = 0; tick < 300 && world.Player.Position.Z < 6f; tick++)
        {
            world.Tick(IntoTheOpening(world));
            Assert.False(CharacterCollision.Overlaps(world.Level, world.Player.Position, Radius), $"tick {tick}: {world.Player.Position}");
        }

        Assert.True(world.Player.Position.Z >= 6f);
        Assert.True(world.Rooms[1].IsSealed);
    }

    [Fact]
    public void A_player_in_the_outer_reveal_at_the_hard_stop_is_pushed_into_the_opening_and_not_into_the_plinth()
    {
        // In co-op a second player can still stand in the doorway when the leaves stop. The outer reveal - 0.65 m into
        // the boss room, 0.8 m beside the middle - is free of stone; clamped to the door box (2 m to either side)
        // instead of the opening, the push would set the player into the plinth.
        var world = World();
        var door = world.Doors[0];
        Run(world, PressF, 1);
        for (var tick = 0; tick < 300 && !world.Rooms[1].IsSealed; tick++)
        {
            world.Tick(IntoTheOpening(world));
        }

        Assert.True(world.Rooms[1].IsSealed);
        Assert.False(door.Blocks);
        var inTheReveal = new Vector3(MiddleX - 0.8f, 0f, DoorPlaneZ + 0.65f);
        Assert.False(CharacterCollision.Overlaps(world.Level, inTheReveal, Radius));
        world.Player.PlaceAt(inTheReveal);

        for (var tick = 0; tick < 200 && !door.Blocks; tick++)
        {
            world.Tick(PlayerInput.None);
        }

        var at = world.Player.Position;
        Assert.True(door.Blocks);
        Assert.False(CharacterCollision.Overlaps(world.Level, at, Radius), $"the player is at {at}");
        var inside = BossPortalShape.OpeningHalfWidth - Radius;
        Assert.InRange(at.X, MiddleX - inside - 1e-4f, MiddleX + inside + 1e-4f);
    }

    [Fact]
    public void A_shot_at_a_jamb_ends_on_its_face()
    {
        var world = World();
        StandInTheDoorway(world);
        world.Player.View.ApplyMouse(new Vector2(MathF.PI / 2f, 0f), 1f);   // from +Z to -X: the left jamb
        Assert.Equal(-1f, world.Player.View.Forward.X, 0.0001f);

        world.Tick(Shoot);

        var impact = Assert.Single(world.Events.OfType<BulletImpact>());
        Assert.Equal(MiddleX - BossPortalShape.OpeningHalfWidth, impact.Point.X, 0.01f);
        Assert.Equal(Vector3.UnitX, impact.Normal);
    }

    [Fact]
    public void A_shot_through_the_opening_flies_on_to_the_far_wall()
    {
        var world = World();
        StandInTheDoorway(world);

        world.Tick(Shoot);

        var impact = Assert.Single(world.Events.OfType<BulletImpact>());
        Assert.Equal(20f, impact.Point.Z, 0.01f);
        Assert.Equal(-Vector3.UnitZ, impact.Normal);
    }

    private static GameWorld World()
    {
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 6, 1, 0, Rows(0, 0), [], []),
            new(1, RoomKind.Boss, "boss", 0, 2, 6, 8, 1, Rows(2, 9), [(4, 9)], []),
        ];
        LevelDoor[] doors = [new(2, 1, 0, 1, RoomSide.South, DoorKind.Boss) { MoreCells = [(3, 1)] }];
        return new GameWorld(LevelParser.Parse(Map), 1, Balance.Default, doors, rooms);

        static List<(int X, int Z)> Rows(int from, int to) =>
            Enumerable.Range(from, to - from + 1).SelectMany(z => new[] { (1, z), (2, z), (3, z), (4, z) }).ToList();
    }

    // Opens the door, walks into the opening up to the door plane and lets the player come to rest there.
    private static void StandInTheDoorway(GameWorld world)
    {
        Run(world, PressF, 1);
        for (var tick = 0; tick < 300 && world.Player.Position.Z < DoorPlaneZ - 0.2f; tick++)
        {
            world.Tick(IntoTheOpening(world));
        }

        Run(world, PlayerInput.None, 60);
        Assert.False(world.Rooms[1].IsSealed);
    }

    // Forward (+Z) and towards the middle of the opening, in the player's own view.
    private static PlayerInput IntoTheOpening(GameWorld world)
    {
        var aside = Math.Clamp((MiddleX - world.Player.Position.X) * 2f, -1f, 1f);
        var direction = new Vector3(aside, 0f, 1f);
        var view = world.Player.View;
        return new PlayerInput(new Vector2(Vector3.Dot(direction, view.FlatRight), Vector3.Dot(direction, view.FlatForward)), Vector2.Zero, false);
    }

    private static void Run(GameWorld world, PlayerInput input, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            world.Tick(input);
        }
    }
}
