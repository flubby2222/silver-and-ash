namespace SilverAndAsh.Core.World;

/// <summary>
/// Builds a level from room templates with a seed (ADR 0005). The start room is placed first and gets exactly one
/// door (its other door slots stay wall, so the way out lies in another direction per seed); combat rooms are
/// joined to free door slots - directly through a shared wall or through a corridor - until the number from
/// <see cref="LayoutBalance"/> is reached; while loops are wanted, places are sought where a new room (or a room
/// and a corridor) joins two free door slots, so there is a second way round. Then the boss room, behind one of the
/// rooms farthest from the start, so it is always the farthest room and has one entrance - always behind the built-in
/// <see cref="Antechamber"/> (issue #565), which takes the place the boss room had before. Last the treasure rooms
/// (<see cref="LayoutBalance.TreasureRooms"/>, <see cref="LayoutBalance.LockedTreasureRooms"/>): dead ends, never at the
/// start room, never as far as the boss; a locked one gets a <see cref="DoorKind.Locked"/> door - so nothing important
/// ever lies behind a lock. Before them the secret rooms (<see cref="LayoutBalance.SecretRooms"/>): each sits
/// directly at a combat room, behind a <see cref="DoorKind.Cracked"/> wall.
/// <para>Same seed and same templates = same level. If a try gets stuck (no room fits anywhere), the generator
/// starts again with the next random numbers of the same seed.</para>
/// </summary>
public static class LevelGenerator
{
    /// <summary>Tries before giving up. With sensible templates the first or second try works.</summary>
    private const int MaxTries = 50;

    /// <summary>Corridors tried per door slot before giving up on reaching a room through a corridor there.</summary>
    private const int CorridorsPerSlot = 12;

    /// <summary>Door slots farther apart than this (in cells, along X plus along Z) are never joined by one corridor.</summary>
    private const int MaxCorridorSpan = 12;

    /// <summary>
    /// Builds a level from <paramref name="templates"/> (needs at least one start, combat and boss room).
    /// </summary>
    /// <exception cref="ArgumentException">If a needed kind of room is missing.</exception>
    /// <exception cref="InvalidOperationException">If no level could be built (templates too awkward).</exception>
    public static GeneratedLevel Generate(IReadOnlyList<RoomTemplate> templates, int seed, LayoutBalance layout)
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(layout);

        foreach (var needed in new[] { RoomKind.Start, RoomKind.Combat, RoomKind.Boss })
        {
            if (!templates.Any(template => template.Kind == needed))
            {
                throw new ArgumentException($"The level generator needs at least one {needed.ToString().ToLowerInvariant()} room.", nameof(templates));
            }
        }

        var catalog = templates.GroupBy(template => template.Kind)
            .ToDictionary(group => group.Key, group => group.SelectMany(Variant.AllOf).ToList());

        // The antechamber is built in: its eight ways to lie, always in the same order.
        catalog[RoomKind.Antechamber] = Variant.AllOf(Antechamber.Template).ToList();
        var random = new Random(seed);

        for (var attempt = 0; attempt < MaxTries; attempt++)
        {
            var builder = new Builder(catalog, random, layout);
            if (builder.TryBuild())
            {
                return builder.ToLevel(seed);
            }
        }

        throw new InvalidOperationException($"No level could be built from these rooms with seed {seed}.");
    }

    private static RoomSide Opposite(RoomSide side) => (RoomSide)(((int)side + 2) % 4);

    /// <summary>One way a template can lie: turned 0-3 quarter turns, mirrored or not. Cells are template cells.</summary>
    private sealed class Variant
    {
        private Variant(RoomTemplate template)
        {
            Template = template;
            var floor = new List<(int X, int Z)>();
            for (var z = 0; z < template.Depth; z++)
            {
                for (var x = 0; x < template.Width; x++)
                {
                    if (!template.CellAt(x, z).IsSolid)
                    {
                        floor.Add((x, z));
                    }
                }
            }

            Floor = floor;
            FloorSet = floor.ToHashSet();

            // The room claims its floor and every cell around it, even outside the template: so two rooms
            // can share a wall, but floor never touches floor of another room (not even diagonally).
            Claimed = floor.SelectMany(cell =>
                    from dx in new[] { -1, 0, 1 }
                    from dz in new[] { -1, 0, 1 }
                    select (cell.X + dx, cell.Z + dz))
                .Distinct()
                .ToList();
        }

        public RoomTemplate Template { get; }

        public RoomKind Kind => Template.Kind;

        public IReadOnlyList<(int X, int Z)> Floor { get; }

        public HashSet<(int X, int Z)> FloorSet { get; }

        public IReadOnlyList<(int X, int Z)> Claimed { get; }

        public static IEnumerable<Variant> AllOf(RoomTemplate template)
        {
            for (var turns = 0; turns < 4; turns++)
            {
                var turned = template.Rotated(turns);
                yield return new Variant(turned);
                yield return new Variant(turned.Mirrored());
            }
        }
    }

    /// <summary>A variant at a place in the level being built (offset = level cell of the template's corner).</summary>
    private sealed record Placement(Variant Variant, int OffsetX, int OffsetZ)
    {
        public bool IsFloorAt(int x, int z) => Variant.FloorSet.Contains((x - OffsetX, z - OffsetZ));

        public IEnumerable<DoorSlot> Slots => Variant.Template.DoorSlots
            .Select(slot => slot with { X = slot.X + OffsetX, Z = slot.Z + OffsetZ });
    }

    /// <summary>A free door slot of a placed room: here another room may be joined.</summary>
    private sealed record OpenSlot(int Room, int X, int Z, RoomSide Side);

    private sealed class Builder(Dictionary<RoomKind, List<Variant>> catalog, Random random, LayoutBalance layout)
    {
        private readonly List<Placement> _rooms = [];
        private readonly Dictionary<(int X, int Z), (int Room, bool Floor)> _claimed = [];
        private readonly List<OpenSlot> _open = [];
        private readonly List<LevelDoor> _doors = [];
        private int _loops;

        // Corridors are used only if there are corridor templates and the layout allows them (chance above 0).
        private bool UsesCorridors => catalog.ContainsKey(RoomKind.Corridor) && layout.CorridorChance > 0;

        public bool TryBuild()
        {
            var starts = catalog[RoomKind.Start];
            Place(new Placement(starts[random.Next(starts.Count)], 0, 0), entrance: null);

            var combatRooms = random.Next(layout.CombatRoomsMin, layout.CombatRoomsMax + 1);
            for (var placed = 0; placed < combatRooms; placed++)
            {
                if (!TryJoinSomewhere(RoomKind.Combat))
                {
                    return false;
                }
            }

            while (_loops < layout.MaxLoops && TryCloseLoopWithCorridor())
            {
            }

            return TryJoinBoss() && TryJoinSecrets() && TryJoinTreasures();
        }

        public GeneratedLevel ToLevel(int seed)
        {
            // Shift everything so the level starts at 0/0 with a ring of wall around it.
            var minX = _claimed.Keys.Min(cell => cell.X) - 1;
            var minZ = _claimed.Keys.Min(cell => cell.Z) - 1;
            var width = _claimed.Keys.Max(cell => cell.X) - minX + 2;
            var depth = _claimed.Keys.Max(cell => cell.Z) - minZ + 2;

            var cells = new Cell[width, depth];
            for (var x = 0; x < width; x++)
            {
                for (var z = 0; z < depth; z++)
                {
                    cells[x, z] = Cell.Wall;
                }
            }

            var distances = Distances();
            var rooms = new List<PlacedRoom>();
            for (var index = 0; index < _rooms.Count; index++)
            {
                var (variant, offsetX, offsetZ) = _rooms[index];
                var template = variant.Template;
                foreach (var (x, z) in variant.Floor)
                {
                    cells[x + offsetX - minX, z + offsetZ - minZ] = template.CellAt(x, z);
                }

                (int X, int Z) ToLevelCell((int X, int Z) cell) => (cell.X + offsetX - minX, cell.Z + offsetZ - minZ);

                rooms.Add(new PlacedRoom(
                    index,
                    template.Kind,
                    template.Name,
                    offsetX - minX,
                    offsetZ - minZ,
                    template.Width,
                    template.Depth,
                    distances[index],
                    variant.Floor.Select(ToLevelCell).ToList(),
                    template.GhoulSlots.Select(ToLevelCell).ToList(),
                    template.BatSlots.Select(ToLevelCell).ToList(),
                    template.ChestSlots.Select(ToLevelCell).ToList(),
                    template.BossSlots.Select(ToLevelCell).ToList()));
            }

            // Door cells are floor with the low standard ceiling (a 3.5 m high doorway).
            var doors = _doors.Select(door => door with { X = door.X - minX, Z = door.Z - minZ }).ToList();
            foreach (var door in doors)
            {
                cells[door.X, door.Z] = Cell.Floor;
            }

            var (startX, startZ) = _rooms[0].Variant.Template.PlayerStart
                ?? throw new InvalidOperationException("The start room has no player start.");
            var level = new Level(cells, startX + _rooms[0].OffsetX - minX, startZ + _rooms[0].OffsetZ - minZ);

            return new GeneratedLevel(seed, level, rooms, doors);
        }

        // Joins a room of 'kind' to a random free door slot; slots where nothing fits are given up.
        private bool TryJoinSomewhere(RoomKind kind)
        {
            if (_loops < layout.MaxLoops && (TryCloseLoopWithRoom(kind) || TryCloseLoopWithRoomAndCorridor(kind)))
            {
                return true;
            }

            while (_open.Count > 0)
            {
                var slot = _open[random.Next(_open.Count)];
                if (TryJoin(kind, slot))
                {
                    return true;
                }

                _open.Remove(slot);
            }

            return false;
        }

        // The boss must end up farther from the start than every other room. The antechamber in front of it takes the
        // place the boss room would have had (same slots, same random numbers), so it is farther than every other room, too.
        private bool TryJoinBoss()
        {
            var distances = Distances();
            var farthest = distances.Max();
            var slots = _open.OrderByDescending(slot => distances[slot.Room]).ThenBy(_ => random.Next()).ToList();

            foreach (var slot in slots)
            {
                var direct = distances[slot.Room] + 1 > farthest;
                var throughCorridor = UsesCorridors && distances[slot.Room] + 2 > farthest;
                var corridorFirst = random.NextDouble() < layout.CorridorChance;

                foreach (var useCorridor in corridorFirst ? new[] { true, false } : [false, true])
                {
                    if ((useCorridor ? throughCorridor : direct) && TryJoinBossBehindAntechamber(slot, useCorridor))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // The antechamber joins 'slot' (directly or through a corridor) with its entrance, the boss room joins the
        // antechamber's other door slot. If there is no room for both, nothing is placed.
        private bool TryJoinBossBehindAntechamber(OpenSlot slot, bool useCorridor)
        {
            if (!useCorridor)
            {
                return AntechamberAndBosses(slot.X, slot.Z, slot.Side, corridor: null) is { } direct
                    && PlaceAntechamberAndBoss(direct, slot);
            }

            if (!UsesCorridors)
            {
                return false;
            }

            var corridors = Fitting(RoomKind.Corridor, slot.X, slot.Z, slot.Side)
                .OrderBy(_ => random.Next()).Take(CorridorsPerSlot);
            foreach (var corridor in corridors)
            {
                foreach (var exit in corridor.Slots.Where(exit => (exit.X, exit.Z) != (slot.X, slot.Z)))
                {
                    if (AntechamberAndBosses(exit.X, exit.Z, exit.Side, corridor) is { } fit)
                    {
                        var corridorIndex = Place(corridor, slot);
                        return PlaceAntechamberAndBoss(fit, new OpenSlot(corridorIndex, exit.X, exit.Z, exit.Side));
                    }
                }
            }

            return false;
        }

        // The first antechamber (in the fixed order of its variants - no random number) that fits with its entrance on
        // the door cell (x, z) and leaves room for a boss room at its other door slot, with every boss room that fits
        // there. 'corridor' is about to be placed in front of it: the boss room must not run into it either.
        private (Placement Antechamber, List<Placement> Bosses)? AntechamberAndBosses(int x, int z, RoomSide side, Placement? corridor)
        {
            foreach (var antechamber in Fitting(RoomKind.Antechamber, x, z, side))
            {
                var template = antechamber.Variant.Template;
                if (!Antechamber.IsEntrance(template, antechamber.Slots.Single(door => (door.X, door.Z) == (x, z))))
                {
                    continue;
                }

                var bossDoor = antechamber.Slots.Single(door => !Antechamber.IsEntrance(template, door));
                var bosses = Fitting(RoomKind.Boss, bossDoor.X, bossDoor.Z, bossDoor.Side)
                    .Where(boss => corridor is null || !Overlaps(boss, corridor))
                    .ToList();
                if (bosses.Count > 0)
                {
                    return (antechamber, bosses);
                }
            }

            return null;
        }

        private bool PlaceAntechamberAndBoss((Placement Antechamber, List<Placement> Bosses) fit, OpenSlot entrance)
        {
            var antechamber = Place(fit.Antechamber, entrance);
            var bossDoor = _open.Single(open => open.Room == antechamber);
            Place(fit.Bosses[random.Next(fit.Bosses.Count)], bossDoor);
            return true;
        }

        // Like Collides, but between two rooms not placed yet: floor of one on a cell the other claims.
        private static bool Overlaps(Placement a, Placement b)
        {
            var claimedByB = b.Variant.Claimed.Select(cell => (cell.X + b.OffsetX, cell.Z + b.OffsetZ)).ToHashSet();
            return a.Variant.Claimed
                .Select(cell => (X: cell.X + a.OffsetX, Z: cell.Z + a.OffsetZ))
                .Any(cell => claimedByB.Contains(cell) && (a.IsFloorAt(cell.X, cell.Z) || b.IsFloorAt(cell.X, cell.Z)));
        }

        // A secret room lies directly at a combat room (never through a corridor), nearer to the start than the boss;
        // its only way in is a cracked wall.
        private bool TryJoinSecrets()
        {
            if (!catalog.ContainsKey(RoomKind.Secret))
            {
                return true;
            }

            for (var i = 0; i < layout.SecretRooms; i++)
            {
                var distances = Distances();
                var boss = distances[_rooms.FindIndex(room => room.Variant.Kind == RoomKind.Boss)];
                var slots = _open
                    .Where(slot => _rooms[slot.Room].Variant.Kind == RoomKind.Combat && distances[slot.Room] + 1 < boss)
                    .OrderBy(_ => random.Next())
                    .ToList();
                if (!slots.Any(slot => TryJoin(RoomKind.Secret, slot, useCorridor: false)))
                {
                    return false;
                }

                var entrance = _doors.FindIndex(door => door.RoomB == _rooms.Count - 1);
                _doors[entrance] = _doors[entrance] with { Kind = DoorKind.Cracked };
            }

            return true;
        }

        // Treasure rooms come after the boss: they must stay nearer to the start than the boss room.
        private bool TryJoinTreasures()
        {
            if (!catalog.ContainsKey(RoomKind.Treasure))
            {
                return true;
            }

            for (var i = 0; i < layout.TreasureRooms; i++)
            {
                if (!TryJoinTreasure(locked: false))
                {
                    return false;
                }
            }

            for (var i = 0; i < layout.LockedTreasureRooms; i++)
            {
                if (!TryJoinTreasure(locked: true))
                {
                    return false;
                }
            }

            return true;
        }

        // A dead end at a free slot of any room but the start room, closer to the start than the boss room.
        private bool TryJoinTreasure(bool locked)
        {
            var distances = Distances();
            var boss = distances[_rooms.FindIndex(room => room.Variant.Kind == RoomKind.Boss)];
            var slots = _open.Where(slot => slot.Room != 0).OrderBy(_ => random.Next()).ToList();

            foreach (var slot in slots)
            {
                var direct = distances[slot.Room] + 1 < boss;
                var throughCorridor = UsesCorridors && distances[slot.Room] + 2 < boss;
                var corridorFirst = UsesCorridors && random.NextDouble() < layout.CorridorChance;

                // With corridors always (chance 1) a treasure room is never joined through a shared wall.
                var ways = corridorFirst ? new[] { true, false } : [false, true];
                foreach (var useCorridor in ways.Where(corridor => corridor || layout.CorridorChance < 1f))
                {
                    if ((useCorridor ? throughCorridor : direct) && TryJoin(RoomKind.Treasure, slot, useCorridor))
                    {
                        if (locked)
                        {
                            // Its only door - the one leading into it - gets the lock.
                            var entrance = _doors.FindIndex(door => door.RoomB == _rooms.Count - 1);
                            _doors[entrance] = _doors[entrance] with { Kind = DoorKind.Locked };
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryJoin(RoomKind kind, OpenSlot slot)
        {
            var corridorFirst = UsesCorridors && random.NextDouble() < layout.CorridorChance;
            return corridorFirst
                ? TryJoin(kind, slot, useCorridor: true) || TryJoin(kind, slot, useCorridor: false)
                : TryJoin(kind, slot, useCorridor: false) || (UsesCorridors && TryJoin(kind, slot, useCorridor: true));
        }

        private bool TryJoin(RoomKind kind, OpenSlot slot, bool useCorridor)
        {
            if (!useCorridor)
            {
                var rooms = Fitting(kind, slot.X, slot.Z, slot.Side);
                if (rooms.Count == 0)
                {
                    return false;
                }

                Place(rooms[random.Next(rooms.Count)], slot);
                return true;
            }

            if (!UsesCorridors)
            {
                return false;
            }

            var corridors = Fitting(RoomKind.Corridor, slot.X, slot.Z, slot.Side)
                .OrderBy(_ => random.Next()).Take(CorridorsPerSlot);
            foreach (var corridor in corridors)
            {
                foreach (var exit in corridor.Slots.Where(exit => (exit.X, exit.Z) != (slot.X, slot.Z)))
                {
                    var rooms = Fitting(kind, exit.X, exit.Z, exit.Side);
                    if (rooms.Count > 0)
                    {
                        var corridorIndex = Place(corridor, slot);
                        Place(rooms[random.Next(rooms.Count)], new OpenSlot(corridorIndex, exit.X, exit.Z, exit.Side));
                        return true;
                    }
                }
            }

            return false;
        }

        // A room that fits at one free door slot and with another of its door slots meets a second free slot.
        private bool TryCloseLoopWithRoom(RoomKind kind)
        {
            var closingLoops = _open
                .SelectMany(slot => Fitting(kind, slot.X, slot.Z, slot.Side)
                    .Where(room => LoopSlots(room, (slot.X, slot.Z)).Any(other => other.Room != slot.Room))
                    .Select(room => (Room: room, Slot: slot)))
                .ToList();
            if (closingLoops.Count == 0)
            {
                return false;
            }

            var (placement, entrance) = closingLoops[random.Next(closingLoops.Count)];
            Place(placement, entrance);
            return true;
        }

        // A room joined directly to one free door slot, and a corridor from a second free slot (of another room)
        // to one of the new room's other door slots.
        private bool TryCloseLoopWithRoomAndCorridor(RoomKind kind)
        {
            if (!UsesCorridors)
            {
                return false;
            }

            var options = new List<(Placement Room, OpenSlot Entrance, Placement Corridor, OpenSlot Start)>();
            foreach (var entrance in _open)
            {
                foreach (var room in Fitting(kind, entrance.X, entrance.Z, entrance.Side))
                {
                    foreach (var end in room.Slots.Where(door => (door.X, door.Z) != (entrance.X, entrance.Z)))
                    {
                        foreach (var start in _open.Where(start => start.Room != entrance.Room
                                     && Math.Abs(start.X - end.X) + Math.Abs(start.Z - end.Z) <= MaxCorridorSpan))
                        {
                            foreach (var corridor in CorridorsBetween(start, end))
                            {
                                if (!Collides(corridor))
                                {
                                    options.Add((room, entrance, corridor, start));
                                }
                            }
                        }
                    }
                }
            }

            if (options.Count == 0)
            {
                return false;
            }

            var (chosenRoom, chosenEntrance, chosenCorridor, chosenStart) = options[random.Next(options.Count)];
            var roomIndex = Place(chosenRoom, chosenEntrance);
            var corridorIndex = Place(chosenCorridor, chosenStart);
            var roomEnd = _open.First(open => open.Room == roomIndex
                && chosenCorridor.Slots.Any(door => (door.X, door.Z) == (open.X, open.Z)));
            AddDoor(roomEnd, corridorIndex);
            _loops++;
            return true;
        }

        // Corridor placements with one door slot on 'start' and another on 'end', both facing the right way.
        private IEnumerable<Placement> CorridorsBetween(OpenSlot start, DoorSlot end)
        {
            foreach (var variant in catalog[RoomKind.Corridor])
            {
                foreach (var first in variant.Template.DoorSlots.Where(door => door.Side == Opposite(start.Side)))
                {
                    var corridor = new Placement(variant, start.X - first.X, start.Z - first.Z);
                    if (corridor.Slots.Any(door => door.X == end.X && door.Z == end.Z && door.Side == Opposite(end.Side)))
                    {
                        yield return corridor;
                    }
                }
            }
        }

        // A corridor whose two ends meet free door slots of two different rooms.
        private bool TryCloseLoopWithCorridor()
        {
            if (!UsesCorridors)
            {
                return false;
            }

            foreach (var slot in _open.OrderBy(_ => random.Next()).ToList())
            {
                foreach (var corridor in Fitting(RoomKind.Corridor, slot.X, slot.Z, slot.Side))
                {
                    foreach (var exit in corridor.Slots.Where(exit => (exit.X, exit.Z) != (slot.X, slot.Z)))
                    {
                        var other = _open.FirstOrDefault(open =>
                            open.X == exit.X && open.Z == exit.Z && open.Side == Opposite(exit.Side)
                            && open.Room != slot.Room && !AreLinked(open.Room, slot.Room));
                        if (other is not null)
                        {
                            var corridorIndex = Place(corridor, slot);
                            AddDoor(other, corridorIndex);
                            _loops++;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        // Every place where a variant of 'kind' fits with one of its door slots on the door cell (x, z).
        private List<Placement> Fitting(RoomKind kind, int x, int z, RoomSide side)
        {
            var result = new List<Placement>();
            foreach (var variant in catalog[kind])
            {
                foreach (var entrance in variant.Template.DoorSlots.Where(door => door.Side == Opposite(side)))
                {
                    var placement = new Placement(variant, x - entrance.X, z - entrance.Z);
                    if (!Collides(placement))
                    {
                        result.Add(placement);
                    }
                }
            }

            return result;
        }

        // Free door slots of other rooms that lie on a door slot of 'placement' (not its entrance), facing it.
        private IEnumerable<OpenSlot> LoopSlots(Placement placement, (int X, int Z) entrance)
        {
            if (placement.Variant.Kind == RoomKind.Corridor)
            {
                return [];
            }

            return placement.Slots
                .Where(door => (door.X, door.Z) != entrance)
                .SelectMany(door => _open.Where(open =>
                    open.X == door.X && open.Z == door.Z && open.Side == Opposite(door.Side)));
        }

        // Two rooms that are about to be joined at a door slot (a corridor and the room behind it) need not be
        // checked against each other: every template has walls all round, so they lie on the two sides of the
        // door's wall line and only meet there - wall on wall.
        private bool Collides(Placement placement)
        {
            foreach (var (cellX, cellZ) in placement.Variant.Claimed)
            {
                var x = cellX + placement.OffsetX;
                var z = cellZ + placement.OffsetZ;
                var floor = placement.IsFloorAt(x, z);

                // Sharing a wall is fine; floor on a cell another room claims is not.
                if (_claimed.TryGetValue((x, z), out var other) && (floor || other.Floor))
                {
                    return true;
                }
            }

            return false;
        }

        // Puts the room into the level, joined through 'entrance' (null for the start room). Returns its index.
        private int Place(Placement placement, OpenSlot? entrance)
        {
            var index = _rooms.Count;
            var entranceCell = entrance is null ? (int.MinValue, int.MinValue) : (entrance.X, entrance.Z);
            var loopSlots = LoopSlots(placement, entranceCell).ToList();
            _rooms.Add(placement);

            foreach (var (cellX, cellZ) in placement.Variant.Claimed)
            {
                var cell = (cellX + placement.OffsetX, cellZ + placement.OffsetZ);
                _claimed.TryAdd(cell, (index, placement.IsFloorAt(cell.Item1, cell.Item2)));
            }

            if (entrance is not null)
            {
                AddDoor(entrance, index);
            }

            var kind = placement.Variant.Kind;
            foreach (var door in placement.Slots)
            {
                if (entrance is not null && (door.X, door.Z) == (entrance.X, entrance.Z))
                {
                    continue;
                }

                var loop = loopSlots.FirstOrDefault(open => (open.X, open.Z) == (door.X, door.Z));
                if (loop is not null && _loops < layout.MaxLoops && !AreLinked(loop.Room, index))
                {
                    AddDoor(loop, index);
                    _loops++;
                }
                else if (kind != RoomKind.Corridor)
                {
                    _open.Add(new OpenSlot(index, door.X, door.Z, door.Side));
                }
            }

            return index;
        }

        private void AddDoor(OpenSlot from, int toRoom)
        {
            _open.Remove(from);
            var kind = _rooms[from.Room].Variant.Kind == RoomKind.Boss || _rooms[toRoom].Variant.Kind == RoomKind.Boss
                ? DoorKind.Boss
                : DoorKind.Normal;
            _doors.Add(new LevelDoor(from.X, from.Z, from.Room, toRoom, from.Side, kind));

            // The start room has one way out: once its first door is set, its other door slots stay wall.
            if (_rooms[from.Room].Variant.Kind == RoomKind.Start || _rooms[toRoom].Variant.Kind == RoomKind.Start)
            {
                _open.RemoveAll(slot => _rooms[slot.Room].Variant.Kind == RoomKind.Start);
            }
        }

        // True if two rooms share a door or are both joined to the same corridor.
        private bool AreLinked(int a, int b)
        {
            var nearA = Neighbours(a);
            var nearB = Neighbours(b);
            return nearA.Contains(b)
                || nearA.Any(room => _rooms[room].Variant.Kind == RoomKind.Corridor && nearB.Contains(room));
        }

        private List<int> Neighbours(int room) => _doors
            .Where(door => door.RoomA == room || door.RoomB == room)
            .Select(door => door.RoomA == room ? door.RoomB : door.RoomA)
            .ToList();

        // Shortest number of doors from the start room to every room (breadth-first search).
        private int[] Distances()
        {
            var distances = Enumerable.Repeat(int.MaxValue, _rooms.Count).ToArray();
            distances[0] = 0;
            var open = new Queue<int>([0]);
            while (open.TryDequeue(out var room))
            {
                foreach (var door in _doors.Where(door => door.RoomA == room || door.RoomB == room))
                {
                    var other = door.RoomA == room ? door.RoomB : door.RoomA;
                    if (distances[other] == int.MaxValue)
                    {
                        distances[other] = distances[room] + 1;
                        open.Enqueue(other);
                    }
                }
            }

            return distances;
        }
    }
}
