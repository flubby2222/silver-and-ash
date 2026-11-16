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
}
