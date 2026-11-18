namespace SilverAndAsh.Game.Rendering;

/// <summary>Which page of the title screen or the Esc menu is shown (issue #349).</summary>
internal enum MenuPage
{
    /// <summary>The menu's own buttons.</summary>
    Main,

    /// <summary>The settings.</summary>
    Settings,

    /// <summary>The question before the game closes.</summary>
    ConfirmQuit,
}
