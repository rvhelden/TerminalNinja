using TerminalNinja.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A panel that arranges child elements sequentially in a horizontal or vertical orientation.
/// Children can be sized using StackPanel.SizeMode and StackPanel.FixedSize attached properties.
/// </summary>
[ContentProperty("Children")]
[RuntimeNameProperty("Name")]
public class StackPanel : Panel
{
    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(StackPanel),
            new FrameworkPropertyMetadata(Orientation.Vertical, affectsRender: true));

    public static readonly DependencyProperty CrossAxisAlignmentProperty =
        DependencyProperty.Register(nameof(CrossAxisAlignment), typeof(Alignment), typeof(StackPanel),
            new FrameworkPropertyMetadata(Alignment.Start, affectsRender: true));

    // ─── Attached Dependency Properties ──────────────────────────────

    public static readonly DependencyProperty SizeModeProperty =
        DependencyProperty.RegisterAttached("SizeMode", typeof(ChildSizeMode), typeof(StackPanel),
            new PropertyMetadata(ChildSizeMode.Auto));

    public static readonly DependencyProperty FixedSizeProperty =
        DependencyProperty.RegisterAttached("FixedSize", typeof(int), typeof(StackPanel),
            new PropertyMetadata(0));

    /// <summary>
    /// Gets or sets the orientation (Horizontal or Vertical) of the stack.
    /// </summary>
    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty)!;
        set => SetValue(OrientationProperty, value);
    }
    
    /// <summary>
    /// Gets or sets the alignment of children on the cross-axis.
    /// </summary>
    public Alignment CrossAxisAlignment
    {
        get => (Alignment)GetValue(CrossAxisAlignmentProperty)!;
        set => SetValue(CrossAxisAlignmentProperty, value);
    }

    // ─── Attached Property Accessors ─────────────────────────────────

    /// <summary>
    /// Gets the StackPanel.SizeMode attached property value for a control.
    /// </summary>
    public static ChildSizeMode GetSizeMode(DependencyObject control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return (ChildSizeMode)control.GetValue(SizeModeProperty)!;
    }
    
    /// <summary>
    /// Sets the StackPanel.SizeMode attached property value for a control.
    /// </summary>
    public static void SetSizeMode(DependencyObject control, ChildSizeMode mode)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(SizeModeProperty, mode);
    }
    
    /// <summary>
    /// Gets the StackPanel.FixedSize attached property value for a control.
    /// </summary>
    public static int GetFixedSize(DependencyObject control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return (int)control.GetValue(FixedSizeProperty)!;
    }
    
    /// <summary>
    /// Sets the StackPanel.FixedSize attached property value for a control.
    /// </summary>
    public static void SetFixedSize(DependencyObject control, int size)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.SetValue(FixedSizeProperty, Math.Max(0, size));
    }
    
    /// <summary>
    /// Returns the preferred size of this stack (sum of children's preferred sizes).
    /// </summary>
    public override Size2D GetPreferredSize(Rect parent)
    {
        if (Children.Count == 0)
            return new Size2D(0, 0);
        
        var totalMainAxis = 0;
        var maxCrossAxis = 0;
        
        foreach (var child in Children)
        {
            var sizeMode = GetSizeMode(child);
            var preferredSize = child.GetPreferredSize(parent);
            
            if (Orientation == Orientation.Horizontal)
            {
                totalMainAxis += sizeMode == ChildSizeMode.Fixed 
                    ? GetFixedSize(child)
                    : preferredSize.Width;
                maxCrossAxis = Math.Max(maxCrossAxis, preferredSize.Height);
            }
            else
            {
                totalMainAxis += sizeMode == ChildSizeMode.Fixed 
                    ? GetFixedSize(child)
                    : preferredSize.Height;
                maxCrossAxis = Math.Max(maxCrossAxis, preferredSize.Width);
            }
        }
        
        return Orientation == Orientation.Horizontal
            ? new Size2D(totalMainAxis, maxCrossAxis)
            : new Size2D(maxCrossAxis, totalMainAxis);
    }
    
    /// <summary>
    /// Calculates bounds (StackPanel always fills parent).
    /// </summary>
    public override Rect CalculateBounds(Rect parent) => parent;
    
    /// <summary>
    /// Renders the stack panel and all its children.
    /// </summary>
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        if (Children.Count == 0) return;
        
        var bounds = CalculateBounds(parentBounds);
        
        // Calculate sizes for each child
        var childSizes = CalculateChildSizes(bounds);
        
        // Render each child at its calculated position
        var position = Orientation == Orientation.Horizontal ? bounds.X : bounds.Y;
        
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            var size = childSizes[i];
            
            if (size <= 0) continue; // Skip zero-size children
            
            var childBounds = CreateChildBounds(bounds, position, size);
            child.Render(buffer, childBounds);
            
            position += size;
        }
    }
    
    /// <inheritdoc />
    public override IEnumerable<(Visual Child, Rect ChildParentBounds)> GetChildrenWithBounds(Rect myBounds)
    {
        var childSizes = CalculateChildSizes(myBounds);
        var position = Orientation == Orientation.Horizontal ? myBounds.X : myBounds.Y;

        for (var i = 0; i < Children.Count; i++)
        {
            var size = childSizes[i];
            if (size <= 0) continue;
            yield return (Children[i], CreateChildBounds(myBounds, position, size));
            position += size;
        }
    }
    
    /// <summary>
    /// Calculates the size for each child along the main axis.
    /// </summary>
    internal int[] CalculateChildSizes(Rect bounds)
    {
        var sizes = new int[Children.Count];
        var mainAxisSize = Orientation == Orientation.Horizontal ? bounds.Width : bounds.Height;
        
        var totalFixed = 0;
        var stretchCount = 0;
        
        // First pass: calculate Fixed and Auto sizes
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            var sizeMode = GetSizeMode(child);
            
            switch (sizeMode)
            {
                case ChildSizeMode.Fixed:
                    sizes[i] = GetFixedSize(child);
                    totalFixed += sizes[i];
                    break;
                
                case ChildSizeMode.Auto:
                    var preferredSize = child.GetPreferredSize(bounds);
                    sizes[i] = Orientation == Orientation.Horizontal 
                        ? preferredSize.Width 
                        : preferredSize.Height;
                    totalFixed += sizes[i];
                    break;
                
                case ChildSizeMode.Stretch:
                    stretchCount++;
                    break;
            }
        }
        
        // Second pass: distribute remaining space to Stretch children
        var remainingSpace = Math.Max(0, mainAxisSize - totalFixed);
        var stretchSize = stretchCount > 0 ? remainingSpace / stretchCount : 0;
        
        for (var i = 0; i < Children.Count; i++)
        {
            if (GetSizeMode(Children[i]) == ChildSizeMode.Stretch)
            {
                sizes[i] = stretchSize;
            }
        }
        
        return sizes;
    }
    
    /// <summary>
    /// Creates bounds for a child control based on stack orientation.
    /// Children fill the entire cross-axis (height for horizontal, width for vertical).
    /// </summary>
    internal Rect CreateChildBounds(Rect stackBounds, int position, int size)
    {
        if (Orientation == Orientation.Horizontal)
        {
            // Horizontal stack: position is X coordinate, size is width
            // Child fills the full height of the stack
            return new Rect(position, stackBounds.Y, size, stackBounds.Height);
        }
        else
        {
            // Vertical stack: position is Y coordinate, size is height
            // Child fills the full width of the stack
            return new Rect(stackBounds.X, position, stackBounds.Width, size);
        }
    }
}
