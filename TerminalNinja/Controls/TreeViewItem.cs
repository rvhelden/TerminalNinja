using System.Collections.ObjectModel;
using System.Windows.Markup;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Represents a node in a <see cref="TreeView"/>. Each node has a <see cref="Header"/>
/// for display, an <see cref="IsExpanded"/> state, and a collection of child <see cref="Items"/>.
/// TreeViewItem does not render itself — the parent TreeView handles all rendering.
/// Corresponds to WPF's System.Windows.Controls.TreeViewItem.
/// </summary>
[ContentProperty("Items")]
[RuntimeNameProperty("Name")]
public class TreeViewItem : ContentControl
{
    public TreeViewItem()
    {
        DefaultStyleKey = typeof(TreeViewItem);
        Focusable = false;
        _items = [];
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(object), typeof(TreeViewItem),
            new FrameworkPropertyMetadata((object?)null, affectsRender: true));

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(TreeViewItem),
            new FrameworkPropertyMetadata(false, affectsRender: true));

    /// <summary>Gets or sets the display text for this node.</summary>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>Gets or sets whether the node's children are visible.</summary>
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty)!;
        set => SetValue(IsExpandedProperty, value);
    }

    // ─── Child Items ─────────────────────────────────────────────────

    private readonly ObservableCollection<TreeViewItem> _items;

    /// <summary>Gets the collection of child nodes.</summary>
    public IList<TreeViewItem> Items => _items;

    /// <summary>Gets whether this node has child items.</summary>
    public bool HasItems => _items.Count > 0;

    /// <summary>Gets the header text for display.</summary>
    internal string HeaderText => Header?.ToString() ?? "";
}
