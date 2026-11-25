using System.Numerics;
using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// One line of the status display above an enemy (issue #506): the kind, its colour from <see cref="StatusLooks"/>
/// and its time left as text ("0.4s") - null for a kind that shows no timer (Leech) - and the shape it takes on the body
/// (issue #775).
/// </summary>
public readonly record struct StatusLine(
    StatusKind Kind, (byte R, byte G, byte B) Colour, string? Timer, StatusForm Form = StatusForm.Motes);

/// <summary>
/// What the status display shows on one enemy (issue #506), read-only like <see cref="HudReadout"/>: where the enemy is
/// drawn, how high its head reaches above its feet and how wide it is (the shimmer goes round it, the timers stand above
/// it) and at most
/// <see cref="MaxLines"/> lines, in the order of the kinds. Core decides what is shown; the game only draws it.
/// </summary>
public sealed record EnemyStatusReadout(int EnemyId, Vector3 Feet, float Height, float Radius, IReadOnlyList<StatusLine> Lines)
{
    /// <summary>At most this many effects stand one under the other above an enemy.</summary>
    public const int MaxLines = 3;

    /// <summary>
    /// The point just above the head where the lowest line of the timers sits - above the sparks of a stun, if any
    /// (<see cref="TimerLift"/>), so the two do not cover each other (issue #778).
    /// </summary>
    public Vector3 Anchor => Feet + new Vector3(0f, Height + 0.05f + TimerLift, 0f);

    /// <summary>
    /// How much higher the timers stand than just above the head, in metres (issue #778): as high as the sparks of a stun
    /// reach (<see cref="StunLook.TimerLiftFor"/>), 0 without them.
    /// </summary>
    public float TimerLift { get; init; }

    /// <summary>
    /// The height of the floor below the enemy, in metres (issue #775) - below its feet for a bat in the air, at its feet
    /// for one that walks, also at a step (issue #785, <see cref="FrostDisc.FloorBelow"/>).
    /// </summary>
    public float Floor { get; init; }

    /// <summary>
    /// The rods of its body the rime flecks sit on (issue #785, <see cref="FrostCore"/>); none: the middle of its body
    /// (<see cref="FrostCore.Middle"/>).
    /// </summary>
    public IReadOnlyList<FrostCore> Cores { get; init; } = [];

    /// <summary>
    /// The disc of frost laid onto the floors below it (issue #785, <see cref="FrostDisc"/>); null without Grave Chill.
    /// </summary>
    public FrostDisc? Disc { get; init; }

    /// <summary>
    /// Where the head sits across from the feet, level (issue #778): the middle of the highest of the shot bodies - a
    /// ghoul's head hangs forward of its feet; on an enemy shot as a whole (bat, Gorm) none.
    /// </summary>
    public Vector3 HeadOffset { get; init; }

    /// <summary>The top of the head (issue #778): above the middle of the head, at <see cref="Height"/> above the feet.</summary>
    public Vector3 Crown => Feet + HeadOffset + new Vector3(0f, Height, 0f);

    /// <summary>
    /// Radius of the disc of frost on the floor, in metres - after the size of the enemy's body
    /// (<see cref="FrostLook.DiscRadiusFor"/>).
    /// </summary>
    public float DiscRadius => FrostLook.DiscRadiusFor(Radius);

    /// <summary>
    /// The display for every enemy that is alive, not hidden and carries at least one effect. A dead enemy shows none,
    /// even while its effects still run out (issue #506, from the review of #756).
    /// </summary>
    public static IReadOnlyList<EnemyStatusReadout> Of(GameWorld world) => Of(world, StatusLooks.All);

    /// <summary>The same with another table of looks (for tests with more kinds than the game has yet).</summary>
    internal static IReadOnlyList<EnemyStatusReadout> Of(GameWorld world, IReadOnlyList<StatusLook> looks)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(looks);
        var readouts = new List<EnemyStatusReadout>();
        foreach (var enemy in world.Enemies)
        {
            if (enemy.IsDead || enemy.IsHidden)
            {
                continue;
            }

            var lines = LinesOf(enemy.Status, looks);
            if (lines.Count == 0)
            {
                continue;
            }

            // The head is where the shot bodies end (a ghoul's head sits lower than its body for walking).
            var body = enemy.Body;
            var head = enemy.ShotBodies.MaxBy(shot => shot.Position.Y + shot.Height);
            var top = head.Position.Y + head.Height - body.Position.Y;
            var floor = FrostDisc.FloorBelow(world.Level, body);
            var frost = lines.Any(line => line.Form == StatusForm.Frost);
            readouts.Add(new EnemyStatusReadout(enemy.Id, enemy.ShownPosition, top, body.Radius, lines)
            {
                Floor = floor,
                HeadOffset = (head.Position - body.Position) with { Y = 0f },
                TimerLift = StunLook.TimerLiftFor(lines, body.Radius),
                Cores = FrostCore.Of(enemy),
                Disc = frost ? FrostDisc.Of(world.Level, enemy.ShownPosition, FrostLook.DiscRadiusFor(body.Radius), floor) : null,
            });
        }

        return readouts;
    }

    /// <summary>
    /// The time left as the display shows it: seconds with one decimal, rounded up to the next tenth, so the last tick
    /// still shows "0.1s" and never "0.0s".
    /// </summary>
    public static string TimerText(int ticksLeft)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticksLeft);
        var tenths = ((ticksLeft * 10) + SimulationClock.TicksPerSecond - 1) / SimulationClock.TicksPerSecond;
        return $"{tenths / 10}.{tenths % 10}s";
    }

    private static List<StatusLine> LinesOf(StatusEffects status, IReadOnlyList<StatusLook> looks)
    {
        var lines = new List<StatusLine>();
        foreach (var (kind, effect) in status.InOrder())
        {
            if (lines.Count == MaxLines)
            {
                break;
            }

            var look = StatusLooks.Of(kind, looks);
            lines.Add(new StatusLine(kind, look.Colour, look.ShowsTimer ? TimerText(effect.TicksLeft) : null, look.Form));
        }

        return lines;
    }
}
