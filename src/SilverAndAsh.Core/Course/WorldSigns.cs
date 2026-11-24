using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Showroom;

namespace SilverAndAsh.Core.Course;

/// <summary>
/// Which signs a world shows over its pieces (issue #722): the labels of the exhibits in the showroom, the numbered
/// station signs on the test course - in the game and on the photos alike, because the world decides, not the launch
/// option - and none in a run.
/// </summary>
public static class WorldSigns
{
    /// <summary>The signs of <paramref name="world"/>, or null if it has none (a run, or a test world that is neither).</summary>
    public static IReadOnlyList<ShowroomLabel>? Of(GameWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.IsShowroom)
        {
            return ShowroomLayout.Build().Labels;
        }

        return world.IsTestCourse ? TestCourse.Build(world.Seed, world.Balance).Labels : null;
    }
}
