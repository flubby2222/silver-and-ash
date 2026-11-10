using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Gameplay;

/// <summary>Stairs, platforms and pits - the level format from #34 together with the physics from #21.</summary>
public class PlayerHeightTests
{
    private const float Tolerance = 0.001f;

    // The player looks along +Z at the start, which is "down" in the level text.
    private static readonly PlayerInput Forward = PlayerInput.None with { Move = new Vector2(0f, 1f) };
    private static readonly PlayerInput ForwardAndJump = Forward with { Jump = true };

    [Fact]
    public void A_staircase_can_be_walked_up_without_jumping()
    {
        var player = new Player(LevelParser.Parse("P\n1\n2\n3"));

        RunTicks(player, Forward, 2 * SimulationClock.TicksPerSecond);

        Assert.Equal(0.75f, player.Position.Y, Tolerance);
        Assert.True(player.IsGrounded);
    }

    [Fact]
    public void A_high_platform_blocks_walking()
    {
        var player = new Player(LevelParser.Parse("P\n.\n3"));

        RunTicks(player, Forward, 2 * SimulationClock.TicksPerSecond);

        Assert.Equal(0f, player.Position.Y, Tolerance);
        Assert.Equal(4f - Tuning.Player.Radius, player.Position.Z, Tolerance);
    }

    [Fact]
    public void A_high_platform_can_be_reached_with_a_jump()
    {
        var player = new Player(LevelParser.Parse("P\n.\n3"));
        RunTicks(player, Forward, 2 * SimulationClock.TicksPerSecond);   // stand in front of the edge

        RunTicks(player, ForwardAndJump, 1);                               // fresh jump press ...
        RunTicks(player, Forward, SimulationClock.TicksPerSecond);         // ... and keep walking

        Assert.Equal(0.75f, player.Position.Y, Tolerance);
        Assert.True(player.Position.Z > 4f);
    }

    [Fact]
    public void Walking_over_a_pit_drops_the_player_in()
    {
        var player = new Player(LevelParser.Parse("P\n_\n_\n."));

        RunTicks(player, Forward, SimulationClock.TicksPerSecond);

        Assert.Equal(LevelParser.PitFloorHeight, player.Position.Y, Tolerance);
    }

    [Fact]
    public void The_pit_edge_blocks_walking_out()
    {
        var player = new Player(LevelParser.Parse("P\n_\n_\n."));

        RunTicks(player, Forward, 3 * SimulationClock.TicksPerSecond);

        Assert.Equal(LevelParser.PitFloorHeight, player.Position.Y, Tolerance);
        Assert.Equal(6f - Tuning.Player.Radius, player.Position.Z, Tolerance);
    }

    [Fact]
    public void A_jump_gets_the_player_out_of_the_pit()
    {
        var player = new Player(LevelParser.Parse("P\n_\n_\n."));
        RunTicks(player, Forward, 3 * SimulationClock.TicksPerSecond);   // standing in the pit at the far edge

        RunTicks(player, ForwardAndJump, 1);
        RunTicks(player, Forward, SimulationClock.TicksPerSecond);

        Assert.Equal(0f, player.Position.Y, Tolerance);
        Assert.True(player.Position.Z > 6f);
    }

    [Fact]
    public void The_shipped_heights_level_is_valid_and_has_all_heights()
    {
        var level = LevelParser.ParseFile(RepositoryFiles.Find("assets/levels/heights.txt"));
        var floors = new HashSet<float>();
        for (var z = 0; z < level.Depth; z++)
        {
            for (var x = 0; x < level.Width; x++)
            {
                if (!level[x, z].IsSolid)
                {
                    floors.Add(level[x, z].FloorHeight);
                }
            }
        }

        Assert.Equal(new HashSet<float> { -1f, 0f, 0.25f, 0.5f, 0.75f }, floors);
    }

    private static void RunTicks(Player player, PlayerInput input, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            player.Tick(input);
        }
    }
}
