using Portable.Xaml.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Elements;

/// <summary>
/// A layout container that arranges child elements horizontally or vertically.
/// </summary>
[ContentProperty("Children")]
[RuntimeNameProperty("Name")]
public sealed class Stack : ElementBase
{
    // Layout properties (kept as init for now)
    /// <summary>Gets or sets the orientation (Horizontal or Vertical).</summary>
    public StackOrientation Orientation { get; init; } = StackOrientation.Horizontal;
    
    /// <summary>Gets or sets the alignment of children on the cross-axis.</summary>
    public Alignment CrossAxisAlignment { get; init; } = Alignment.Start;
    
    private List<StackChild> _children = [];
    /// <summary>Gets or sets the list of child elements with their sizing modes.</summary>
    public List<StackChild> Children
    {
        get => _children;
        init
        {
            // Wire up Parent for each child element
            foreach (var child in _children)
            {
                if (child.Element != null)
                    child.Element.Parent = null;
            }
            
            _children = value;
            
            foreach (var child in _children)
            {
                if (child.Element != null)
                    child.Element.Parent = this;
            }
        }
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
            var preferredSize = child.Element.GetPreferredSize(parent);
            
            if (Orientation == StackOrientation.Horizontal)
            {
                totalMainAxis += child.SizeMode == ChildSizeMode.Fixed 
                    ? child.FixedSize 
                    : preferredSize.Width;
                maxCrossAxis = Math.Max(maxCrossAxis, preferredSize.Height);
            }
            else
            {
                totalMainAxis += child.SizeMode == ChildSizeMode.Fixed 
                    ? child.FixedSize 
                    : preferredSize.Height;
                maxCrossAxis = Math.Max(maxCrossAxis, preferredSize.Width);
            }
        }
        
        return Orientation == StackOrientation.Horizontal
            ? new Size2D(totalMainAxis, maxCrossAxis)
            : new Size2D(maxCrossAxis, totalMainAxis);
    }
    
    /// <summary>
    /// Calculates bounds (Stack always fills parent).
    /// </summary>
    public override Rect CalculateBounds(Rect parent) => parent;
    
    /// <summary>
    /// Renders the stack and all its children.
    /// </summary>
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        if (Children.Count == 0) return;
        
        var bounds = CalculateBounds(parentBounds);
        
        // Calculate sizes for each child
        var childSizes = CalculateChildSizes(bounds);
        
        // Render each child at its calculated position
        var position = Orientation == StackOrientation.Horizontal ? bounds.X : bounds.Y;
        
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            var size = childSizes[i];
            
            if (size <= 0) continue; // Skip zero-size children
            
            var childBounds = CreateChildBounds(bounds, position, size);
            child.Element.Render(buffer, childBounds);
            
            position += size;
        }
    }
    
    /// <summary>
    /// Calculates the size for each child along the main axis.
    /// </summary>
    internal int[] CalculateChildSizes(Rect bounds)
    {
        var sizes = new int[Children.Count];
        var mainAxisSize = Orientation == StackOrientation.Horizontal ? bounds.Width : bounds.Height;
        
        var totalFixed = 0;
        var stretchCount = 0;
        
        // First pass: calculate Fixed and Auto sizes
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            
            switch (child.SizeMode)
            {
                case ChildSizeMode.Fixed:
                    sizes[i] = child.FixedSize;
                    totalFixed += child.FixedSize;
                    break;
                
                case ChildSizeMode.Auto:
                    var preferredSize = child.Element.GetPreferredSize(bounds);
                    sizes[i] = Orientation == StackOrientation.Horizontal 
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
            if (Children[i].SizeMode == ChildSizeMode.Stretch)
            {
                sizes[i] = stretchSize;
            }
        }
        
        return sizes;
    }
    
    /// <summary>
    /// Creates bounds for a child element based on stack orientation.
    /// Children fill the entire cross-axis (height for horizontal, width for vertical).
    /// </summary>
    internal Rect CreateChildBounds(Rect stackBounds, int position, int size)
    {
        if (Orientation == StackOrientation.Horizontal)
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
    
    #region Attached Properties for XAML Support
    
    /// <summary>
    /// Storage for attached properties on child elements.
    /// </summary>
    private sealed class StackChildProperties
    {
        public ChildSizeMode SizeMode { get; set; } = ChildSizeMode.Auto;
        public int FixedSize { get; set; }
    }
    
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IElement, StackChildProperties> 
        _attachedProperties = new();
    
    /// <summary>
    /// Sets the size mode for a child element in a Stack (attached property).
    /// </summary>
    public static void SetSizeMode(IElement element, ChildSizeMode mode)
    {
        ArgumentNullException.ThrowIfNull(element);
        var props = _attachedProperties.GetOrCreateValue(element);
        props.SizeMode = mode;
    }
    
    /// <summary>
    /// Gets the size mode for a child element in a Stack (attached property).
    /// </summary>
    public static ChildSizeMode GetSizeMode(IElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return _attachedProperties.TryGetValue(element, out var props) 
            ? props.SizeMode 
            : ChildSizeMode.Auto;
    }
    
    /// <summary>
    /// Sets the fixed size for a child element in a Stack (attached property).
    /// </summary>
    public static void SetFixedSize(IElement element, int size)
    {
        ArgumentNullException.ThrowIfNull(element);
        var props = _attachedProperties.GetOrCreateValue(element);
        props.FixedSize = size;
    }
    
    /// <summary>
    /// Gets the fixed size for a child element in a Stack (attached property).
    /// </summary>
    public static int GetFixedSize(IElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return _attachedProperties.TryGetValue(element, out var props) 
            ? props.FixedSize 
            : 0;
    }
    
    #endregion
}
