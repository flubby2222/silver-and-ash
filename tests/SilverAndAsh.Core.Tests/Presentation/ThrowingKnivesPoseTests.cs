using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// The throwing knives in the hand, in flight and stuck (issue #821): which knives the hand shows, the throw and the
/// reload tick by tick as the game runs them, and rule 30 of the art bible on the checked-in files - no knife runs
/// through the sleeve anywhere but its lid, none through another knife, and nothing of a knife behind the lid is seen.
/// </summary>
public class ThrowingKnivesPoseTests
{
    private static readonly RangedBalance Knives = Balance.Default.ThrowingKnives;
    private static readonly int CooldownTicks = SimulationClock.SecondsToTicks(Knives.Cooldown);
    private static readonly int ReloadTicks = SimulationClock.SecondsToTicks(Knives.ReloadTime);

    private static readonly Vector3[][] HandKnife = Mesh(ThrowingKnivesPose.HandFile, ThrowingKnivesPose.Knife);
    private static readonly Vector3[][] WholeKnife = Mesh(ThrowingKnivesPose.KnifeFile, ThrowingKnivesPose.Knife);
    private static readonly Vector3[][] Cuff = Mesh(ThrowingKnivesPose.SleeveFile, WeaponPose.Cuff);
    private static readonly Vector3[][] ForearmMesh = Mesh(ThrowingKnivesPose.SleeveFile, WeaponPose.Forearm);
    private static readonly Vector3 ForearmPivot = WeaponPoseTests.Triangles(ThrowingKnivesPose.SleeveFile)
        .Single(part => part.Name == WeaponPose.Forearm).Pivot;

    // The cuff's lid: the triangles that face along the sleeve and lie in the plane of the hold point (the file's
    // origin). Only through these a knife may go - that is where the grips end out of sight.
    private static readonly Vector3[][] Lid = [.. Cuff.Where(IsLid)];
    private static readonly Vector3[][] CuffWall = [.. Cuff.Where(triangle => !IsLid(triangle))];

    // ---- the files -------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(ThrowingKnivesPose.HandFile)]
    [InlineData(ThrowingKnivesPose.KnifeFile)]
    public void Both_files_have_the_one_part_and_the_blade_of_the_pose(string file)
    {
        var part = Assert.Single(WeaponPoseTests.Triangles(file));

        Assert.Equal(ThrowingKnivesPose.Knife, part.Name);
        Assert.Equal(Vector3.Zero, part.Pivot);
        Assert.Equal(ThrowingKnivesPose.BladeLength * ThrowingKnivesPose.FileScale, part.Corners.Max(corner => corner.Z), 3);
    }

    [Fact]
    public void The_knife_in_the_hand_is_cut_off_and_the_whole_one_has_its_grip()
    {
        static float Back(Vector3[][] mesh) => mesh.SelectMany(triangle => triangle).Min(corner => corner.Z);

        Assert.True(Back(WholeKnife) < Back(HandKnife) - 0.05f, $"whole {Back(WholeKnife)}, hand {Back(HandKnife)}");
        Assert.True(HandKnife.Length < WholeKnife.Length);
    }

    [Fact]
    public void The_sleeve_has_a_lid_at_its_hold_point()
    {
        Assert.NotEmpty(Lid);
        Assert.True(Lid.Length < Cuff.Length / 2, $"{Lid.Length} of {Cuff.Length} triangles count as lid");
    }

    // ---- which knives the hand shows -------------------------------------------------------------------------------

    [Theory]
    [InlineData(3, true, true, true)]
    [InlineData(2, true, true, false)]
    [InlineData(1, false, true, false)]
    [InlineData(0, false, false, false)]
    public void The_left_knife_is_thrown_first_then_the_right_one_and_the_middle_one_last(int rounds, bool right, bool middle, bool left)
    {
        Assert.Equal(right, ThrowingKnivesPose.Shows(0, rounds));
        Assert.Equal(middle, ThrowingKnivesPose.Shows(1, rounds));
        Assert.Equal(left, ThrowingKnivesPose.Shows(2, rounds));
    }

    [Fact]
    public void The_hand_shows_as_many_knives_as_the_magazine_holds_through_three_throws()
    {
        var world = World();
        var shown = new List<int>();
        foreach (var frame in Play(world, throws: 3, reload: false))
        {
            shown.Add(Enumerable.Range(0, ThrowingKnivesPose.Count).Count(index => ThrowingKnivesPose.Shows(index, frame.Rounds)));
            Assert.Equal(world.Player.Ranged.Magazine.Rounds, shown[^1]);
        }

        Assert.Equal([2, 1, 0], shown.Distinct());
    }

    [Fact]
    public void In_a_reload_the_hand_comes_back_with_all_knives_though_the_magazine_counts_them_at_the_end()
    {
        var world = World();
        var frames = Play(world, throws: 1, reload: true).Where(frame => frame.Reloading).ToList();

        Assert.Equal(ReloadTicks, frames.Count);
        Assert.All(frames, frame => Assert.Equal(frame.Progress >= ThrowingKnivesPose.RefilledAt ? Knives.MagazineSize : 2, frame.Rounds));
        Assert.Contains(frames, frame => frame.Rounds == 2);
        Assert.Contains(frames, frame => frame.Rounds == Knives.MagazineSize);

        // The knives change while the hand is down out of the picture: every corner of the three lies under its edge.
        var change = frames.First(frame => frame.Rounds == Knives.MagazineSize);
        for (var index = 0; index < ThrowingKnivesPose.Count; index++)
        {
            foreach (var corner in Placed(HandKnife, ThrowingKnivesPose.InHand(index, change.Motion)).SelectMany(triangle => triangle))
            {
                Assert.True(corner.Y < -corner.Z * WeaponPoseTests.HalfHeight, $"knife {index}: {corner} is in the picture when the knives change");
            }
        }
    }

    // ---- the motion ------------------------------------------------------------------------------------------------

    [Fact]
    public void A_throw_starts_on_its_way_snaps_to_its_height_and_is_over_after_its_ticks()
    {
        Assert.Equal(0.30f, ThrowingKnivesPose.ThrowAmount(0f), 3);
        Assert.Equal(1f, ThrowingKnivesPose.ThrowAmount(6f), 3);
        Assert.Equal(1.08f, ThrowingKnivesPose.ThrowAmount(10f), 3);
        Assert.Equal(0f, ThrowingKnivesPose.ThrowAmount(34f), 3);
        Assert.Equal(ThrowingKnivesPose.Rest, ThrowingKnivesPose.Throw(ThrowingKnivesPose.ThrowTicks));
        Assert.Equal(ThrowingKnivesPose.Rest, ThrowingKnivesPose.Throw(-1f));

        // At its height the hand is in front of rest, lower and nearer to the middle of the picture, the tips forward.
        var height = ThrowingKnivesPose.Throw(6f);
        Assert.True(height.Shift is { X: < -0.05f, Y: < -0.03f, Z: > 0.03f }, $"{height.Shift}");
        Assert.True(height.PitchDegrees < -30f);
    }

    [Fact]
    public void After_the_release_the_hand_never_jumps_from_one_tick_to_the_next()
    {
        for (var tick = 1; tick <= ThrowingKnivesPose.ThrowTicks; tick++)
        {
            var (before, now) = (ThrowingKnivesPose.Throw(tick - 1), ThrowingKnivesPose.Throw(tick));

            Assert.True((now.Shift - before.Shift).Length() < 0.04f, $"tick {tick}: the hand moves {(now.Shift - before.Shift).Length()} m");
            Assert.True(MathF.Abs(now.PitchDegrees - before.PitchDegrees) < 12f, $"tick {tick}: pitch {before.PitchDegrees} -> {now.PitchDegrees}");
            Assert.True(MathF.Abs(now.RollDegrees - before.RollDegrees) < 8f, $"tick {tick}: roll {before.RollDegrees} -> {now.RollDegrees}");
        }
    }

    [Fact]
    public void A_reload_starts_and_ends_at_rest_and_opens_the_fan_at_its_end()
    {
        Assert.Equal(ThrowingKnivesPose.Rest, ThrowingKnivesPose.Reload(0f));
        var end = ThrowingKnivesPose.Reload(1f);
        Assert.True(end.Shift.Length() < 1e-5f && MathF.Abs(end.PitchDegrees) < 1e-3f && MathF.Abs(end.RollDegrees) < 1e-3f, $"{end}");
        Assert.Equal(ThrowingKnivesPose.FanRestDegrees, end.FanDegrees, 3);

        Assert.True(ThrowingKnivesPose.Reload(ThrowingKnivesPose.RefilledAt).Shift.Y < -0.2f);
        Assert.Equal(ThrowingKnivesPose.FanClosedDegrees, ThrowingKnivesPose.Reload(0.72f).FanDegrees, 3);
        Assert.Equal(ThrowingKnivesPose.FanRestDegrees, ThrowingKnivesPose.Reload(0.88f).FanDegrees, 3);
    }

    [Fact]
    public void Through_a_reload_of_the_balance_the_hand_never_jumps_from_one_tick_to_the_next()
    {
        var frames = Play(World(), throws: 3, reload: true).Where(frame => frame.Reloading).ToList();

        Assert.Equal(ReloadTicks, frames.Count);
        foreach (var (before, now) in frames.Zip(frames.Skip(1)))
        {
            var moved = (now.Motion.Shift - before.Motion.Shift).Length();
            Assert.True(moved < 0.06f, $"progress {now.Progress}: the hand moves {moved} m in a tick");

            // The fan closes only while the hand is out of the picture (when the knives change) and opens in sight.
            var changes = before.Rounds != now.Rounds;
            Assert.True(changes || MathF.Abs(now.Motion.FanDegrees - before.Motion.FanDegrees) < 0.5f, $"progress {now.Progress}: the fan jumps");
        }
    }

    [Fact]
    public void The_knives_have_a_reload_motion()
    {
        Assert.True(ReloadMotion.HasMotion(WeaponKind.ThrowingKnives));
    }

    // ---- rule 30: nothing runs through the sleeve ------------------------------------------------------------------

    [Fact]
    public void At_rest_the_three_knives_stand_as_pose_s12_of_the_probe()
    {
        // The base of the middle blade: 12 mm in front of the hold point along the knife, 8 mm to the left of it, the
        // knife 6 mm further along its thickness than the right one - all at the files' scale, turned into the picture.
        var middle = Vector3.Transform(Vector3.Zero, ThrowingKnivesPose.InHand(1, ThrowingKnivesPose.Rest));
        var tip = Vector3.Transform(new Vector3(0f, 0f, ThrowingKnivesPose.BladeLength * ThrowingKnivesPose.FileScale), ThrowingKnivesPose.InHand(1, ThrowingKnivesPose.Rest));
        var along = Vector3.Normalize(tip - middle);

        // The tip points 44 degrees up from the line of sight (a little less in the picture, the fan leans over by 6).
        Assert.InRange(MathF.Asin(along.Y) * 180f / MathF.PI, ThrowingKnivesPose.PitchDegrees - 0.5f, ThrowingKnivesPose.PitchDegrees);
        Assert.InRange(along.X, -0.1f, -0.05f);

        // The outer knives lean away from it by the fan's angle, the right one to the right of the picture.
        for (var index = 0; index < ThrowingKnivesPose.Count; index += 2)
        {
            var pose = ThrowingKnivesPose.InHand(index, ThrowingKnivesPose.Rest);
            var outer = Vector3.Normalize(
                Vector3.Transform(new Vector3(0f, 0f, 1f), pose) - Vector3.Transform(Vector3.Zero, pose));
            Assert.Equal(ThrowingKnivesPose.FanRestDegrees, MathF.Acos(Vector3.Dot(outer, along)) * 180f / MathF.PI, 1);
            Assert.True(index == 0 ? outer.X > along.X + 0.1f : outer.X < along.X - 0.1f, $"knife {index} leans to {outer}");
        }

        // The broad side of the blades faces the eyes: the blade's width (the file's y) lies across the picture.
        var across = Vector3.TransformNormal(Vector3.UnitY, ThrowingKnivesPose.InHand(1, ThrowingKnivesPose.Rest));
        Assert.True(MathF.Abs(across.X) > 0.95f, $"the blade's width points to {across}");
    }

    [Theory]
    [InlineData(ThrowingKnivesPose.FanRestDegrees)]
    [InlineData(16.5f)]
    [InlineData(ThrowingKnivesPose.FanClosedDegrees)]
    public void At_rest_no_knife_runs_through_the_sleeve_or_another_knife(float fan)
    {
        AssertRule30(ThrowingKnivesPose.Rest with { FanDegrees = fan }, rounds: ThrowingKnivesPose.Count, $"fan {fan}");
    }

    [Fact]
    public void Through_three_throws_and_the_reload_no_knife_runs_through_the_sleeve_or_another_knife()
    {
        var checkedFrames = 0;
        foreach (var frame in Play(World(), throws: 3, reload: true))
        {
            AssertRule30(frame.Motion, frame.Rounds, $"tick {frame.Tick} (throw tick {frame.ThrowTick}, reload {frame.Progress:0.00})");
            checkedFrames++;
        }

        Assert.Equal((3 * CooldownTicks) + ReloadTicks, checkedFrames);
    }

    [Fact]
    public void Behind_the_lid_the_cut_stumps_stand_in_each_other_as_the_art_bible_counts_them()
    {
        // Rule 30 names 108 pairs of triangles at the written file, all behind the lid. The count proves that the check
        // looks at the knives where they really stand - and that the pose is the accepted s12.
        var pairs = new[] { (0, 1), (1, 2), (0, 2) }
            .Select(pair => KnifeAgainstKnife(pair.Item1, pair.Item2, ThrowingKnivesPose.FanRestDegrees))
            .ToList();

        Assert.Equal(0, pairs.Sum(pair => pair.InFront));
        Assert.Equal(108, pairs.Sum(pair => pair.Behind));
    }

    [Fact]
    public void A_fan_one_degree_narrower_than_the_reload_closes_it_lets_the_grips_cut_each_other_in_front_of_the_lid()
    {
        // Why the reload closes the fan to 16 and not to the 15 degrees of the motion probe: at 15 the grips of the
        // middle and the left knife touch each other just in front of the lid (rule 30 counts that).
        var (inFront, _) = KnifeAgainstKnife(1, 2, ThrowingKnivesPose.FanClosedDegrees - 1f);

        Assert.True(inFront > 0, "One degree narrower the grips should cut each other in front of the lid.");
    }

    [Fact]
    public void The_check_of_rule_30_finds_knives_that_stand_too_deep_in_the_cuff()
    {
        // The proof that the check can fail: pulled 5 cm back into the sleeve, the fan's outer blades cut its wall.
        var deeper = Matrix4x4.CreateTranslation(0f, 0f, -0.05f * ThrowingKnivesPose.FileScale);
        var pairs = Enumerable.Range(0, ThrowingKnivesPose.Count).Sum(index => Pairs(
            Placed(HandKnife, ThrowingKnivesPose.InSleeve(index, ThrowingKnivesPose.FanRestDegrees) * deeper), CuffWall));

        Assert.True(pairs > 0, "Knives pulled into the cuff should run through its wall.");
    }

    // ---- within reach, beyond the near plane -----------------------------------------------------------------------

    [Fact]
    public void Through_three_throws_and_the_reload_knives_and_sleeve_stay_within_reach_and_beyond_the_near_plane()
    {
        var shrink = Matrix4x4.CreateScale(WeaponPose.ShrinkTowardsEye);
        foreach (var frame in Play(World(), throws: 3, reload: true))
        {
            var forearm = ThrowingKnivesPose.Forearm(frame.Motion, ForearmPivot);
            WeaponPoseTests.AssertWithinReach(
                ThrowingKnivesPose.SleeveFile, ThrowingKnivesPose.Sleeve(frame.Motion),
                name => name == WeaponPose.Forearm ? forearm : Matrix4x4.Identity);
            for (var index = 0; index < ThrowingKnivesPose.Count; index++)
            {
                if (!ThrowingKnivesPose.Shows(index, frame.Rounds))
                {
                    continue;
                }

                var pose = ThrowingKnivesPose.InHand(index, frame.Motion);
                WeaponPoseTests.AssertWithinReach(ThrowingKnivesPose.HandFile, pose, _ => Matrix4x4.Identity);
                var nearest = Placed(HandKnife, pose * shrink).SelectMany(triangle => triangle).Min(corner => corner.Z);
                Assert.True(nearest > 5f * WeaponPose.NearPlane, $"tick {frame.Tick}: knife {index} comes as near as {nearest} m to the eye's plane");
            }
        }
    }

    [Fact]
    public void At_rest_all_three_blades_are_in_the_picture()
    {
        for (var index = 0; index < ThrowingKnivesPose.Count; index++)
        {
            var tip = Vector3.Transform(
                new Vector3(0f, 0f, ThrowingKnivesPose.BladeLength * ThrowingKnivesPose.FileScale),
                ThrowingKnivesPose.InHand(index, ThrowingKnivesPose.Rest));

            Assert.True(
                MathF.Abs(tip.X) < tip.Z * WeaponPoseTests.HalfWidth && MathF.Abs(tip.Y) < tip.Z * WeaponPoseTests.HalfHeight,
                $"the tip of knife {index} at {tip} is outside the picture");
        }
    }

    [Fact]
    public void A_knife_thrown_in_the_very_tick_the_knives_were_drawn_still_shows_its_throw()
    {
        // The game's frame reads the tick's events before the views are updated: the release comes before the first Step.
        var world = World();
        var clock = new KnifeThrowClock();
        world.Tick(PlayerInput.None with { Shoot = true });
        clock.Released(Assert.Single(world.Events.OfType<ShotFired>()).Weapon);
        clock.Step(world.Player.Ranged.Kind, SimulationClock.SecondsPerTick);

        Assert.Equal(1f, clock.Tick, 3);
        Assert.True(ThrowingKnivesPose.ThrowAmount(clock.Tick) > 0.3f);

        // Another weapon in the hand ends it at once.
        clock.Step(WeaponKind.Pistol, SimulationClock.SecondsPerTick);
        Assert.Equal(ThrowingKnivesPose.ThrowTicks, clock.Tick);
    }

    // ---- in flight and stuck ---------------------------------------------------------------------------------------

    [Fact]
    public void A_flying_knife_has_its_tip_at_the_point_of_the_world_state_whenever_it_points_ahead()
    {
        var point = new Vector3(3f, 1.4f, -2f);
        var direction = Vector3.Normalize(new Vector3(0.3f, -0.1f, 1f));
        var fullTurn = 360f / ThrowingKnivesPose.SpinDegreesPerMetre;
        var tipInFile = new Vector3(0f, 0f, ThrowingKnivesPose.BladeLength * ThrowingKnivesPose.FileScale);

        foreach (var rangeLeft in (float[])[0f, fullTurn, 3f * fullTurn])
        {
            var tip = Vector3.Transform(tipInFile, ThrowingKnivesPose.Flying(point, direction, rangeLeft));
            Assert.True((tip - point).Length() < 1e-3f, $"range left {rangeLeft}: the tip is at {tip}");
        }

        // Half a turn later the handle leads; the knife still turns around the base of its blade, behind the point.
        var turned = ThrowingKnivesPose.Flying(point, direction, fullTurn / 2f);
        var basePoint = Vector3.Transform(Vector3.Zero, turned);
        Assert.True((basePoint - (point - (direction * ThrowingKnivesPose.BladeLength))).Length() < 1e-3f);
        Assert.True(Vector3.Dot(Vector3.Transform(tipInFile, turned) - basePoint, direction) < -0.1f);
    }

    [Fact]
    public void Out_of_the_hand_the_knife_shows_its_broad_side_to_who_looks_along_the_flight()
    {
        // Seen along the flight (+z), the knife is far broader across (x) than a blade on edge would be.
        static float Across(Matrix4x4 pose)
        {
            var corners = WholeKnife.SelectMany(triangle => triangle).Select(corner => Vector3.Transform(corner, pose).X).ToList();
            return corners.Max() - corners.Min();
        }

        var quarterTurn = 90f / ThrowingKnivesPose.SpinDegreesPerMetre;
        Assert.True(Across(ThrowingKnivesPose.Flying(Vector3.Zero, Vector3.UnitZ, 0f)) > 0.02f);
        Assert.True(Across(ThrowingKnivesPose.Flying(Vector3.Zero, Vector3.UnitZ, quarterTurn)) > 0.02f);
        Assert.True(Across(ThrowingKnivesPose.Stuck(Vector3.Zero, Vector3.UnitZ, 0)) > 0.02f);
    }

    [Fact]
    public void A_knife_thrown_in_the_game_turns_tick_by_tick_and_is_drawn_at_the_size_of_the_world()
    {
        var world = World("P\n.\n.\n.\n.\n.\n.\n.\n.\n.\n.");
        world.Tick(PlayerInput.None with { Shoot = true });
        var turns = new List<Vector3>();
        for (var tick = 0; tick < 6; tick++)
        {
            var knife = Assert.Single(world.FlyingKnives);
            var pose = ThrowingKnivesPose.Flying(knife.Position, knife.Direction, knife.RangeLeft);
            var basePoint = Vector3.Transform(Vector3.Zero, pose);
            var tip = Vector3.Transform(new Vector3(0f, 0f, ThrowingKnivesPose.BladeLength * ThrowingKnivesPose.FileScale), pose);

            Assert.Equal(ThrowingKnivesPose.BladeLength, (tip - basePoint).Length(), 3);
            Assert.True((basePoint - knife.Position).Length() < ThrowingKnivesPose.BladeLength + 1e-3f);
            turns.Add(Vector3.Normalize(tip - basePoint));
            world.Tick(PlayerInput.None);
        }

        Assert.True(turns.Zip(turns.Skip(1)).All(pair => (pair.First - pair.Second).Length() > 0.3f), string.Join(" ", turns));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void A_stuck_knife_has_part_of_its_blade_in_the_target_and_its_handle_hanging_up_outside(int id)
    {
        var point = new Vector3(2f, 1.2f, 5f);

        // Part of the blade, not all of it: at least 2 cm are in the target, and more than a third of the blade shows.
        Assert.InRange(ThrowingKnivesPose.StuckDepth, 0.02f, ThrowingKnivesPose.BladeLength * 2f / 3f);
        foreach (var direction in (Vector3[])[Vector3.UnitZ, Vector3.Normalize(new Vector3(1f, -0.2f, 0.4f)), -Vector3.UnitX])
        {
            var pose = ThrowingKnivesPose.Stuck(point, direction, id);
            var corners = WholeKnife.SelectMany(triangle => triangle).Select(corner => Vector3.Transform(corner, pose)).ToList();
            var tip = Vector3.Transform(new Vector3(0f, 0f, ThrowingKnivesPose.BladeLength * ThrowingKnivesPose.FileScale), pose);
            var pommel = corners.MinBy(corner => Vector3.Dot(corner - point, direction));

            // The tip is in the target, StuckDepth along the flight; the base of the blade and the grip are outside.
            Assert.True((tip - (point + (direction * ThrowingKnivesPose.StuckDepth))).Length() < 1e-3f, $"tip at {tip}");
            Assert.True(corners.Max(corner => Vector3.Dot(corner - point, direction)) < ThrowingKnivesPose.StuckDepth + 0.005f);
            Assert.True(Vector3.Dot(pommel - point, direction) < -0.08f, $"the pommel is {Vector3.Dot(pommel - point, direction)} m along the flight");

            // The handle hangs higher than a knife that sticks straight would have it.
            var straight = point - (direction * (ThrowingKnivesPose.BladeLength - ThrowingKnivesPose.StuckDepth + 0.09f));
            Assert.True(pommel.Y > straight.Y + 0.02f, $"pommel {pommel}, straight {straight}");
        }
    }

    [Fact]
    public void Knives_stuck_side_by_side_hang_to_different_sides()
    {
        static Vector3 Base(int id) => Vector3.Transform(Vector3.Zero, ThrowingKnivesPose.Stuck(Vector3.Zero, Vector3.UnitZ, id));

        Assert.True(Base(0).X * Base(1).X < 0f, $"{Base(0)} and {Base(1)}");
        Assert.Equal(Base(0), Base(2));
    }

    // ---- helpers ---------------------------------------------------------------------------------------------------

    /// <summary>One frame of the hand as the game draws it: what the view reads from the world in that tick.</summary>
    private readonly record struct Frame(int Tick, int Rounds, float ThrowTick, bool Reloading, float Progress, KnifeMotion Motion);

    private static GameWorld World(string map = "P\n.\n.\n.\n.\n.\n.\n.\n.")
    {
        var world = new GameWorld(LevelParser.Parse(map), 1, Balance.Default);
        world.Player.Equip(0, WeaponKind.ThrowingKnives);
        return world;
    }

    /// <summary>
    /// Plays the game tick by tick as the view follows it: <paramref name="throws"/> throws one cooldown apart, then -
    /// if asked - R and the whole reload. The throw's clock starts at <see cref="ShotFired"/>, the knives shown and the
    /// reload's share come from the magazine, the reload's weight from <see cref="ReloadMotion"/>.
    /// </summary>
    private static IEnumerable<Frame> Play(GameWorld world, int throws, bool reload)
    {
        var motion = new ReloadMotion();
        var clock = new KnifeThrowClock();
        var total = (throws * CooldownTicks) + (reload ? ReloadTicks : 0);
        for (var tick = 0; tick < total; tick++)
        {
            var input = PlayerInput.None with
            {
                Shoot = tick < throws * CooldownTicks && tick % CooldownTicks == 0,
                Reload = reload && tick == throws * CooldownTicks,
            };
            world.Tick(input);
            // The order of the game's frame: first the tick's events (WeaponViews.OnEvent), then the views' update.
            foreach (var gameEvent in world.Events)
            {
                if (gameEvent is ShotFired shot)
                {
                    clock.Released(shot.Weapon);
                }
                else if (gameEvent is ReloadFinished)
                {
                    motion.Reset();
                }
            }

            var ranged = world.Player.Ranged;
            clock.Step(ranged.Kind, SimulationClock.SecondsPerTick);
            var magazine = ranged.Magazine;
            motion.Step(ranged.Kind, magazine.IsReloading, magazine.ReloadProgress, SimulationClock.SecondsPerTick);
            yield return new Frame(
                tick,
                ThrowingKnivesPose.ShownRounds(magazine.Rounds, magazine.Size, magazine.IsReloading, magazine.ReloadProgress),
                clock.Tick,
                magazine.IsReloading,
                magazine.ReloadProgress,
                ThrowingKnivesPose.Motion(clock.Tick, motion.Progress, motion.Weight));
        }
    }

    // Rule 30 for one frame: the shown knives against the sleeve's wall and forearm, against each other, and what of
    // them lies behind the lid against the eye.
    private static void AssertRule30(KnifeMotion motion, int rounds, string when)
    {
        var sleeve = ThrowingKnivesPose.Sleeve(motion);
        var wall = Placed(CuffWall, sleeve)
            .Concat(Placed(ForearmMesh, ThrowingKnivesPose.Forearm(motion, ForearmPivot) * Matrix4x4.CreateTranslation(ForearmPivot) * sleeve))
            .ToArray();
        var cover = wall.Concat(Placed(Lid, sleeve)).ToArray();
        var knives = Enumerable.Range(0, ThrowingKnivesPose.Count)
            .Where(index => ThrowingKnivesPose.Shows(index, rounds))
            .Select(index => (Index: index, Mesh: Placed(HandKnife, ThrowingKnivesPose.InHand(index, motion))))
            .ToList();

        foreach (var (index, mesh) in knives)
        {
            var pairs = Pairs(mesh, wall);
            Assert.True(pairs == 0, $"{when}: knife {index} runs through the sleeve in {pairs} pairs of triangles.");

            // Behind the lid nothing of the knife is seen: each such corner in the picture has sleeve in front of it.
            var inSleeve = ThrowingKnivesPose.InSleeve(index, motion.FanDegrees);
            foreach (var corner in HandKnife.SelectMany(triangle => triangle).Distinct())
            {
                if (!BehindLid(Vector3.Transform(corner, inSleeve)))
                {
                    continue;
                }

                var seen = Vector3.Transform(corner, inSleeve * sleeve);
                var inPicture = seen.Z > 0f && MathF.Abs(seen.X) <= seen.Z * WeaponPoseTests.HalfWidth && MathF.Abs(seen.Y) <= seen.Z * WeaponPoseTests.HalfHeight;
                Assert.True(
                    !inPicture || cover.Any(triangle => ClubBendTests.Hit(Vector3.Zero, seen, triangle) is > 0f and < 1f),
                    $"{when}: a corner of knife {index} behind the lid is seen at {seen}.");
            }
        }

        // Knife against knife depends on the fan alone. In front of the lid none cuts another; behind it the cut
        // stumps may (rule 30: they are seen from nowhere).
        foreach (var index in knives.Select(knife => knife.Index))
        {
            foreach (var other in knives.Select(knife => knife.Index).Where(other => other > index))
            {
                var (inFront, _) = KnifeAgainstKnife(index, other, motion.FanDegrees);
                Assert.True(inFront == 0, $"{when}: knives {index} and {other} run through each other in front of the lid in {inFront} pairs of triangles.");
            }
        }
    }

    private static readonly Dictionary<(int, int, float), (int InFront, int Behind)> KnifePairs = [];

    // Pairs of triangles of two knives in the hand that cut each other: in front of the lid and wholly behind it.
    private static (int InFront, int Behind) KnifeAgainstKnife(int index, int other, float fan)
    {
        lock (KnifePairs)
        {
            if (KnifePairs.TryGetValue((index, other, fan), out var known))
            {
                return known;
            }

            var first = Placed(HandKnife, ThrowingKnivesPose.InSleeve(index, fan));
            var second = Placed(HandKnife, ThrowingKnivesPose.InSleeve(other, fan));
            var (inFront, behind) = (0, 0);
            foreach (var a in first)
            {
                foreach (var b in second)
                {
                    var cuts = CutPoints(a, b).Concat(CutPoints(b, a)).ToList();
                    if (cuts.Count == 0)
                    {
                        continue;
                    }

                    if (cuts.Any(point => !BehindLid(point)))
                    {
                        inFront++;
                    }
                    else
                    {
                        behind++;
                    }
                }
            }

            return KnifePairs[(index, other, fan)] = (inFront, behind);
        }
    }

    private static Vector3[][] Mesh(string file, string partName)
    {
        var part = WeaponPoseTests.Triangles(file).Single(candidate => candidate.Name == partName);
        var triangles = new Vector3[part.Triangles.Length / 3][];
        for (var i = 0; i < triangles.Length; i++)
        {
            triangles[i] = [part.Corners[part.Triangles[3 * i]], part.Corners[part.Triangles[(3 * i) + 1]], part.Corners[part.Triangles[(3 * i) + 2]]];
        }

        return triangles;
    }

    private static bool IsLid(Vector3[] triangle)
    {
        var normal = Vector3.Normalize(Vector3.Cross(triangle[1] - triangle[0], triangle[2] - triangle[0]));
        return MathF.Abs(normal.Z) > 0.9f && triangle.All(corner => MathF.Abs(corner.Z) < 0.01f);
    }

    // The lid is a shallow cone, so "behind it" is not a plane: a point of the sleeve's file space lies behind the lid
    // if the way forward along the sleeve (+z) goes through it.
    private static bool BehindLid(Vector3 point) =>
        Lid.Any(triangle => ClubBendTests.Hit(point, Vector3.UnitZ, triangle) is > 1e-4f);

    private static Vector3[][] Placed(Vector3[][] mesh, Matrix4x4 pose) =>
        [.. mesh.Select(triangle => triangle.Select(corner => Vector3.Transform(corner, pose)).ToArray())];

    // How many pairs of triangles cut each other: an edge of the one goes through the other.
    private static int Pairs(Vector3[][] first, Vector3[][] second) =>
        first.Sum(a => second.Count(b => Cuts(a, b) || Cuts(b, a)));

    private static bool Cuts(Vector3[] edges, Vector3[] triangle) => CutPoints(edges, triangle).Any();

    // Where the edges of the one triangle go through the other.
    private static IEnumerable<Vector3> CutPoints(Vector3[] edges, Vector3[] triangle)
    {
        for (var i = 0; i < 3; i++)
        {
            var from = edges[i];
            var along = edges[(i + 1) % 3] - from;
            if (ClubBendTests.Hit(from, along, triangle) is { } t and > 1e-4f and < 1f - 1e-4f)
            {
                yield return from + (along * t);
            }
        }
    }
}
