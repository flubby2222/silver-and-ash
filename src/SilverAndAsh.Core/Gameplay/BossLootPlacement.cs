using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Where the pieces of Gorm's loot lie once he is dead (decision record 0007): pure geometry over the level and the
/// boss room's cells - no state, no events, no random numbers. Moved out of <see cref="GameWorld"/> (issue #696).
/// </summary>
public static class BossLootPlacement
{
    /// <summary>How far behind Gorm his equipment lies (metres) - less than his body's radius, so never in a wall.</summary>
    public const float GormEquipmentDistance = BodySizes.Gorm.Radius * 0.7f;

    /// <summary>
    /// The gap (metres) between the box of Gorm's piece and the box of the staircase, when he died next to where it
    /// rises. With it, F never finds the stairs nearer than the piece for a player looking at it from outside.
    /// </summary>
    public const float StairsClearance = 0.1f;

    /// <summary>
    /// The gap (metres) between a piece of Gorm's loot and a standing column (issue #608): a piece never touches one, so
    /// a player walking round the column reaches it and F finds it.
    /// </summary>
    public const float ColumnClearance = 0.1f;

    /// <summary>
    /// Interaction picks the nearest box a player looks at, and inside the staircase's box that is the stairs (issue
    /// #338). So a piece whose box would touch the staircase's box moves out: away from the stairs' middle (straight
    /// behind Gorm if he died right on it), just far enough, and only onto the boss room's own floor at Gorm's height -
    /// every corner of its box on such a cell, so never in a wall. If no direction fits (a room too small), it stays.
    /// <paramref name="bossCells"/> are the boss room's cells, or null in a level without a boss room.
    /// </summary>
    public static Vector3 OutsideTheStairs(
        Level level, IReadOnlySet<(int X, int Z)>? bossCells, Vector3 place, Vector3 gormPosition, Vector3 behind)
    {
        if (bossCells is null)
        {
            return place;
        }

        var (stairsX, stairsZ) = Stairs.CellIn(bossCells);
        var stairs = level.CellCenter(stairsX, stairsZ);
        var reach = Stairs.Radius + EquipmentDrop.BoxRadius + StairsClearance;
        if (MathF.Max(MathF.Abs(place.X - stairs.X), MathF.Abs(place.Z - stairs.Z)) >= reach)
        {
            return place;
        }

        var away = new Vector2(place.X - stairs.X, place.Z - stairs.Z);
        if (away.LengthSquared() < 1e-6f)
        {
            away = new Vector2(behind.X, behind.Z);
        }

        var start = MathF.Atan2(away.X, away.Y);
        for (var step = 0; step < 16; step++)
        {
            // 0, +22.5°, -22.5°, +45°, ... - the nearest directions to "away" first.
            var angle = start + ((step + 1) / 2 * (step % 2 == 1 ? 1 : -1) * MathF.PI / 8f);
            var direction = new Vector2(MathF.Sin(angle), MathF.Cos(angle));
            var distance = reach / MathF.Max(MathF.Abs(direction.X), MathF.Abs(direction.Y));
            var candidate = new Vector3(stairs.X + (direction.X * distance), gormPosition.Y, stairs.Z + (direction.Y * distance));
            if (OnFloorOf(level, bossCells, candidate, EquipmentDrop.BoxRadius, gormPosition.Y))
            {
                return candidate;
            }
        }

        return place;
    }

    /// <summary>
    /// A piece of Gorm's loot (a square of half-width <paramref name="halfWidth"/>) must lie where it can be taken: on
    /// open floor at his height, clear of every standing column and of the staircase (issue #608). Where it would not,
    /// it turns round him at the same distance - the nearest directions first - to the first place that is free. His own
    /// body kept the ground under him clear, so turning stays close to where he fell. If no direction is free, the piece
    /// stays where it would have lain, even in a column (in generated boss rooms this never happened: review of PR #613).
    /// </summary>
    public static Vector3 FreePlace(
        Level level, IReadOnlySet<(int X, int Z)>? bossCells, Vector3 place, float halfWidth, Vector3 gormPosition)
    {
        if (IsFreeFor(level, bossCells, place, halfWidth, gormPosition.Y))
        {
            return place;
        }

        var offset = new Vector2(place.X - gormPosition.X, place.Z - gormPosition.Z);
        var distance = offset.Length();
        if (distance < 1e-4f)
        {
            return place;
        }

        var start = MathF.Atan2(offset.X, offset.Y);
        for (var step = 1; step < 16; step++)
        {
            // +22.5°, -22.5°, +45°, ... - the nearest directions to where it should have lain first.
            var angle = start + ((step + 1) / 2 * (step % 2 == 1 ? 1 : -1) * MathF.PI / 8f);
            var candidate = new Vector3(
                gormPosition.X + (MathF.Sin(angle) * distance), place.Y, gormPosition.Z + (MathF.Cos(angle) * distance));
            if (IsFreeFor(level, bossCells, candidate, halfWidth, gormPosition.Y))
            {
                return candidate;
            }
        }

        return place;
    }

    // True if a square of half-width halfWidth around position lies on open floor at floorHeight (in the boss room, if
    // there is one), keeps ColumnClearance from every standing column and does not reach the staircase's box.
    private static bool IsFreeFor(
        Level level, IReadOnlySet<(int X, int Z)>? bossCells, Vector3 position, float halfWidth, float floorHeight)
    {
        var flat = new Vector2(position.X, position.Z);
        foreach (var column in level.Columns)
        {
            var nearest = Vector2.Clamp(column.Centre, flat - new Vector2(halfWidth), flat + new Vector2(halfWidth));
            if (Vector2.Distance(nearest, column.Centre) < Column.Radius + ColumnClearance)
            {
                return false;
            }
        }

        if (InPortalStone(level, flat, halfWidth))
        {
            return false;
        }

        if (bossCells is null)
        {
            return OnFloorOf(level, null, position, halfWidth, floorHeight);
        }

        var (stairsX, stairsZ) = Stairs.CellIn(bossCells);
        var stairs = level.CellCenter(stairsX, stairsZ);
        var reach = Stairs.Radius + halfWidth + StairsClearance;
        return MathF.Max(MathF.Abs(position.X - stairs.X), MathF.Abs(position.Z - stairs.Z)) >= reach
            && OnFloorOf(level, bossCells, position, halfWidth, floorHeight);
    }

    // True if the square of half-width halfWidth around flat reaches into the stone of a boss door's portal: loot there
    // would lie in the jamb or on the plinth (issue #627).
    private static bool InPortalStone(Level level, Vector2 flat, float halfWidth) =>
        level.Portals.Any(portal => portal.Covers(flat, halfWidth));

    // True if every corner of the square of half-width radius around position lies on an open cell whose floor is at
    // floorHeight - a cell of the room, or in a level without a boss room (roomCells null) any cell of the level.
    private static bool OnFloorOf(
        Level level, IReadOnlySet<(int X, int Z)>? roomCells, Vector3 position, float radius, float floorHeight)
    {
        foreach (var (dx, dz) in new[] { (-1f, -1f), (-1f, 1f), (1f, -1f), (1f, 1f) })
        {
            var cell = Level.ToGrid(position.X + (dx * radius), position.Z + (dz * radius));
            var inside = roomCells?.Contains(cell) ?? level.IsInside(cell.X, cell.Z);
            if (!inside || level[cell.X, cell.Z].IsSolid
                || MathF.Abs(level[cell.X, cell.Z].FloorHeight - floorHeight) > 0.01f)
            {
                return false;
            }
        }

        return true;
    }
}
