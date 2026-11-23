using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Physics;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// Issue #611, tick by tick in boss rooms as the game generates them: a broken column's pieces fall towards whoever
/// struck its first blow (Gorm), and none of them comes to rest in a wall - each lies on the floor of the boss room,
/// at least a column's radius (a piece is no thicker than the column) from the nearest wall.
/// </summary>
public class ColumnFallInTheBossRoomTests
{
    private static readonly int Second = SimulationClock.TicksPerSecond;
    private static readonly IReadOnlyList<RoomTemplate> Templates = RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms"));

    /// <summary>A player who survives a lot - to let Gorm strike until a column breaks.</summary>
    private static readonly Balance Tough = Balance.Default with { Player = Balance.Default.Player with { MaxHealth = 100_000 } };

    public static TheoryData<int> Seeds => new(Enumerable.Range(1, 6));

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Every_column_of_the_boss_room_breaks_towards_Gorm_and_no_piece_comes_to_rest_in_a_wall(int seed)
    {
        var generated = new LevelZero(Templates).Build(seed, Balance.Default);
        var boss = generated.Rooms.Single(room => room.Kind == RoomKind.Boss);
        var floor = boss.FloorCells.ToHashSet();
        var gorm = generated.Level.BossPositions.Single();
        var gormCell = Level.ToGrid(gorm.X, gorm.Z);
        var columns = generated.Level.Columns;
        Assert.NotEmpty(columns);

        // For every column one fight: the player stands behind it, seen from Gorm, so Gorm strikes it first.
        var fallen = new HashSet<Column>();
        foreach (var column in columns)
        {
            var away = Vector2.Normalize(column.Centre - new Vector2(gorm.X, gorm.Z)) * 3f;
            var start = Level.ToGrid(column.X + away.X, column.Z + away.Y);
            Assert.Contains(start, floor);
            var level = LevelFrom(generated.Level, start, gormCell);
            var world = new GameWorld(level, seed, Tough);
            var looks = new ColumnLooks();

            long? brokeAt = null;
            Vector3? firstStriker = null;
            for (var tick = 0; tick < 120 * Second && (brokeAt is null || world.Ticks - brokeAt < ColumnBreakLook.SwapTick); tick++)
            {
                world.Tick(PlayerInput.None);
                foreach (var gameEvent in world.Events)
                {
                    looks.OnEvent(gameEvent, world.Ticks, world.Gorm?.Position);
                    if (gameEvent is ColumnHit hit && hit.Column == column)
                    {
                        firstStriker ??= world.Gorm!.Position;
                    }

                    if (gameEvent is ColumnBroke broke && broke.Column == column)
                    {
                        brokeAt = world.Ticks;
                    }
                }
            }

            Assert.True(brokeAt is not null, $"Seed {seed}: the column at {column.Centre} never broke (player at {start}).");
            var look = looks.Of(column, world.Ticks);
            Assert.Equal(ColumnStage.Broken, look.Stage);
            Assert.Equal(ColumnBreakLook.YawToward(column.Centre, firstStriker!.Value), look.Yaw);
            fallen.Add(column);

            // Where the pieces lie when the fall ends: the last tick of the falling model, placed as the game draws it.
            var cell = level.CellAt(new Vector3(column.X, 0f, column.Z));
            var walls = level.WithColumns([]);
            var figure = ColumnBreakLook.Figure(new Vector3(column.X, cell.FloorHeight, column.Z), cell.CeilingHeight - cell.FloorHeight, look.Yaw);
            for (var piece = 0; piece < ColumnBreakLook.FallPieces.Count; piece++)
            {
                var lies = Vector3.Transform(ColumnBreakLook.PieceMiddle(piece, ColumnBreakLook.SwapTick - 1), figure);
                var name = $"Seed {seed}, tick {brokeAt}: {ColumnBreakLook.FallPieces[piece]} of the column at {column.Centre}, Gorm at {firstStriker}, lies at {lies}";
                Assert.True(floor.Contains(Level.ToGrid(lies.X, lies.Z)), $"{name} - not on the boss room's floor.");
                Assert.False(CharacterCollision.Overlaps(walls, lies with { Y = cell.FloorHeight }, Column.Radius), $"{name} - in a wall.");
            }
        }

        Assert.Equal(columns.Count, fallen.Count);
    }

    // The generated level with its columns, the player at start and Gorm at his place - nobody else.
    private static Level LevelFrom(Level level, (int X, int Z) start, (int X, int Z) gorm)
    {
        var cells = new Cell[level.Width, level.Depth];
        for (var x = 0; x < level.Width; x++)
        {
            for (var z = 0; z < level.Depth; z++)
            {
                cells[x, z] = level[x, z];
            }
        }

        return new Level(cells, start.X, start.Z, bossCells: [gorm]).WithColumns(level.Columns);
    }
}
