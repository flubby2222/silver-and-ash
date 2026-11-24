using System.Numerics;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Physics;

/// <summary>
/// Moves an upright capsule (a character) through a level without passing through walls.
/// Walls are full-height cells, so from above the capsule is a circle and every wall is a square -
/// the whole problem becomes "circle against squares" on the ground plane.
/// <para>
/// The two steps of any collision code:
/// <b>detection</b> (does the circle overlap a wall?) and
/// <b>resolution</b> (push it out, and remove the part of the velocity that points into the wall,
/// so the character slides along instead of sticking).
/// </para>
/// </summary>
public static class CharacterCollision
{
    /// <summary>A move is split into steps of at most this fraction of the radius, so fast movement cannot skip walls.</summary>
    private const float MaxStepFraction = 0.5f;

    /// <summary>How often overlaps are resolved per step. Corners need more than one pass.</summary>
    private const int ResolveIterations = 8;

    /// <summary>
    /// One pass over columns, standing bodies and boxes, as before issue #694: a second pass could push a body again and
    /// change where it ends up.
    /// </summary>
    private const int SinglePass = 1;

    /// <summary>Upper limit for steps per move, so absurd inputs cannot freeze the game.</summary>
    private const int MaxSteps = 1000;

    /// <summary>Distances below this are treated as zero (avoids dividing by zero).</summary>
    private const float Epsilon = 1e-5f;

    /// <summary>
    /// True if <paramref name="cell"/> stops a character whose feet are at <paramref name="feetY"/>:
    /// solid walls, floors higher than one step, and gaps too low for the character to fit in.
    /// In version 1.0 all floors and ceilings are equal, so only walls block - the rest prepares stairs and pits.
    /// </summary>
    public static bool BlocksMovement(Cell cell, float feetY, float height, float stepHeight)
    {
        if (cell.IsSolid)
        {
            return true;
        }

        var tooHighToStepUp = cell.FloorHeight > feetY + stepHeight;
        var standingHeight = MathF.Max(cell.FloorHeight, feetY);
        var tooLowToFitIn = cell.CeilingHeight - standingHeight < height;
        return tooHighToStepUp || tooLowToFitIn;
    }

    /// <summary>
    /// Moves <paramref name="position"/> by <paramref name="velocity"/> for <paramref name="seconds"/> and resolves
    /// collisions with blocking cells (see <see cref="BlocksMovement"/>). Height (Y) is not touched. The velocity
    /// that comes back has its "into the wall" part removed, which makes the character slide along walls.
    /// </summary>
    public static (Vector3 Position, Vector3 Velocity) Move(
        Level level, Vector3 position, Vector3 velocity, float radius, float seconds,
        float height = BodySizes.Player.Height, float stepHeight = BodySizes.Player.StepHeight,
        IReadOnlyList<CylinderBody>? bodies = null, IReadOnlyList<BoxBody>? boxes = null)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);
        if (!float.IsFinite(velocity.X) || !float.IsFinite(velocity.Z) || !float.IsFinite(seconds))
        {
            throw new ArgumentException("Velocity and time must be finite numbers.");
        }

        var horizontalDistance = new Vector2(velocity.X, velocity.Z).Length() * seconds;
        var steps = Math.Clamp((int)MathF.Ceiling(horizontalDistance / (radius * MaxStepFraction)), 1, MaxSteps);
        var stepSeconds = seconds / steps;

        for (var step = 0; step < steps; step++)
        {
            position += new Vector3(velocity.X, 0f, velocity.Z) * stepSeconds;

            // Bodies and boxes first, walls last: walls always win, so nobody is ever pushed into a wall.
            (position, velocity) = ResolveSolidOverlaps(bodies, position, velocity, radius, height, stepHeight, SinglePass);
            (position, velocity) = ResolveSolidOverlaps(boxes, position, velocity, radius, height, stepHeight, SinglePass);
            (position, velocity) = ResolveSolidOverlaps(level.Columns, position, velocity, radius, height, stepHeight, SinglePass);

            // The jambs are stepped: in an inner corner the circle touches two blocks, so more than one pass.
            (position, velocity) = ResolveSolidOverlaps(level.Portals, position, velocity, radius, height, stepHeight, ResolveIterations);
            (position, velocity) = ResolveOverlaps(level, position, velocity, radius, height, stepHeight);
        }

        return (position, velocity);
    }

    /// <summary>
    /// Applies <paramref name="gravity"/> for <paramref name="seconds"/>, moves up or down and keeps the character
    /// between floor and ceiling. Floor and ceiling come from all walkable cells the circle stands on:
    /// the highest floor (so a character on the edge of a step stands on the step) and the lowest ceiling.
    /// </summary>
    /// <returns>New position and velocity, and whether the character stands on the ground.</returns>
    public static (Vector3 Position, Vector3 Velocity, bool IsGrounded) MoveVertically(
        Level level, Vector3 position, Vector3 velocity, float radius, float height, float gravity, float seconds,
        IReadOnlyList<BoxBody>? boxes = null, float stepHeight = BodySizes.Player.StepHeight)
    {
        ArgumentNullException.ThrowIfNull(level);

        var (floor, ceiling) = FloorAndCeiling(level, position, radius, height);

        // A box the circle stands over, whose top is not above the feet (by more than a step), is floor too - so a
        // character lands on a chest, and a chest rising under someone lifts them up.
        foreach (var box in boxes ?? [])
        {
            if (box.Max.Y <= position.Y + stepHeight && BoxPenetration(position, radius, box, out _) > 0f)
            {
                floor = MathF.Max(floor, box.Max.Y);
            }
        }

        floor = MathF.Max(floor, PortalStoneUnder(level, position, radius, stepHeight));
        ceiling = MathF.Min(ceiling, PortalStoneOver(level, position, radius, height));

        // Move with the AVERAGE speed of this tick (start speed and end speed, halved).
        // Moving with only the start or only the end speed would make every jump slightly too high or too low;
        // with constant gravity the average is exact.
        var startSpeed = velocity.Y;
        var endSpeed = startSpeed - gravity * seconds;
        position.Y += (startSpeed + endSpeed) / 2f * seconds;
        velocity.Y = endSpeed;

        // Head hits the ceiling: stop rising.
        if (position.Y + height > ceiling)
        {
            position.Y = ceiling - height;
            velocity.Y = MathF.Min(velocity.Y, 0f);
        }

        // Feet reach the floor: stand on it.
        if (position.Y <= floor)
        {
            position.Y = floor;
            velocity.Y = MathF.Max(velocity.Y, 0f);
            return (position, velocity, true);
        }

        return (position, velocity, false);
    }

    /// <summary>Highest floor and lowest ceiling of all walkable (non-blocking) cells that the circle touches.</summary>
    public static (float Floor, float Ceiling) FloorAndCeiling(
        Level level, Vector3 position, float radius,
        float height = BodySizes.Player.Height, float stepHeight = BodySizes.Player.StepHeight)
    {
        var floor = float.NegativeInfinity;
        var ceiling = float.PositiveInfinity;

        foreach (var (x, z) in CellsAround(position, radius))
        {
            var cell = level[x, z];
            if (BlocksMovement(cell, position.Y, height, stepHeight) || PenetrationDepth(position, radius, x, z, out _) <= 0f)
            {
                continue;
            }

            floor = MathF.Max(floor, cell.FloorHeight);
            ceiling = MathF.Min(ceiling, cell.CeilingHeight);
        }

        if (float.IsNegativeInfinity(floor))
        {
            // Should not happen (the circle always touches at least the cell it stands in) - use that cell.
            var cell = level.CellAt(position);
            return (cell.FloorHeight, cell.CeilingHeight);
        }

        return (floor, ceiling);
    }

    /// <summary>True if a circle at <paramref name="position"/> overlaps any blocking cell or column.</summary>
    public static bool Overlaps(
        Level level, Vector3 position, float radius,
        float height = BodySizes.Player.Height, float stepHeight = BodySizes.Player.StepHeight)
    {
        ArgumentNullException.ThrowIfNull(level);
        foreach (var column in level.Columns)
        {
            if (Column.Radius + radius - Vector2.Distance(new Vector2(position.X, position.Z), column.Centre) > Epsilon)
            {
                return true;
            }
        }

        foreach (var (min, max) in PortalStoneInTheWay(level, position, height, stepHeight))
        {
            if (RectanglePenetration(position, radius, min.X, min.Y, max.X, max.Y, out _) > Epsilon)
            {
                return true;
            }
        }

        foreach (var (x, z) in CellsAround(position, radius))
        {
            if (BlocksMovement(level[x, z], position.Y, height, stepHeight)
                && PenetrationDepth(position, radius, x, z, out _) > Epsilon)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True if a circle at <paramref name="position"/> reaches into something that stops it only at some heights:
    /// stone of a boss portal, or a cell whose floor or ceiling leaves no room for it there. Columns and solid walls
    /// stop a body at every height alike - <see cref="Move"/> keeps it out of them - so they do not count here: a
    /// flier pressed against a column may still climb or sink beside it (issue #635).
    /// </summary>
    public static bool BlockedAtHeight(Level level, Vector3 position, float radius, float height, float stepHeight)
    {
        ArgumentNullException.ThrowIfNull(level);
        foreach (var (min, max) in PortalStoneInTheWay(level, position, height, stepHeight))
        {
            if (RectanglePenetration(position, radius, min.X, min.Y, max.X, max.Y, out _) > Epsilon)
            {
                return true;
            }
        }

        foreach (var (x, z) in CellsAround(position, radius))
        {
            var cell = level[x, z];
            if (!cell.IsSolid
                && BlocksMovement(cell, position.Y, height, stepHeight)
                && PenetrationDepth(position, radius, x, z, out _) > Epsilon)
            {
                return true;
            }
        }

        return false;
    }

    // The walls (blocking cells) are no ISolid (issue #694): they are the level's grid, not a list of solids, and their
    // sliding keeps the velocity's Y exactly, where Slide writes Y - 0 * into (a Y of -0 comes back as +0).
    private static (Vector3 Position, Vector3 Velocity) ResolveOverlaps(
        Level level, Vector3 position, Vector3 velocity, float radius, float height, float stepHeight)
    {
        for (var iteration = 0; iteration < ResolveIterations; iteration++)
        {
            var pushedOut = false;

            foreach (var (x, z) in CellsAround(position, radius))
            {
                if (!BlocksMovement(level[x, z], position.Y, height, stepHeight))
                {
                    continue;
                }

                var depth = PenetrationDepth(position, radius, x, z, out var normal);
                if (depth <= 0f)
                {
                    continue;
                }

                // Resolution, part 1: push the circle out of the wall along the wall's normal.
                position += new Vector3(normal.X, 0f, normal.Y) * depth;

                // Resolution, part 2: remove the velocity component that points into the wall.
                // What remains points along the wall - that is the "sliding".
                var horizontal = new Vector2(velocity.X, velocity.Z);
                var intoWall = Vector2.Dot(horizontal, normal);
                if (intoWall < 0f)
                {
                    horizontal -= normal * intoWall;
                    velocity = new Vector3(horizontal.X, velocity.Y, horizontal.Y);
                }

                pushedOut = true;
            }

            if (!pushedOut)
            {
                break;
            }
        }

        return (position, velocity);
    }

    /// <summary>
    /// Pushes the character out of <paramref name="solids"/> (issue #694: standing bodies, boxes, the level's columns, the
    /// stone of the boss portals - each decides itself at which heights it is in the way) and removes the "into the solid"
    /// part of the velocity - the same sliding as along walls. Solid by solid in list order, in up to
    /// <paramref name="passes"/> passes; it stops after the first pass that pushed nothing.
    /// </summary>
    internal static (Vector3 Position, Vector3 Velocity) ResolveSolidOverlaps<TSolid>(
        IReadOnlyList<TSolid>? solids, Vector3 position, Vector3 velocity, float radius, float height, float stepHeight, int passes)
        where TSolid : ISolid
    {
        if (solids is null)
        {
            return (position, velocity);
        }

        for (var pass = 0; pass < passes; pass++)
        {
            var pushedOut = false;

            // By index, without an enumerator: Move runs every tick (issue #653).
            for (var i = 0; i < solids.Count; i++)
            {
                pushedOut |= solids[i].PushOut(ref position, ref velocity, radius, height, stepHeight);
            }

            if (!pushedOut)
            {
                break;
            }
        }

        return (position, velocity);
    }

    /// <summary>
    /// <see cref="ISolid.PushOut"/> for a solid that is a circle on the ground plane (a column, a standing body): out along
    /// the line from its <paramref name="centre"/>, with sliding.
    /// </summary>
    internal static bool PushOutOfCircle(ref Vector3 position, ref Vector3 velocity, float radius, Vector2 centre, float solidRadius)
    {
        var offset = new Vector2(position.X, position.Z) - centre;
        var distance = offset.Length();
        var penetration = radius + solidRadius - distance;
        if (penetration <= 0f)
        {
            return false;
        }

        // Exactly on the middle (should not happen): leave in any fixed direction.
        var normal = distance > Epsilon ? offset / distance : Vector2.UnitX;
        Slide(ref position, ref velocity, normal, penetration);
        return true;
    }

    /// <summary>
    /// <see cref="ISolid.PushOut"/> for a solid that is a rectangle on the ground plane (a box, a block of portal stone,
    /// from <paramref name="min"/> to <paramref name="max"/> in X and Z): out by the nearest way, with sliding.
    /// </summary>
    internal static bool PushOutOfRectangle(ref Vector3 position, ref Vector3 velocity, float radius, Vector2 min, Vector2 max)
    {
        var depth = RectanglePenetration(position, radius, min.X, min.Y, max.X, max.Y, out var normal);
        if (depth <= 0f)
        {
            return false;
        }

        Slide(ref position, ref velocity, normal, depth);
        return true;
    }

    // Resolution of a solid: push the circle out along the normal, then remove the velocity that points into the solid.
    private static void Slide(ref Vector3 position, ref Vector3 velocity, Vector2 normal, float depth)
    {
        position += new Vector3(normal.X, 0f, normal.Y) * depth;
        var into = (velocity.X * normal.X) + (velocity.Z * normal.Y);
        if (into < 0f)
        {
            velocity -= new Vector3(normal.X, 0f, normal.Y) * into;
        }
    }

    /// <summary>
    /// The heights (world) between which a body standing at <paramref name="position"/> meets portal stone: from a step
    /// above its feet to its head (a head that only touches a block's bottom does not meet it: there the block is a ceiling).
    /// </summary>
    internal static (float From, float To) StoneInTheWayHeights(Vector3 position, float height, float stepHeight) =>
        (position.Y + StepOntoStone(stepHeight), position.Y + height - HeadTouch);

    // The ground plans of the portal blocks a body standing at position.Y meets (see StoneInTheWayHeights).
    private static StoneInTheWay PortalStoneInTheWay(Level level, Vector3 position, float height, float stepHeight)
    {
        var (from, to) = StoneInTheWayHeights(position, height, stepHeight);
        return new(level.Portals, from, to);
    }

    /// <summary>
    /// The walk of <see cref="PortalStoneInTheWay"/>: portal by portal (by index), the blocks of each its
    /// <see cref="BossPortal.Footprint"/> picks. A struct that is its own enumerator, without LINQ: <see cref="Move"/>
    /// asks for it every tick, and a lambda with its enumerators would make new objects each time (issue #653).
    /// </summary>
    private struct StoneInTheWay(IReadOnlyList<BossPortal> portals, float from, float to)
    {
        private int _portal = -1;
        private BossPortal.Stones _stones;

        public readonly PortalStone Current => _stones.Current;

        public readonly StoneInTheWay GetEnumerator() => this;

        public bool MoveNext()
        {
            while (_portal < 0 || !_stones.MoveNext())
            {
                if (_portal >= portals.Count - 1)
                {
                    _portal = portals.Count;
                    return false;
                }

                _portal++;
                _stones = portals[_portal].Footprint(from, to);
            }

            return true;
        }
    }

    /// <summary>
    /// The highest top of boss portal stone under the circle that is not above its feet by more than a step - the plinth
    /// a dead bat or a dropped piece falls onto (issue #627), the same rule as for boxes. Minus infinity if there is none.
    /// </summary>
    private static float PortalStoneUnder(Level level, Vector3 position, float radius, float stepHeight)
    {
        var top = float.NegativeInfinity;
        for (var i = 0; i < level.Portals.Count; i++)
        {
            var portal = level.Portals[i];
            if (!Reaches(portal, position, radius))
            {
                continue;
            }

            foreach (var stone in portal.Tops(position.Y + stepHeight))
            {
                if (RectanglePenetration(position, radius, stone.Min.X, stone.Min.Y, stone.Max.X, stone.Max.Y, out _) > Epsilon)
                {
                    top = MathF.Max(top, stone.Top);
                }
            }
        }

        return top;
    }

    /// <summary>
    /// The lowest bottom of boss portal stone over the circle that is not below its head - a ceiling, like the
    /// impost above the plinth (issue #635): a body jumping under it bumps its head and stops rising instead of being
    /// pushed aside by <see cref="Move"/> once its head is in the stone. Plus infinity if there is none. A head touching
    /// the bottom (to rounding) still counts, so the stone stays a ceiling for a body that is held right below it.
    /// </summary>
    private static float PortalStoneOver(Level level, Vector3 position, float radius, float height)
    {
        var bottom = float.PositiveInfinity;
        for (var i = 0; i < level.Portals.Count; i++)
        {
            var portal = level.Portals[i];
            if (!Reaches(portal, position, radius))
            {
                continue;
            }

            foreach (var stone in portal.Bottoms(position.Y + height - HeadTouch))
            {
                if (RectanglePenetration(position, radius, stone.Min.X, stone.Min.Y, stone.Max.X, stone.Max.Y, out _) > Epsilon)
                {
                    bottom = MathF.Min(bottom, stone.Bottom);
                }
            }
        }

        return bottom;
    }

    /// <summary>
    /// True if the circle can overlap the ground plan of any block of <paramref name="portal"/>: its middle lies at most
    /// a radius from the box around all of its stone (on the ground plane; the height does not matter here). A body
    /// further off meets none of its blocks, so <see cref="MoveVertically"/> does not go through them all every tick
    /// (issue #642).
    /// </summary>
    private static bool Reaches(BossPortal portal, Vector3 position, float radius) =>
        portal.IsNear(position with { Y = portal.Floor }, radius);

    /// <summary>
    /// How far (metres) a head may reach past the bottom of the portal's stone and still only touch it: the rounding of
    /// "ceiling minus height plus height". Within it the stone is a ceiling (<see cref="PortalStoneOver"/>), not in the
    /// way (<see cref="PortalStoneInTheWay"/>).
    /// </summary>
    private const float HeadTouch = 1e-4f;

    /// <summary>
    /// How high a body steps up onto the portal's stone. A walking body climbs a block up to its step height. A flying
    /// body (unbounded step: it passes any floor height, see <c>Bat</c>) does not climb stone - it flies over the
    /// floors, but meets every block from its feet up, so a bat in the passage still bumps into the jambs.
    /// </summary>
    private static float StepOntoStone(float stepHeight) => float.IsPositiveInfinity(stepHeight) ? 0f : stepHeight;

    private static float BoxPenetration(Vector3 position, float radius, BoxBody box, out Vector2 normal) =>
        RectanglePenetration(position, radius, box.Min.X, box.Min.Z, box.Max.X, box.Max.Z, out normal);

    /// <summary>
    /// How deep the circle reaches into the square of cell (x, z), and in which direction (the normal)
    /// it has to be pushed to get out. Returns 0 or less if there is no overlap.
    /// </summary>
    private static float PenetrationDepth(Vector3 position, float radius, int x, int z, out Vector2 normal)
    {
        var minX = x * Level.CellSize;
        var minZ = z * Level.CellSize;
        return RectanglePenetration(position, radius, minX, minZ, minX + Level.CellSize, minZ + Level.CellSize, out normal);
    }

    // The same for any rectangle on the ground plane.
    private static float RectanglePenetration(
        Vector3 position, float radius, float minX, float minZ, float maxX, float maxZ, out Vector2 normal)
    {
        var centre = new Vector2(position.X, position.Z);

        // Detection: the point of the square that is closest to the circle's centre.
        var closest = new Vector2(Math.Clamp(centre.X, minX, maxX), Math.Clamp(centre.Y, minZ, maxZ));
        var offset = centre - closest;
        var distance = offset.Length();

        if (distance > Epsilon)
        {
            normal = offset / distance;
            return radius - distance;
        }

        // The centre is inside the square (should only happen in odd cases): leave by the nearest side.
        var toLeft = centre.X - minX;
        var toRight = maxX - centre.X;
        var toBottom = centre.Y - minZ;
        var toTop = maxZ - centre.Y;
        var nearest = MathF.Min(MathF.Min(toLeft, toRight), MathF.Min(toBottom, toTop));

        normal = nearest == toLeft ? -Vector2.UnitX
            : nearest == toRight ? Vector2.UnitX
            : nearest == toBottom ? -Vector2.UnitY
            : Vector2.UnitY;
        return nearest + radius;
    }

    /// <summary>All grid cells that a circle at <paramref name="position"/> could touch.</summary>
    private static IEnumerable<(int X, int Z)> CellsAround(Vector3 position, float radius)
    {
        var (minX, minZ) = Level.ToGrid(position.X - radius, position.Z - radius);
        var (maxX, maxZ) = Level.ToGrid(position.X + radius, position.Z + radius);

        for (var z = minZ; z <= maxZ; z++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                yield return (x, z);
            }
        }
    }
}
