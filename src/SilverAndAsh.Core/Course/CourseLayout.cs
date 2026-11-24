using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Showroom;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Course;

/// <summary>
/// The test course as built (issue #718): its level with rooms, doors, chests, teleporters and well
/// (<paramref name="Generated"/>), the sign of each station (<paramref name="Labels"/>), what lies on its floor
/// (<paramref name="Finds"/>) and the room of each station, in their order (<paramref name="Stations"/>).
/// </summary>
public sealed record CourseLayout(
    GeneratedLevel Generated, IReadOnlyList<ShowroomLabel> Labels, GroundFinds Finds, IReadOnlyList<PlacedRoom> Stations);
