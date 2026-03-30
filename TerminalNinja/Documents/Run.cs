using System.Windows.Markup;
using TerminalNinja.Controls;
using TerminalNinja.Primitives;

namespace TerminalNinja.Documents;

/// <summary>
/// An inline element that contains a text string.
/// Equivalent to WPF's <c>Run</c> — the leaf inline element.
/// </summary>
[ContentProperty("Text")]
[RuntimeNameProperty("Name")]
public sealed class Run : Inline
{
    public Run()
    {
        DefaultStyleKey = typeof(Run);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    /// <summary>Identifies the <see cref="Text"/> dependency property.</summary>
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(Run),
            new FrameworkPropertyMetadata("", affectsRender: true));

    /// <summary>Gets or sets the text content of this run.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty)!;
        set => SetValue(TextProperty, value);
    }

    /// <inheritdoc />
    public override void CollectRuns(List<InlineRun> runs, Color inheritedForeground, Color inheritedBackground, TextDecorations inheritedDecorations)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        var fg = ResolveForeground(inheritedForeground);
        var bg = ResolveBackground(inheritedBackground);
        var deco = ResolveDecorations(inheritedDecorations);

        runs.Add(new InlineRun(Text, fg, bg, deco));
    }
}
