using System.Numerics;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>
/// The dust of a breaking wall (issue #614): a soft cloud like the column's, timed in ticks, thin enough that the stone
/// chunks stay in sight. The wall is the one the photo scene breaks with a charged blow, seen from where the player stood.
/// </summary>
public class WallBreakDustTests
{
    private static readonly Lazy<Break> Struck = new(StrikeTheWall);

    [Fact]
    public void The_dust_starts_with_the_break_and_is_gone_after_its_ticks()
    {
        Assert.Empty(Puffs(-1));
        Assert.Contains(Enumerable.Range(0, WallBreakDust.MaxDelayTicks), tick => Puffs(tick).Count > 0);

        // Every puff has started by the latest delay and none has ended yet.
        Assert.Equal(WallBreakDust.PuffCount, Puffs(WallBreakDust.MaxDelayTicks).Count);
        Assert.Empty(Puffs(WallBreakDust.Ticks));

        // The photo of the wall 100 ticks after the blow shows it settled.
        Assert.True(WallBreakDust.Ticks <= 100);
    }

    [Fact]
    public void The_same_tick_brings_the_same_puffs_and_another_wall_other_ones()
    {
        var wall = Struck.Value;
        for (var tick = 0; tick < WallBreakDust.Ticks; tick += 7)
        {
            Assert.Equal(Puffs(tick), Puffs(tick));
        }

        var other = new List<DustPuff>();
        WallBreakDust.Puffs(30, wall.Id + 1, wall.Door.BoxMin, wall.Door.BoxMax, wall.Away, other);
        Assert.NotEqual(Puffs(30), other);
    }

    [Fact]
    public void The_puffs_start_in_the_wall_and_drift_away_from_the_striker_and_up()
    {
        var wall = Struck.Value;
        var middle = (wall.Door.BoxMin + wall.Door.BoxMax) / 2f;

        // At its first tick a puff has barely moved from its spot in the wall.
        foreach (var puff in Puffs(0))
        {
            var inside = Vector3.Clamp(puff.Position, wall.Door.BoxMin, wall.Door.BoxMax);
            Assert.True(Vector3.Distance(inside, puff.Position) < 0.05f, $"A puff starts at {puff.Position}, outside the wall.");
        }

        var early = Mean(Puffs(WallBreakDust.MaxDelayTicks));
        var late = Mean(Puffs(WallBreakDust.MaxDelayTicks + 40));
        Assert.True(Vector3.Dot(late - early, wall.Away) > 0.2f, "The cloud does not drift away from the striker.");
        Assert.True(Vector3.Dot(late - middle, wall.Away) > 0f, "The cloud ends up on the striker's side of the wall.");
        Assert.True(late.Y > early.Y, "The cloud does not rise.");
    }

    [Fact]
    public void A_puff_swells_then_thins_out_and_grows_the_whole_time()
    {
        for (var tick = 0; tick < WallBreakDust.Ticks; tick++)
        {
            foreach (var puff in Puffs(tick))
            {
                Assert.InRange(puff.Opacity, 0f, WallBreakDust.Density);
                Assert.InRange(puff.Radius, 0.1f, 1.5f);
            }
        }

        // Thin at the start, dense once every puff has swollen, faint at the end; larger later.
        var first = Puffs(0);
        var swollen = Puffs(WallBreakDust.MaxDelayTicks + WallBreakDust.SwellTicks);
        var later = Puffs(WallBreakDust.MaxDelayTicks + 40);
        var faint = Puffs(Enumerable.Range(0, WallBreakDust.Ticks).Last(tick => Puffs(tick).Count > 0));
        Assert.True(first.Max(puff => puff.Opacity) < 0.5f * WallBreakDust.Density);
        Assert.True(swollen.Max(puff => puff.Opacity) > 0.5f * WallBreakDust.Density);
        Assert.True(faint.Max(puff => puff.Opacity) < 0.05f);
        Assert.True(later.Average(puff => puff.Radius) > swollen.Average(puff => puff.Radius));
    }

    [Fact]
    public void The_cloud_is_seen_but_the_chunks_in_and_behind_it_still_show_through()
    {
        // Where the chunks are: the wall itself and up to 2 m beyond it, along the way they fly.
        var wall = Struck.Value;
        var size = wall.Door.BoxMax - wall.Door.BoxMin;
        var targets = new List<Vector3>();
        for (var along = 0f; along <= 2f; along += 0.5f)
        {
            for (var u = 0.1f; u < 1f; u += 0.2f)
            {
                for (var v = 0.1f; v < 1f; v += 0.2f)
                {
                    targets.Add(wall.Door.BoxMin + new Vector3(u * size.X, v * size.Y, u * size.Z) + (wall.Away * along));
                }
            }
        }

        var densest = 0f;
        for (var tick = 0; tick < WallBreakDust.Ticks; tick++)
        {
            var puffs = Puffs(tick);
            densest = MathF.Max(densest, targets.Max(target => DustCloud.Cover(wall.Eye, target, puffs)));
        }

        // Plainly seen where it is thickest (half of what lies behind hidden), and never more than 65 % hidden.
        Assert.InRange(densest, 0.5f, 0.65f);
    }

    private static List<DustPuff> Puffs(int ticksSinceBreak)
    {
        var wall = Struck.Value;
        var puffs = new List<DustPuff>();
        WallBreakDust.Puffs(ticksSinceBreak, wall.Id, wall.Door.BoxMin, wall.Door.BoxMax, wall.Away, puffs);
        return puffs;
    }

    private static Vector3 Mean(List<DustPuff> puffs) => puffs.Aggregate(Vector3.Zero, (sum, puff) => sum + puff.Position) / puffs.Count;

    // The photo scene's charged blow on the cracked wall, tick by tick until it breaks: the wall, where the player's eye
    // was, and the flat direction from the player to the wall's middle (as the game's view takes it).
    private static Break StrikeTheWall()
    {
        var studio = new PhotoStudio(RoomTemplateParser.LoadAll(RepositoryFiles.Find("assets/rooms")), Balance.Default);
        var setup = PhotoScenes.Find("cracked-wall-break")!.Build(studio);
        Break? struck = null;
        setup.Run(() =>
        {
            foreach (var broken in setup.World.Events.OfType<WallBroken>())
            {
                var door = setup.World.Doors[broken.DoorId];
                var player = setup.World.Player;
                var away = Vector3.Normalize((((door.BoxMin + door.BoxMax) / 2f) - player.Position) with { Y = 0f });
                struck ??= new Break(broken.DoorId, door, player.EyePosition, away);
            }
        });

        return struck ?? throw new InvalidOperationException("The scene did not break the wall.");
    }

    private sealed record Break(int Id, Door Door, Vector3 Eye, Vector3 Away);
}
