using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Tests.Presentation;

/// <summary>Issue #447: a ghoul's eyes glow red while it draws back for a blow - only then, and each ghoul on its own.</summary>
public class GhoulEyesTests
{
    private static readonly int WindUpTicks = SimulationClock.SecondsToTicks(Balance.Default.Ghoul.WindUpTime);
    private static readonly int Second = SimulationClock.TicksPerSecond;

    [Fact]
    public void The_eyes_glow_exactly_from_the_wind_up_to_the_blow()
    {
        var world = new GameWorld(LevelParser.Parse("P..G"));
        var ghoul = world.Ghouls[0];

        // Crawling up: no glow.
        var ticks = 0;
        while (!world.Events.Any(e => e is GhoulWindUp) && ticks++ < 3 * Second)
        {
            Assert.False(ghoul.IsAnnouncingBlow);
            Assert.Null(GhoulEyes.GlowStep(ghoul));
            world.Tick(PlayerInput.None);
        }

        Assert.Contains(world.Events, e => e is GhoulWindUp);

        // From the tick it starts drawing back until the blow: glowing, every tick of the wind-up.
        var glowing = 0;
        while (!world.Events.Any(e => e is GhoulStruck) && glowing <= WindUpTicks)
        {
            Assert.True(ghoul.IsAnnouncingBlow);
            Assert.NotNull(GhoulEyes.GlowStep(ghoul));
            glowing++;
            world.Tick(PlayerInput.None);
        }

        Assert.Equal(WindUpTicks, glowing);

        // The blow has fallen: resting, the eyes are dark again.
        Assert.Equal(GhoulState.Recovering, ghoul.State);
        Assert.False(ghoul.IsAnnouncingBlow);
        Assert.Null(GhoulEyes.GlowStep(ghoul));
    }

    [Fact]
    public void Each_ghoul_glows_on_its_own()
    {
        // Two ghouls at 4 m and 8 m: the nearer one draws back first, while the other is still crawling.
        var world = new GameWorld(LevelParser.Parse("G.P...G"));
        var windingUp = new HashSet<int>();
        var onlyOne = 0;

        // Until the player falls: then the ghouls lose interest and stop mid-swing (no blow, no event).
        for (var tick = 0; tick < 6 * Second; tick++)
        {
            world.Tick(PlayerInput.None);
            if (world.Player.IsDead)
            {
                break;
            }

            foreach (var gameEvent in world.Events)
            {
                switch (gameEvent)
                {
                    case GhoulWindUp windUp:
                        windingUp.Add(windUp.GhoulId);
                        break;
                    case GhoulStruck struck:
                        windingUp.Remove(struck.GhoulId);
                        break;
                }
            }

            foreach (var ghoul in world.Ghouls)
            {
                Assert.Equal(windingUp.Contains(ghoul.Id), ghoul.IsAnnouncingBlow);
            }

            if (world.Ghouls.Count(ghoul => ghoul.IsAnnouncingBlow) == 1)
            {
                onlyOne++;
            }
        }

        // The test only means something if one glowed while the other did not.
        Assert.True(onlyOne > 0, "Never only one ghoul glowing.");
    }

    [Fact]
    public void A_ghoul_killed_while_drawing_back_does_not_glow()
    {
        var world = new GameWorld(LevelParser.Parse("P..G"));
        var ghoul = world.Ghouls[0];
        for (var i = 0; i < 3 * Second && !ghoul.IsAnnouncingBlow; i++)
        {
            world.Tick(PlayerInput.None);
        }

        Assert.True(ghoul.IsAnnouncingBlow);

        ghoul.TakeDamage(Balance.Default.Ghoul.MaxHealth, []);
        world.Tick(PlayerInput.None);

        Assert.Equal(GhoulState.WindingUp, ghoul.State);     // its state stays where death caught it ...
        Assert.False(ghoul.IsAnnouncingBlow);                // ... but the eyes of the body are dark
        Assert.Null(GhoulEyes.GlowStep(ghoul));
    }

    [Fact]
    public void The_glow_brightens_in_hard_steps_of_blood_red_towards_the_blow()
    {
        var world = new GameWorld(LevelParser.Parse("P..G"));
        var ghoul = world.Ghouls[0];
        for (var i = 0; i < 3 * Second && !ghoul.IsAnnouncingBlow; i++)
        {
            world.Tick(PlayerInput.None);
        }

        var steps = new List<int>();
        while (GhoulEyes.GlowStep(ghoul) is { } step)
        {
            steps.Add(step);
            world.Tick(PlayerInput.None);
        }

        // Only the steps of the ramp, each one for a third of the wind-up, darkest first.
        Assert.Equal(WindUpTicks, steps.Count);
        Assert.Equal(GhoulEyes.BloodRedSteps, steps.Distinct());
        foreach (var step in GhoulEyes.BloodRedSteps)
        {
            Assert.Equal(WindUpTicks / GhoulEyes.BloodRedSteps.Count, steps.Count(s => s == step));
        }
    }
}
