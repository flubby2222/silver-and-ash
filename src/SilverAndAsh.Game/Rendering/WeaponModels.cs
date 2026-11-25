using SilverAndAsh.Core.Gameplay;
using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The designer's weapon models in the hands (issue #249): the pistol and the melee weapons with a model
/// (<see cref="WeaponPose.MeleeModel"/>: the silver blade, the club and the hunter's axe, issues #403, #776), each with its sleeve (cuff and
/// forearm, issue #265). A weapon is drawn as a model only if both its files loaded - otherwise the placeholder boxes,
/// so a weapon never floats without a sleeve or a sleeve without a weapon.
/// </summary>
internal sealed class WeaponModels : IDisposable
{
    private readonly Dictionary<WeaponKind, (TexturedModel Weapon, TexturedModel Hand)> _melee = [];

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

    public TexturedModel? PistolHand { get; }

    /// <summary>The model and sleeve of melee weapon <paramref name="kind"/>, or false for the placeholder boxes.</summary>
    public bool TryGetMelee(WeaponKind kind, out TexturedModel weapon, out TexturedModel hand)
    {
        var found = _melee.TryGetValue(kind, out var pair);
        (weapon, hand) = pair;
        return found;
    }

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

        foreach (var kind in Enum.GetValues<WeaponKind>())
        {
            if (WeaponPose.MeleeModel(kind) is not { } files)
            {
                continue;
            }

            var weapon = TexturedModel.TryLoad(Path.Combine(directory, files.Weapon), files.Parts, log);
            var hand = TexturedModel.TryLoad(Path.Combine(directory, files.Hand), WeaponPose.HandParts, log);
            if (weapon is not null && hand is not null)
            {
                models._melee[kind] = (weapon, hand);
            }
            else
            {
                weapon?.Dispose();
                hand?.Dispose();
            }
        }

        return models;
    }

    public void Dispose()
    {
        Pistol?.Dispose();
        PistolHand?.Dispose();
        foreach (var (weapon, hand) in _melee.Values)
        {
            weapon.Dispose();
            hand.Dispose();
        }

        _melee.Clear();
    }
}
