using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Input;

namespace SilverAndAsh.Core.Photos;

/// <summary>
/// A photo scene, built and ready: the world, how many ticks run before the picture is taken and what the player
/// "presses" meanwhile. Everything goes through the normal rules - the scene only chooses where things start.
/// </summary>
/// <param name="World">The world as the scene built it, not yet ticked.</param>
public sealed record PhotoSetup(GameWorld World)
{
    /// <summary>Ticks that run before the picture (at most, see <see cref="StopWhen"/>). At least 1.</summary>
    public int Ticks { get; init; } = 1;

    /// <summary>The input for tick 0, 1, 2 ... - null: no input at all (the player stands still).</summary>
    public Func<int, PlayerInput>? Input { get; init; }

    /// <summary>Checked after every tick: once true, the picture is taken at once. Null: all <see cref="Ticks"/> run.</summary>
    public Func<GameWorld, bool>? StopWhen { get; init; }

    /// <summary>True if the picture shows the map kept open (M) - a matter of the screen, not of the world.</summary>
    public bool MapOpen { get; init; }

    /// <summary>Which menu the picture shows over the scene (issue #349) - a matter of the screen, not of the world.</summary>
    public PhotoMenu Menu { get; init; }

    /// <summary>
    /// True for the model probe: the single target stands for a model a designer wants to see under game conditions.
    /// The game may draw it with another view than the placeholder.
    /// </summary>
    public bool IsModelProbe { get; init; }

    /// <summary>
    /// Runs the ticks of this scene on <see cref="World"/>; <paramref name="afterTick"/> is called after each one (the
    /// game reads the events there). Returns how many ticks ran.
    /// </summary>
    public int Run(Action? afterTick = null)
    {
        var ticks = Math.Max(1, Ticks);
        for (var tick = 0; tick < ticks; tick++)
        {
            World.Tick(Input?.Invoke(tick) ?? PlayerInput.None);
            afterTick?.Invoke();
            if (StopWhen?.Invoke(World) == true)
            {
                return tick + 1;
            }
        }

        return ticks;
    }

    /// <summary>
    /// Films this scene (issue #286): the same ticks as <see cref="Run"/>, but <paramref name="frame"/> is called with the
    /// number of ticks run so far before the first tick (0) and after every <paramref name="frameEvery"/>-th tick - so
    /// the pictures stand evenly apart in game time. <paramref name="afterTick"/> is called after every tick, before a
    /// picture. Returns the ticks at which pictures were taken.
    /// </summary>
    public IReadOnlyList<int> Film(int frameEvery, Action<int> frame, Action? afterTick = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(frameEvery, 1);
        ArgumentNullException.ThrowIfNull(frame);
        var taken = new List<int> { 0 };
        frame(0);
        var ticks = Math.Max(1, Ticks);
        for (var tick = 0; tick < ticks; tick++)
        {
            World.Tick(Input?.Invoke(tick) ?? PlayerInput.None);
            afterTick?.Invoke();
            var done = tick + 1;
            if (done % frameEvery == 0)
            {
                frame(done);
                taken.Add(done);
            }

            if (StopWhen?.Invoke(World) == true)
            {
                break;
            }
        }

        return taken;
    }
}

/// <summary>A menu a photo shows (issue #349): none, the title screen, the pause menu or the settings opened from it.</summary>
public enum PhotoMenu
{
    None,
    Title,
    Pause,
    Settings,
}
