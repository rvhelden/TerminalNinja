using System.Windows.Markup;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// An inline container that groups other inline elements and applies shared formatting.
/// Equivalent to WPF's <c>Span</c>.
/// </summary>
[ContentProperty("Inlines")]
[RuntimeNameProperty("Name")]
public sealed class Span : Inline
{
    private InlineCollection? _inlines;

    /// <summary>
    /// Gets the collection of child inline elements.
    /// </summary>
    public InlineCollection Inlines => _inlines ??= new InlineCollection(this);

    /// <inheritdoc />
    public override void CollectRuns(List<InlineRun> runs, Color inheritedForeground, Color inheritedBackground, TextDecorations inheritedDecorations)
    {
        if (_inlines == null || _inlines.Count == 0)
        {
            return;
        }

        var fg = ResolveForeground(inheritedForeground);
        var bg = ResolveBackground(inheritedBackground);
        var deco = ResolveDecorations(inheritedDecorations);

        foreach (var inline in _inlines)
        {
            inline.CollectRuns(runs, fg, bg, deco);
        }
    }

    /// <inheritdoc />
    protected internal override IEnumerable<FrameworkElement> GetLogicalChildren()
    {
        if (_inlines == null)
        {
            yield break;
        }

        foreach (var inline in _inlines)
        {
            yield return inline;
        }
    }
}
