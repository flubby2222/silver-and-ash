using SilverAndAsh.Core.Presentation;
using SilverAndAsh.Core.World;

namespace SilverAndAsh.Core.Gameplay;

/// <summary>
/// Level 0, the catacombs (ADR 0005): every run is a freshly generated level from the room templates, with enemies
/// placed by distance and chests in some combat rooms. A lost run is followed by a new one with a new seed - so a new layout.
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
        var generated = Build(seed, balance);
        return new GameWorld(generated.Level, seed, balance, generated.Doors, generated.Rooms, generated.Chests, generated.Well)
        {
            LevelName = Name,
            Surfaces = SurfaceVariants.For(generated),
        };
    }

    /// <summary>
    /// The level of a run with <paramref name="seed"/>, exactly as <see cref="NewRun"/> uses it: rooms, then enemies,
    /// then chests, then the Well of Sirius. The balance report builds its levels with this, too.
    /// </summary>
    public GeneratedLevel Build(int seed, Balance balance)
    {
        ArgumentNullException.ThrowIfNull(balance);
        var built = LevelGenerator.Generate(_templates, seed, balance.Layout);
        var withChests = ChestPlacement.Place(EnemyPlacement.Populate(built, balance.Difficulty), balance.Chests);
        return WellPlacement.Place(withChests, balance.Well);
    }

    /// <summary>The run after <paramref name="finished"/>: a new seed taken from its seed, the same balancing values.</summary>
    public GameWorld NextRun(GameWorld finished)
    {
        ArgumentNullException.ThrowIfNull(finished);
        return NewRun(new Random(finished.Seed).Next(), finished.Balance);
    }
}
