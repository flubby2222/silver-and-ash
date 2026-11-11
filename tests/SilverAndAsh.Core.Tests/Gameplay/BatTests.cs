using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

public class BatTests
{
    private static readonly int CorpseTicks = SimulationClock.SecondsToTicks(Tuning.Bat.CorpseTime);
    private static readonly Level Room = LevelParser.Parse("P.B\n...");

    private readonly List<GameEvent> _events = [];

    [Fact]
    public void B_is_floor_with_a_bat_above_it()
    {
        var level = LevelParser.Parse("P.B\nB..");

        Assert.False(level[2, 0].IsSolid);
        Assert.Equal([level.CellCenter(2, 0), level.CellCenter(0, 1)], level.BatPositions);
    }

    [Fact]
    public void Error_message_lists_the_bat_symbol()
    {
        Assert.Contains("'B'", Assert.Throws<LevelFormatException>(() => LevelParser.Parse("P4")).Message);
    }

    [Theory]
    [InlineData("assets/levels/test.txt")]
    [InlineData("assets/levels/heights.txt")]
    public void The_shipped_levels_have_bats(string path)
    {
        Assert.NotEmpty(LevelParser.ParseFile(RepositoryFiles.Find(path)).BatPositions);
    }

    [Fact]
    public void A_bat_hovers_at_its_height_and_stays_there()
    {
        var world = new GameWorld(Room);
        var bat = world.Bats[0];
        var floor = Room.CellCenter(2, 0);

        Assert.Equal(floor + new Vector3(0f, Tuning.Bat.HoverHeight, 0f), bat.Position);
        Assert.Equal(Tuning.Bat.MaxHealth, bat.Health);
        Assert.True(bat.IsHittable);

        Run(world, PlayerInput.None, 2 * SimulationClock.TicksPerSecond);

        Assert.Equal(floor.Y + Tuning.Bat.HoverHeight, bat.Position.Y, 0.001f);
    }

    [Fact]
    public void Under_a_low_ceiling_it_keeps_its_gap_to_the_ceiling()
    {
        // Above the high platform (floor 0.75 m) the hover height would put its top at 3.35 m - too close to the
        // ceiling (3.5 m minus the gap of 0.3 m = 3.2 m). It hovers lower there.
        var level = LevelParser.Parse("P3");
        var floor = level.CellCenter(1, 0);

        var position = Bat.HoverPosition(level, floor);

        var top = position.Y + Tuning.Bat.Height;
        Assert.True(top <= Cell.DefaultCeilingHeight - Tuning.Bat.CeilingGap + 0.001f, $"Top at {top} m.");
        Assert.True(position.Y > floor.Y);
    }

    [Fact]
    public void Damage_kills_it_once_and_is_reported()
    {
        var bat = new Bat(5, new Vector3(1f, 2.2f, 1f));

        bat.TakeDamage(20, _events);
        bat.TakeDamage(20, _events);
        bat.TakeDamage(20, _events);                        // already dead: ignored

        Assert.True(bat.IsDead);
        Assert.False(bat.IsHittable);
        Assert.Equal([new BatHit(5, 20), new BatHit(5, 10), new BatDied(5)], _events);
    }

    [Fact]
    public void A_dead_bat_falls_to_the_floor_and_disappears_later()
    {
        var world = new GameWorld(Room);
        var bat = world.Bats[0];
        bat.TakeDamage(Tuning.Bat.MaxHealth, []);

        Run(world, PlayerInput.None, SimulationClock.TicksPerSecond);
        Assert.Equal(Room.CellCenter(2, 0).Y, bat.Position.Y, 0.001f);
        Assert.False(bat.IsGone);

        Run(world, PlayerInput.None, CorpseTicks - SimulationClock.TicksPerSecond);
        Assert.True(bat.IsGone);
    }

    [Fact]
    public void The_pistol_shoots_it_out_of_the_air()
    {
        var world = new GameWorld(LevelParser.Parse("P\n.\n.\nB"));
        var bat = world.Bats[0];
        AimAt(world.Player, bat.Position + new Vector3(0f, Tuning.Bat.Height / 2f, 0f));

        world.Tick(PlayerInput.None with { Shoot = true });

        Assert.True(bat.IsDead);
        Assert.Contains(new BatDied(bat.Id), world.Events);
    }

    [Fact]
    public void The_blade_hits_it_when_it_is_close_above()
    {
        // The bat hovers 2 m ahead, 0.6 m above the eyes: in reach (1.7 m to its body). Looking up at it, it is
        // in the middle of the blade's cone. (Standing right below it, it would be too steep above the view.)
        var world = new GameWorld(LevelParser.Parse("P\nB"));
        var bat = world.Bats[0];
        AimAt(world.Player, bat.Position + new Vector3(0f, Tuning.Bat.Height / 2f, 0f));

        world.Tick(PlayerInput.None with { Melee = true });
        world.Tick(PlayerInput.None);

        Assert.True(bat.IsDead);
    }

    [Fact]
    public void The_player_walks_underneath_a_bat()
    {
        var world = new GameWorld(LevelParser.Parse("P\nB\n.\n."));

        Run(world, PlayerInput.None with { Move = new Vector2(0f, 1f) }, SimulationClock.TicksPerSecond);

        Assert.True(world.Player.Position.Z > world.Bats[0].Position.Z, "The bat blocked the way on the ground.");
    }

    [Fact]
    public void Bats_get_numbers_after_dummies_and_ghouls()
    {
        var world = new GameWorld(LevelParser.Parse("PDG\nB.."));

        var ids = world.Targets.Select(target => target.Id).ToList();

        Assert.Equal(3, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    private static void AimAt(Player player, Vector3 point)
    {
        var direction = Vector3.Normalize(point - player.EyePosition);
        player.Look(PlayerInput.None with
        {
            Look = new Vector2(
                -(MathF.Atan2(direction.X, direction.Z) - player.View.YawRadians) / Tuning.Controls.MouseSensitivity,
                -(MathF.Asin(direction.Y) - player.View.PitchRadians) / Tuning.Controls.MouseSensitivity),
        });
    }

    private static void Run(GameWorld world, PlayerInput input, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            world.Tick(input);
        }
    }
}
