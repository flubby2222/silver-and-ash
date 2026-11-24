using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.World;
using static SilverAndAsh.Core.Tests.TestKit;

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

    // The same with a bat in the start room (cell (4, 0)), awake: the world flies it (issue #627).
    private const string BatMap =
        "#.P.B#\n##..##\n#....#\n#....#\n#....#\n#....#\n#....#\n#....#\n#....#\n#...G#\n######";

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
        RunTicks(world, PressF, 1);

        RunTicks(world, Forward, 120);

        var at = world.Player.Position;
        var plinth = BossPortalShape.Blocks[BossPortalShape.Runs];
        Assert.Equal(DoorPlaneZ - plinth.AlongTo - Radius, at.Z, 0.01f);
        Assert.Equal(5f, at.X, 0.01f);
        Assert.False(CharacterCollision.Overlaps(world.Level, at, Radius));
        Assert.False(world.Rooms[1].IsSealed);
    }

    [Fact]
    public void A_player_who_jumps_onto_the_plinth_lands_on_it_and_stands_there()
    {
        // Too high to step onto (0.5 m against a step of 0.4 m) - but a jump carries him up, and its top holds him. He
        // jumps in front of the opening, 0.3 m beside the middle, drifting slowly (a tenth of full input) towards the left
        // plinth, so he comes over it only on the way down: straight above it his head would reach the impost (3.075 m)
        // at the top of the jump.
        var world = World();
        RunTicks(world, PressF, 1);
        var plinth = BossPortalShape.Blocks[BossPortalShape.Runs];
        var start = new Vector3(MiddleX - 0.3f, 0f, DoorPlaneZ - plinth.AlongTo + 0.075f - Radius);
        world.Player.PlaceAt(start);
        Assert.False(InStone(world.Level, start, Radius, BodySizes.Player.Height));

        var drift = new PlayerInput(new Vector2(0.1f, 0f), Vector2.Zero, false);
        RunTicks(world, drift with { Jump = true }, 1);
        for (var tick = 0; tick < 2 * SimulationClock.TicksPerSecond && !world.Player.IsGrounded; tick++)
        {
            world.Tick(drift);

            var at = world.Player.Position;
            Assert.False(InStone(world.Level, at + new Vector3(0f, 0.01f, 0f), Radius, BodySizes.Player.Height - 0.01f), $"tick {tick}: {at}");
        }

        Assert.True(world.Player.IsGrounded, $"never landed: {world.Player.Position}");
        RunTicks(world, PlayerInput.None, 2 * SimulationClock.TicksPerSecond);

        var standing = world.Player.Position;
        Assert.Equal(plinth.Top, standing.Y, 0.001f);
        Assert.True(world.Player.IsGrounded);
        Assert.InRange(MiddleX - standing.X, plinth.AcrossFrom - Radius + 0.01f, plinth.AcrossTo + Radius);
        Assert.False(InStone(world.Level, standing + new Vector3(0f, 0.01f, 0f), Radius, BodySizes.Player.Height - 0.01f));
        RunTicks(world, PlayerInput.None, SimulationClock.TicksPerSecond);
        Assert.Equal(standing, world.Player.Position);
    }

    [Fact]
    public void A_player_who_jumps_on_the_plinth_below_the_impost_bumps_his_head_and_stays_on_his_line()
    {
        // Issue #635: he stands on the left plinth at the wall, 1.3 m beside the middle - right below the impost (1-1.72 m
        // beside the middle, 0.125 m out of the wall at 3.075-3.2 m), clear of the buttress beside it. His jump would
        // carry his head (2.3 m on the plinth) up to 3.6 m. The impost is a ceiling: his head stops at its bottom, he
        // loses his upward speed there and comes down on the plinth again - he is not pushed away from the wall.
        var world = World();
        var plinth = BossPortalShape.Blocks[BossPortalShape.Runs];
        var impost = BossPortalShape.Blocks.Single(block => block.Top == BossPortalShape.Spring);
        var onThePlinth = new Vector3(MiddleX - 1.3f, plinth.Top, DoorPlaneZ - BossPortalShape.HalfDepth - Radius - 0.01f);
        world.Player.PlaceAt(onThePlinth);
        RunTicks(world, PlayerInput.None, 10);
        Assert.True(world.Player.IsGrounded);
        Assert.Equal(onThePlinth, world.Player.Position);

        world.Tick(PlayerInput.None with { Jump = true });
        var highest = 0f;
        var bumped = 0;
        for (var tick = 0; tick < 2 * SimulationClock.TicksPerSecond && !world.Player.IsGrounded; tick++)
        {
            var before = world.Player.Velocity.Y;
            world.Tick(PlayerInput.None);

            var at = world.Player.Position;
            Assert.Equal(onThePlinth.X, at.X, 0.0001f);
            Assert.Equal(onThePlinth.Z, at.Z, 0.0001f);
            Assert.True(at.Y + BodySizes.Player.Height <= impost.Bottom + 0.0001f, $"tick {tick}: his head at {at.Y + BodySizes.Player.Height}");
            Assert.False(InStone(world.Level, at + new Vector3(0f, 0.01f, 0f), Radius, BodySizes.Player.Height - 0.02f), $"tick {tick}: {at}");
            if (before > 0f && world.Player.Velocity.Y <= 0f && at.Y + BodySizes.Player.Height > impost.Bottom - 0.001f)
            {
                bumped++;
            }

            highest = MathF.Max(highest, at.Y);
        }

        // His head really reached the impost and stopped rising there - the test would prove nothing otherwise.
        Assert.Equal(1, bumped);
        Assert.Equal(impost.Bottom - BodySizes.Player.Height, highest, 0.0001f);
        Assert.True(world.Player.IsGrounded, $"never landed: {world.Player.Position}");
        Assert.Equal(onThePlinth, world.Player.Position);
    }

    [Fact]
    public void A_player_who_jumps_beside_the_impost_rises_to_the_full_height_of_his_jump()
    {
        // Fehlerzweig of issue #635: the same jump in front of the plinth, a metre out from the wall - nothing over his
        // head. The impost is a ceiling only above it: here he rises higher than its bottom.
        var world = World();
        var impost = BossPortalShape.Blocks.Single(block => block.Top == BossPortalShape.Spring);
        var inFront = new Vector3(MiddleX - 1.3f, 0f, DoorPlaneZ - BossPortalShape.HalfDepth - Radius - 1f);
        world.Player.PlaceAt(inFront);
        RunTicks(world, PlayerInput.None, 10);

        world.Tick(PlayerInput.None with { Jump = true });
        var highest = 0f;
        for (var tick = 0; tick < 2 * SimulationClock.TicksPerSecond && !world.Player.IsGrounded; tick++)
        {
            world.Tick(PlayerInput.None);
            highest = MathF.Max(highest, world.Player.Position.Y);
        }

        // From the floor his head rises above the impost's bottom (3.075 m) - beside it, not below it.
        Assert.True(highest + BodySizes.Player.Height > impost.Bottom, $"his head rose only to {highest + BodySizes.Player.Height}");
        Assert.True(world.Player.IsGrounded);
        Assert.Equal(inFront.X, world.Player.Position.X, 0.0001f);
        Assert.Equal(inFront.Z, world.Player.Position.Z, 0.0001f);
    }

    [Theory]
    [InlineData(0f, false)]
    [InlineData(0.00005f, false)]
    [InlineData(0f, true)]
    [InlineData(0.00005f, true)]
    public void A_head_at_the_impost_s_bottom_has_it_as_a_ceiling_and_is_not_pushed_aside(float intoTheStone, bool ghoul)
    {
        // Issue #635, the border between ceiling and stone in the way: a body on the left plinth below the impost, its
        // head exactly at the impost's bottom or 0.05 mm into it (less than the 0.1 mm a head may touch it). The impost
        // is its ceiling - rising stops there - and not in its way: standing still it is not pushed off its place. The
        // rule holds for every body, not only a player's: also for a ghoul (0.4 m wide, 1 m high).
        var level = World().Level;
        var plinth = BossPortalShape.Blocks[BossPortalShape.Runs];
        var impost = BossPortalShape.Blocks.Single(block => block.Top == BossPortalShape.Spring);
        var (radius, height) = ghoul
            ? (BodySizes.Ghoul.Radius, BodySizes.Ghoul.Height)
            : (BodySizes.Player.Radius, BodySizes.Player.Height);
        var at = new Vector3(MiddleX - 1.3f, impost.Bottom + intoTheStone - height, DoorPlaneZ - BossPortalShape.HalfDepth - radius - 0.01f);
        Assert.True(at.Y > plinth.Top);
        var player = Balance.Default.Player;

        var (moved, _) = CharacterCollision.Move(level, at, Vector3.Zero, radius, SimulationClock.SecondsPerTick, height);
        var (risen, velocity, _) = CharacterCollision.MoveVertically(
            level, at, new Vector3(0f, player.JumpSpeed, 0f), radius, height, player.Gravity, SimulationClock.SecondsPerTick);

        Assert.Equal(at.X, moved.X);
        Assert.Equal(at.Z, moved.Z);
        Assert.True(velocity.Y <= 0f, $"still rising at {velocity.Y} m/s");
        Assert.True(risen.Y + height <= impost.Bottom + intoTheStone, $"its head rose to {risen.Y + height}");
    }

    [Fact]
    public void A_player_who_jumps_on_the_plinth_of_a_raised_portal_stays_on_his_line_though_his_head_rounds_into_the_impost()
    {
        // Issue #635, the same jump as on floor 0, but every cell 2.74 m up. There the head stopped at the impost's bottom
        // does not come out at that bottom again in float32: (5.815 - 1.8) + 1.8 is a hair (0.5 micrometres) above 5.815.
        // He is not pushed off his line for that hair - his head only touches the impost.
        const float Floor = 2.74f;
        var world = World(floor: Floor);
        var portal = Assert.Single(world.Level.Portals);
        Assert.Equal(Floor, portal.Floor);
        var plinth = BossPortalShape.Blocks[BossPortalShape.Runs];
        var impost = BossPortalShape.Blocks.Single(block => block.Top == BossPortalShape.Spring);
        var bottom = Floor + impost.Bottom;
        var onThePlinth = new Vector3(MiddleX - 1.3f, Floor + plinth.Top, DoorPlaneZ - BossPortalShape.HalfDepth - Radius - 0.01f);
        world.Player.PlaceAt(onThePlinth);
        RunTicks(world, PlayerInput.None, 10);
        Assert.Equal(onThePlinth, world.Player.Position);

        world.Tick(PlayerInput.None with { Jump = true });
        var roundedIn = 0;
        for (var tick = 0; tick < 2 * SimulationClock.TicksPerSecond && !world.Player.IsGrounded; tick++)
        {
            world.Tick(PlayerInput.None);

            var at = world.Player.Position;
            Assert.Equal(onThePlinth.X, at.X, 0.0001f);
            Assert.Equal(onThePlinth.Z, at.Z, 0.0001f);
            Assert.True(at.Y + BodySizes.Player.Height <= bottom + 0.0001f, $"tick {tick}: his head at {at.Y + BodySizes.Player.Height}");
            roundedIn += at.Y + BodySizes.Player.Height > bottom ? 1 : 0;
        }

        // His head really came out above the impost's bottom - the test would prove nothing otherwise.
        Assert.True(roundedIn >= 1, "his head never rounded above the impost's bottom");
        Assert.True(world.Player.IsGrounded, $"never landed: {world.Player.Position}");
        Assert.Equal(onThePlinth, world.Player.Position);
    }

    [Fact]
    public void A_player_who_strafes_at_a_jamb_in_the_doorway_stops_at_the_opening()
    {
        var world = World();
        StandInTheDoorway(world);
        Assert.InRange(world.Player.Position.Z, DoorPlaneZ - BossPortalShape.RunStart(2), DoorPlaneZ + BossPortalShape.RunStart(2));

        RunTicks(world, StrafeToTheLeftJamb, 120);

        // At the door plane the innermost run leaves 1 m to either side of the middle.
        var at = world.Player.Position;
        Assert.Equal(MiddleX - (BossPortalShape.OpeningHalfWidth - Radius), at.X, 0.01f);
        Assert.False(CharacterCollision.Overlaps(world.Level, at, Radius));
        RunTicks(world, StrafeToTheLeftJamb, 30);
        Assert.Equal(at.X, world.Player.Position.X, 0.001f);
    }

    [Fact]
    public void A_player_who_walks_through_the_opening_gets_into_the_boss_room_without_touching_the_stone()
    {
        var world = World();
        RunTicks(world, PressF, 1);

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
        RunTicks(world, PressF, 1);
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

    // A bat flies over any floor, but not through stone: hit by a charged club, it flies with the push (Bat.Act) at its
    // hover height (2.2 m, below the springing) - into the jamb in the doorway, the buttress in the boss room, or on
    // through the opening.
    [Fact]
    public void A_bat_knocked_at_a_jamb_in_the_doorway_stops_at_the_opening()
    {
        var world = World(BatMap);

        var at = KnockBat(world, new Vector3(MiddleX, BodySizes.Bat.HoverHeight, DoorPlaneZ), -Vector3.UnitX);

        Assert.Equal(MiddleX - (BossPortalShape.OpeningHalfWidth - BodySizes.Bat.Radius), at.X, 0.01f);
        Assert.Equal(DoorPlaneZ, at.Z, 0.001f);
    }

    [Fact]
    public void A_bat_knocked_at_a_buttress_stops_in_front_of_it()
    {
        // The left buttress stands 1.72-2 m beside the middle (x 4-4.28 m), up to 1.24 m in front of the door plane; the
        // bat comes at the middle of its face (nearer its edge it would round the stepped corner into the opening).
        var world = World(BatMap);
        var buttress = BossPortalShape.Blocks.Single(block => block.AcrossFrom == 1.72f);
        var x = MiddleX - ((buttress.AcrossFrom + buttress.AcrossTo) / 2f);

        var at = KnockBat(world, new Vector3(x, BodySizes.Bat.HoverHeight, 5.5f), -Vector3.UnitZ);

        Assert.Equal(DoorPlaneZ + buttress.AlongTo + BodySizes.Bat.Radius, at.Z, 0.01f);
        Assert.Equal(x, at.X, 0.001f);
    }

    [Fact]
    public void A_bat_knocked_through_the_opening_flies_on_through_it()
    {
        var world = World(BatMap);

        var at = KnockBat(world, new Vector3(MiddleX, BodySizes.Bat.HoverHeight, 5.5f), -Vector3.UnitZ);

        Assert.True(at.Z < DoorPlaneZ - 1f, $"the bat is at {at}");
        Assert.Equal(MiddleX, at.X, 0.001f);
    }

    // Knocks the world's bat away with a charged club from where it is placed and lets the world fly the push out, tick
    // by tick (GameWorld.Tick, issue #627); it never enters the stone.
    private static Vector3 KnockBat(GameWorld world, Vector3 from, Vector3 direction)
    {
        // The door open (the bars lifted, the leaves swung back), so only the stone stands in the way.
        RunTicks(world, PressF, 1);
        RunTicks(world, PlayerInput.None, SimulationClock.SecondsToTicks(Balance.Default.Doors.BossOpenTime) + 1);
        Assert.False(world.Doors[0].Blocks);
        var bat = Assert.Single(world.Bats);
        bat.PlaceAt(from);
        var club = Balance.Default.Club;
        bat.Knockback.Start(direction, club.ChargedKnockback, club.KnockbackTime);
        for (var tick = 0; tick < 60 && bat.Knockback.IsActive; tick++)
        {
            world.Tick(PlayerInput.None);
            Assert.False(InStone(world.Level, bat.Position, BodySizes.Bat.Radius, BodySizes.Bat.Height), $"tick {tick}: {bat.Position}");
        }

        Assert.False(bat.Knockback.IsActive);
        Assert.Equal(BodySizes.Bat.HoverHeight, bat.Position.Y, 0.001f);
        return bat.Position;
    }

    /// <summary>
    /// True if a body (feet at <paramref name="at"/>) touches the portal's stone anywhere from its feet to its top - the
    /// portal's own geometry, independent of the collision's step rule, so it holds for walking and flying bodies alike.
    /// </summary>
    internal static bool InStone(Level level, Vector3 at, float radius, float height)
    {
        var portal = Assert.Single(level.Portals);
        for (var y = at.Y; y <= at.Y + height; y += 0.1f)
        {
            for (var angle = 0f; angle < MathF.Tau; angle += MathF.Tau / 16f)
            {
                var edge = at + new Vector3(MathF.Cos(angle) * radius * 0.99f, y - at.Y, MathF.Sin(angle) * radius * 0.99f);
                if (portal.Contains(edge))
                {
                    return true;
                }
            }
        }

        return false;
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

    [Theory]
    [InlineData(2.9f)]
    [InlineData(3.1f)]
    [InlineData(3.3f)]
    [InlineData(3.5f)]
    public void A_shot_at_the_lower_corner_of_a_buttress_ends_on_its_face(float x)
    {
        // Issue #642: a player in the start room, beside the left buttress (x 4-4.28 m, 1.76-2 m in z, from 0.5 m up),
        // shoots steeply down at its outer face (x 4 m) 1.2 cm above its bottom edge. Only about 1.3 cm of the shot lie in
        // the stone before it comes out of the bottom - a long step of 0.1 m would land past the corner, and the shot
        // would end on the plinth or the floor behind it. From four places, so the long steps fall differently.
        var world = World();
        var buttress = BossPortalShape.Blocks.Single(block => block.AcrossFrom == 1.72f);
        var target = new Vector3(MiddleX - buttress.AcrossTo, buttress.Bottom + 0.012f, 1.8f);
        var at = new Vector3(x, 0f, DoorPlaneZ - BossPortalShape.HalfDepth - Radius - 0.05f);
        world.Player.PlaceAt(at);
        RunTicks(world, PlayerInput.None, 10);
        Assert.Equal(at, world.Player.Position);

        var eye = world.Player.EyePosition;
        var aim = Vector3.Normalize(target - eye);
        var view = world.Player.View;
        view.ApplyMouse(new Vector2(view.YawRadians - MathF.Atan2(aim.X, aim.Z), view.PitchRadians - MathF.Asin(aim.Y)), 1f);
        Assert.True(Vector3.Distance(aim, view.Forward) < 1e-5f, $"aims along {view.Forward}, not {aim}");

        // Out of the stone below its bottom the shot is still in front of the plinth (z under 1.875 m): it would end
        // somewhere else, not on the buttress's outer face.
        var plinth = BossPortalShape.Blocks[BossPortalShape.Runs];
        var leaves = eye + (aim * ((buttress.Bottom - eye.Y) / aim.Y));
        Assert.True(leaves.Z < DoorPlaneZ - plinth.AlongTo, $"leaves the buttress at {leaves}");

        world.Tick(Shoot);

        var impact = Assert.Single(world.Events.OfType<BulletImpact>());
        Assert.Equal(-Vector3.UnitX, impact.Normal);
        Assert.True(Vector3.Distance(target, impact.Point) < 0.01f, $"the shot ended at {impact.Point}");
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

    // floor: every cell raised by that much, floor and ceiling alike - the portal stands on it.
    private static GameWorld World(string map = Map, float floor = 0f)
    {
        PlacedRoom[] rooms =
        [
            new(0, RoomKind.Start, "start", 0, 0, 6, 1, 0, Rows(0, 0), [], []),
            new(1, RoomKind.Boss, "boss", 0, 2, 6, 8, 1, Rows(2, 9), [(4, 9)], []),
        ];
        LevelDoor[] doors = [new(2, 1, 0, 1, RoomSide.South, DoorKind.Boss) { MoreCells = [(3, 1)] }];
        var level = LevelParser.Parse(map);
        level = level.WithCells(
            Enumerable.Range(0, level.Depth).SelectMany(z => Enumerable.Range(0, level.Width).Select(x =>
                (x, z, level[x, z] with { FloorHeight = level[x, z].FloorHeight + floor, CeilingHeight = level[x, z].CeilingHeight + floor }))));
        return new GameWorld(level, 1, Balance.Default, doors, rooms);

        static List<(int X, int Z)> Rows(int from, int to) =>
            Enumerable.Range(from, to - from + 1).SelectMany(z => new[] { (1, z), (2, z), (3, z), (4, z) }).ToList();
    }

    // Opens the door, walks into the opening up to the door plane and lets the player come to rest there.
    private static void StandInTheDoorway(GameWorld world)
    {
        RunTicks(world, PressF, 1);
        for (var tick = 0; tick < 300 && world.Player.Position.Z < DoorPlaneZ - 0.2f; tick++)
        {
            world.Tick(IntoTheOpening(world));
        }

        RunTicks(world, PlayerInput.None, 60);
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
}
