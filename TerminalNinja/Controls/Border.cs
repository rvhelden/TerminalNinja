using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;
using Rect = TerminalNinja.Primitives.Rect;
using Size = TerminalNinja.Primitives.Size;

namespace TerminalNinja.Controls;

/// <summary>
/// A border container control that draws a background and optional border around a single child.
/// Equivalent to WPF's Border.
/// </summary>
[ContentProperty("Child")]
[RuntimeNameProperty("Name")]
public sealed class Border : FrameworkElement
{
    public static readonly DependencyProperty BackgroundColorProperty =
        DependencyProperty.Register(nameof(BackgroundColor), typeof(Color), typeof(Border),
            new FrameworkPropertyMetadata(default(Color), affectsRender: true));
    
    
    public Color BackgroundColor
    {
        get { return (Color)GetValue(BackgroundColorProperty)!; }
        set { SetValue(BackgroundColorProperty, value); }
    }
    
    private Color _foregroundColor = Color.White;
    /// <summary>Gets or sets the foreground color (used for borders).</summary>
    public Color ForegroundColor
    {
        get => _foregroundColor;
        set => SetProperty(ref _foregroundColor, value);
    }
    
    private Styling.Border _borderStyle = Styling.Border.None;
    /// <summary>Gets or sets the border style and color.</summary>
    public Styling.Border BorderStyle
    {
        get => _borderStyle;
        set => SetProperty(ref _borderStyle, value);
    }
    
    private UIElement? _child;
    /// <summary>Gets or sets the child control to render inside this border.</summary>
    public UIElement? Child
    {
        get => _child;
        set
        {
            if (ReferenceEquals(_child, value))
                return;

            if (_child != null)
                _child.Parent = null;

            SetProperty(ref _child, value);

            if (_child != null)
                _child.Parent = this;
        }
    }

    /// <summary>Gets or sets the width (absolute, relative, or stretch).</summary>
    public Size Width { get; set; } = Size.Stretch;

    /// <summary>Gets or sets the height (absolute, relative, or stretch).</summary>
    public Size Height { get; set; } = Size.Stretch;
    
    /// <inheritdoc />
    public override IEnumerable<(Visual Child, Rect ChildParentBounds)> GetChildrenWithBounds(Rect myBounds)
    {
        if (Child == null) yield break;
        var innerBounds = BorderStyle.HasBorder && myBounds.Width >= 2 && myBounds.Height >= 2
            ? new Rect(myBounds.X + 1, myBounds.Y + 1, myBounds.Width - 2, myBounds.Height - 2)
            : myBounds;
        yield return (Child, innerBounds);
    }

    /// <summary>
    /// Returns the preferred size of this border within the given parent bounds.
    /// Uses resolved Width/Height if Absolute, otherwise returns the parent size.
    /// </summary>
    public override Size2D GetPreferredSize(Rect parent)
    {
        var w = Width.Mode == SizeMode.Absolute ? Width.Resolve(parent.Width) : parent.Width;
        var h = Height.Mode == SizeMode.Absolute ? Height.Resolve(parent.Height) : parent.Height;
        return new Size2D(w, h);
    }
    
    /// <summary>
    /// Calculates the absolute bounds of this border within the parent bounds.
    /// </summary>
    public override Rect CalculateBounds(Rect parent)
    {
        // Resolve dimensions
        var w = Width.Resolve(parent.Width);
        var h = Height.Resolve(parent.Height);
        
        return new Rect(parent.X, parent.Y, w, h);
    }
    
    /// <summary>
    /// Renders this border to the specified cell buffer.
    /// </summary>
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
        if (BorderStyle.HasBorder && bounds.Width >= 2 && bounds.Height >= 2)
        {
            RenderBorder(buffer, bounds);
        }
        
        // Render child control if present
        if (Child != null)
        {
            // Calculate inner bounds (subtract border if present)
            var childBounds = BorderStyle.HasBorder && bounds.Width >= 2 && bounds.Height >= 2
                ? new Rect(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2)
                : bounds;
            
            Child.Render(buffer, childBounds);
        }
    }
    
    private void RenderBorder(CellBuffer buffer, Rect bounds)
    {
        var chars = BorderStyle.Chars;
        var color = BorderStyle.Color;
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
