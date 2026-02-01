using Portable.Xaml.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;
using TerminalNinja.Styling;

namespace TerminalNinja.Elements;

/// <summary>
/// A rectangle UI element with positioning, sizing, borders, and background color.
/// </summary>
[ContentProperty("Child")]
[RuntimeNameProperty("Name")]
public sealed class Rectangle : FrameworkElement
{
    // Bindable properties (with change notification)
    private Color _backgroundColor = Color.Black;
    /// <summary>Gets or sets the background color.</summary>
    public Color BackgroundColor
    {
        get => _backgroundColor;
        set => SetProperty(ref _backgroundColor, value);
    }
    
    private Color _foregroundColor = Color.White;
    /// <summary>Gets or sets the foreground color (used for borders).</summary>
    public Color ForegroundColor
    {
        get => _foregroundColor;
        set => SetProperty(ref _foregroundColor, value);
    }
    
    private Border _border = Border.None;
    /// <summary>Gets or sets the border style and color.</summary>
    public Border Border
    {
        get => _border;
        set => SetProperty(ref _border, value);
    }
    
    private IElement? _child;
    /// <summary>Gets or sets the child element to render inside this rectangle.</summary>
    public IElement? Child
    {
        get => _child;
        set
        {
            if (_child != null)
                _child.Parent = null;
            SetProperty(ref _child, value);
            if (_child != null)
                _child.Parent = this;
        }
    }
    
    // Layout properties (kept as init for now)
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
    
    /// <summary>
    /// Returns the preferred size of this rectangle within the given parent bounds.
    /// Uses resolved Width/Height if Absolute, otherwise returns the parent size.
    /// </summary>
    /// <param name="parent">The parent container bounds.</param>
    /// <returns>The preferred width and height in cells.</returns>
    public override Size2D GetPreferredSize(Rect parent)
    {
        var w = Width.Mode == SizeMode.Absolute ? Width.Resolve(parent.Width) : parent.Width;
        var h = Height.Mode == SizeMode.Absolute ? Height.Resolve(parent.Height) : parent.Height;
        return new Size2D(w, h);
    }
    
    /// <summary>
    /// Calculates the absolute bounds of this rectangle within the parent bounds.
    /// </summary>
    /// <param name="parent">The parent container bounds.</param>
    /// <returns>The calculated absolute rectangle bounds.</returns>
    public override Rect CalculateBounds(Rect parent)
    {
        // Resolve dimensions
        var w = Width.Resolve(parent.Width);
        var h = Height.Resolve(parent.Height);
        
        // Resolve position offsets
        var xOffset = X.Resolve(parent.Width);
        var yOffset = Y.Resolve(parent.Height);
        
        // Apply horizontal alignment
        var x = HorizontalAlignment switch
        {
            Alignment.Start => parent.X + xOffset,
            Alignment.Center => parent.X + (parent.Width - w) / 2 + xOffset,
            Alignment.End => parent.X + parent.Width - w - xOffset,
            _ => parent.X + xOffset
        };
        
        // Apply vertical alignment
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
    /// Renders this rectangle to the specified cell buffer.
    /// </summary>
    /// <param name="buffer">The buffer to render into.</param>
    /// <param name="parentBounds">The bounds of the parent container.</param>
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        
        // Clip to buffer bounds
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;
        
        // Fill background
        var bgCell = new Cell(' ', ForegroundColor, BackgroundColor);
        buffer.FillRect(clipped, bgCell);
        
        // Draw border if present
        if (Border.HasBorder && bounds.Width >= 2 && bounds.Height >= 2)
        {
            RenderBorder(buffer, bounds);
        }
        
        // Render child element if present
        if (Child != null)
        {
            // Calculate inner bounds (subtract border if present)
            var childBounds = Border.HasBorder && bounds.Width >= 2 && bounds.Height >= 2
                ? new Rect(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2)
                : bounds;
            
            Child.Render(buffer, childBounds);
        }
    }
    
    private void RenderBorder(CellBuffer buffer, Rect bounds)
    {
        var chars = Border.Chars;
        var color = Border.Color;
        var bg = BackgroundColor;
        
        // Draw corners
        buffer.SetCell(bounds.X, bounds.Y, 
            new Cell(chars.TopLeft, color, bg));
        buffer.SetCell(bounds.Right - 1, bounds.Y, 
            new Cell(chars.TopRight, color, bg));
        buffer.SetCell(bounds.X, bounds.Bottom - 1, 
            new Cell(chars.BottomLeft, color, bg));
        buffer.SetCell(bounds.Right - 1, bounds.Bottom - 1, 
            new Cell(chars.BottomRight, color, bg));
        
        // Draw horizontal edges (top and bottom)
        var hCell = new Cell(chars.Horizontal, color, bg);
        for (var x = bounds.X + 1; x < bounds.Right - 1; x++)
        {
            buffer.SetCell(x, bounds.Y, hCell);
            buffer.SetCell(x, bounds.Bottom - 1, hCell);
        }
        
        // Draw vertical edges (left and right)
        var vCell = new Cell(chars.Vertical, color, bg);
        for (var y = bounds.Y + 1; y < bounds.Bottom - 1; y++)
        {
            buffer.SetCell(bounds.X, y, vCell);
            buffer.SetCell(bounds.Right - 1, y, vCell);
        }
    }
}
