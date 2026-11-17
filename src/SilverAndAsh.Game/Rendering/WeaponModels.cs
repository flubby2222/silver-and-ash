using SilverAndAsh.Core.Logging;
using SilverAndAsh.Core.Presentation;

namespace SilverAndAsh.Game.Rendering;

/// <summary>
/// The designer's weapon models in the hands (issue #249): the pistol and the silver blade, each with its sleeve (cuff
/// and forearm, issue #265). A weapon is drawn as a model only if both its files loaded - otherwise the placeholder
/// boxes, so a weapon never floats without a sleeve or a sleeve without a weapon.
/// </summary>
internal sealed class WeaponModels : IDisposable
{
    private WeaponModels(TexturedModel? pistol, TexturedModel? pistolHand, TexturedModel? blade, TexturedModel? bladeHand)
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

        if (blade is not null && bladeHand is not null)
        {
            Blade = blade;
            BladeHand = bladeHand;
        }
        else
        {
            blade?.Dispose();
            bladeHand?.Dispose();
        }
    }

    public TexturedModel? Pistol { get; }

    public TexturedModel? PistolHand { get; }

    public TexturedModel? Blade { get; }

    public TexturedModel? BladeHand { get; }

    /// <summary>
    /// Loads pistol.glb, pistol_hand.glb, blade.glb and blade_hand.glb from <paramref name="directory"/>. A missing or
    /// broken file: a warning in the log and the placeholder for that weapon. Needs the window.
    /// </summary>
    public static WeaponModels Load(string directory, FileLog? log) => new(
        TexturedModel.TryLoad(Path.Combine(directory, "pistol.glb"), WeaponPose.PistolParts, log),
        TexturedModel.TryLoad(Path.Combine(directory, "pistol_hand.glb"), WeaponPose.HandParts, log),
        TexturedModel.TryLoad(Path.Combine(directory, "blade.glb"), WeaponPose.BladeParts, log),
        TexturedModel.TryLoad(Path.Combine(directory, "blade_hand.glb"), WeaponPose.HandParts, log));

    public void Dispose()
    {
        Pistol?.Dispose();
        PistolHand?.Dispose();
        Blade?.Dispose();
        BladeHand?.Dispose();
    }
}
