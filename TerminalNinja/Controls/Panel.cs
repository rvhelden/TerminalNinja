using System.Collections;
using TerminalNinja.Markup;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Base class for layout panels that arrange child elements.
/// Provides a Children collection and common panel functionality.
/// </summary>
[ContentProperty("Children")]
public abstract class Panel : FrameworkElement
{
    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(nameof(Background), typeof(Color?), typeof(Panel),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true));

    private readonly ObservableControlCollection _children;
    
    /// <summary>
    /// Initializes a new instance of the Panel class.
    /// </summary>
    protected Panel()
    {
        _children = new ObservableControlCollection(this);
    }
    
    /// <summary>
    /// Gets the collection of child controls in this panel.
    /// </summary>
    public IList<UIElement> Children => _children;
    
    /// <summary>
    /// Gets or sets the background color of the panel.
    /// </summary>
    public Color? Background
    {
        get => (Color?)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }
    
    /// <inheritdoc />
    protected internal override IEnumerable<FrameworkElement> GetLogicalChildren()
    {
        foreach (var child in _children)
        {
            if (child is FrameworkElement fe)
                yield return fe;
        }
    }
    
    /// <summary>
    /// Called when a child control is added to the Children collection.
    /// </summary>
    /// <param name="child">The child control that was added.</param>
    internal virtual void OnChildAdded(UIElement child)
    {
    }
    
    /// <summary>
    /// Called when a child control is removed from the Children collection.
    /// </summary>
    /// <param name="child">The child control that was removed.</param>
    internal virtual void OnChildRemoved(UIElement child)
    {
    }
}

/// <summary>
/// Observable collection for Panel.Children that automatically sets Parent and triggers callbacks.
/// </summary>
internal class ObservableControlCollection : IList<UIElement>
{
    private readonly List<UIElement> _items = new();
    private readonly Panel _owner;
    
    public ObservableControlCollection(Panel owner)
    {
        _owner = owner;
    }
    
    public UIElement this[int index]
    {
        get => _items[index];
        set
        {
            var oldItem = _items[index];
            oldItem.Parent = null;
            _owner.OnChildRemoved(oldItem);
            
            _items[index] = value;
            value.Parent = _owner;
            _owner.OnChildAdded(value);
            _owner.InvalidateVisual();
        }
    }
    
    public int Count => _items.Count;
    public bool IsReadOnly => false;
    
    public void Add(UIElement item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Parent = _owner;
        _items.Add(item);
        _owner.OnChildAdded(item);
        _owner.InvalidateVisual();
    }
    
    public void Clear()
    {
        foreach (var item in _items)
        {
            item.Parent = null;
            _owner.OnChildRemoved(item);
        }
        _items.Clear();
        _owner.InvalidateVisual();
    }
    
    public bool Contains(UIElement item) => _items.Contains(item);
    
    public void CopyTo(UIElement[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    
    public IEnumerator<UIElement> GetEnumerator() => _items.GetEnumerator();
    
    public int IndexOf(UIElement item) => _items.IndexOf(item);
    
    public void Insert(int index, UIElement item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Parent = _owner;
        _items.Insert(index, item);
        _owner.OnChildAdded(item);
        _owner.InvalidateVisual();
    }
    
    public bool Remove(UIElement item)
    {
        if (_items.Remove(item))
        {
            item.Parent = null;
            _owner.OnChildRemoved(item);
            _owner.InvalidateVisual();
            return true;
        }
        return false;
    }
    
    public void RemoveAt(int index)
    {
        var item = _items[index];
        _items.RemoveAt(index);
        item.Parent = null;
        _owner.OnChildRemoved(item);
        _owner.InvalidateVisual();
    }
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
