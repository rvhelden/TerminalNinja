using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Displays the items panel of an <see cref="ItemsControl"/>.
/// Place an <see cref="ItemsPresenter"/> inside a <see cref="UserControl"/> or
/// <see cref="ContentControl"/> that wraps an <see cref="ItemsControl"/> to control
/// where the items are rendered within a larger visual tree.
///
/// The presenter walks up the <see cref="Visual.Parent"/> chain to find its
/// owning <see cref="ItemsControl"/>, then delegates layout and rendering
/// to that control's <see cref="ItemsControl.ItemsPanel"/>.
///
/// Corresponds to WPF's System.Windows.Controls.ItemsPresenter.
/// </summary>
public class ItemsPresenter : FrameworkElement
{
    public ItemsPresenter()
    {
        DefaultStyleKey = typeof(ItemsPresenter);
    }

    private ItemsControl? _owner;

    /// <summary>
    /// Gets the <see cref="ItemsControl"/> that this presenter is connected to.
    /// Resolved lazily by walking up the parent chain.
    /// </summary>
    public ItemsControl? Owner => _owner ??= FindOwner();

    /// <summary>
    /// Walks up the <see cref="Visual.Parent"/> chain to find the nearest
    /// <see cref="ItemsControl"/> ancestor.
    /// </summary>
    private ItemsControl? FindOwner()
    {
        var current = Parent;
        while (current != null)
        {
            if (current is ItemsControl ic)
            {
                return ic;
            }

            current = current.Parent;
        }
        return null;
    }

    /// <summary>
    /// Invalidates the cached owner so it will be re-discovered on next access.
    /// Call this if the presenter is re-parented.
    /// </summary>
    internal void InvalidateOwner()
    {
        _owner = null;
    }

    // ─── Layout & Rendering ──────────────────────────────────────────

    /// <inheritdoc />
    public override IEnumerable<(Visual Child, Rect ChildParentBounds)> GetChildrenWithBounds(Rect myBounds)
    {
        var panel = Owner?.ItemsPanel;
        if (panel != null)
        {
            yield return (panel, myBounds);
        }
    }

    /// <inheritdoc />
    protected internal override IEnumerable<FrameworkElement> GetLogicalChildren()
    {
        var panel = Owner?.ItemsPanel;
        if (panel != null)
        {
            yield return panel;
        }
    }

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent)
    {
        return Owner?.ItemsPanel.GetPreferredSize(parent) ?? new Size2D(0, 0);
    }

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parent) => parent;

    /// <inheritdoc />
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        Owner?.ItemsPanel.Render(buffer, bounds);
    }
}
