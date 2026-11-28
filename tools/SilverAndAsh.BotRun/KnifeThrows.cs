using System.Numerics;
using SilverAndAsh.Core;
using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.Photos;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.BotRun;

/// <summary>
/// Mode "knives" (issues #816, #821): a player with the throwing knives stands at the start and in the middle of every
/// room, turns once round and throws hand after hand - level, up and down, reloading in between. After every tick: no
/// more knives stick than the balance allows, no knife flies inside the stone or longer than its range lasts, a knife in
/// a target sticks in one that can be hit, and the hand never holds more or fewer knives than there are. At the end
/// every thrown knife is accounted for: it stuck, or its range ran out.
/// </summary>
internal static class KnifeThrows
{
    // Directions of one turn round, and the heights looked at 5 m away (down at the floor, level, up at the ceiling).
    private const int Turns = 12;
    private static readonly float[] Heights = [-4f, 0f, 4f];

    public static void Run(LevelZero zero, int seed, Report report)
    {
        var probe = zero.NewRun(seed, Play.Balance);
        report.Try($"seed {seed} knives start", () => From(zero, seed, null, report));
        foreach (var room in probe.Rooms)
        {
            report.Try($"seed {seed} knives room {room.Index}", () => From(zero, seed, room.Index, report));
        }
    }

    // One world: the player at the start (room null) or in the middle of the room, one turn round at every height.
    private static void From(LevelZero zero, int seed, int? room, Report report)
    {
        var world = zero.NewRun(seed, Play.Balance);
        var play = new Play(world, report, room is { } index ? $"seed {seed} knives room {index}" : $"seed {seed} knives start");
        world.Player.Equip(0, WeaponKind.ThrowingKnives);
        if (room is { } at)
        {
            play.PlaceAt(Middle(world, world.Rooms[at]));
        }

        var watch = new Watch(play, report);
        foreach (var height in Heights)
        {
            for (var turn = 0; turn < Turns; turn++)
            {
                var angle = MathF.Tau * turn / Turns;
                var eye = world.Player.EyePosition;
                PhotoStudio.Face(world.Player, eye + new Vector3(MathF.Sin(angle) * 5f, height, MathF.Cos(angle) * 5f));
                watch.Throw();
            }
        }

        watch.Finish();
        report.Count("places thrown from");
    }

    // The room's open cell nearest to the middle of its cells.
    private static (int X, int Z) Middle(GameWorld world, RoomState room)
    {
        var centre = new Vector2((float)room.Cells.Average(cell => cell.X), (float)room.Cells.Average(cell => cell.Z));
        return room.Cells
            .Where(cell => !world.Level[cell.X, cell.Z].IsSolid)
            .OrderBy(cell => Vector2.DistanceSquared(new Vector2(cell.X, cell.Z), centre))
            .ThenBy(cell => cell.X)
            .ThenBy(cell => cell.Z)
            .First();
    }

    // Throws and counts, and checks after every tick what must always hold of the knives.
    private sealed class Watch(Play play, Report report)
    {
        private readonly Dictionary<int, int> _flownTicks = [];
        private int _events;
        private int _thrown;
        private int _stuck;

        private GameWorld World => play.World;

        private RangedBalance Knives => World.Balance.ThrowingKnives;

        private ThrownBalance Flight => World.Balance.KnifeFlight;

        // The ticks a knife can be in the air at most: its range at its speed, and one tick for the rounding.
        private int LongestFlight => (int)MathF.Ceiling(Knives.Range / (Flight.Speed * SimulationClock.SecondsPerTick)) + 1;

        /// <summary>One throw: reloads first if the hand is empty, then presses until a knife leaves the hand.</summary>
        public void Throw()
        {
            var magazine = World.Player.Ranged.Magazine;
            if (magazine.Rounds == 0)
            {
                Tick(PlayerInput.None with { Reload = true });
                if (report.Check(!Until(magazine.ReloadTicks + 10, PlayerInput.None, () => magazine is { IsReloading: false, Rounds: > 0 }), $"{play.Context}: the reload never fills the hand"))
                {
                    return;
                }

                report.Count("reloads");
            }

            var before = _thrown;
            var wait = SimulationClock.SecondsToTicks(Knives.Cooldown) + 10;
            for (var tick = 0; tick < wait && _thrown == before; tick++)
            {
                // The throw is a press, not a hold: pressed anew every other tick.
                Tick(PlayerInput.None with { Shoot = tick % 2 == 0 });
            }

            report.Check(_thrown == before, $"{play.Context}: no knife leaves the hand within {wait} ticks");
        }

        /// <summary>Lets the last knives land, then every thrown knife must have stuck or flown out its range.</summary>
        public void Finish()
        {
            report.Check(!Until(LongestFlight + 5, PlayerInput.None, () => World.FlyingKnives.Count == 0), $"{play.Context}: a knife still flies {LongestFlight + 5} ticks after the last throw");
            report.Check(_stuck > _thrown, $"{play.Context}: {_stuck} knives stuck, only {_thrown} thrown");
            report.Check(_thrown == 0, $"{play.Context}: no knife was thrown at all - the mode checks nothing");
        }

        private bool Until(int max, PlayerInput input, Func<bool> done)
        {
            for (var i = 0; i < max && !done(); i++)
            {
                Tick(input);
            }

            return done();
        }

        private void Tick(PlayerInput input)
        {
            play.Tick(input);
            foreach (var happened in play.Events.Skip(_events))
            {
                Count(happened);
            }

            _events = play.Events.Count;
            Check();
        }

        private void Count(GameEvent happened)
        {
            switch (happened)
            {
                case ShotFired { Weapon: WeaponKind.ThrowingKnives }:
                    _thrown++;
                    report.Count("knives thrown");
                    break;
                case KnifeStuck { TargetId: null }:
                    _stuck++;
                    report.Count("knives stuck in stone");
                    break;
                case KnifeStuck:
                    _stuck++;
                    report.Count("knives stuck in a target");
                    break;
            }
        }

        private void Check()
        {
            var magazine = World.Player.Ranged.Magazine;
            report.Check(magazine.Rounds < 0 || magazine.Rounds > magazine.Size, $"{play.Context}: {magazine.Rounds} knives in a hand of {magazine.Size}");
            report.Check(World.StuckKnives.Count > Flight.StuckMax, $"{play.Context}: {World.StuckKnives.Count} knives stick, more than the {Flight.StuckMax} of the balance");
            foreach (var stuck in World.StuckKnives.Where(stuck => stuck.TargetId is not null))
            {
                report.Check(!World.Targets.Any(target => target.Id == stuck.TargetId && target.IsHittable), $"{play.Context}: knife {stuck.Id} sticks in target {stuck.TargetId}, which cannot be hit");
            }

            foreach (var knife in World.FlyingKnives)
            {
                var ticks = _flownTicks[knife.Id] = _flownTicks.GetValueOrDefault(knife.Id) + 1;
                report.Check(ticks > LongestFlight, $"{play.Context}: knife {knife.Id} flies for {ticks} ticks, its range lasts {LongestFlight}");
                report.Check(knife.RangeLeft <= 0f || knife.RangeLeft > Knives.Range, $"{play.Context}: knife {knife.Id} in flight has {knife.RangeLeft} m of range left");
                report.Check(World.Level.CellAt(knife.Position).IsSolid, $"{play.Context}: knife {knife.Id} flies inside the stone at {knife.Position}");
            }
        }
    }
}
