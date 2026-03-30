using System.Collections;
using TerminalNinja.Controls;

namespace TerminalNinja.Documents;

/// <summary>
/// A collection of <see cref="Inline"/> elements that manages Parent relationships
/// and invalidates the owning element's visual state on mutations.
/// Used by <see cref="TextBlock.Inlines"/> and <see cref="Span.Inlines"/>.
/// Implements <see cref="System.Collections.IList"/> so the XAML loader's fallback
/// <c>AddToCollection</c> path works without any XamlLoader changes.
/// </summary>
public sealed class InlineCollection : IList<Inline>, IList
{
    private readonly List<Inline> _items = [];
    private readonly FrameworkElement _owner;

    /// <summary>
    /// Creates a new InlineCollection owned by the specified element.
    /// </summary>
    public InlineCollection(FrameworkElement owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    // ─── IList<Inline> ──────────────────────────────────────────────

    /// <summary>Gets or sets the inline at the specified index.</summary>
    public Inline this[int index]
    {
        get => _items[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var old = _items[index];
            old.Parent = null;
            _items[index] = value;
            value.Parent = _owner;
            _owner.InvalidateVisual();
        }
    }

    /// <summary>Gets the number of inlines in this collection.</summary>
    public int Count => _items.Count;

    /// <summary>Gets whether this collection is read-only (always false).</summary>
    public bool IsReadOnly => false;

    /// <summary>Adds an inline to this collection.</summary>
    public void Add(Inline item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Parent = _owner;
        _items.Add(item);
        _owner.InvalidateVisual();
    }

    /// <summary>Removes all inlines from this collection.</summary>
    public void Clear()
    {
        foreach (var item in _items)
        {
            item.Parent = null;
        }

        _items.Clear();
        _owner.InvalidateVisual();
    }

    /// <summary>Determines whether this collection contains the specified inline.</summary>
    public bool Contains(Inline item) => _items.Contains(item);

    /// <summary>Copies the collection to an array.</summary>
    public void CopyTo(Inline[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    /// <summary>Returns an enumerator for the inlines.</summary>
    public IEnumerator<Inline> GetEnumerator() => _items.GetEnumerator();

    /// <summary>Returns the index of the specified inline.</summary>
    public int IndexOf(Inline item) => _items.IndexOf(item);

    /// <summary>Inserts an inline at the specified index.</summary>
    public void Insert(int index, Inline item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Parent = _owner;
        _items.Insert(index, item);
        _owner.InvalidateVisual();
    }

    /// <summary>Removes an inline from this collection.</summary>
    public bool Remove(Inline item)
    {
        if (_items.Remove(item))
        {
            item.Parent = null;
            _owner.InvalidateVisual();
            return true;
        }
        return false;
    }

    /// <summary>Removes the inline at the specified index.</summary>
    public void RemoveAt(int index)
    {
        var item = _items[index];
        _items.RemoveAt(index);
        item.Parent = null;
        _owner.InvalidateVisual();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // ─── IList (non-generic, for XAML loader fallback) ──────────────

    bool IList.IsFixedSize => false;
    bool IList.IsReadOnly => false;
    int ICollection.Count => _items.Count;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => ((ICollection)_items).SyncRoot;

    object? IList.this[int index]
    {
        get => _items[index];
        set
        {
            if (value is Inline inline)
            {
                this[index] = inline;
            }
            else
            {
                throw new ArgumentException($"Expected Inline but got {value?.GetType().Name ?? "null"}", nameof(value));
            }
        }
    }

    int IList.Add(object? value)
    {
        if (value is Inline inline)
        {
            Add(inline);
            return _items.Count - 1;
        }
        throw new ArgumentException($"Expected Inline but got {value?.GetType().Name ?? "null"}", nameof(value));
    }

    void IList.Clear() => Clear();
    bool IList.Contains(object? value) => value is Inline inline && Contains(inline);
    int IList.IndexOf(object? value) => value is Inline inline ? IndexOf(inline) : -1;

    void IList.Insert(int index, object? value)
    {
        if (value is Inline inline)
        {
            Insert(index, inline);
        }
        else
        {
            throw new ArgumentException($"Expected Inline but got {value?.GetType().Name ?? "null"}", nameof(value));
        }
    }

    void IList.Remove(object? value)
    {
        if (value is Inline inline)
        {
            Remove(inline);
        }
    }

    void IList.RemoveAt(int index) => RemoveAt(index);
    void ICollection.CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
}
