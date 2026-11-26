using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.BotRun;

/// <summary>What the fight bot does with this tick (issue #803).</summary>
internal enum FightGoal
{
    /// <summary>Stand still (reloading).</summary>
    Hold,

    /// <summary>Stand, aim at the target and attack.</summary>
    Fight,

    /// <summary>Walk up to the target.</summary>
    Approach,

    /// <summary>Walk to a blood vial.</summary>
    Vial,

    /// <summary>Walk to a key lying about.</summary>
    Key,

    /// <summary>Walk on along the route to Gorm.</summary>
    Route,
}

/// <summary>
/// What the bot sees of one moment. Distances are in metres, flat, to the edge of the body. Everything the policy needs
/// and nothing of the world - so the decision is a function and can be tested with plain numbers.
/// </summary>
internal readonly record struct FightView(
    int Health,
    int MaxHealth,
    bool IsRanged,
    float Reach,
    bool IsReloading,
    int Rounds,
    float? TargetDistance,
    bool TargetInSight,
    float? VialDistance,
    float? KeyDistance);

/// <summary>The bot's decision: where to go or stand, whether to attack, whether to reload.</summary>
internal readonly record struct FightChoice(FightGoal Goal, bool Attack, bool Reload);

/// <summary>
/// The bot's whole play, fixed and simple (issue #803): reload when empty, heal when hurt and a vial lies in reach,
/// fight what is in front of it, otherwise walk the route. No dodging, no retreat, no choice of weapon. What it wins is
/// a lower bound of what a player can do.
/// </summary>
internal static class FightPolicy
{
    /// <summary>The bot goes for a vial when it is below this share of its health.</summary>
    public const float HurtShare = 0.5f;

    /// <summary>The farthest vial or key the bot walks to.</summary>
    public const float PickupRange = 25f;

    /// <summary>The farthest it shoots, whatever the weapon reaches.</summary>
    public const float ShotRange = 25f;

    /// <summary>A melee attack starts at this share of the weapon's range, so the blow is sure to reach.</summary>
    public const float MeleeShare = 0.9f;

    /// <summary>The distance an attack with this weapon starts from.</summary>
    public static float AttackDistance(bool isRanged, float reach) => isRanged ? MathF.Min(reach, ShotRange) : reach * MeleeShare;

    public static FightChoice Decide(FightView view)
    {
        if (view.IsRanged && view.Rounds == 0 && !view.IsReloading)
        {
            return new(FightGoal.Hold, false, true);
        }

        if (view.IsReloading)
        {
            return new(FightGoal.Hold, false, false);
        }

        if (IsHurt(view) && view.VialDistance <= PickupRange)
        {
            return new(FightGoal.Vial, false, false);
        }

        if (view.TargetDistance is { } distance)
        {
            return Engage(view, distance);
        }

        return view.KeyDistance <= PickupRange ? new(FightGoal.Key, false, false) : new(FightGoal.Route, false, false);
    }

    private static bool IsHurt(FightView view) => view.Health < view.MaxHealth * HurtShare;

    private static FightChoice Engage(FightView view, float distance) =>
        view.TargetInSight && distance <= AttackDistance(view.IsRanged, view.Reach)
            ? new(FightGoal.Fight, true, false)
            : new(FightGoal.Approach, false, false);
}

/// <summary>Who dealt the last damage: read from the events of the tick, since a hit names no source.</summary>
internal static class DamageSource
{
    /// <summary>The source of a <see cref="PlayerHit"/> among <paramref name="events"/> (one tick), or null without one.</summary>
    public static string? Of(IEnumerable<GameEvent> events)
    {
        var list = events.ToList();
        if (!list.OfType<PlayerHit>().Any())
        {
            return null;
        }

        foreach (var (matches, name) in Sources)
        {
            if (list.Any(matches))
            {
                return name;
            }
        }

        return "unknown";
    }

    // One line per source: a new one is a new line here. The order matters only where two land in one tick.
    private static readonly (Func<GameEvent, bool> Matches, string Name)[] Sources =
    [
        (e => e is GhoulStruck { Hit: true }, "ghoul"),
        (e => e is BatBit, "bat"),
        (e => e is GormSwung { Hit: true }, "Gorm (swing)"),
        (e => e is ShockwaveHit, "Gorm (shockwave)"),
        (e => e is HookHit, "Gorm (hook)"),
        (e => e is StoneHit, "Gorm (stone)"),
        (e => e is ProjectileHitPlayer, "projectile"),
    ];
}
