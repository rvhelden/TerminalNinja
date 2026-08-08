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
    public Border()
    {
        DefaultStyleKey = typeof(Border);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(nameof(Background), typeof(Color), typeof(Border),
            new FrameworkPropertyMetadata(default(Color), affectsRender: true));

    public static readonly DependencyProperty BorderBrushProperty =
        DependencyProperty.Register(nameof(BorderBrush), typeof(Color), typeof(Border),
            new FrameworkPropertyMetadata(Color.White, affectsRender: true));

    public static readonly DependencyProperty FocusBorderBrushProperty =
        DependencyProperty.Register(nameof(FocusBorderBrush), typeof(Color), typeof(Border),
            new FrameworkPropertyMetadata(Color.Cyan, affectsRender: true));

    public static readonly DependencyProperty ShowFocusBorderProperty =
        DependencyProperty.Register(nameof(ShowFocusBorder), typeof(bool), typeof(Border),
            new FrameworkPropertyMetadata(true, affectsRender: true));

    public static readonly DependencyProperty BorderStyleProperty =
        DependencyProperty.Register(nameof(BorderStyle), typeof(Styling.BorderStyle), typeof(Border),
            new FrameworkPropertyMetadata(Styling.BorderStyle.None, affectsRender: true));

    public static readonly DependencyProperty WidthProperty =
        DependencyProperty.Register(nameof(Width), typeof(Size), typeof(Border),
            new FrameworkPropertyMetadata(Size.Stretch, affectsRender: true));

    public static readonly DependencyProperty HeightProperty =
        DependencyProperty.Register(nameof(Height), typeof(Size), typeof(Border),
            new FrameworkPropertyMetadata(Size.Stretch, affectsRender: true));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(nameof(Padding), typeof(Thickness), typeof(Border),
            new FrameworkPropertyMetadata(new Thickness(0), affectsRender: true));

    public static readonly DependencyProperty ChildProperty =
        DependencyProperty.Register(nameof(Child), typeof(UIElement), typeof(Border),
            new FrameworkPropertyMetadata(null, affectsRender: true,
                propertyChangedCallback: OnChildChanged));

    private static void OnChildChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is UIElement oldChild)
        {
            oldChild.Parent = null;
        }

        if (e.NewValue is UIElement newChild)
        {
            newChild.Parent = d as Visual;
        }
    }

    public Color Background
    {
        get => (Color)GetValue(BackgroundProperty)!;
        set => SetValue(BackgroundProperty, value);
    }
    
    /// <summary>Gets or sets the brush (color) used to draw the border lines.</summary>
    public Color BorderBrush
    {
        get => (Color)GetValue(BorderBrushProperty)!;
        set => SetValue(BorderBrushProperty, value);
    }
    
    /// <summary>
    /// Gets or sets the colour the border lines take while focus is somewhere inside this border.
    /// </summary>
    /// <remarks>
    /// This is the framework's focus visual. A terminal has no adorner layer — nothing can be drawn
    /// outside a control's own bounds — so a control that tries to show focus itself has no choice
    /// but to recolour cells it is already using for content: on an unframed list that lands on the
    /// header row and the first and last character of every row. The border is the one place in a
    /// layout that owns cells purely as chrome, so it is where a focus visual belongs.
    /// </remarks>
    public Color FocusBorderBrush
    {
        get => (Color)GetValue(FocusBorderBrushProperty)!;
        set => SetValue(FocusBorderBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the border switches to <see cref="FocusBorderBrush"/> while it contains
    /// focus. Default is true. Set false for a border that is decoration only — a panel frame that
    /// happens to contain an editable field, say — where lighting up would say nothing.
    /// </summary>
    public bool ShowFocusBorder
    {
        get => (bool)GetValue(ShowFocusBorderProperty)!;
        set => SetValue(ShowFocusBorderProperty, value);
    }

    /// <summary>
    /// Gets whether the focused element is this border or a descendant of it. The equivalent of
    /// WPF's <c>IsKeyboardFocusWithin</c>, walked over the visual parent chain because focus here is
    /// a single element held by the <see cref="Input.FocusManager"/> rather than a routed state.
    /// </summary>
    public bool ContainsFocus
    {
        get
        {
            if (App.Application.Current?.FocusManager.FocusedElement is not { } focused)
            {
                return false;
            }

            for (Visual? visual = focused; visual is not null; visual = visual.Parent)
            {
                if (ReferenceEquals(visual, this))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Gets or sets the border style and color.</summary>
    public Styling.BorderStyle BorderStyle
    {
        get => (Styling.BorderStyle)GetValue(BorderStyleProperty)!;
        set => SetValue(BorderStyleProperty, value);
    }
    
    /// <summary>Gets or sets the inner padding between border and child content.</summary>
    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty)!;
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>Gets or sets the child control to render inside this border.</summary>
    public UIElement? Child
    {
        get => (UIElement?)GetValue(ChildProperty);
        set => SetValue(ChildProperty, value);
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
    
    /// <inheritdoc />
    public override IEnumerable<(Visual Child, Rect ChildParentBounds)> GetChildrenWithBounds(Rect myBounds)
    {
        if (Child == null)
        {
            yield break;
        }

        var innerBounds = BorderStyle.HasBorder && myBounds is { Width: >= 2, Height: >= 2 }
            ? new Rect(myBounds.X + 1 + Padding.Left, myBounds.Y + 1 + Padding.Top,
                Math.Max(0, myBounds.Width - 2 - Padding.HorizontalTotal),
                Math.Max(0, myBounds.Height - 2 - Padding.VerticalTotal))
            : new Rect(myBounds.X + Padding.Left, myBounds.Y + Padding.Top,
                Math.Max(0, myBounds.Width - Padding.HorizontalTotal),
                Math.Max(0, myBounds.Height - Padding.VerticalTotal));
        yield return (Child, innerBounds);
    }

    /// <inheritdoc />
    protected internal override IEnumerable<FrameworkElement> GetLogicalChildren()
    {
        if (Child is FrameworkElement fe)
        {
            yield return fe;
        }
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
        
        return ApplyAlignment(parent, w, h);
    }
    
    /// <summary>
    /// Renders this border to the specified cell buffer.
    /// </summary>
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        
        // Clip to buffer bounds
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0)
        {
            return;
        }

        // Fill background
        var bgCell = new Cell(' ', BorderBrush, Background);
        buffer.FillRect(clipped, bgCell);
        
        // Draw border if present
        if (BorderStyle.HasBorder && bounds is { Width: >= 2, Height: >= 2 })
        {
            RenderBorder(buffer, bounds);
        }
        
        // Render child control if present
        if (Child != null)
        {
            // Calculate inner bounds (subtract border and padding)
            var childBounds = BorderStyle.HasBorder && bounds is { Width: >= 2, Height: >= 2 }
                ? new Rect(bounds.X + 1 + Padding.Left, bounds.Y + 1 + Padding.Top,
                    Math.Max(0, bounds.Width - 2 - Padding.HorizontalTotal),
                    Math.Max(0, bounds.Height - 2 - Padding.VerticalTotal))
                : new Rect(bounds.X + Padding.Left, bounds.Y + Padding.Top,
                    Math.Max(0, bounds.Width - Padding.HorizontalTotal),
                    Math.Max(0, bounds.Height - Padding.VerticalTotal));
            
            Child.Render(buffer, childBounds);
        }
    }
    
    private void RenderBorder(CellBuffer buffer, Rect bounds)
    {
        var chars = BorderStyle.Chars;
        var color = ShowFocusBorder && ContainsFocus ? FocusBorderBrush : BorderBrush;
        var bg = Background;
        
        // Draw corners
        buffer.SetCell(bounds.X, bounds.Y, new Cell(chars.TopLeft, color, bg));
        buffer.SetCell(bounds.Right - 1, bounds.Y, new Cell(chars.TopRight, color, bg));
        buffer.SetCell(bounds.X, bounds.Bottom - 1, new Cell(chars.BottomLeft, color, bg));
        buffer.SetCell(bounds.Right - 1, bounds.Bottom - 1, new Cell(chars.BottomRight, color, bg));
        
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
