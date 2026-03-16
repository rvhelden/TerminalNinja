using Portable.Xaml.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;
using SWM = System.Windows.Markup;

namespace TerminalNinja.Controls;

/// <summary>
/// A text label UI control with alignment, wrapping, truncation, and padding support.
/// </summary>
[RuntimeNameProperty("Name")]
[SWM.RuntimeNameProperty("Name")]
public sealed class Label : FrameworkElement
{
    // Bindable properties (with change notification)
    private string _text = "";
    /// <summary>Gets or sets the text content to display.</summary>
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }
    
    private Color _foregroundColor = Color.White;
    /// <summary>Gets or sets the foreground (text) color.</summary>
    public Color ForegroundColor
    {
        get => _foregroundColor;
        set => SetProperty(ref _foregroundColor, value);
    }
    
    private Color _backgroundColor = Color.Transparent;
    /// <summary>Gets or sets the background color. Defaults to Transparent so the label does not paint over its parent's background.</summary>
    public Color BackgroundColor
    {
        get => _backgroundColor;
        set => SetProperty(ref _backgroundColor, value);
    }
    
    // Layout properties (kept as init for now, can be extended later)
    /// <summary>Gets or sets the X position (absolute, relative, or stretch).</summary>
    public Size X { get; init; } = Size.Absolute(0);
    
    /// <summary>Gets or sets the Y position (absolute, relative, or stretch).</summary>
    public Size Y { get; init; } = Size.Absolute(0);
    
    /// <summary>Gets or sets the horizontal alignment within the parent.</summary>
    public Alignment HorizontalAlignment { get; init; } = Alignment.Start;
    
    /// <summary>Gets or sets the vertical alignment within the parent.</summary>
    public Alignment VerticalAlignment { get; init; } = Alignment.Start;
    
    /// <summary>Gets or sets the width (absolute, relative, or stretch).</summary>
    public Size Width { get; init; } = Size.Stretch;
    
    /// <summary>Gets or sets the height (absolute, relative, or stretch).</summary>
    public Size Height { get; init; } = Size.Stretch;
    
    /// <summary>Gets or sets the horizontal text alignment within the label bounds.</summary>
    public TextAlignment HorizontalTextAlignment { get; init; } = TextAlignment.Start;
    
    /// <summary>Gets or sets the vertical text alignment within the label bounds.</summary>
    public TextAlignment VerticalTextAlignment { get; init; } = TextAlignment.Start;
    
    /// <summary>Gets or sets how text wraps when it exceeds the available width.</summary>
    public TextWrapping TextWrapping { get; init; } = TextWrapping.NoWrap;
    
    /// <summary>Gets or sets how text is trimmed when it exceeds the available space.</summary>
    public TextTrimming TextTrimming { get; init; } = TextTrimming.None;
    
    /// <summary>Gets or sets the internal padding around the text.</summary>
    public Thickness Padding { get; init; } = new Thickness(0);
    
    /// <summary>
    /// Returns the preferred size of this label based on text length and padding.
    /// </summary>
    public override Size2D GetPreferredSize(Rect parent)
    {
        if (string.IsNullOrEmpty(Text))
            return new Size2D(Padding.HorizontalTotal, Padding.VerticalTotal);

        var lines = NormalizeText(Text).Split('\n');
        var maxLineWidth = lines.Max(l => l.Length);
        return new Size2D(maxLineWidth + Padding.HorizontalTotal, lines.Length + Padding.VerticalTotal);
    }
    
    /// <summary>
    /// Calculates the absolute bounds of this label within the parent bounds.
    /// </summary>
    public override Rect CalculateBounds(Rect parent)
    {
        // Same logic as Rectangle
        var w = Width.Resolve(parent.Width);
        var h = Height.Resolve(parent.Height);
        
        var xOffset = X.Resolve(parent.Width);
        var yOffset = Y.Resolve(parent.Height);
        
        var x = HorizontalAlignment switch
        {
            Alignment.Start => parent.X + xOffset,
            Alignment.Center => parent.X + (parent.Width - w) / 2 + xOffset,
            Alignment.End => parent.X + parent.Width - w - xOffset,
            _ => parent.X + xOffset
        };
        
        var y = VerticalAlignment switch
        {
            Alignment.Start => parent.Y + yOffset,
            Alignment.Center => parent.Y + (parent.Height - h) / 2 + yOffset,
            Alignment.End => parent.Y + parent.Height - h - yOffset,
            _ => parent.Y + yOffset
        };
        
        return new Rect(x, y, w, h);
    }
    
    /// <summary>
    /// Renders this label to the specified cell buffer.
    /// </summary>
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        
        // Clip to buffer bounds
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;
        
        // Fill background (skip when transparent — let parent's background show through)
        if (!BackgroundColor.IsTransparent)
        {
            var bgCell = new Cell(' ', ForegroundColor, BackgroundColor);
            buffer.FillRect(clipped, bgCell);
        }
        
        // If no text, we're done
        if (string.IsNullOrEmpty(Text)) return;
        
        // Calculate text area (bounds minus padding)
        var textX = bounds.X + Padding.Left;
        var textY = bounds.Y + Padding.Top;
        var textWidth = Math.Max(0, bounds.Width - Padding.HorizontalTotal);
        var textHeight = Math.Max(0, bounds.Height - Padding.VerticalTotal);
        
        if (textWidth <= 0 || textHeight <= 0) return;
        
        // Render text with wrapping/alignment
        RenderText(buffer, textX, textY, textWidth, textHeight);
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
                
                buffer.SetChar(charX, lineY, line[j], ForegroundColor, BackgroundColor);
            }
        }
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
