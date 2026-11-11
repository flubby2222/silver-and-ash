using SilverAndAsh.Core.Input;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Everything that is simulated together: the level, the player and the training dummies.
/// One tick of the world advances all of them and collects their events in one list.
/// <para>
/// The whole game state lives in this object, not in static fields - a requirement for online co-op later
/// (decision record 0003). There is one player for now; the name "Player" may become a list then.
/// </para>
/// </summary>
public sealed class GameWorld
{
    private readonly List<GameEvent> _events = [];
    private readonly List<TrainingDummy> _dummies;

    /// <summary>Creates the world for <paramref name="level"/>: the player at its start, dummies at their cells.</summary>
    public GameWorld(Level level)
    {
        ArgumentNullException.ThrowIfNull(level);
        Level = level;
        _dummies = level.DummyPositions.Select((position, index) => new TrainingDummy(index, position)).ToList();

        // The player cannot walk through standing dummies; lying ones are stepped over.
        Player = new Player(level, () => _dummies.Where(dummy => !dummy.IsDown).Select(dummy => dummy.Body));
    }

    public Level Level { get; }

    public Player Player { get; }

    public IReadOnlyList<TrainingDummy> Dummies => _dummies;

    /// <summary>Everything that happened in the latest tick (player and dummies). Read it after every tick.</summary>
    public IReadOnlyList<GameEvent> Events => _events;

    /// <summary>A swing hits every standing dummy in the blade's cone (see <see cref="BladeStrike"/>).</summary>
    private void StrikeWithBlade(BladeSwung swing)
    {
        foreach (var dummy in _dummies)
        {
            if (BladeStrike.Hits(Level, swing.Origin, swing.Direction, dummy.Body))
            {
                dummy.TakeDamage(Tuning.Combat.BladeDamage, _events);
            }
        }
    }

    /// <summary>Advances the whole world by one simulation tick.</summary>
    public void Tick(PlayerInput input)
    {
        _events.Clear();
        Player.Tick(input);
        _events.AddRange(Player.Events);

        foreach (var swing in Player.Events.OfType<BladeSwung>())
        {
            StrikeWithBlade(swing);
        }

        foreach (var dummy in _dummies)
        {
            dummy.Tick(_events);
        }
    }
}
