using System.Numerics;
using SilverAndAsh.Core.Course;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Course;

/// <summary>
/// The test course (issue #718, docs/pruefstrecke.md): one level with every station, walked tick by tick with the real
/// rules - doors open with F, the portcullis bars, the boss door slams shut, Gorm fights - and a player who cannot die.
/// </summary>
public class TestCourseTests
{
    private const int Seed = 1;
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly PlayerInput PressF = PlayerInput.None with { Interact = true };
    private static readonly PlayerInput Melee = PlayerInput.None with { Melee = true };

    /// <summary>
    /// The acceptance run: from the start along the way through every station - each door opened with F, the enemies
    /// struck down where they stand, the arena waited out until its grates rise - through the middle of the portal into
    /// the boss room, which seals behind the player. Every station is entered, in the order of the list.
    /// </summary>
    [Fact]
    public void A_player_walks_from_the_start_through_every_station_into_the_sealed_boss_room()
    {
        var run = new CourseRun(Seed);

        run.WalkToTheBossRoom();

        Assert.Equal(Enumerable.Range(0, TestCourse.Stations.Count), run.Entered);
        var boss = run.World.Rooms.Single(room => room.Kind == RoomKind.Boss);
        Assert.True(boss.IsSealed, "The boss room never sealed.");
        Assert.Contains(run.Events, e => e is RoomSealed { RoomIndex: var index } && index == boss.Index);
        Assert.Contains(run.Events, e => e is BossFightStarted);
    }

    /// <summary>
    /// The course can be won like a run - Gorm slain, the stairs climbed - but its player could not die and carried every
    /// weapon: the balancing round must never count it, so the run log stays untouched (review of PR #720).
    /// </summary>
    [Fact]
    public void A_win_on_the_course_stays_out_of_the_run_log()
    {
        var run = new CourseRun(Seed);
        run.WalkToTheBossRoom();
        run.World.Gorm!.TakeDamage(10_000, run.Events);
        run.Run(PlayerInput.None, SimulationClock.SecondsToTicks(Balance.Default.Gorm.StairsRiseTime) + 1);
        var stairs = Assert.IsType<Stairs>(run.World.Stairs);
        Assert.True(stairs.CanInteract, "The stairs never stood.");

        var before = stairs.Position - new Vector3(0f, 0f, Stairs.Radius + 0.8f);
        run.WalkTo(before);
        run.LookAt(stairs.Position + new Vector3(0f, run.World.Player.EyePosition.Y, 0f));
        run.Run(PressF, 1);
        Assert.True(run.World.IsWon, "The course was not won - the test would prove nothing.");
        var summary = run.World.TakeEndSummary();
        Assert.NotNull(summary);

        var folder = Path.Combine(Path.GetTempPath(), $"course-runs-{Guid.NewGuid():N}");
        var path = Path.Combine(folder, "runs.log");
        try
        {
            Assert.True(RunLog.AppendRun(path, run.World, summary.ToLogLine(DateTimeOffset.Now)));
            Assert.False(File.Exists(path), "The win on the test course was written to the run log.");
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public void The_course_has_the_stations_of_issue_718_in_their_order()
    {
        Assert.Equal(
            ["Arms", "Columns", "Cracked wall", "Portcullis", "Teleporter", "Boss"],
            TestCourse.Stations.Select(station => station.Name));
    }

    [Fact]
    public void Each_station_is_one_room_of_its_kind_with_its_numbered_sign_over_the_entrance()
    {
        var course = TestCourse.Build(Seed, Balance.Default);

        Assert.Equal(TestCourse.Stations.Count, course.Stations.Count);
        Assert.Equal(TestCourse.Stations.Count, course.Labels.Count);
        for (var i = 0; i < TestCourse.Stations.Count; i++)
        {
            var station = TestCourse.Stations[i];
            var room = course.Stations[i];
            Assert.Equal(station.Kind, room.Kind);
            Assert.Equal($"{i + 1} - {station.Name}", course.Labels[i].Text);
            var under = Level.ToGrid(course.Labels[i].Position.X, course.Labels[i].Position.Z);
            Assert.Equal(room.FloorCells.Min(cell => cell.X), under.X);
            Assert.Contains(under, room.FloorCells);
            Assert.True(course.Labels[i].Position.Y < room.FloorCells.Min(cell => course.Generated.Level[cell.X, cell.Z].CeilingHeight));
        }
    }

    [Fact]
    public void No_room_of_the_course_overlaps_another()
    {
        var course = TestCourse.Build(Seed, Balance.Default);
        var rooms = course.Generated.Rooms;

        foreach (var room in rooms)
        {
            foreach (var other in rooms.Where(other => other.Index > room.Index))
            {
                Assert.Empty(room.FloorCells.Intersect(other.FloorCells));
            }
        }

        var doorCells = course.Generated.Doors.SelectMany(door => door.Cells).ToList();
        Assert.Equal(doorCells.Count, doorCells.Distinct().Count());
        Assert.Empty(rooms.SelectMany(room => room.FloorCells).Intersect(doorCells));
    }

    [Fact]
    public void A_station_that_runs_into_another_is_refused()
    {
        var twice = new CourseStation("Two secrets", RoomKind.Start, 5, room =>
        {
            room.SecretRoomNorth();
            room.SecretRoomNorth();
        });

        var error = Assert.Throws<InvalidOperationException>(() => TestCourse.Build(Seed, Balance.Default, [twice]));

        Assert.Contains("runs into", error.Message);
    }

    [Fact]
    public void A_station_that_reaches_past_the_ring_of_wall_is_refused()
    {
        // As the first station, the antechamber's great hall would reach six columns west of the course's first column.
        var first = new CourseStation("Boss first", RoomKind.Antechamber, Antechamber.Width, room => room.BossRoomSouth());

        var error = Assert.Throws<InvalidOperationException>(() => TestCourse.Build(Seed, Balance.Default, [first]));

        Assert.Contains("outside the ring of wall", error.Message);
        Assert.Contains("Boss first", error.Message);
    }

    [Fact]
    public void A_column_that_does_not_fit_is_refused()
    {
        // A column in the corner of a station is too close to its walls for the rule of the generated levels.
        var cramped = new CourseStation("Cramped", RoomKind.Start, 5, room => room.Column(0, CourseRoom.Top));

        var error = Assert.Throws<InvalidOperationException>(() => TestCourse.Build(Seed, Balance.Default, [cramped]));

        Assert.Contains("Cramped", error.Message);
    }

    [Fact]
    public void A_new_station_is_one_more_entry_of_the_list()
    {
        var more = TestCourse.Stations.Take(2).Append(new CourseStation("More", RoomKind.Start, 3, _ => { })).ToList();

        var course = TestCourse.Build(Seed, Balance.Default, more);

        Assert.Equal(3, course.Stations.Count);
        Assert.Equal("3 - More", course.Labels[2].Text);
        Assert.Equal(course.Stations[1].FloorCells.Max(cell => cell.X) + 2, course.Stations[2].FloorCells.Min(cell => cell.X));
    }

    [Fact]
    public void Every_player_starts_armed_unable_to_die_and_looking_along_the_way()
    {
        var world = TestCourse.NewWorld(Seed, Balance.Default);

        Assert.Equal(TestCourse.Name, world.LevelName);
        Assert.False(world.IsShowroom);
        Assert.True(world.IsTestWorld);
        foreach (var player in world.Players)
        {
            Assert.True(player.CannotDie);
            Assert.Equal(WeaponKind.SilverBlade, player.Melee);
            Assert.Equal([WeaponKind.Pistol, WeaponKind.Rifle, WeaponKind.Blunderbuss], player.RangedSlots.Select(weapon => weapon!.Kind));
            Assert.Contains(WeaponKind.Club, player.Bag);
            Assert.Equal(1f, Vector3.Dot(Vector3.Normalize(player.View.Forward with { Y = 0f }), Vector3.UnitX), 0.001f);
        }
    }

    [Fact]
    public void Every_weapon_and_both_vampiric_items_lie_in_the_first_station_and_loot_before_the_portal()
    {
        var world = TestCourse.NewWorld(Seed, Balance.Default);
        var course = TestCourse.Build(Seed, Balance.Default);

        Assert.Equal(Enum.GetValues<WeaponKind>().Length, world.EquipmentDrops.OfType<WeaponDrop>().Count());
        Assert.Equal([ItemKind.Leech, ItemKind.AshenHeart], world.EquipmentDrops.OfType<ItemDrop>().Select(drop => drop.Kind));
        Assert.All(world.EquipmentDrops, drop => Assert.Contains(CellOf(drop.Position), course.Stations[0].FloorCells));
        Assert.Contains(world.Loot, loot => course.Stations[1].FloorCells.Contains(CellOf(loot.Position)));
        Assert.Contains(world.Loot, loot => course.Stations[5].FloorCells.Contains(CellOf(loot.Position)));
        Assert.Contains(world.Pickups, pickup => course.Stations[5].FloorCells.Contains(CellOf(pickup.Position)));
    }

    [Fact]
    public void The_cracked_wall_breaks_open_to_a_secret_room_with_a_chest()
    {
        var run = new CourseRun(Seed);
        var wall = run.World.Doors.Single(door => door.Kind == DoorKind.Cracked);
        var secret = run.World.Rooms.Single(room => room.Kind == RoomKind.Secret);
        Assert.Contains(run.World.Chests, chest => secret.Cells.Contains(CellOf(chest.Position)));

        run.WalkToStation(2);
        run.WalkTo(run.World.Level.CellCenter(wall.X, wall.Z + 1));
        run.LookAt(MiddleOf(wall));
        var events = run.Run(Melee, SimulationClock.SecondsToTicks(Balance.Default.Blade.ChargeTime) + 5);
        events.AddRange(run.Run(PlayerInput.None, Second));

        Assert.Contains(new WallBroken(wall.Id, run.World.Player.Id), events);
        Assert.True(wall.IsOpen);
    }

    [Fact]
    public void The_portcullis_bars_the_way_back_while_the_arena_fights()
    {
        var run = new CourseRun(Seed);
        var arena = run.World.Rooms[run.Course.Stations[3].Index];
        var entrance = run.DoorInto(3);

        run.WalkToStation(3, killEnemies: false);
        run.WalkTo(run.World.Level.CellCenter(entrance.X + 4, CourseRoom.Way));
        run.Run(PlayerInput.None, Second);

        Assert.True(arena.IsSealed, "The arena never sealed.");
        Assert.Contains(new GrateStarted(entrance.Id, Falling: true), run.Events);
        Assert.True(entrance.IsBarred);
        var back = run.World.Level.CellCenter(entrance.X - 1, CourseRoom.Way);
        for (var tick = 0; tick < 5 * Second; tick++)
        {
            run.LookAt(back with { Y = run.World.Player.EyePosition.Y });
            run.Run(Forward, 1);
        }

        Assert.True(run.World.Player.Position.X > (entrance.X + 1) * Level.CellSize, $"The player got out of the arena to {run.World.Player.Position}.");
        Assert.Contains(run.Events, e => e is WaveEmerged);
    }

    [Fact]
    public void The_boss_door_slams_shut_behind_a_player_and_gorm_attacks_without_killing_them()
    {
        var run = new CourseRun(Seed);
        run.WalkToTheBossRoom();
        var portal = run.World.Doors.Single(door => door.Kind == DoorKind.Boss);

        Assert.Contains(new DoorSwingStarted(portal.Id, Closing: true), run.Events);
        var events = run.Run(PlayerInput.None, 60 * Second, until: () => run.World.Player.Health == 1);

        Assert.True(portal.IsBarred);
        Assert.Contains(events, e => e is PlayerHit { Damage: > 0 });
        Assert.Equal(1, run.World.Player.Health);
        events.AddRange(run.Run(PlayerInput.None, 5 * Second));
        Assert.Contains(events, e => e is PlayerHit { Damage: 0 });
        Assert.DoesNotContain(events, e => e is PlayerDied);
        Assert.False(run.World.Player.IsDead);
    }

    /// <summary>
    /// Columns break only under Gorm's blows (decision on issue #608). A player hides behind the column nearest to him in
    /// the boss room of the course; it breaks after exactly as many blows as the balance says.
    /// </summary>
    [Fact]
    public void A_column_of_the_boss_room_breaks_after_as_many_blows_as_the_balance_says()
    {
        var run = new CourseRun(Seed);
        run.WalkToTheBossRoom();
        var gorm = run.World.Gorm!;
        var hall = run.World.Rooms.Single(room => room.Kind == RoomKind.Boss);
        var columns = run.World.Level.Columns.Where(c => hall.Cells.Contains(Level.ToGrid(c.X, c.Z))).ToList();
        Assert.NotEmpty(columns);
        var column = columns.MinBy(c => Vector2.Distance(c.Centre, new Vector2(gorm.Position.X, gorm.Position.Z)));

        var events = new List<GameEvent>();
        for (var tick = 0; tick < 120 * Second && !events.Contains(new ColumnBroke(column)); tick++)
        {
            // Behind the column, seen from Gorm - wherever he stands now.
            var away = Vector2.Normalize(column.Centre - new Vector2(gorm.Position.X, gorm.Position.Z));
            var behind = column.Centre + (away * (Column.Radius + BodySizes.Player.Radius + 0.3f));
            var player = run.World.Player;
            var to = new Vector2(behind.X - player.Position.X, behind.Y - player.Position.Z);
            if (to.Length() > 0.15f)
            {
                run.LookAt(new Vector3(behind.X, player.EyePosition.Y, behind.Y));
                events.AddRange(run.Run(Forward, 1));
            }
            else
            {
                events.AddRange(run.Run(PlayerInput.None, 1));
            }
        }

        var hits = events.OfType<ColumnHit>().Where(hit => hit.Column == column).ToList();
        Assert.Contains(new ColumnBroke(column), events);
        Assert.Equal(Balance.Default.Columns.HitsToBreak, hits.Count);
        Assert.Equal(Enumerable.Range(1, Balance.Default.Columns.HitsToBreak), hits.Select(hit => hit.Hits));
        Assert.DoesNotContain(column, run.World.Level.Columns);
    }

    [Fact]
    public void The_two_teleporters_offer_a_journey_between_the_start_and_the_fifth_station()
    {
        var run = new CourseRun(Seed);
        var far = run.World.Teleporters.Single(teleporter => teleporter.Plan.Site == TeleporterSite.BossGate);

        run.WalkToStation(4);
        run.WalkTo(run.World.Level.CellCenter(far.Plan.X, far.Plan.Z));

        Assert.All(run.World.Teleporters, teleporter => Assert.True(teleporter.IsUnlocked));
        var choices = run.World.TravelChoices(run.World.Player);
        Assert.Equal(2, choices.Count);
        Assert.Contains(choices, choice => ReferenceEquals(choice.Teleporter, far) && choice.State == TravelState.Here);
        Assert.Contains(choices, choice => choice.State == TravelState.Ready);
        Assert.NotEqual(Vector2.Zero, far.Plan.Facing);
        Assert.NotNull(run.World.Well);
        Assert.Contains(CellOf(run.World.Well.Position), run.Course.Stations[4].FloorCells);
    }

    [Fact]
    public void The_travel_choices_carry_the_names_of_the_stations_as_their_signs_do()
    {
        // Issue #722: the second teleporter stands at the fifth station, not before a boss door - not "Boss Gate".
        var run = new CourseRun(Seed);
        var far = run.World.Teleporters.Single(teleporter => teleporter.Plan.Site == TeleporterSite.BossGate);
        run.WalkToStation(4);
        run.WalkTo(run.World.Level.CellCenter(far.Plan.X, far.Plan.Z));

        var names = run.World.TravelChoices(run.World.Player).Select(TravelLayout.Name).ToList();

        Assert.Equal(2, names.Count);
        Assert.All(names, name => Assert.Contains(name, run.Course.Labels.Select(label => label.Text)));
        Assert.DoesNotContain(TeleporterLook.PlaceName(TeleporterSite.BossGate), names);
    }

    [Fact]
    public void A_station_whose_room_touches_another_without_a_wall_is_refused()
    {
        // Two antechambers close together: the second great hall starts in the column right after the first one ends.
        var stations = new[]
        {
            new CourseStation("Gap A", RoomKind.Start, 5, _ => { }),
            new CourseStation("Boss A", RoomKind.Antechamber, Antechamber.Width, room => room.BossRoomSouth()),
            new CourseStation("Gap B", RoomKind.Start, 8, _ => { }),
            new CourseStation("Boss B", RoomKind.Antechamber, Antechamber.Width, room => room.BossRoomSouth()),
        };

        var error = Assert.Throws<InvalidOperationException>(() => TestCourse.Build(Seed, Balance.Default, stations));

        Assert.Contains("touches", error.Message);
        Assert.Contains("Boss B", error.Message);
    }

    [Theory]
    [InlineData(4, 4, true)]    // over the corner: (3, 3) and (4, 4) touch diagonally
    [InlineData(4, 1, true)]    // side by side: column 3 and column 4
    [InlineData(5, 1, false)]   // one column of wall between them
    [InlineData(5, 5, false)]   // one cell of wall over the corner
    public void Two_rooms_need_a_wall_between_them_even_over_the_corner(int x0, int z0, bool touches)
    {
        var course = new CourseBuilder();
        course.AddRoom(RoomKind.Start, "First", 1, 1, 3, 3, 0);

        var second = Record.Exception(() => course.AddRoom(RoomKind.Start, "Second", x0, z0, x0 + 2, z0 + 2, 1));

        Assert.Equal(touches, second is InvalidOperationException { Message: var message } && message.Contains("touches"));
        Assert.Equal(touches ? 1 : 2, course.Rooms.Count);
    }

    [Fact]
    public void Starting_again_gives_the_course_from_the_start()
    {
        var run = new CourseRun(Seed);
        run.WalkToStation(1);

        var again = TestCourse.NextWorld(run.World);

        Assert.Equal(TestCourse.Name, again.LevelName);
        Assert.True(again.IsTestWorld);
        Assert.Equal(run.World.Seed, again.Seed);
        Assert.Equal(again.Level.PlayerStartPosition.X, again.Player.Position.X, 0.001f);
        Assert.All(again.Doors.Where(door => door.Kind != DoorKind.Cracked), door => Assert.False(door.IsOpen));
    }

    private static (int X, int Z) CellOf(Vector3 position) => Level.ToGrid(position.X, position.Z);

    private static Vector3 MiddleOf(Door door) => (door.BoxMin + door.BoxMax) / 2f;

    /// <summary>
    /// A player walking the course along its way: <see cref="Entered"/> holds the stations they have entered, in order,
    /// and <see cref="Events"/> everything that happened.
    /// </summary>
    private sealed class CourseRun
    {
        public CourseRun(int seed)
        {
            Course = TestCourse.Build(seed, Balance.Default);
            World = TestCourse.NewWorld(seed, Balance.Default);
            Note();
        }

        public CourseLayout Course { get; }

        public GameWorld World { get; }

        public List<int> Entered { get; } = [];

        public List<GameEvent> Events { get; } = [];

        /// <summary>The door on the way into station <paramref name="station"/> (from 1 on).</summary>
        public Door DoorInto(int station)
        {
            var first = Course.Stations[station].FloorCells.Min(cell => cell.X);
            return World.Doors.Single(door => door.X == first - 1 && door.Z == CourseRoom.Way);
        }

        /// <summary>
        /// Walks along the way into station <paramref name="station"/>: through each door before it (opened with F), past
        /// every enemy (struck down where it stands, unless <paramref name="killEnemies"/> is false for the last
        /// station), out of each arena only when its grates have risen. Stops one cell inside it.
        /// </summary>
        public void WalkToStation(int station, bool killEnemies = true)
        {
            for (var next = Entered.Max() + 1; next <= station; next++)
            {
                var door = DoorInto(next);
                LeaveArena(door);
                WalkTo(World.Level.CellCenter(door.X - 1, CourseRoom.Way));
                if (!door.IsOpen)
                {
                    LookAt(MiddleOf(door));
                    Run(PressF, 1);
                    Assert.True(door.IsOpen, $"The door into station {next + 1} did not open with F.");
                }

                if (killEnemies || next < station)
                {
                    KillEnemiesOf(Course.Stations[next].Index, hiddenToo: false);
                }

                WalkTo(World.Level.CellCenter(door.X + 1, CourseRoom.Way));
            }
        }

        /// <summary>To the end of the way, then through the middle of the portal into the great hall until it seals.</summary>
        public void WalkToTheBossRoom()
        {
            WalkToStation(TestCourse.Stations.Count - 1);
            var portal = World.Doors.Single(door => door.Kind == DoorKind.Boss);
            var middle = MiddleOf(portal);
            WalkTo(middle with { Z = (portal.Z - 1 + 0.5f) * Level.CellSize });
            LookAt(middle);
            Run(PressF, 1);
            Assert.True(portal.IsOpen, "The boss door did not open with F.");
            WalkTo(middle with { Z = (portal.Z + 3 + 0.5f) * Level.CellSize });
            Run(PlayerInput.None, Second / 2);
        }

        /// <summary>Walks (turning towards it each tick) until standing on <paramref name="spot"/>, then lets the player stop.</summary>
        public void WalkTo(Vector3 spot)
        {
            var player = World.Player;
            for (var tick = 0; new Vector2(spot.X - player.Position.X, spot.Z - player.Position.Z).Length() > 0.15f; tick++)
            {
                Assert.True(tick < 15 * Second, $"The player never got to {spot} (stuck at {player.Position}).");
                LookAt(spot with { Y = player.EyePosition.Y });
                Run(Forward, 1);
            }

            Run(PlayerInput.None, Second / 4);
        }

        /// <summary>Turns the player's view (through the mouse, as in the game) to look at <paramref name="point"/>.</summary>
        public void LookAt(Vector3 point)
        {
            var player = World.Player;
            var to = point - player.EyePosition;
            var yaw = MathF.Atan2(to.X, to.Z);
            var pitch = MathF.Atan2(to.Y, new Vector2(to.X, to.Z).Length());
            var turn = MathF.IEEERemainder(player.View.YawRadians - yaw, MathF.Tau);
            player.Look(PlayerInput.None with
            {
                Look = new Vector2(turn, player.View.PitchRadians - pitch) / ControlSettings.MouseSensitivity,
            });
        }

        /// <summary>Ticks <paramref name="ticks"/> times (or until <paramref name="until"/>); returns the events.</summary>
        public List<GameEvent> Run(PlayerInput input, int ticks, Func<bool>? until = null)
        {
            var events = new List<GameEvent>();
            for (var tick = 0; tick < ticks && !(until?.Invoke() ?? false); tick++)
            {
                World.Tick(input);
                events.AddRange(World.Events);
                Events.AddRange(World.Events);
                Note();
            }

            return events;
        }

        // In a sealed arena: strikes down each enemy as it comes out, until the room is cleared and the grate of the
        // door ahead has risen.
        private void LeaveArena(Door ahead)
        {
            var room = World.Rooms.FirstOrDefault(room => room.IsSealed && room.Index == ahead.RoomA);
            for (var tick = 0; room is not null && (!room.IsCleared || ahead.IsBarred); tick++)
            {
                Assert.True(tick < 30 * Second, $"The arena {room.Index} never let the player out.");
                KillEnemiesOf(room.Index, hiddenToo: false);
                Run(PlayerInput.None, 1);
            }
        }

        private void KillEnemiesOf(int roomIndex, bool hiddenToo)
        {
            foreach (var enemy in World.Rooms[roomIndex].Enemies)
            {
                switch (enemy)
                {
                    case Ghoul ghoul when ghoul.IsHittable || hiddenToo:
                        ghoul.TakeDamage(10_000, Events);
                        break;
                    case Bat bat when bat.IsHittable || hiddenToo:
                        bat.TakeDamage(10_000, Events);
                        break;
                }
            }
        }

        // Records the station the player stands in when it is a new one.
        private void Note()
        {
            var cell = CellOf(World.Player.Position);
            var station = Course.Stations.ToList().FindIndex(room => room.FloorCells.Contains(cell));
            if (station >= 0 && !Entered.Contains(station))
            {
                Entered.Add(station);
            }
        }
    }
}
