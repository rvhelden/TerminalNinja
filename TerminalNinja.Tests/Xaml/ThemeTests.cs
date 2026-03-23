using TerminalNinja.App;
using TerminalNinja.Themes;

namespace TerminalNinja.Tests.Xaml;

/// <summary>
/// Tests for the theme system covering:
/// - LoadResourceDictionary from XAML strings
/// - Implicit style keying by TargetType
/// - Theme loading/switching/clearing via ResourceDictionary
/// - Implicit style application to controls
/// - Built-in theme loading (Dark, Dracula, GruvboxDark)
/// - ThemeResourceKeys constants
/// - StaticResource resolution within standalone ResourceDictionary
///
/// Note: Tests avoid constructing <see cref="Application"/> directly because the
/// <see cref="TerminalNinja.Input.InputReader"/> requires a real console handle.
/// Instead, theme loading is tested by loading embedded resources directly and
/// wiring <see cref="FrameworkElement.ApplicationResourceLookup"/> manually.
/// </summary>
public class ThemeTests : IDisposable
{
    // ─── Test Infrastructure ─────────────────────────────────────────

    /// <summary>
    /// Simulates Application.Resources for tests that need ApplicationResourceLookup.
    /// </summary>
    private ResourceDictionary? _appResources;

    /// <summary>
    /// Saves the original ApplicationResourceLookup so we can restore it after each test.
    /// </summary>
    private readonly Func<object, object?>? _originalLookup = FrameworkElement.ApplicationResourceLookup;

    /// <summary>
    /// Loads a built-in theme from the TerminalNinja assembly's embedded resources,
    /// the same way <see cref="Application.ThemeName"/> setter does.
    /// </summary>
    private static ResourceDictionary LoadBuiltInTheme(string themeName)
    {
        var resourceName = $"TerminalNinja.Themes.{themeName}.xaml";
        var assembly = typeof(Application).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            var available = assembly.GetManifestResourceNames();
            throw new InvalidOperationException(
                $"Theme '{themeName}' not found. Expected embedded resource '{resourceName}'. " +
                $"Available: [{string.Join(", ", available)}].");
        }

        return TerminalXaml.LoadResourceDictionary(stream);
    }

    /// <summary>
    /// Sets up a fake Application-level resource dictionary with the given theme
    /// and wires FrameworkElement.ApplicationResourceLookup so controls can resolve resources.
    /// </summary>
    private ResourceDictionary SetupTheme(string themeName)
    {
        _appResources = new ResourceDictionary();
        var theme = LoadBuiltInTheme(themeName);
        _appResources.MergedDictionaries.Insert(0, theme);
        FrameworkElement.ApplicationResourceLookup = key =>
            _appResources.TryGetValue(key, out var value) ? value : null;
        return theme;
    }

    public void Dispose()
    {
        // Restore original lookup
        FrameworkElement.ApplicationResourceLookup = _originalLookup;
    }

    // ─── LoadResourceDictionary — Color Resources ────────────────────

    #region LoadResourceDictionary — Color Resources

    [Test]
    public async Task LoadResourceDictionary_ColorResources_LoadsCorrectly()
    {
        // Arrange
        var xaml = """
            <ResourceDictionary
                xmlns="http://schemas.terminalninja.dev/xaml"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Color x:Key="MyBg">#1e1e1e</Color>
                <Color x:Key="MyFg">White</Color>
            </ResourceDictionary>
            """;

        // Act
        var dict = TerminalXaml.LoadResourceDictionary(xaml);

        // Assert
        await Assert.That(dict.ContainsKey("MyBg")).IsTrue();
        await Assert.That(dict.ContainsKey("MyFg")).IsTrue();
        await Assert.That(dict["MyBg"]).IsTypeOf<Color>();
        await Assert.That(dict["MyFg"]).IsTypeOf<Color>();
    }

    [Test]
    public async Task LoadResourceDictionary_MultipleColors_AllPresent()
    {
        // Arrange
        var xaml = """
            <ResourceDictionary
                xmlns="http://schemas.terminalninja.dev/xaml"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Color x:Key="C1">Red</Color>
                <Color x:Key="C2">Green</Color>
                <Color x:Key="C3">Blue</Color>
            </ResourceDictionary>
            """;

        // Act
        var dict = TerminalXaml.LoadResourceDictionary(xaml);

        // Assert
        await Assert.That(dict.Count).IsGreaterThanOrEqualTo(3);
        await Assert.That(dict.ContainsKey("C1")).IsTrue();
        await Assert.That(dict.ContainsKey("C2")).IsTrue();
        await Assert.That(dict.ContainsKey("C3")).IsTrue();
    }

    #endregion

    #region LoadResourceDictionary — Implicit Styles

    [Test]
    public async Task LoadResourceDictionary_ImplicitStyle_KeyedByTargetType()
    {
        // Arrange
        var xaml = """
            <ResourceDictionary
                xmlns="http://schemas.terminalninja.dev/xaml"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Style TargetType="TextBlock">
                    <Setter Property="Foreground" Value="Red" />
                </Style>
            </ResourceDictionary>
            """;

        // Act
        var dict = TerminalXaml.LoadResourceDictionary(xaml);

        // Assert — Style should be keyed by typeof(TextBlock)
        await Assert.That(dict.ContainsKey(typeof(TextBlock))).IsTrue();
        var style = dict[typeof(TextBlock)] as Style;
        await Assert.That(style).IsNotNull();
        await Assert.That(style!.TargetType).IsEqualTo(typeof(TextBlock));
        await Assert.That(style.Setters.Count).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task LoadResourceDictionary_MultipleImplicitStyles_AllKeyedCorrectly()
    {
        // Arrange
        var xaml = """
            <ResourceDictionary
                xmlns="http://schemas.terminalninja.dev/xaml"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Style TargetType="TextBlock">
                    <Setter Property="Foreground" Value="Red" />
                </Style>
                <Style TargetType="Button">
                    <Setter Property="Background" Value="Blue" />
                </Style>
            </ResourceDictionary>
            """;

        // Act
        var dict = TerminalXaml.LoadResourceDictionary(xaml);

        // Assert
        await Assert.That(dict.ContainsKey(typeof(TextBlock))).IsTrue();
        await Assert.That(dict.ContainsKey(typeof(Button))).IsTrue();
    }

    [Test]
    public async Task LoadResourceDictionary_ExplicitKeyedStyle_UsesStringKey()
    {
        // Arrange
        var xaml = """
            <ResourceDictionary
                xmlns="http://schemas.terminalninja.dev/xaml"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Style x:Key="MyStyle" TargetType="TextBlock">
                    <Setter Property="Foreground" Value="Red" />
                </Style>
            </ResourceDictionary>
            """;

        // Act
        var dict = TerminalXaml.LoadResourceDictionary(xaml);

        // Assert — explicitly keyed style uses the string key, not Type
        await Assert.That(dict.ContainsKey("MyStyle")).IsTrue();
    }

    #endregion

    #region Theme Loading — Load / Switch / Clear

    [Test]
    public async Task ThemeLoad_BuiltInTheme_LoadsThemeDictionary()
    {
        // Act
        var theme = SetupTheme("Dark");

        // Assert
        await Assert.That(_appResources!.MergedDictionaries.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(theme.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task ThemeSwitch_ReplacesOldTheme()
    {
        // Arrange — load Dark first
        SetupTheme("Dark");
        var countAfterDark = _appResources!.MergedDictionaries.Count;

        // Act — switch to Dracula
        var draculaTheme = LoadBuiltInTheme("Dracula");
        _appResources.MergedDictionaries.RemoveAt(0);
        _appResources.MergedDictionaries.Insert(0, draculaTheme);

        // Assert — same count (old removed, new added)
        await Assert.That(_appResources.MergedDictionaries.Count).IsEqualTo(countAfterDark);
    }

    [Test]
    public async Task ThemeClear_RemovesThemeDictionary()
    {
        // Arrange
        SetupTheme("Dark");
        var countWithTheme = _appResources!.MergedDictionaries.Count;

        // Act — clear theme
        _appResources.MergedDictionaries.RemoveAt(0);

        // Assert
        await Assert.That(_appResources.MergedDictionaries.Count).IsEqualTo(countWithTheme - 1);
    }

    [Test]
    public async Task ThemeLoad_InvalidTheme_ThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.That(() => LoadBuiltInTheme("NonExistentTheme"))
            .ThrowsExactly<InvalidOperationException>();
    }

    #endregion

    #region Built-In Themes — Load Without Error

    [Test]
    [Arguments("Dark")]
    [Arguments("Dracula")]
    [Arguments("GruvboxDark")]
    public async Task BuiltInTheme_LoadsWithoutError(string themeName)
    {
        // Act — should not throw
        var theme = LoadBuiltInTheme(themeName);

        // Assert
        await Assert.That(theme.Count).IsGreaterThan(0);
    }

    [Test]
    [Arguments("Dark")]
    [Arguments("Dracula")]
    [Arguments("GruvboxDark")]
    public async Task BuiltInTheme_ContainsThemeResourceKeys(string themeName)
    {
        // Arrange
        var theme = LoadBuiltInTheme(themeName);

        // Act — look up key theme resources directly from the theme dictionary
        var bg = theme.TryGetValue(ThemeResourceKeys.BackgroundColor, out var bgVal);
        var fg = theme.TryGetValue(ThemeResourceKeys.ForegroundColor, out var fgVal);
        var accent = theme.TryGetValue(ThemeResourceKeys.AccentColor, out var accentVal);

        // Assert
        await Assert.That(bg).IsTrue();
        await Assert.That(fg).IsTrue();
        await Assert.That(accent).IsTrue();
        await Assert.That(bgVal).IsTypeOf<Color>();
        await Assert.That(fgVal).IsTypeOf<Color>();
        await Assert.That(accentVal).IsTypeOf<Color>();
    }

    [Test]
    [Arguments("Dark")]
    [Arguments("Dracula")]
    [Arguments("GruvboxDark")]
    public async Task BuiltInTheme_ContainsImplicitStyles(string themeName)
    {
        // Arrange
        var theme = LoadBuiltInTheme(themeName);

        // Act — look up implicit styles keyed by Type
        var hasTextBlockStyle = theme.TryGetValue(typeof(TextBlock), out var tbStyle);
        var hasButtonStyle = theme.TryGetValue(typeof(Button), out var btnStyle);
        var hasBorderStyle = theme.TryGetValue(typeof(global::TerminalNinja.Controls.Border), out var borderStyle);

        // Assert
        await Assert.That(hasTextBlockStyle).IsTrue();
        await Assert.That(hasButtonStyle).IsTrue();
        await Assert.That(hasBorderStyle).IsTrue();
        await Assert.That(tbStyle).IsTypeOf<Style>();
        await Assert.That(btnStyle).IsTypeOf<Style>();
        await Assert.That(borderStyle).IsTypeOf<Style>();
    }

    #endregion

    #region Implicit Style Application to Controls

    [Test]
    public async Task ImplicitStyle_TextBlockPicksUpThemeStyle()
    {
        // Arrange — put theme resources on the Window directly (avoids static lookup races)
        var darkTheme = LoadBuiltInTheme("Dark");
        var window = new Window();
        window.Resources.MergedDictionaries.Add(darkTheme);

        var textBlock = new TextBlock { Text = "Hello" };
        window.Content = textBlock;

        // Assert — TextBlock should have picked up the Dark theme's implicit TextBlock style
        // The Dark theme sets TextBlock.Foreground to "#d4d4d4"
        var expectedFg = Color.FromHex("#d4d4d4");
        await Assert.That(textBlock.Foreground).IsEqualTo(expectedFg);
    }

    [Test]
    public async Task ImplicitStyle_ExplicitStyleOverridesImplicitStyle()
    {
        // Arrange — put theme resources on the Window directly
        var darkTheme = LoadBuiltInTheme("Dark");
        var window = new Window();
        window.Resources.MergedDictionaries.Add(darkTheme);

        var textBlock = new TextBlock { Text = "Hello" };

        // Set an explicit style that overrides Foreground
        var explicitStyle = new Style(typeof(TextBlock));
        explicitStyle.Setters.Add(new Setter("Foreground", Color.Magenta));
        textBlock.Style = explicitStyle;

        window.Content = textBlock;

        // Assert — explicit style should win over implicit theme style
        await Assert.That(textBlock.Foreground).IsEqualTo(Color.Magenta);
    }

    [Test]
    public async Task ImplicitStyle_ThemeSwitch_UpdatesControlStyle()
    {
        // This test verifies that when implicit styles change in the resource tree,
        // newly-parented controls pick up the new style. We use local window resources
        // to avoid static ApplicationResourceLookup concurrency issues.

        // Arrange — Dark theme implicit style
        var darkTheme = LoadBuiltInTheme("Dark");
        var window1 = new Window();
        window1.Resources.MergedDictionaries.Add(darkTheme);
        var textBlock1 = new TextBlock { Text = "Hello" };
        window1.Content = textBlock1;

        var darkFg = textBlock1.Foreground;
        var darkExpected = Color.FromHex("#d4d4d4");
        await Assert.That(darkFg).IsEqualTo(darkExpected);

        // Act — Dracula theme implicit style
        var draculaTheme = LoadBuiltInTheme("Dracula");
        var window2 = new Window();
        window2.Resources.MergedDictionaries.Add(draculaTheme);
        var textBlock2 = new TextBlock { Text = "Hello" };
        window2.Content = textBlock2;

        var draculaFg = textBlock2.Foreground;
        var draculaExpected = Color.FromHex("#f8f8f2");

        // Assert — each textBlock got its respective theme color
        await Assert.That(draculaFg).IsEqualTo(draculaExpected);
    }

    #endregion

    #region ThemeResourceKeys Constants

    [Test]
    public async Task ThemeResourceKeys_AllConstantsAreNonEmpty()
    {
        // Collect all key constants into a list (avoids TUnit constant-value restriction)
        var allKeys = GetAllThemeResourceKeys();

        foreach (var key in allKeys)
        {
            await Assert.That(key).IsNotNull().And.IsNotEqualTo("");
        }
    }

    [Test]
    public async Task ThemeResourceKeys_AllStartWithThemePrefix()
    {
        // All keys should follow the "Theme*" naming convention
        var allKeys = GetAllThemeResourceKeys();

        foreach (var key in allKeys)
        {
            await Assert.That(key).StartsWith("Theme");
        }
    }

    [Test]
    public async Task ThemeResourceKeys_Has16Constants()
    {
        var allKeys = GetAllThemeResourceKeys();
        await Assert.That(allKeys.Count).IsEqualTo(16);
    }

    /// <summary>
    /// Returns all ThemeResourceKeys constant values as a list.
    /// Using reflection to read the const fields avoids TUnit's "constant value" assertion error.
    /// </summary>
    private static List<string> GetAllThemeResourceKeys()
    {
        return typeof(ThemeResourceKeys)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .ToList();
    }

    #endregion

    #region BuiltInThemes List

    [Test]
    public async Task BuiltInThemes_ContainsExpectedThemes()
    {
        var themes = Application.BuiltInThemes;

        await Assert.That(themes).Contains("Dark");
        await Assert.That(themes).Contains("Dracula");
        await Assert.That(themes).Contains("GruvboxDark");
    }

    [Test]
    public async Task BuiltInThemes_HasThreeEntries()
    {
        await Assert.That(Application.BuiltInThemes.Count).IsEqualTo(3);
    }

    #endregion

    #region Style Setters in ResourceDictionary

    [Test]
    public async Task LoadResourceDictionary_StyleWithSetters_SettersPreserved()
    {
        // Arrange
        var xaml = """
            <ResourceDictionary
                xmlns="http://schemas.terminalninja.dev/xaml"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <Style TargetType="TextBlock">
                    <Setter Property="Foreground" Value="Cyan" />
                    <Setter Property="Background" Value="Black" />
                </Style>
            </ResourceDictionary>
            """;

        // Act
        var dict = TerminalXaml.LoadResourceDictionary(xaml);
        var style = dict[typeof(TextBlock)] as Style;

        // Assert
        await Assert.That(style).IsNotNull();
        await Assert.That(style!.Setters.Count).IsEqualTo(2);
        await Assert.That(style.Setters[0].Property).IsEqualTo("Foreground");
        await Assert.That(style.Setters[1].Property).IsEqualTo("Background");
    }

    #endregion

    #region All 14 Color Resources Present in Each Theme

    [Test]
    [Arguments("Dark")]
    [Arguments("Dracula")]
    [Arguments("GruvboxDark")]
    public async Task BuiltInTheme_ContainsAll14ColorResources(string themeName)
    {
        // Arrange
        var theme = LoadBuiltInTheme(themeName);

        // Act & Assert — all 14 core ThemeResourceKeys should be present
        string[] keys =
        [
            ThemeResourceKeys.BackgroundColor,
            ThemeResourceKeys.ForegroundColor,
            ThemeResourceKeys.AccentColor,
            ThemeResourceKeys.AccentSecondaryColor,
            ThemeResourceKeys.BorderForegroundColor,
            ThemeResourceKeys.BorderBackgroundColor,
            ThemeResourceKeys.ButtonBackgroundColor,
            ThemeResourceKeys.ButtonForegroundColor,
            ThemeResourceKeys.ButtonFocusColor,
            ThemeResourceKeys.ButtonHoverColor,
            ThemeResourceKeys.TextForegroundColor,
            ThemeResourceKeys.TextBackgroundColor,
            ThemeResourceKeys.SelectedBackgroundColor,
            ThemeResourceKeys.SelectedForegroundColor,
        ];

        foreach (var key in keys)
        {
            var found = theme.TryGetValue(key, out var value);
            await Assert.That(found).IsTrue();
            await Assert.That(value).IsTypeOf<Color>();
        }
    }

    [Test]
    [Arguments("Dark")]
    [Arguments("Dracula")]
    [Arguments("GruvboxDark")]
    public async Task BuiltInTheme_ContainsSurfaceAndMutedColors(string themeName)
    {
        // Arrange
        var theme = LoadBuiltInTheme(themeName);

        // Assert
        var hasSurface = theme.TryGetValue(ThemeResourceKeys.SurfaceColor, out var surfaceVal);
        var hasMuted = theme.TryGetValue(ThemeResourceKeys.MutedForegroundColor, out var mutedVal);

        await Assert.That(hasSurface).IsTrue();
        await Assert.That(hasMuted).IsTrue();
        await Assert.That(surfaceVal).IsTypeOf<Color>();
        await Assert.That(mutedVal).IsTypeOf<Color>();
    }

    #endregion

    #region DefaultStyleKey

    [Test]
    public async Task TextBlock_DefaultStyleKeyIsTextBlockType()
    {
        // Arrange — put a Style keyed by typeof(TextBlock) in a parent's resources
        // and verify the control picks it up via DefaultStyleKey
        var window = new Window();
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter("Text", "ImplicitStyleApplied"));
        window.Resources.Add(typeof(TextBlock), style);

        var textBlock = new TextBlock();
        window.Content = textBlock;

        // Assert — textBlock should have picked up the implicit style
        await Assert.That(textBlock.Text).IsEqualTo("ImplicitStyleApplied");
    }

    [Test]
    public async Task Button_DefaultStyleKeyIsButtonType()
    {
        // Arrange
        var window = new Window();
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter("Text", "ThemedButton"));
        window.Resources.Add(typeof(Button), style);

        var button = new Button();
        window.Content = button;

        // Assert
        await Assert.That(button.Text).IsEqualTo("ThemedButton");
    }

    #endregion

    #region App-Level Resource Overrides Theme

    [Test]
    public async Task AppResource_OverridesThemeResource()
    {
        // Arrange — set up Dark theme, then add an app-level override
        SetupTheme("Dark");
        _appResources!.Add(ThemeResourceKeys.BackgroundColor, Color.Magenta);

        // Act
        var found = _appResources.TryGetValue(ThemeResourceKeys.BackgroundColor, out var value);

        // Assert — app-level resources have higher priority than theme (merged at position 0)
        await Assert.That(found).IsTrue();
        await Assert.That(value).IsEqualTo(Color.Magenta);
    }

    #endregion
}
