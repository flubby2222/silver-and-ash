using SilverAndAsh.Core.Gameplay;

namespace SilverAndAsh.Core.Presentation;

/// <summary>
/// One weapon place of the HUD: the weapon in it (null while empty), its rounds (null for a melee weapon or an empty
/// place), whether it is reloading and whether it is the chosen ranged weapon, and how far the reload has come (0 to 1,
/// 0 while not reloading; issue #370) and how many rounds its magazine holds (null like the rounds; issue #388).
/// </summary>
public readonly record struct HudWeapon(WeaponKind? Kind, int? Rounds, bool IsReloading, bool IsSelected, float ReloadProgress = 0f,
    int? MagazineSize = null);

/// <summary>The boss of a running boss fight: his name and how much of his health is left (0 to 1).</summary>
public readonly record struct HudBoss(string Name, float Health);

/// <summary>
/// Everything the HUD shows of one player (issue #369), read from the world in one go: fractions from 0 to 1 for the
/// bars, plain numbers for what keeps its number. The Game project only draws it (via <see cref="HudLayout"/>).
/// </summary>
/// <param name="IsDead">True once the player has died: no crosshair and no weapons any more.</param>
/// <param name="Life">Health left, 0 to 1.</param>
/// <param name="Stamina">Stamina left, 0 to 1.</param>
/// <param name="Level">The level of the player.</param>
/// <param name="Experience">The way to the next level, 0 to 1.</param>
/// <param name="PendingBoons">Level ups whose boon is still to be chosen (the skill point sign shows while above 0).</param>
/// <param name="Silver">The silver of this run.</param>
/// <param name="Keys">The keys carried.</param>
/// <param name="Weapons">The melee hand first, then the ranged places in order.</param>
/// <param name="Items">The items found, in the order taken.</param>
/// <param name="Boss">The boss while a boss fight is on, else null.</param>
/// <param name="ConsecratedSecondsLeft">Seconds left of consecrated ammunition, 0 while it is not.</param>
/// <param name="IsMeleeInUse">
/// True while the melee weapon is in use - charged or swung, until it is ready again (issue #388): the large rounds
/// number of the chosen ranged weapon gives way meanwhile.
/// </param>
/// <param name="MaxLife">
/// The highest health of the player (issue #435): the life bar is as long, one pixel per point on a canvas 480 lines high.
/// </param>
/// <param name="LifePerTick">What one health boon adds: the life bar has a tick mark every so many points (issue #435).</param>
public sealed record HudReadout(
    bool IsDead,
    float Life,
    float Stamina,
    int Level,
    float Experience,
    int PendingBoons,
    int Silver,
    int Keys,
    IReadOnlyList<HudWeapon> Weapons,
    IReadOnlyList<ItemKind> Items,
    HudBoss? Boss,
    float ConsecratedSecondsLeft,
    bool IsMeleeInUse = false,
    int MaxLife = 100,
    int LifePerTick = 5)
{
    /// <summary>The HUD of <paramref name="player"/> in <paramref name="world"/> as it is now.</summary>
    public static HudReadout Of(GameWorld world, Player player)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);

        var weapons = new List<HudWeapon>(1 + Player.RangedSlotCount) { new(player.Melee, null, false, false) };
        for (var slot = 0; slot < Player.RangedSlotCount; slot++)
        {
            weapons.Add(player.RangedSlots[slot] is { } ranged
                ? new HudWeapon(ranged.Kind, ranged.Magazine.Rounds, ranged.Magazine.IsReloading, slot == player.SelectedSlot,
                    ranged.Magazine.ReloadProgress, ranged.Magazine.Size)
                : new HudWeapon(null, null, false, false));
        }

        var boss = world.IsBossFightOn && world.Gorm is { } gorm
            ? new HudBoss(Gorm.Name, Fraction(gorm.Health, gorm.MaxHealth))
            : (HudBoss?)null;

        return new HudReadout(
            player.IsDead,
            Fraction(player.Health, player.MaxHealth),
            Math.Clamp(player.Stamina.Fraction, 0f, 1f),
            player.Level,
            Fraction(player.Experience, player.ExperienceToNextLevel),
            player.PendingBoons,
            player.Silver,
            player.Keys,
            weapons,
            player.Items.ToList(),
            boss,
            player.IsConsecrated ? player.ConsecratedSecondsLeft : 0f,
            player.MeleeCharge.IsCharging || !player.MeleeCooldown.IsReady,
            player.MaxHealth,
            world.Balance.Boons.Health);
    }

    private static float Fraction(int part, int whole) => whole > 0 ? Math.Clamp((float)part / whole, 0f, 1f) : 0f;
}
