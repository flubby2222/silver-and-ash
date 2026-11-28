using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The designer's weapon models in the hands (issue #249): the pistol and the melee weapons with a model
/// (<see cref="WeaponPose.MeleeModel"/>: the silver blade, the club, the hunter's axe and the whip, issues #403, #776, #786), each with its
/// sleeve (cuff and forearm, issue #265). A weapon is drawn as a model only if all its files loaded - otherwise the placeholder boxes,
/// so a weapon never floats without a sleeve or a sleeve without a weapon.
/// </summary>
internal sealed class WeaponModels : IDisposable
{
    private readonly Dictionary<WeaponKind, (TexturedModel Weapon, TexturedModel Hand)> _melee = [];
    private readonly Dictionary<WeaponKind, TexturedModel> _strikes = [];

    private WeaponModels(TexturedModel? pistol, TexturedModel? pistolHand)
    {
        if (pistol is not null && pistolHand is not null)
        {
            Pistol = pistol;
            PistolHand = pistolHand;
        }
        else
        {
            pistol?.Dispose();
            pistolHand?.Dispose();
        }
    }

    public TexturedModel? Pistol { get; }

    /// <summary>The stake crossbow's loaded file (issue #793); null together with <see cref="CrossbowRelaxed"/> if either is missing.</summary>
    public TexturedModel? CrossbowLoaded { get; private set; }

    /// <summary>The stake crossbow's relaxed file (issue #793).</summary>
    public TexturedModel? CrossbowRelaxed { get; private set; }

    public TexturedModel? PistolHand { get; }

    /// <summary>The throwing knife in the hand, cut off behind the cuff (issue #821); null together with <see cref="Knife"/> if either is missing.</summary>
    public TexturedModel? KnifeHand { get; private set; }

    /// <summary>The whole throwing knife, for the flight and for what it sticks in (issue #821).</summary>
    public TexturedModel? Knife { get; private set; }

    /// <summary>The model and sleeve of melee weapon <paramref name="kind"/>, or false for the placeholder boxes.</summary>
    public bool TryGetMelee(WeaponKind kind, out TexturedModel weapon, out TexturedModel hand)
    {
        var found = _melee.TryGetValue(kind, out var pair);
        (weapon, hand) = pair;
        return found;
    }

    /// <summary>
    /// The second file of melee weapon <paramref name="kind"/>, drawn while it strikes (the whip's straight lash, issue
    /// #786), or false if it has none.
    /// </summary>
    public bool TryGetStrike(WeaponKind kind, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TexturedModel? strike) =>
        _strikes.TryGetValue(kind, out strike);

    /// <summary>
    /// Loads pistol.glb, pistol_hand.glb and the files of every melee weapon with a model from
    /// <paramref name="directory"/>. A missing or broken file: a warning in the log and the placeholder for that weapon.
    /// Needs the window.
    /// </summary>
    public static WeaponModels Load(string directory, FileLog? log)
    {
        var models = new WeaponModels(
            TexturedModel.TryLoad(Path.Combine(directory, "pistol.glb"), WeaponPose.PistolParts, log),
            TexturedModel.TryLoad(Path.Combine(directory, "pistol_hand.glb"), WeaponPose.HandParts, log));

        models.LoadCrossbow(directory, log);
        models.LoadKnives(directory, log);
        foreach (var kind in Enum.GetValues<WeaponKind>())
        {
            if (WeaponPose.MeleeModel(kind) is not { } files)
            {
                continue;
            }

            var weapon = TexturedModel.TryLoad(Path.Combine(directory, files.Weapon), files.Parts, log);
            var hand = TexturedModel.TryLoad(Path.Combine(directory, files.Hand), WeaponPose.HandParts, log);
            var strike = files.StrikeWeapon is { } strikeFile
                ? TexturedModel.TryLoad(Path.Combine(directory, strikeFile), files.Parts, log)
                : null;
            if (weapon is not null && hand is not null && (files.StrikeWeapon is null || strike is not null))
            {
                models._melee[kind] = (weapon, hand);
                if (strike is not null)
                {
                    models._strikes[kind] = strike;
                }
            }
            else
            {
                weapon?.Dispose();
                hand?.Dispose();
                strike?.Dispose();
            }
        }

        return models;
    }

    // The stake crossbow's two files: both or neither, so it never shows a bow without its string (the boxes then).
    private void LoadCrossbow(string directory, FileLog? log)
    {
        var loaded = TexturedModel.TryLoad(Path.Combine(directory, StakeCrossbowPose.LoadedFile), StakeCrossbowPose.Parts, log);
        var relaxed = TexturedModel.TryLoad(Path.Combine(directory, StakeCrossbowPose.RelaxedFile), StakeCrossbowPose.Parts, log);
        if (loaded is not null && relaxed is not null)
        {
            CrossbowLoaded = loaded;
            CrossbowRelaxed = relaxed;
        }
        else
        {
            loaded?.Dispose();
            relaxed?.Dispose();
        }
    }

    // The throwing knives' two files: both or neither, so the knife that leaves the hand is the one that flies.
    private void LoadKnives(string directory, FileLog? log)
    {
        var hand = TexturedModel.TryLoad(Path.Combine(directory, ThrowingKnivesPose.HandFile), ThrowingKnivesPose.Parts, log);
        var whole = TexturedModel.TryLoad(Path.Combine(directory, ThrowingKnivesPose.KnifeFile), ThrowingKnivesPose.Parts, log);
        if (hand is not null && whole is not null)
        {
            KnifeHand = hand;
            Knife = whole;
        }
        else
        {
            hand?.Dispose();
            whole?.Dispose();
        }
    }

    public void Dispose()
    {
        KnifeHand?.Dispose();
        Knife?.Dispose();
        CrossbowLoaded?.Dispose();
        CrossbowRelaxed?.Dispose();
        Pistol?.Dispose();
        PistolHand?.Dispose();
        foreach (var (weapon, hand) in _melee.Values)
        {
            weapon.Dispose();
            hand.Dispose();
        }

        foreach (var strike in _strikes.Values)
        {
            strike.Dispose();
        }

        _melee.Clear();
        _strikes.Clear();
    }
}
