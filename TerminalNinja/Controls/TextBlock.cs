using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A text display control with alignment, wrapping, truncation, and padding support.
/// Equivalent to WPF's TextBlock — displays read-only text.
/// Supports inline formatting via <see cref="Inlines"/> (Run, Span) with per-run
/// foreground, background, and text decorations.
/// </summary>
[ContentProperty("Inlines")]
[RuntimeNameProperty("Name")]
public sealed class TextBlock : FrameworkElement
{
    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(TextBlock),
            new FrameworkPropertyMetadata("", affectsRender: true));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(nameof(Foreground), typeof(Color), typeof(TextBlock),
            new FrameworkPropertyMetadata(Color.White, affectsRender: true));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(nameof(Background), typeof(Color), typeof(TextBlock),
            new FrameworkPropertyMetadata(Color.Transparent, affectsRender: true));

    public static readonly DependencyProperty WidthProperty =
        DependencyProperty.Register(nameof(Width), typeof(Size), typeof(TextBlock),
            new FrameworkPropertyMetadata(Size.Stretch, affectsRender: true));

    public static readonly DependencyProperty HeightProperty =
        DependencyProperty.Register(nameof(Height), typeof(Size), typeof(TextBlock),
            new FrameworkPropertyMetadata(Size.Stretch, affectsRender: true));

    public static readonly DependencyProperty HorizontalTextAlignmentProperty =
        DependencyProperty.Register(nameof(HorizontalTextAlignment), typeof(TextAlignment), typeof(TextBlock),
            new FrameworkPropertyMetadata(TextAlignment.Start, affectsRender: true));

    public static readonly DependencyProperty VerticalTextAlignmentProperty =
        DependencyProperty.Register(nameof(VerticalTextAlignment), typeof(TextAlignment), typeof(TextBlock),
            new FrameworkPropertyMetadata(TextAlignment.Start, affectsRender: true));

    public static readonly DependencyProperty TextWrappingProperty =
        DependencyProperty.Register(nameof(TextWrapping), typeof(TextWrapping), typeof(TextBlock),
            new FrameworkPropertyMetadata(TextWrapping.NoWrap, affectsRender: true));

    public static readonly DependencyProperty TextTrimmingProperty =
        DependencyProperty.Register(nameof(TextTrimming), typeof(TextTrimming), typeof(TextBlock),
            new FrameworkPropertyMetadata(TextTrimming.None, affectsRender: true));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(nameof(Padding), typeof(Thickness), typeof(TextBlock),
            new FrameworkPropertyMetadata(new Thickness(0), affectsRender: true));

    public static readonly DependencyProperty TextDecorationsProperty =
        DependencyProperty.Register(nameof(TextDecorations), typeof(TextDecorations), typeof(TextBlock),
            new FrameworkPropertyMetadata(TextDecorations.None, affectsRender: true));

    private InlineCollection? _inlines;

    /// <summary>Gets or sets the text content to display.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty)!;
        set => SetValue(TextProperty, value);
    }
    
    /// <summary>Gets or sets the foreground (text) color.</summary>
    public Color Foreground
    {
        get => (Color)GetValue(ForegroundProperty)!;
        set => SetValue(ForegroundProperty, value);
    }
    
    /// <summary>Gets or sets the background color. Defaults to Transparent so the text block does not paint over its parent's background.</summary>
    public Color Background
    {
        get => (Color)GetValue(BackgroundProperty)!;
        set => SetValue(BackgroundProperty, value);
    }
    
    /// <summary>Gets or sets the width (absolute, relative, or stretch).</summary>
    public Size Width
    {
        get => (Size)GetValue(WidthProperty)!;
        set => SetValue(WidthProperty, value);
    }
    
    /// <summary>Gets or sets the height (absolute, relative, or stretch).</summary>
    public Size Height
    {
        get => (Size)GetValue(HeightProperty)!;
        set => SetValue(HeightProperty, value);
    }
    
    /// <summary>Gets or sets the horizontal text alignment within the text block bounds.</summary>
    public TextAlignment HorizontalTextAlignment
    {
        get => (TextAlignment)GetValue(HorizontalTextAlignmentProperty)!;
        set => SetValue(HorizontalTextAlignmentProperty, value);
    }
    
    /// <summary>Gets or sets the vertical text alignment within the text block bounds.</summary>
    public TextAlignment VerticalTextAlignment
    {
        get => (TextAlignment)GetValue(VerticalTextAlignmentProperty)!;
        set => SetValue(VerticalTextAlignmentProperty, value);
    }
    
    /// <summary>Gets or sets how text wraps when it exceeds the available width.</summary>
    public TextWrapping TextWrapping
    {
        get => (TextWrapping)GetValue(TextWrappingProperty)!;
        set => SetValue(TextWrappingProperty, value);
    }
    
    /// <summary>Gets or sets how text is trimmed when it exceeds the available space.</summary>
    public TextTrimming TextTrimming
    {
        get => (TextTrimming)GetValue(TextTrimmingProperty)!;
        set => SetValue(TextTrimmingProperty, value);
    }
    
    /// <summary>Gets or sets the internal padding around the text.</summary>
    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty)!;
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>Gets or sets the text decorations applied to the entire text block (when not using inlines).</summary>
    public TextDecorations TextDecorations
    {
        get => (TextDecorations)GetValue(TextDecorationsProperty)!;
        set => SetValue(TextDecorationsProperty, value);
    }

    /// <summary>
    /// Gets the collection of inline elements for rich text formatting.
    /// When inlines are present, they take precedence over the <see cref="Text"/> property.
    /// </summary>
    public InlineCollection Inlines => _inlines ??= new InlineCollection(this);

    /// <summary>
    /// Gets whether this text block has inline elements.
    /// </summary>
    internal bool HasInlines => _inlines != null && _inlines.Count > 0;

    /// <summary>
    /// Returns the preferred size of this text block based on text length and padding.
    /// </summary>
    public override Size2D GetPreferredSize(Rect parent)
    {
        if (HasInlines)
        {
            var totalLength = GetInlineTextLength();
            if (totalLength == 0)
                return new Size2D(Padding.HorizontalTotal, Padding.VerticalTotal);
            return new Size2D(totalLength + Padding.HorizontalTotal, 1 + Padding.VerticalTotal);
        }

        if (string.IsNullOrEmpty(Text))
            return new Size2D(Padding.HorizontalTotal, Padding.VerticalTotal);

        var lines = NormalizeText(Text).Split('\n');
        var maxLineWidth = lines.Max(l => l.Length);
        return new Size2D(maxLineWidth + Padding.HorizontalTotal, lines.Length + Padding.VerticalTotal);
    }
    
    /// <summary>
    /// Calculates the absolute bounds of this text block within the parent bounds.
    /// </summary>
    public override Rect CalculateBounds(Rect parent)
    {
        var w = Width.Resolve(parent.Width);
        var h = Height.Resolve(parent.Height);
        
        return ApplyAlignment(parent, w, h);
    }
    
    /// <summary>
    /// Renders this text block to the specified cell buffer.
    /// </summary>
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        
        // Clip to buffer bounds
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;
        
        // Fill background (skip when transparent — let parent's background show through)
        if (!Background.IsTransparent)
        {
            var bgCell = new Cell(' ', Foreground, Background);
            buffer.FillRect(clipped, bgCell);
        }
        
        // Calculate text area (bounds minus padding)
        var textX = bounds.X + Padding.Left;
        var textY = bounds.Y + Padding.Top;
        var textWidth = Math.Max(0, bounds.Width - Padding.HorizontalTotal);
        var textHeight = Math.Max(0, bounds.Height - Padding.VerticalTotal);
        
        if (textWidth <= 0 || textHeight <= 0) return;

        if (HasInlines)
        {
            RenderInlines(buffer, textX, textY, textWidth, textHeight);
        }
        else
        {
            // If no text, we're done
            if (string.IsNullOrEmpty(Text)) return;
            
            // Render text with wrapping/alignment
            RenderText(buffer, textX, textY, textWidth, textHeight);
        }
    }

    /// <inheritdoc />
    protected internal override IEnumerable<FrameworkElement> GetLogicalChildren()
    {
        if (_inlines == null) yield break;
        foreach (var inline in _inlines)
            yield return inline;
    }
    
    /// <summary>Normalizes line endings to \n.</summary>
    private static string NormalizeText(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private void RenderText(CellBuffer buffer, int x, int y, int width, int height)
    {
        var normalized = NormalizeText(Text);
        List<string> lines;
        if (TextWrapping == TextWrapping.Wrap)
        {
            lines = [];
            foreach (var segment in normalized.Split('\n'))
                lines.AddRange(WrapText(segment, width));
        }
        else
        {
            lines = [.. normalized.Split('\n')];
        }
        
        // Apply vertical alignment
        var startY = VerticalTextAlignment switch
        {
            TextAlignment.Center => y + (height - lines.Count) / 2,
            TextAlignment.End => y + height - lines.Count,
            _ => y
        };
        
        // Clamp startY to valid range
        startY = Math.Max(y, Math.Min(startY, y + height - 1));
        
        var decorations = TextDecorations;
        
        // Render each line
        for (var i = 0; i < lines.Count && startY + i < y + height; i++)
        {
            var line = lines[i];
            var lineY = startY + i;
            
            // Skip lines outside buffer
            if (lineY < 0 || lineY >= buffer.Height) continue;
            
            // Apply trimming if needed
            if (line.Length > width && TextTrimming == TextTrimming.Ellipsis)
            {
                line = width >= 3 
                    ? line[..(width - 3)] + "..." 
                    : line[..width];
            }
            else if (line.Length > width)
            {
                line = line[..width];
            }
            
            // Apply horizontal alignment
            var startX = HorizontalTextAlignment switch
            {
                TextAlignment.Center => x + (width - line.Length) / 2,
                TextAlignment.End => x + width - line.Length,
                _ => x
            };
            
            // Clamp startX to valid range
            startX = Math.Max(x, Math.Min(startX, x + width - 1));
            
            // Render characters
            for (var j = 0; j < line.Length && startX + j < x + width; j++)
            {
                var charX = startX + j;
                
                // Skip characters outside buffer
                if (charX < 0 || charX >= buffer.Width) continue;
                
                buffer.SetChar(charX, lineY, line[j], Foreground, Background, decorations);
            }
        }
    }

    /// <summary>
    /// Renders inline elements to the buffer with per-run foreground, background, and decorations.
    /// </summary>
    private void RenderInlines(CellBuffer buffer, int x, int y, int width, int height)
    {
        // Collect all runs from the inline tree
        var runs = new List<InlineRun>();
        foreach (var inline in _inlines!)
        {
            inline.CollectRuns(runs, Foreground, Background, TextDecorations);
        }

        if (runs.Count == 0) return;

        // Build the full text and a parallel array of style info per character
        var totalLength = 0;
        foreach (var run in runs)
            totalLength += run.Text.Length;

        if (totalLength == 0) return;

        // For NoWrap mode, render as a single line with per-character styling
        if (TextWrapping == TextWrapping.NoWrap)
        {
            RenderInlineLine(buffer, runs, x, y, width, height, totalLength);
        }
        else
        {
            RenderInlinesWrapped(buffer, runs, x, y, width, height);
        }
    }

    /// <summary>
    /// Renders inlines as a single line (NoWrap mode) with trimming and alignment.
    /// </summary>
    private void RenderInlineLine(CellBuffer buffer, List<InlineRun> runs, int x, int y, int width, int height, int totalLength)
    {
        // Apply vertical alignment (single line)
        var lineY = VerticalTextAlignment switch
        {
            TextAlignment.Center => y + height / 2,
            TextAlignment.End => y + height - 1,
            _ => y
        };

        if (lineY < 0 || lineY >= buffer.Height || lineY >= y + height) return;

        // Determine visible length (after trimming)
        var visibleLength = Math.Min(totalLength, width);
        var needsEllipsis = totalLength > width && TextTrimming == TextTrimming.Ellipsis && width >= 3;
        var ellipsisStart = needsEllipsis ? width - 3 : -1;

        // Apply horizontal alignment
        var startX = HorizontalTextAlignment switch
        {
            TextAlignment.Center => x + (width - visibleLength) / 2,
            TextAlignment.End => x + width - visibleLength,
            _ => x
        };
        startX = Math.Max(x, Math.Min(startX, x + width - 1));

        // Render character by character across runs
        var charIndex = 0;
        foreach (var run in runs)
        {
            for (var i = 0; i < run.Text.Length; i++)
            {
                if (charIndex >= width) return; // past visible area

                var charX = startX + charIndex;
                if (charX >= x + width) return;

                if (charX >= 0 && charX < buffer.Width)
                {
                    if (needsEllipsis && charIndex >= ellipsisStart)
                    {
                        // Render ellipsis with the last run's styling
                        buffer.SetChar(charX, lineY, '.', run.Foreground, run.Background, run.Decorations);
                    }
                    else
                    {
                        buffer.SetChar(charX, lineY, run.Text[i], run.Foreground, run.Background, run.Decorations);
                    }
                }

                charIndex++;
            }
        }
    }

    /// <summary>
    /// Renders inlines with word wrapping across multiple lines.
    /// </summary>
    private void RenderInlinesWrapped(CellBuffer buffer, List<InlineRun> runs, int x, int y, int width, int height)
    {
        // Build a flat list of styled characters
        var styledChars = new List<(char Char, Color Fg, Color Bg, TextDecorations Deco)>();
        foreach (var run in runs)
        {
            foreach (var c in run.Text)
                styledChars.Add((c, run.Foreground, run.Background, run.Decorations));
        }

        // Wrap into lines
        var wrappedLines = new List<List<(char Char, Color Fg, Color Bg, TextDecorations Deco)>>();
        var currentLine = new List<(char Char, Color Fg, Color Bg, TextDecorations Deco)>();

        foreach (var sc in styledChars)
        {
            if (sc.Char == '\n')
            {
                wrappedLines.Add(currentLine);
                currentLine = [];
                continue;
            }

            currentLine.Add(sc);
            if (currentLine.Count >= width)
            {
                wrappedLines.Add(currentLine);
                currentLine = [];
            }
        }
        if (currentLine.Count > 0)
            wrappedLines.Add(currentLine);

        // Apply vertical alignment
        var startY = VerticalTextAlignment switch
        {
            TextAlignment.Center => y + (height - wrappedLines.Count) / 2,
            TextAlignment.End => y + height - wrappedLines.Count,
            _ => y
        };
        startY = Math.Max(y, Math.Min(startY, y + height - 1));

        // Render each wrapped line
        for (var lineIdx = 0; lineIdx < wrappedLines.Count && startY + lineIdx < y + height; lineIdx++)
        {
            var line = wrappedLines[lineIdx];
            var lineY = startY + lineIdx;
            if (lineY < 0 || lineY >= buffer.Height) continue;

            var startX = HorizontalTextAlignment switch
            {
                TextAlignment.Center => x + (width - line.Count) / 2,
                TextAlignment.End => x + width - line.Count,
                _ => x
            };
            startX = Math.Max(x, Math.Min(startX, x + width - 1));

            for (var j = 0; j < line.Count && startX + j < x + width; j++)
            {
                var charX = startX + j;
                if (charX < 0 || charX >= buffer.Width) continue;

                var sc = line[j];
                buffer.SetChar(charX, lineY, sc.Char, sc.Fg, sc.Bg, sc.Deco);
            }
        }
    }

    /// <summary>
    /// Gets the total text length from all inline runs.
    /// </summary>
    private int GetInlineTextLength()
    {
        var runs = new List<InlineRun>();
        foreach (var inline in _inlines!)
            inline.CollectRuns(runs, Foreground, Background, TextDecorations);

        var total = 0;
        foreach (var run in runs)
            total += run.Text.Length;
        return total;
    }
    
    private static List<string> WrapText(string text, int maxWidth)
    {
        if (maxWidth <= 0) return [];
        if (text.Length <= maxWidth) return [text];
        
        var lines = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentLine = new System.Text.StringBuilder();
        
        foreach (var word in words)
        {
            // If word itself is longer than maxWidth, split it
            if (word.Length > maxWidth)
            {
                // Flush current line first
                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine.ToString().TrimEnd());
                    currentLine.Clear();
                }
                
                // Split long word across lines
                for (var i = 0; i < word.Length; i += maxWidth)
                {
                    var chunk = word.Substring(i, Math.Min(maxWidth, word.Length - i));
                    lines.Add(chunk);
                }
                continue;
            }
            
            // Check if adding this word would exceed maxWidth
            var testLength = currentLine.Length + (currentLine.Length > 0 ? 1 : 0) + word.Length;
            
            if (testLength > maxWidth)
            {
                // Start new line
                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine.ToString().TrimEnd());
                    currentLine.Clear();
                }
                currentLine.Append(word);
            }
            else
            {
                // Add word to current line
                if (currentLine.Length > 0)
                    currentLine.Append(' ');
                currentLine.Append(word);
            }
        }
        
        // Add final line
        if (currentLine.Length > 0)
        {
            lines.Add(currentLine.ToString().TrimEnd());
        }
        
        return lines.Count > 0 ? lines : [text];
    }
}
