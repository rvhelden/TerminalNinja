using System.Collections;
using System.Windows.Markup;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Base class for layout panels that arrange child elements.
/// Provides a Children collection and common panel functionality.
/// </summary>
[ContentProperty("Children")]
[Portable.Xaml.Markup.ContentProperty("Children")] // TEMPORARY: Required until Portable.Xaml is removed
public abstract class Panel : FrameworkElement
{
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
    public IList<IControl> Children => _children;
    
    /// <summary>
    /// Gets or sets the background color of the panel.
    /// </summary>
    public Color? Background { get; set; }
    
    /// <summary>
    /// Called when a child control is added to the Children collection.
    /// </summary>
    /// <param name="child">The child control that was added.</param>
    internal virtual void OnChildAdded(IControl child)
    {
    }
    
    /// <summary>
    /// Called when a child control is removed from the Children collection.
    /// </summary>
    /// <param name="child">The child control that was removed.</param>
    internal virtual void OnChildRemoved(IControl child)
    {
    }
}

/// <summary>
/// Observable collection for Panel.Children that automatically sets Parent and triggers callbacks.
/// </summary>
internal class ObservableControlCollection : IList<IControl>
{
    private readonly List<IControl> _items = new();
    private readonly Panel _owner;
    
    public ObservableControlCollection(Panel owner)
    {
        _owner = owner;
    }
    
    public IControl this[int index]
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
    
    public void Add(IControl item)
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
    
    public bool Contains(IControl item) => _items.Contains(item);
    
    public void CopyTo(IControl[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    
    public IEnumerator<IControl> GetEnumerator() => _items.GetEnumerator();
    
    public int IndexOf(IControl item) => _items.IndexOf(item);
    
    public void Insert(int index, IControl item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Parent = _owner;
        _items.Insert(index, item);
        _owner.OnChildAdded(item);
        _owner.InvalidateVisual();
    }
    
    public bool Remove(IControl item)
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
