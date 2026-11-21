namespace SilverAndAsh.Core.World;

/// <summary>
/// A hole in the wall face on the <paramref name="Side"/> of the open cell (<paramref name="X"/>, <paramref name="Z"/>)
/// (issue #553): <see cref="LevelGeometry"/> leaves out that part of the face, so a model set into the wall shows there
/// (the deep wall niche). Only the picture changes: the cell behind stays solid, so walking, collision, sight and shots
/// are the same as without the hole.
/// </summary>
/// <param name="X">Column of the open cell in front of the wall.</param>
/// <param name="Z">Row of the open cell in front of the wall.</param>
/// <param name="Side">The side of the cell the wall is on (north = toward -z, west = toward -x).</param>
/// <param name="From">Start of the hole along the wall, in metres from the cell's corner with the smaller x (north and
/// south walls) or z (west and east walls).</param>
/// <param name="To">End of the hole along the wall, measured like <paramref name="From"/>.</param>
/// <param name="Bottom">Lower edge of the hole, in metres above the cell's floor.</param>
/// <param name="Top">Upper edge of the hole, in metres above the cell's floor.</param>
public readonly record struct WallCutout(int X, int Z, RoomSide Side, float From, float To, float Bottom, float Top)
{
    /// <summary>The offset from the open cell to the solid cell behind the wall.</summary>
    public (int Dx, int Dz) Behind => Side switch
    {
        RoomSide.North => (0, -1),
        RoomSide.South => (0, 1),
        RoomSide.West => (-1, 0),
        RoomSide.East => (1, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(Side), Side, null),
    };
}
