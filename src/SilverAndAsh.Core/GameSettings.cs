namespace SilverAndAsh.Core;

/// <summary>
/// The player's own settings (not balancing, not part of the world): mouse sensitivity, three volumes and full screen
/// or window (issue #349). Changed in the settings menu and kept in a small local file (see <see cref="SettingsFile"/>) -
/// no save game, just settings. Only the game window uses them; the simulation never sees them.
/// </summary>
public sealed record GameSettings
{
    /// <summary>Lowest mouse sensitivity (a fifth of the standard).</summary>
    public const float MinMouseSensitivity = 0.2f;

    /// <summary>Highest mouse sensitivity (three times the standard).</summary>
    public const float MaxMouseSensitivity = 3f;

    /// <summary>How much one click on "-" or "+" changes the sensitivity.</summary>
    public const float MouseSensitivityStep = 0.1f;

    /// <summary>How much one click on "-" or "+" changes a volume (10 %). Volumes run from 0 (silent) to 1 (full).</summary>
    public const float VolumeStep = 0.1f;

    public static GameSettings Default { get; } = new();

    /// <summary>
    /// Factor on <see cref="ControlSettings.MouseSensitivity"/>: 1 = standard, 2 = the view turns twice as far per
    /// pixel of mouse movement.
    /// </summary>
    public float MouseSensitivity { get; init; } = 1f;

    /// <summary>Volume of everything, 0 to 1 - on top of the effects and the ambience.</summary>
    public float MasterVolume { get; init; } = 1f;

    /// <summary>Volume of the short sounds (blows, shots, steps, enemies, menu click), 0 to 1.</summary>
    public float EffectsVolume { get; init; } = 1f;

    /// <summary>Volume of the ambience - the title theme and the catacomb music -, 0 to 1.</summary>
    public float AmbienceVolume { get; init; } = 1f;

    /// <summary>True: borderless full screen in the desktop's resolution; false: a window.</summary>
    public bool FullScreen { get; init; }

    /// <summary>The settings with the sensitivity changed by <paramref name="steps"/> steps (+ faster, - slower), within the range.</summary>
    public GameSettings ChangedSensitivity(int steps) =>
        (this with { MouseSensitivity = MouseSensitivity + steps * MouseSensitivityStep }).InRange();

    /// <summary>The settings with the master volume changed by <paramref name="steps"/> steps of 10 %, within 0 to 1.</summary>
    public GameSettings ChangedMasterVolume(int steps) =>
        (this with { MasterVolume = MasterVolume + steps * VolumeStep }).InRange();

    /// <summary>The settings with the effects volume changed by <paramref name="steps"/> steps of 10 %, within 0 to 1.</summary>
    public GameSettings ChangedEffectsVolume(int steps) =>
        (this with { EffectsVolume = EffectsVolume + steps * VolumeStep }).InRange();

    /// <summary>The settings with the ambience volume changed by <paramref name="steps"/> steps of 10 %, within 0 to 1.</summary>
    public GameSettings ChangedAmbienceVolume(int steps) =>
        (this with { AmbienceVolume = AmbienceVolume + steps * VolumeStep }).InRange();

    /// <summary>
    /// These settings with every value brought into its allowed range. Rounded to two decimals, so 1.0 + 0.1 + 0.1
    /// shows as 1.20 and not as 1.2000001.
    /// </summary>
    internal GameSettings InRange() => this with
    {
        MouseSensitivity = Clamped(MouseSensitivity, MinMouseSensitivity, MaxMouseSensitivity),
        MasterVolume = Clamped(MasterVolume, 0f, 1f),
        EffectsVolume = Clamped(EffectsVolume, 0f, 1f),
        AmbienceVolume = Clamped(AmbienceVolume, 0f, 1f),
    };

    private static float Clamped(float value, float min, float max) =>
        Math.Clamp(MathF.Round(value * 100f) / 100f, min, max);
}
