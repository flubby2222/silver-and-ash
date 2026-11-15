using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Level 0, the catacombs (ADR 0005): every run is a freshly generated level from the room templates, with enemies
/// placed by distance. A lost run is followed by a new one with a new seed - so a new layout.
/// </summary>
public sealed class LevelZero
{
    /// <summary>Shown briefly when the level is entered.</summary>
    public const string Name = "THE CATACOMBS";

    private readonly IReadOnlyList<RoomTemplate> _templates;

    /// <summary>Level 0 built from <paramref name="templates"/> (needs start, combat and boss rooms).</summary>
    public LevelZero(IReadOnlyList<RoomTemplate> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        _templates = templates;
    }

    /// <summary>A new run: level and world both start from <paramref name="seed"/>; the player stands in the start room.</summary>
    public GameWorld NewRun(int seed, Balance balance)
    {
        ArgumentNullException.ThrowIfNull(balance);
        var generated = EnemyPlacement.Populate(LevelGenerator.Generate(_templates, seed, balance.Layout), balance.Difficulty);
        return new GameWorld(generated.Level, seed, balance, generated.Doors, generated.Rooms) { LevelName = Name };
    }

    /// <summary>The run after <paramref name="finished"/>: a new seed taken from its seed, the same balancing values.</summary>
    public GameWorld NextRun(GameWorld finished)
    {
        ArgumentNullException.ThrowIfNull(finished);
        return NewRun(new Random(finished.Seed).Next(), finished.Balance);
    }
}
