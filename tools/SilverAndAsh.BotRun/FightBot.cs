using System.Numerics;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.BotRun;

/// <summary>
/// The player of mode "fight" (issue #803): one input per tick, chosen by <see cref="FightPolicy"/> from what the bot
/// sees. It acts only through <see cref="PlayerInput"/> and turns its view like a mouse (<see cref="Play.Face"/>, the
/// same call the game makes every frame). It never sets health, position or damage. The route runs through the doors
/// from the room it stands in to the boss room, by the shortest way; side rooms it does not visit.
/// </summary>
internal sealed class FightBot
{
    // The way (a few points) is worked out again this often, and when the goal moves to another cell.
    private const int RepathEvery = 15;

    // An enemy outside the bot's room is fought if it is this close and nothing is in the way.
    private const float SightRange = 12f;

    // The turns from "straight away" that a retreat tries, in this order (degrees), and how far it looks ahead (m).
    private static readonly float[] RetreatTurns = [0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f];

    private const float RetreatLook = 3f;

    // Enemies this close (to the edge of the body) push the retreat away from themselves, too.
    private const float CrowdRange = 6f;

    private const float DoorReach = 1.8f;

    private const float StairsReach = 1.2f;

    private const float WaypointReach = 0.4f;

    // How long it stands that no cell of the room behind a door could be reached.
    private const int EntryRetryTicks = 60;

    private readonly Dictionary<int, (Vector3? Point, long RetryAt)> _entries = [];
    // Standing still: less than this far in a tick, this many ticks in a row. Then a sidestep of this many ticks.
    private const float StillDistance = 0.02f;

    private const int StillTicks = 12;

    private const int SidestepTicks = 40;

    private readonly Dictionary<int, Vector3?> _nearSides = [];
    private readonly Play _play;
    private readonly GameWorld _world;
    private readonly WeaponKind _weapon;
    private readonly bool _isRanged;
    private readonly float _reach;
    private bool _pressed;
    private Vector3 _lastWalk;
    private int _still;
    private int _sidestepLeft;
    private bool _sidestepRight;
    private int _sinceRepath = RepathEvery;
    private (int X, int Z) _wayGoal;
    private IReadOnlyList<Vector3> _way = [];
    private RoomState _room;

    public FightBot(Play play, WeaponKind weapon, Balance balance)
    {
        _play = play;
        _world = play.World;
        _weapon = weapon;
        _isRanged = Weapons.Info(weapon).Hand == WeaponHand.Ranged;
        _reach = _isRanged ? balance.Ranged(weapon).Range : balance.Melee(weapon).Range;
        _room = _world.Rooms.First(room => room.Kind == RoomKind.Start);
    }

    /// <summary>The input of the next tick.</summary>
    public PlayerInput Next()
    {
        var player = _world.Player;
        UpdateRoom();
        var input = Choose(player);
        return player.PendingBoons > 0 ? input with { ChooseBoon = player.CurrentOffer[0] } : input;
    }

    private PlayerInput Choose(Player player)
    {
        if (_isRanged && player.Ranged.Kind != _weapon)
        {
            var slot = player.RangedSlots.ToList().FindIndex(each => each?.Kind == _weapon);
            return slot < 0
                ? throw new InvalidOperationException($"{_weapon} is not in a ranged slot of the player")
                : PlayerInput.None with { SelectSlot = slot };
        }

        var seen = Visible(player);
        var target = seen.FirstOrDefault();
        var threat = seen.FirstOrDefault(IsAnnouncingBlow);
        var vial = Nearest(player, _world.KnownBloodVials.Where(each => !each.IsTaken).Select(each => each.Position));
        var key = Nearest(player, _world.Loot.Where(each => each.Kind == LootKind.Key && !each.IsTaken).Select(each => each.Position));
        var rounds = _isRanged ? player.Ranged.Magazine.Rounds : 0;
        var reloading = _isRanged && player.Ranged.Magazine.IsReloading;
        var view = new FightView(
            player.Health, player.MaxHealth, _isRanged, _reach, reloading, rounds,
            target is null ? null : DistanceTo(player, target), target is not null && Sees(player, target.Body.Position),
            vial?.Distance, key?.Distance, threat is null ? null : DistanceTo(player, threat));
        var choice = FightPolicy.Decide(view);
        _pressed = choice.Attack && !_pressed;
        return choice.Goal switch
        {
            FightGoal.Hold => PlayerInput.None with { Reload = choice.Reload },
            FightGoal.Fight => Attack(player, target!),
            FightGoal.Retreat => Retreat(player, target!, threat ?? target!, seen, choice.Attack, choice.Reload),
            FightGoal.Approach => WalkTo(target!.Body.Position),
            FightGoal.Vial => WalkTo(vial!.Value.Point),
            FightGoal.Key => WalkTo(key!.Value.Point),
            _ => Route(player),
        };
    }

    // A tap each other tick: the press starts the swing or shot, the release lets the blade fly. The weapon's own
    // cooldown decides how often it really strikes.
    private PlayerInput Attack(Player player, ITarget target)
    {
        var body = target.Body;
        PhotoStudio.Face(player, new Vector3(body.Position.X, body.Position.Y + (body.Height / 2f), body.Position.Z));
        return _isRanged ? PlayerInput.None with { Shoot = _pressed } : PlayerInput.None with { Melee = _pressed };
    }

    // What an enemy shows before it strikes: the ghoul draws back, the bat hovers and screeches.
    private static bool IsAnnouncingBlow(ITarget enemy) =>
        enemy is Ghoul { State: GhoulState.WindingUp } or Bat { State: BatState.Screeching };

    // Walks away from <from> with the view on the target (a melee bot: on the one that strikes). The way back, else
    // the nearest free direction beside it; where all are walls the bot stands and fights.
    private PlayerInput Retreat(Player player, ITarget target, ITarget from, IReadOnlyList<ITarget> seen, bool shoot, bool reload)
    {
        var aimAt = _isRanged ? target : from;
        PhotoStudio.Face(player, new Vector3(aimAt.Body.Position.X, aimAt.Body.Position.Y + (aimAt.Body.Height / 2f), aimAt.Body.Position.Z));
        if (RetreatMove(player, AwayFrom(player, from, seen)) is not { } move)
        {
            return Attack(player, target);
        }

        return PlayerInput.None with { Move = move, Shoot = shoot && _pressed, Reload = reload };
    }

    // Away from the enemy that strikes, and the more so the closer the others stand: each pushes with 1 / distance squared.
    private static Vector3 AwayFrom(Player player, ITarget from, IReadOnlyList<ITarget> seen)
    {
        var sum = Push(player, from, 4f);
        foreach (var each in seen.Where(other => other != from && DistanceTo(player, other) < CrowdRange))
        {
            sum += Push(player, each, 1f);
        }

        return sum.LengthSquared() > 0f ? Vector3.Normalize(sum) : Vector3.UnitZ;
    }

    private static Vector3 Push(Player player, ITarget enemy, float weight)
    {
        var flat = new Vector3(player.Position.X - enemy.Body.Position.X, 0f, player.Position.Z - enemy.Body.Position.Z);
        var distance = MathF.Max(flat.Length(), 0.1f);
        return flat / distance * (weight / (distance * distance));
    }

    // The first of the directions from straight away to sideways that is free for a few metres, as a move in the view.
    private Vector2? RetreatMove(Player player, Vector3 away)
    {
        foreach (var degrees in RetreatTurns)
        {
            var turn = degrees * MathF.PI / 180f;
            var direction = new Vector3(
                (away.X * MathF.Cos(turn)) - (away.Z * MathF.Sin(turn)), 0f, (away.X * MathF.Sin(turn)) + (away.Z * MathF.Cos(turn)));
            if (Pathfinder.CanWalkStraight(_world.Level, player.Position, player.Position + (direction * RetreatLook), BodySizes.Player.Radius))
            {
                var view = player.View;
                return new Vector2(Vector3.Dot(direction, view.FlatRight), Vector3.Dot(direction, view.FlatForward));
            }
        }

        return null;
    }

    private void UpdateRoom()
    {
        var cell = Play.Cell(_world.Player.Position);
        _room = _world.Rooms.FirstOrDefault(room => room.Cells.Contains(cell)) ?? _room;
    }

    // The enemies of the room the bot stands in, or close by in plain sight, the nearest first.
    private List<ITarget> Visible(Player player) => _world.Targets
        .Where(each => each is Ghoul or Bat or Gorm && each.IsHittable)
        .Select(each => (Enemy: each, Distance: DistanceTo(player, each)))
        .Where(each => _room.Cells.Contains(Play.Cell(each.Enemy.Body.Position)) || (each.Distance <= SightRange && Sees(player, each.Enemy.Body.Position)))
        .OrderBy(each => each.Distance)
        .Select(each => each.Enemy)
        .ToList();

    private static float DistanceTo(Player player, ITarget target) =>
        MathF.Max(0f, Play.Distance(player.Position, target.Body.Position) - target.Body.Radius);

    private bool Sees(Player player, Vector3 point) =>
        Pathfinder.CanWalkStraight(_world.Level, player.Position, point, 0.05f, float.PositiveInfinity);

    private static (Vector3 Point, float Distance)? Nearest(Player player, IEnumerable<Vector3> points) => points
        .Select(point => (Point: point, Distance: Play.Distance(player.Position, point)))
        .OrderBy(each => each.Distance)
        .Cast<(Vector3 Point, float Distance)?>()
        .FirstOrDefault();

    // Walks towards a point: the first point of the way to it, with the view turned that way.
    private PlayerInput WalkTo(Vector3 point)
    {
        _play.Face(NextPoint(point));
        return Walk();
    }

    // Forward - with a sidestep when the bot has stood in one place while walking: the way finder knows walls, not the
    // things that stand in a room (a pedestal, a chest). The sidestep changes side every time.
    private PlayerInput Walk()
    {
        var at = _world.Player.Position;
        _still = Play.Distance(at, _lastWalk) < StillDistance ? _still + 1 : 0;
        _lastWalk = at;
        if (_still >= StillTicks)
        {
            _still = 0;
            _sidestepLeft = SidestepTicks;
            _sidestepRight = !_sidestepRight;
        }

        if (_sidestepLeft == 0)
        {
            return Play.Forward;
        }

        _sidestepLeft--;
        return Play.Forward with { Move = new Vector2(_sidestepRight ? 1f : -1f, 1f) };
    }

    private Vector3 NextPoint(Vector3 point)
    {
        var level = _world.Level;
        var from = Play.Cell(_world.Player.Position);
        var goal = Play.Cell(point);
        if (_sinceRepath >= RepathEvery || goal != _wayGoal)
        {
            _wayGoal = goal;
            _sinceRepath = 0;
            _way = Pathfinder.FindPath(level, from, goal) is { } path
                ? [.. Pathfinder.Smooth(level, path, BodySizes.Player.Radius).SkipLast(1), point]
                : [point];
        }

        _sinceRepath++;
        while (_way.Count > 1 && Play.Distance(_world.Player.Position, _way[0]) < WaypointReach)
        {
            _way = [.. _way.Skip(1)];
        }

        return _way[0];
    }

    // The way on: the stairs once Gorm is down, otherwise the next door on the way to the boss room.
    private PlayerInput Route(Player player)
    {
        if (_world.Stairs is { CanInteract: true } stairs)
        {
            if (Play.Distance(player.Position, stairs.Position) > StairsReach)
            {
                return WalkTo(stairs.Position);
            }

            _play.Face(stairs.Position);
            return Play.Interact;
        }

        if (NextHop() is not { } hop)
        {
            return PlayerInput.None;
        }

        var (door, next) = hop;
        if (door.IsOpen)
        {
            return EntryPoint(door, next) is { } entry ? WalkTo(entry) : PlayerInput.None;
        }

        if (!door.CanInteract)
        {
            return PlayerInput.None;
        }

        var middle = (door.BoxMin + door.BoxMax) / 2;
        if (Play.Distance(player.Position, middle) > DoorReach)
        {
            // A closed door is wall to the way finder: walk to the cell in front of it, then straight at it.
            if (NearSide(door) is { } near && Play.Distance(player.Position, near) > WaypointReach)
            {
                return WalkTo(near);
            }

            _play.Face(middle);
            return Walk();
        }

        _play.Face(middle);
        return Play.Interact;
    }

    // The middle of the cell in front of a door (any side) that can be walked to from here. Found once per door.
    private Vector3? NearSide(Door door)
    {
        if (_nearSides.TryGetValue(door.Id, out var known))
        {
            return known;
        }

        var level = _world.Level;
        var from = Play.Cell(_world.Player.Position);
        var found = door.Cells
            .SelectMany(cell => new[] { (cell.X + 1, cell.Z), (cell.X - 1, cell.Z), (cell.X, cell.Z + 1), (cell.X, cell.Z - 1) })
            .Where(cell => level.IsInside(cell.Item1, cell.Item2) && !door.Cells.Contains(cell))
            .Select(cell => level.CellCenter(cell.Item1, cell.Item2))
            .OrderBy(point => Play.Distance(point, _world.Player.Position))
            .Where(point => Pathfinder.FindPath(level, from, Play.Cell(point)) is not null)
            .Select(point => (Vector3?)point)
            .FirstOrDefault();
        _nearSides[door.Id] = found;
        return found;
    }

    // Where to walk to through an open door: the middle of the cell of the room behind it that lies nearest to the door
    // and can be reached on foot (a hallway of many metres may lie between; some cells of a room are on a ledge).
    // Found once per door; a door without a way is tried again after a while.
    private Vector3? EntryPoint(Door door, RoomState next)
    {
        if (_entries.TryGetValue(door.Id, out var known) && (known.Point is not null || _world.Ticks < known.RetryAt))
        {
            return known.Point;
        }

        var middle = (door.BoxMin + door.BoxMax) / 2;
        var from = Play.Cell(_world.Player.Position);
        var found = next.Cells
            .OrderBy(each => Play.Distance(_world.Level.CellCenter(each.X, each.Z), middle))
            .Where(each => Pathfinder.FindPath(_world.Level, from, each) is not null)
            .Select(each => (Vector3?)_world.Level.CellCenter(each.X, each.Z))
            .FirstOrDefault();
        _entries[door.Id] = (found, _world.Ticks + EntryRetryTicks);
        return found;
    }

    // The first door of the shortest way from the room to the boss room, with the room behind it. A locked door counts
    // only with a key in the pocket; a cracked wall is no door for this bot.
    private (Door Door, RoomState Next)? NextHop()
    {
        var goal = _world.Rooms.Single(room => room.Kind == RoomKind.Boss).Index;
        var first = new Dictionary<int, Door>();
        var queue = new Queue<int>([_room.Index]);
        var seen = new HashSet<int> { _room.Index };
        while (queue.TryDequeue(out var room))
        {
            foreach (var door in _world.Doors.Where(each => IsUsable(each) && (each.RoomA == room || each.RoomB == room)))
            {
                var next = door.RoomA == room ? door.RoomB : door.RoomA;
                if (next < 0 || next >= _world.Rooms.Count || !seen.Add(next))
                {
                    continue;
                }

                first[next] = room == _room.Index ? door : first[room];
                if (next == goal)
                {
                    var through = first[next];
                    return (through, Play.OtherRoom(_world, through, _room));
                }

                queue.Enqueue(next);
            }
        }

        return null;
    }

    private bool IsUsable(Door door) =>
        door.Kind != DoorKind.Cracked && (door.Kind != DoorKind.Locked || _world.Player.Keys > 0);
}
