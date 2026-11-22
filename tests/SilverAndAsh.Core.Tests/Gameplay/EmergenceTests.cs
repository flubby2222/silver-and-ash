using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>
/// Issue #424: the enemies of a combat room are not there until it seals - then they come out of the floor (ghouls)
/// or the ceiling (bats), away from the doors. Before, they stood in the room already, could be shot through the open
/// door and lured out - and the sealed room never cleared (softlock). Checked over many generated levels.
/// </summary>
public class EmergenceTests
{
    private const int Levels = 100;

    private static readonly LevelZero LevelZero = new(RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms")));

    private static readonly Balance Balance = Balance.Default;

    private static readonly int EmergeTicks = SimulationClock.SecondsToTicks(Balance.Difficulty.EmergeTime);

    private static (int X, int Z) Cell(Vector3 position) => Level.ToGrid(position.X, position.Z);

    private static float Metres((int X, int Z) a, (int X, int Z) b) =>
        Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z)) * Level.CellSize;

    // The ghouls and bats that start in a room.
    private static List<ITarget> EnemiesOf(GameWorld world, RoomState room) =>
        world.Ghouls.Cast<ITarget>().Concat(world.Bats).Where(enemy => room.Cells.Contains(Cell(enemy.Body.Position))).ToList();

    private static bool IsHidden(ITarget enemy) => enemy is Ghoul { Emergence.IsHidden: true } or Bat { Emergence.IsHidden: true };

    private static bool IsEmerging(ITarget enemy) => enemy is Ghoul { Emergence.IsEmerging: true } or Bat { Emergence.IsEmerging: true };

    private static bool IsDead(ITarget enemy) => enemy is Ghoul { IsDead: true } or Bat { IsDead: true };

    private static List<(int X, int Z)> DoorCells(GameWorld world, RoomState room) =>
        room.DoorIds.Select(id => (world.Doors[id].X, world.Doors[id].Z)).ToList();

    // The same level, but the player starts in the cell of the room farthest from its doors (and from its enemies):
    // the first tick they stand in it with the whole body, so it seals.
    private static GameWorld StartingIn(GeneratedLevel generated, int seed, PlacedRoom room)
    {
        var level = generated.Level;
        var doors = generated.Doors.Where(door => door.RoomA == room.Index || door.RoomB == room.Index).Select(door => (door.X, door.Z)).ToList();
        var enemies = level.GhoulPositions.Concat(level.BatPositions).Select(Cell).ToHashSet();
        var start = room.FloorCells
            .Where(cell => !enemies.Contains(cell))
            .OrderByDescending(cell => doors.Min(door => Metres(cell, door)))
            .ThenByDescending(cell => enemies.Count == 0 ? 0f : enemies.Min(enemy => Metres(cell, enemy)))
            .First();

        return StartingAt(generated, seed, start, Balance);
    }

    // The same level, but the player starts in the middle of the cell `start`.
    private static GameWorld StartingAt(GeneratedLevel generated, int seed, (int X, int Z) start, Balance balance)
    {
        var level = generated.Level;
        var cells = new Cell[level.Width, level.Depth];
        for (var x = 0; x < level.Width; x++)
        {
            for (var z = 0; z < level.Depth; z++)
            {
                cells[x, z] = level[x, z];
            }
        }

        var moved = new Level(
            cells, start.X, start.Z, level.DummyPositions.Select(Cell), level.GhoulPositions.Select(Cell),
            level.BatPositions.Select(Cell), level.BossPositions.Select(Cell));
        return new GameWorld(moved, seed, balance, generated.Doors, generated.Rooms, generated.Chests);
    }

    private static float FlatMetres(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));

    [Fact]
    public void An_enemy_never_comes_out_closer_than_the_player_distance_even_when_a_player_stands_on_its_start_cell()
    {
        // Issue #445: the worst case - a player stands on the start cell of one enemy of the room when it seals.
        // Before, that enemy came out inside the player. Now it comes out on the nearest free cell that keeps the
        // distance to every player and to the doors; the others stay where they are, and the room still clears.
        var least = Balance.Difficulty.PlayerDistance;
        var moved = 0;
        for (var seed = 0; seed < Levels; seed++)
        {
            var generated = LevelZero.Build(seed, Balance);
            foreach (var plan in generated.Rooms.Where(room => room.Kind == RoomKind.Combat))
            {
                var probe = StartingIn(generated, seed, plan);
                var first = EnemiesOf(probe, probe.Rooms[plan.Index]).OrderBy(enemy => enemy.Id).First();
                var world = StartingAt(generated, seed, Cell(first.Body.Position), Balance);
                var room = world.Rooms[plan.Index];
                var enemies = EnemiesOf(world, room);
                var starts = enemies.ToDictionary(enemy => enemy.Id, enemy => enemy.Body.Position);
                var doors = DoorCells(world, room);

                world.Tick(PlayerInput.None);

                var where = $"Seed {seed}, room {room.Index}";
                Assert.True(room.IsSealed, $"{where}: not sealed.");
                Assert.All(world.Players, player => Assert.All(enemies, enemy => Assert.True(
                    FlatMetres(enemy.Body.Position, player.Position) >= least,
                    $"{where}: enemy {enemy.Id} comes out {FlatMetres(enemy.Body.Position, player.Position)} m from a player.")));
                Assert.All(enemies, enemy =>
                {
                    Assert.True(IsEmerging(enemy), $"{where}: enemy {enemy.Id} does not come out.");
                    Assert.Contains(Cell(enemy.Body.Position), room.Cells);
                    Assert.All(doors, door => Assert.True(
                        Metres(Cell(enemy.Body.Position), door) >= Balance.Difficulty.DoorDistance,
                        $"{where}: enemy {enemy.Id} comes out {Metres(Cell(enemy.Body.Position), door)} m from a door."));

                    // Who was far enough stays on its start cell.
                    if (world.Players.All(player => FlatMetres(starts[enemy.Id], player.Position) >= least))
                    {
                        Assert.Equal(starts[enemy.Id], enemy.Body.Position);
                    }
                });
                Assert.Equal(enemies.Count, enemies.Select(enemy => Cell(enemy.Body.Position)).Distinct().Count());
                moved += enemies.Count(enemy => enemy.Body.Position != starts[enemy.Id]);

                for (var tick = 0; tick < EmergeTicks; tick++)
                {
                    world.Tick(PlayerInput.None);
                }

                foreach (var enemy in enemies)
                {
                    Assert.True(enemy.IsHittable, $"{where}: enemy {enemy.Id} is not out after the emerge time.");
                    enemy.TakeDamage(10_000, []);
                }

                world.Tick(PlayerInput.None);
                Assert.True(room.IsCleared, $"{where}: never cleared.");
            }
        }

        // The case really happens: over all rooms, at least one enemy moved. (That the enemy under the player moved in
        // each room follows from the distance check above.)
        Assert.True(moved > 0);
    }

    [Fact]
    public void An_enemy_that_gives_way_to_a_player_does_not_come_out_beside_a_door()
    {
        // Issue #490: where an enemy comes out instead of its start cell, the same rule holds as for the start cells -
        // straight in front of a door the door distance, beside it the side distance.
        var difficulty = Balance.Difficulty;
        var moved = 0;
        for (var seed = 0; seed < Levels; seed++)
        {
            var generated = LevelZero.Build(seed, Balance);
            foreach (var plan in generated.Rooms.Where(room => room.Kind == RoomKind.Combat))
            {
                var probe = StartingIn(generated, seed, plan);
                var first = EnemiesOf(probe, probe.Rooms[plan.Index]).OrderBy(enemy => enemy.Id).First();
                var world = StartingAt(generated, seed, Cell(first.Body.Position), Balance);
                var room = world.Rooms[plan.Index];
                var enemies = EnemiesOf(world, room);
                var starts = enemies.ToDictionary(enemy => enemy.Id, enemy => enemy.Body.Position);
                var doors = DoorCells(world, room);
                var floor = room.Cells.ToHashSet();

                world.Tick(PlayerInput.None);

                foreach (var enemy in enemies.Where(enemy => enemy.Body.Position != starts[enemy.Id]))
                {
                    moved++;
                    var cell = Cell(enemy.Body.Position);
                    Assert.All(doors, door => Assert.False(
                        World.DoorSides.Breaks(door, cell, floor, difficulty),
                        $"Seed {seed}, room {room.Index}: enemy {enemy.Id} comes out {World.DoorSides.From(door, cell, floor)} (metres, degrees) from a door."));
                }
            }
        }

        Assert.True(moved > 100, $"Only {moved} enemies gave way.");
    }

    [Fact]
    public void Without_any_cell_far_enough_an_enemy_comes_out_on_the_free_cell_farthest_from_the_players()
    {
        // A player distance no room can keep: every enemy has to give way, each to the farthest cell still free
        // (away from the doors). So no free cell is left that lies farther from the player than one of them.
        var balance = Balance with { Difficulty = Balance.Difficulty with { PlayerDistance = 1000f } };
        for (var seed = 0; seed < 20; seed++)
        {
            var generated = LevelZero.Build(seed, balance);
            foreach (var plan in generated.Rooms.Where(room => room.Kind == RoomKind.Combat))
            {
                var probe = StartingIn(generated, seed, plan);
                var first = EnemiesOf(probe, probe.Rooms[plan.Index]).OrderBy(enemy => enemy.Id).First();
                var world = StartingAt(generated, seed, Cell(first.Body.Position), balance);
                var room = world.Rooms[plan.Index];
                var enemies = EnemiesOf(world, room);
                var doors = DoorCells(world, room);

                world.Tick(PlayerInput.None);

                var where = $"Seed {seed}, room {room.Index}";
                var player = world.Player.Position;
                var occupied = enemies.Select(enemy => Cell(enemy.Body.Position)).ToHashSet();
                // Clear of the doors as issue #490 has it: beside a door the side distance, straight ahead the door distance.
                var floor = room.Cells.ToHashSet();
                var free = room.Cells
                    .Where(cell => !occupied.Contains(cell))
                    .Where(cell => doors.All(door => !World.DoorSides.Breaks(door, cell, floor, balance.Difficulty)))
                    .ToList();
                var nearestEnemy = enemies.Min(enemy => FlatMetres(enemy.Body.Position, player));
                Assert.Equal(enemies.Count, occupied.Count);
                Assert.All(free, cell => Assert.True(
                    FlatMetres(world.Level.CellCenter(cell.X, cell.Z), player) <= nearestEnemy,
                    $"{where}: cell {cell} is free and farther from the player than an enemy ({nearestEnemy} m)."));
            }
        }
    }

    [Fact]
    public void Before_a_combat_room_seals_none_of_its_enemies_is_there()
    {
        // The softlock began with an enemy that could be hit (or seen moving) through the open door before the room
        // sealed. Now there is nothing to hit: hidden in floor and ceiling, motionless, not a target.
        for (var seed = 0; seed < Levels; seed++)
        {
            var world = LevelZero.NewRun(seed, Balance);
            var combat = world.Rooms.Where(room => room.Kind == RoomKind.Combat).ToList();
            var enemies = combat.SelectMany(room => EnemiesOf(world, room)).ToList();
            var starts = enemies.Select(enemy => enemy.Body.Position).ToList();

            for (var tick = 0; tick < 120; tick++)
            {
                world.Tick(PlayerInput.None);
            }

            Assert.NotEmpty(enemies);
            Assert.All(enemies, enemy => Assert.True(IsHidden(enemy), $"Seed {seed}: enemy {enemy.Id} is there before its room sealed."));
            Assert.All(enemies, enemy => Assert.False(enemy.IsHittable, $"Seed {seed}: enemy {enemy.Id} can be hit before its room sealed."));
            Assert.Equal(starts, enemies.Select(enemy => enemy.Body.Position));
            Assert.All(combat, room => Assert.Equal(1, room.WaveCount));
            Assert.All(combat, room => Assert.Equal(0, room.WavesReleased));
        }
    }

    [Fact]
    public void After_sealing_every_enemy_of_the_room_comes_out_in_the_room_away_from_its_doors_and_the_room_can_be_cleared()
    {
        for (var seed = 0; seed < Levels; seed++)
        {
            var generated = LevelZero.Build(seed, Balance);
            foreach (var plan in generated.Rooms.Where(room => room.Kind == RoomKind.Combat))
            {
                var world = StartingIn(generated, seed, plan);
                var room = world.Rooms[plan.Index];
                var enemies = EnemiesOf(world, room);
                var doors = DoorCells(world, room);

                world.Tick(PlayerInput.None);
                var events = world.Events.ToList();

                var where = $"Seed {seed}, room {room.Index}";
                Assert.True(room.IsSealed, $"{where}: not sealed.");
                Assert.Contains(new RoomSealed(room.Index), events);
                var wave = Assert.Single(events.OfType<WaveEmerged>());
                Assert.Equal(enemies.Select(enemy => enemy.Id).Order(), wave.EnemyIds.Order());

                // Coming out: in the room, away from every door, neither acting nor hittable.
                Assert.All(enemies, enemy =>
                {
                    Assert.True(IsEmerging(enemy), $"{where}: enemy {enemy.Id} does not come out.");
                    Assert.False(enemy.IsHittable, $"{where}: enemy {enemy.Id} can be hit while coming out.");
                    Assert.Contains(Cell(enemy.Body.Position), room.Cells);
                    Assert.All(doors, door => Assert.True(
                        Metres(Cell(enemy.Body.Position), door) >= Balance.Difficulty.DoorDistance,
                        $"{where}: enemy {enemy.Id} comes out {Metres(Cell(enemy.Body.Position), door)} m from a door."));
                });

                for (var tick = 0; tick < EmergeTicks; tick++)
                {
                    world.Tick(PlayerInput.None);
                }

                Assert.All(enemies, enemy => Assert.True(enemy.IsHittable, $"{where}: enemy {enemy.Id} is not out after the emerge time."));
                Assert.All(enemies, enemy => Assert.Contains(Cell(enemy.Body.Position), room.Cells));

                // A simple bot clears the room: whatever has come out dies, wave after wave, until the grates rise.
                for (var round = 0; round < 10 && !room.IsCleared; round++)
                {
                    foreach (var enemy in enemies.Where(enemy => enemy.IsHittable))
                    {
                        enemy.TakeDamage(10_000, []);
                    }

                    // One tick for the room to notice; if a next wave comes out, until it is out.
                    world.Tick(PlayerInput.None);
                    for (var tick = 0; tick < EmergeTicks && enemies.Any(IsEmerging); tick++)
                    {
                        world.Tick(PlayerInput.None);
                    }
                }

                Assert.True(room.IsCleared, $"{where}: never cleared.");
                Assert.All(enemies, enemy => Assert.True(IsDead(enemy)));

                // Issue #558: the grates of a combat room rise over their own time - free once they are all the way up.
                for (var tick = 0; tick < Balance.Grates.LiftTicks; tick++)
                {
                    world.Tick(PlayerInput.None);
                }

                Assert.All(room.DoorIds, id => Assert.False(world.Doors[id].IsBarred));
            }
        }
    }

    [Fact]
    public void The_boss_room_is_as_before_Gorm_stands_there_from_the_start()
    {
        var world = LevelZero.NewRun(7, Balance);

        Assert.NotNull(world.Gorm);
        Assert.True(world.Gorm!.IsHittable);
        Assert.Equal(0, world.Rooms.Single(room => room.Kind == RoomKind.Boss).WaveCount);
    }
}
