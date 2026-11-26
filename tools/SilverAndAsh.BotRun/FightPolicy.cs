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

    /// <summary>Walk away from the nearest enemy (facing it), shooting on the way when the weapon is ranged.</summary>
    Retreat,

    /// <summary>Walk to a blood vial.</summary>
    Vial,

    /// <summary>Walk to a key lying about.</summary>
    Key,

    /// <summary>Walk on along the route to Gorm.</summary>
    Route,
}

/// <summary>
/// What the bot sees of one moment (<paramref name="ThreatDistance"/>: the nearest enemy that draws back for a blow, as
/// its body shows it; <paramref name="CanDodge"/>: on the ground, not dodging, and the stamina pays a dodge). Distances are in metres, flat, to the edge of the body. Everything the policy needs
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
    float? KeyDistance,
    float? ThreatDistance = null,
    bool CanDodge = false);

/// <summary>The bot's decision: where to go or stand, whether to attack, whether to reload, whether to dodge (Shift).</summary>
internal readonly record struct FightChoice(FightGoal Goal, bool Attack, bool Reload, bool Dodge = false);

/// <summary>
/// The bot's whole play, fixed and simple (issues #803, #805): step back from an enemy that draws back for a blow, keep a
/// ranged weapon's distance, reload only with nobody near, heal when hurt and a vial lies in reach, fight what is in
/// front of it, otherwise walk the route. No choice of weapon. What it wins is a lower bound of what a
/// player can do.
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

    /// <summary>A melee bot steps back from an enemy drawing back for a blow while it is this close (metres to the body's edge).</summary>
    public const float DodgeRange = 3f;

    /// <summary>A ranged bot walks away from an enemy this close (metres to the body's edge) and does not reload there.</summary>
    public const float KeepAway = 4.5f;

    /// <summary>The distance an attack with this weapon starts from.</summary>
    public static float AttackDistance(bool isRanged, float reach) => isRanged ? MathF.Min(reach, ShotRange) : reach * MeleeShare;

    public static FightChoice Decide(FightView view)
    {
        if (ShouldStepBack(view))
        {
            return new(FightGoal.Retreat, CanShootOnTheWay(view), MustReload(view), view.CanDodge && view.ThreatDistance < DodgeRange);
        }

        if (MustReload(view))
        {
            return new(FightGoal.Hold, false, true);
        }

        if (view.IsReloading)
        {
            return new(FightGoal.Hold, false, false);
        }

        if (IsHurt(view) && view.VialDistance <= PickupRange && !IsInStrikingDistance(view))
        {
            return new(FightGoal.Vial, false, false);
        }

        if (view.TargetDistance is { } distance)
        {
            return Engage(view, distance);
        }

        return view.KeyDistance <= PickupRange ? new(FightGoal.Key, false, false) : new(FightGoal.Route, false, false);
    }

    // A melee bot leaves the reach of a blow that is coming; a ranged one keeps its distance from whatever is near.
    private static bool ShouldStepBack(FightView view) => view.IsRanged
        ? view.TargetInSight && view.TargetDistance < KeepAway
        : view.ThreatDistance < DodgeRange;

    private static bool MustReload(FightView view) => view.IsRanged && view.Rounds == 0 && !view.IsReloading;

    private static bool CanShootOnTheWay(FightView view) =>
        view.IsRanged && view.Rounds > 0 && !view.IsReloading && view.TargetInSight
        && view.TargetDistance <= AttackDistance(view.IsRanged, view.Reach);

    // An enemy in sight within the distance an attack starts from is dealt with before the bot walks to a vial.
    private static bool IsInStrikingDistance(FightView view) =>
        view.TargetInSight && view.TargetDistance <= AttackDistance(view.IsRanged, view.Reach);

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
