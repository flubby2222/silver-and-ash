namespace SilverAndAsh.Core;

/// <summary>
/// The player's own settings (not balancing): today only the mouse sensitivity. Changed in the Esc menu and kept
/// in a small local file (see <see cref="SettingsFile"/>) - no save game, just settings.
/// </summary>
public sealed record GameSettings
{
    /// <summary>Lowest mouse sensitivity (a fifth of the standard).</summary>
    public const float MinMouseSensitivity = 0.2f;

    /// <summary>Highest mouse sensitivity (three times the standard).</summary>
    public const float MaxMouseSensitivity = 3f;

    /// <summary>How much one click on "-" or "+" changes the sensitivity.</summary>
    public const float MouseSensitivityStep = 0.1f;

    public static GameSettings Default { get; } = new();

    /// <summary>
    /// Factor on <see cref="ControlSettings.MouseSensitivity"/>: 1 = standard, 2 = the view turns twice as far per
    /// pixel of mouse movement.
    /// </summary>
    public float MouseSensitivity { get; init; } = 1f;

    /// <summary>The settings with the sensitivity changed by <paramref name="steps"/> steps (+ faster, - slower), within the range.</summary>
    public GameSettings Changed(int steps) =>
        WithSensitivity(MouseSensitivity + steps * MouseSensitivityStep);

    /// <summary>These settings with every value brought into its allowed range.</summary>
    internal GameSettings InRange() => WithSensitivity(MouseSensitivity);

    // Rounded to two decimals, so 1.0 + 0.1 + 0.1 shows as 1.20 and not as 1.2000001.
    private GameSettings WithSensitivity(float value) => this with
    {
        MouseSensitivity = Math.Clamp(MathF.Round(value * 100f) / 100f, MinMouseSensitivity, MaxMouseSensitivity),
    };
}
