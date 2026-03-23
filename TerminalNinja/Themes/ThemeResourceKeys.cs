namespace TerminalNinja.Themes;

/// <summary>
/// Well-known resource key strings for theme color resources.
/// Theme XAML files define <c>&lt;Color x:Key="..."&gt;</c> resources using these keys.
/// Controls reference them via <c>{StaticResource ThemeBackgroundColor}</c> etc.
/// </summary>
public static class ThemeResourceKeys
{
    // ────────────────────────────────────────────────────────────────
    //  Global / application-level colors
    // ────────────────────────────────────────────────────────────────

    /// <summary>Default background color for the application / window.</summary>
    public const string BackgroundColor = "ThemeBackgroundColor";

    /// <summary>Default foreground (text) color for the application.</summary>
    public const string ForegroundColor = "ThemeForegroundColor";

    /// <summary>Primary accent color used for highlights, selection, focus indicators.</summary>
    public const string AccentColor = "ThemeAccentColor";

    /// <summary>Secondary accent / hover color.</summary>
    public const string AccentSecondaryColor = "ThemeAccentSecondaryColor";

    // ────────────────────────────────────────────────────────────────
    //  Border colors
    // ────────────────────────────────────────────────────────────────

    /// <summary>Default border foreground color.</summary>
    public const string BorderForegroundColor = "ThemeBorderForegroundColor";

    /// <summary>Default border background color.</summary>
    public const string BorderBackgroundColor = "ThemeBorderBackgroundColor";

    // ────────────────────────────────────────────────────────────────
    //  Button colors
    // ────────────────────────────────────────────────────────────────

    /// <summary>Button background color.</summary>
    public const string ButtonBackgroundColor = "ThemeButtonBackgroundColor";

    /// <summary>Button foreground (text) color.</summary>
    public const string ButtonForegroundColor = "ThemeButtonForegroundColor";

    /// <summary>Button focus border color.</summary>
    public const string ButtonFocusColor = "ThemeButtonFocusColor";

    /// <summary>Button hover border color.</summary>
    public const string ButtonHoverColor = "ThemeButtonHoverColor";

    // ────────────────────────────────────────────────────────────────
    //  TextBlock colors
    // ────────────────────────────────────────────────────────────────

    /// <summary>TextBlock foreground (text) color.</summary>
    public const string TextForegroundColor = "ThemeTextForegroundColor";

    /// <summary>TextBlock background color.</summary>
    public const string TextBackgroundColor = "ThemeTextBackgroundColor";

    // ────────────────────────────────────────────────────────────────
    //  Selection colors (ListBox / ListBoxItem)
    // ────────────────────────────────────────────────────────────────

    /// <summary>Background color of selected items.</summary>
    public const string SelectedBackgroundColor = "ThemeSelectedBackgroundColor";

    /// <summary>Foreground color of selected items.</summary>
    public const string SelectedForegroundColor = "ThemeSelectedForegroundColor";

    // ────────────────────────────────────────────────────────────────
    //  Surface colors (panels, content areas)
    // ────────────────────────────────────────────────────────────────

    /// <summary>Background for panel/surface areas (slightly different from main background).</summary>
    public const string SurfaceColor = "ThemeSurfaceColor";

    /// <summary>Muted/dimmed text color for secondary information.</summary>
    public const string MutedForegroundColor = "ThemeMutedForegroundColor";
}
