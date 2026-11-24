using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Course;

/// <summary>
/// One station of the test course (issue #718, docs/pruefstrecke.md): a room of <paramref name="Kind"/> on the straight
/// way, <paramref name="Width"/> floor cells from west to east, with a sign "number - <paramref name="Name"/>" over its
/// entrance. <paramref name="Furnish"/> puts in what is to be judged there - columns, enemies, finds, a teleporter, a
/// room beside it. A new station is one more entry in <see cref="TestCourse.Stations"/>.
/// </summary>
public sealed record CourseStation(string Name, RoomKind Kind, int Width, Action<CourseRoom> Furnish);
