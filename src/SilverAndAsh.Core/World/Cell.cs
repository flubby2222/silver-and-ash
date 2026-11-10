namespace SilverAndAsh.Core.World;

/// <summary>
/// One square of the level grid. Every cell has its own floor and ceiling height,
/// so stairs, pits and high halls can be added later without changing this model.
/// </summary>
/// <param name="IsSolid">True for walls: nothing can enter the cell.</param>
/// <param name="FloorHeight">Height of the walkable floor in metres.</param>
/// <param name="CeilingHeight">Height of the ceiling in metres.</param>
public readonly record struct Cell(bool IsSolid, float FloorHeight, float CeilingHeight)
{
    /// <summary>Standard floor height for version 1.0.</summary>
    public const float DefaultFloorHeight = 0f;

    /// <summary>Standard ceiling height for version 1.0.</summary>
    public const float DefaultCeilingHeight = 3f;

    /// <summary>A solid wall cell.</summary>
    public static Cell Wall { get; } = new(true, DefaultFloorHeight, DefaultCeilingHeight);

    /// <summary>A walkable cell with standard floor and ceiling height.</summary>
    public static Cell Floor { get; } = new(false, DefaultFloorHeight, DefaultCeilingHeight);
}
