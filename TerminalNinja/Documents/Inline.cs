using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Controls;
using TerminalNinja.Primitives;

namespace TerminalNinja.Documents;

/// <summary>
/// Abstract base class for inline text elements within a <see cref="TextBlock"/>.
/// Provides Foreground, Background, and TextDecorations dependency properties
/// that child runs inherit from their parent inline/TextBlock when set to transparent/none.
/// </summary>
[RuntimeNameProperty("Name")]
public abstract class Inline : FrameworkElement
{
    // ─── Dependency Properties ───────────────────────────────────────

    /// <summary>Identifies the <see cref="Foreground"/> dependency property.</summary>
    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(nameof(Foreground), typeof(Color), typeof(Inline),
            new FrameworkPropertyMetadata(Color.Transparent, affectsRender: true));

    /// <summary>Identifies the <see cref="Background"/> dependency property.</summary>
    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(nameof(Background), typeof(Color), typeof(Inline),
            new FrameworkPropertyMetadata(Color.Transparent, affectsRender: true));

    /// <summary>Identifies the <see cref="TextDecorations"/> dependency property.</summary>
    public static readonly DependencyProperty TextDecorationsProperty =
        DependencyProperty.Register(nameof(TextDecorations), typeof(TextDecorations), typeof(Inline),
            new FrameworkPropertyMetadata(TextDecorations.None, affectsRender: true));

    /// <summary>Gets or sets the foreground color. Transparent means inherit from parent.</summary>
    public Color Foreground
    {
        get => (Color)GetValue(ForegroundProperty)!;
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Gets or sets the background color. Transparent means inherit from parent.</summary>
    public Color Background
    {
        get => (Color)GetValue(BackgroundProperty)!;
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>Gets or sets the text decorations (bold, italic, underline, etc.).</summary>
    public TextDecorations TextDecorations
    {
        get => (TextDecorations)GetValue(TextDecorationsProperty)!;
        set => SetValue(TextDecorationsProperty, value);
    }

    /// <summary>
    /// Flattens this inline into a list of renderable <see cref="InlineRun"/> segments.
    /// </summary>
    /// <param name="runs">The list to add runs to.</param>
    /// <param name="inheritedForeground">The foreground color inherited from the parent context.</param>
    /// <param name="inheritedBackground">The background color inherited from the parent context.</param>
    /// <param name="inheritedDecorations">The text decorations inherited from the parent context.</param>
    public abstract void CollectRuns(List<InlineRun> runs, Color inheritedForeground, Color inheritedBackground, TextDecorations inheritedDecorations);

    /// <summary>
    /// Resolves the effective foreground color, falling back to the inherited value if transparent.
    /// </summary>
    protected Color ResolveForeground(Color inherited) =>
        Foreground.IsTransparent ? inherited : Foreground;

    /// <summary>
    /// Resolves the effective background color, falling back to the inherited value if transparent.
    /// </summary>
    protected Color ResolveBackground(Color inherited) =>
        Background.IsTransparent ? inherited : Background;

    /// <summary>
    /// Resolves the effective text decorations by merging with inherited decorations.
    /// If this inline has decorations set, they combine (OR) with the inherited ones.
    /// </summary>
    protected TextDecorations ResolveDecorations(TextDecorations inherited) =>
        inherited | TextDecorations;

    // Inlines are not rendered directly — they are collected and rendered by TextBlock
    public override Size2D GetPreferredSize(Rect parent) => default;
    public override Rect CalculateBounds(Rect parent) => default;
    public override void Render(CellBuffer buffer, Rect parentBounds) { }
}
