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

    /// <summary>Default border brush (line drawing) color.</summary>
    public const string BorderBrushColor = "ThemeBorderBrushColor";

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

    // ────────────────────────────────────────────────────────────────
    //  ProgressBar colors
    // ────────────────────────────────────────────────────────────────

    /// <summary>ProgressBar filled bar foreground color.</summary>
    public const string ProgressForegroundColor = "ThemeProgressForegroundColor";

    /// <summary>ProgressBar background color (behind the bar).</summary>
    public const string ProgressBackgroundColor = "ThemeProgressBackgroundColor";

    /// <summary>ProgressBar unfilled track foreground color.</summary>
    public const string ProgressTrackColor = "ThemeProgressTrackColor";

    // ────────────────────────────────────────────────────────────────
    //  TextBox colors
    // ────────────────────────────────────────────────────────────────

    /// <summary>TextBox background color.</summary>
    public const string TextBoxBackgroundColor = "ThemeTextBoxBackgroundColor";

    /// <summary>TextBox foreground (text) color.</summary>
    public const string TextBoxForegroundColor = "ThemeTextBoxForegroundColor";

    /// <summary>TextBox focus border color.</summary>
    public const string TextBoxFocusColor = "ThemeTextBoxFocusColor";

    /// <summary>TextBox hover border color.</summary>
    public const string TextBoxHoverColor = "ThemeTextBoxHoverColor";

    /// <summary>TextBox placeholder text color.</summary>
    public const string TextBoxPlaceholderColor = "ThemeTextBoxPlaceholderColor";

    // ────────────────────────────────────────────────────────────────
    //  ScrollViewer colors
    // ────────────────────────────────────────────────────────────────

    /// <summary>ScrollViewer scroll indicator foreground color.</summary>
    public const string ScrollIndicatorColor = "ThemeScrollIndicatorColor";

    // ────────────────────────────────────────────────────────────────
    //  CheckBox / RadioButton colors
    // ────────────────────────────────────────────────────────────────

    /// <summary>CheckBox/RadioButton foreground color.</summary>
    public const string CheckBoxForegroundColor = "ThemeCheckBoxForegroundColor";

    /// <summary>CheckBox/RadioButton focus indicator color.</summary>
    public const string CheckBoxFocusColor = "ThemeCheckBoxFocusColor";

    /// <summary>CheckBox/RadioButton hover indicator color.</summary>
    public const string CheckBoxHoverColor = "ThemeCheckBoxHoverColor";

    // ────────────────────────────────────────────────────────────────
    //  ComboBox colors
    // ────────────────────────────────────────────────────────────────

    /// <summary>ComboBox background color.</summary>
    public const string ComboBoxBackgroundColor = "ThemeComboBoxBackgroundColor";

    /// <summary>ComboBox foreground color.</summary>
    public const string ComboBoxForegroundColor = "ThemeComboBoxForegroundColor";

    /// <summary>ComboBox focus border color.</summary>
    public const string ComboBoxFocusColor = "ThemeComboBoxFocusColor";

    /// <summary>ComboBox hover border color.</summary>
    public const string ComboBoxHoverColor = "ThemeComboBoxHoverColor";

    // ────────────────────────────────────────────────────────────────
    //  Dialog colors
    // ────────────────────────────────────────────────────────────────

    /// <summary>Dialog window background color.</summary>
    public const string DialogBackgroundColor = "ThemeDialogBackgroundColor";

    /// <summary>Dialog window foreground (text) color.</summary>
    public const string DialogForegroundColor = "ThemeDialogForegroundColor";

    /// <summary>Dialog border/accent color.</summary>
    public const string DialogBorderColor = "ThemeDialogBorderColor";

    // ────────────────────────────────────────────────────────────────
    //  TabControl colors
    // ────────────────────────────────────────────────────────────────

    /// <summary>TabControl background color.</summary>
    public const string TabControlBackgroundColor = "ThemeTabControlBackgroundColor";

    /// <summary>TabControl foreground color.</summary>
    public const string TabControlForegroundColor = "ThemeTabControlForegroundColor";

    /// <summary>TabControl border color.</summary>
    public const string TabControlBorderColor = "ThemeTabControlBorderColor";

    /// <summary>Selected tab header background color.</summary>
    public const string TabSelectedBackgroundColor = "ThemeTabSelectedBackgroundColor";

    /// <summary>Selected tab header foreground color.</summary>
    public const string TabSelectedForegroundColor = "ThemeTabSelectedForegroundColor";

    /// <summary>Unselected tab header foreground color.</summary>
    public const string TabUnselectedForegroundColor = "ThemeTabUnselectedForegroundColor";

    // ────────────────────────────────────────────────────────────────
    //  TreeView colors
    // ────────────────────────────────────────────────────────────────

    /// <summary>TreeView background color.</summary>
    public const string TreeViewBackgroundColor = "ThemeTreeViewBackgroundColor";

    /// <summary>TreeView foreground color.</summary>
    public const string TreeViewForegroundColor = "ThemeTreeViewForegroundColor";

    /// <summary>TreeView expand/collapse indicator color.</summary>
    public const string TreeViewExpandIndicatorColor = "ThemeTreeViewExpandIndicatorColor";

    // ────────────────────────────────────────────────────────────────
    //  ListView colors
    // ────────────────────────────────────────────────────────────────

    /// <summary>ListView background color.</summary>
    public const string ListViewBackgroundColor = "ThemeListViewBackgroundColor";

    /// <summary>ListView foreground color.</summary>
    public const string ListViewForegroundColor = "ThemeListViewForegroundColor";

    /// <summary>ListView header row background color.</summary>
    public const string ListViewHeaderBackgroundColor = "ThemeListViewHeaderBackgroundColor";

    /// <summary>ListView header row foreground color.</summary>
    public const string ListViewHeaderForegroundColor = "ThemeListViewHeaderForegroundColor";

    /// <summary>ListView grid line / separator color.</summary>
    public const string ListViewGridLineColor = "ThemeListViewGridLineColor";

    // ────────────────────────────────────────────────────────────────
    //  Picker colors (NumberPicker, DatePicker, TimePicker, DateTimePicker)
    // ────────────────────────────────────────────────────────────────

    /// <summary>Picker background color.</summary>
    public const string PickerBackgroundColor = "ThemePickerBackgroundColor";

    /// <summary>Picker foreground color.</summary>
    public const string PickerForegroundColor = "ThemePickerForegroundColor";

    /// <summary>Picker focus border color.</summary>
    public const string PickerFocusColor = "ThemePickerFocusColor";

    /// <summary>Picker hover border color.</summary>
    public const string PickerHoverColor = "ThemePickerHoverColor";

    /// <summary>Picker active field highlight color.</summary>
    public const string PickerHighlightColor = "ThemePickerHighlightColor";

    // ────────────────────────────────────────────────────────────────
    //  ColorPicker colors
    // ────────────────────────────────────────────────────────────────

    /// <summary>ColorPicker background color.</summary>
    public const string ColorPickerBackgroundColor = "ThemeColorPickerBackgroundColor";

    /// <summary>ColorPicker foreground color.</summary>
    public const string ColorPickerForegroundColor = "ThemeColorPickerForegroundColor";

    /// <summary>ColorPicker focus border color.</summary>
    public const string ColorPickerFocusColor = "ThemeColorPickerFocusColor";

    /// <summary>ColorPicker hover border color.</summary>
    public const string ColorPickerHoverColor = "ThemeColorPickerHoverColor";
}
