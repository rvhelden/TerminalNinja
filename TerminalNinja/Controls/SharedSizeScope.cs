using System.Runtime.CompilerServices;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Collects the sizes that grids under one scope have asked for, per shared-size group, so that
/// a column in several different grids can settle on one width.
/// </summary>
/// <remarks>
/// WPF resolves this with a second measure pass over the scope. There is no measure pass here —
/// layout happens during render — so the first grid to ask collects everyone's vote before
/// answering, by walking down from the scope element. Waiting for a second frame instead would
/// have been simpler, but a headless single-frame capture is how layouts get verified, and a
/// feature that is wrong in exactly that mode is worse than no feature.
///
/// The walk uses the children each container already exposes as plain properties rather than
/// <c>GetChildrenWithBounds</c>, which would run layout and re-enter the grid sizing that asked
/// the question in the first place.
///
/// Contributions are held in a <see cref="ConditionalWeakTable{TKey,TValue}"/>, keyed weakly on
/// the grid. Templated rows come and go constantly — an ItemsControl regenerates its containers —
/// and a strong dictionary would pin every grid that ever rendered, and keep letting dead ones
/// vote on the width.
/// </remarks>
internal sealed class SharedSizeScope
{
    private readonly ConditionalWeakTable<Grid, Dictionary<string, int>> _contributions = new();

    /// <summary>Records what one grid needs for a group, keeping the largest claim.</summary>
    public void Publish(Grid grid, string group, int desired)
    {
        var forGrid = _contributions.GetOrCreateValue(grid);

        if (!forGrid.TryGetValue(group, out var existing) || desired > existing)
        {
            forGrid[group] = desired;
        }
    }

    /// <summary>
    /// Has every grid under <paramref name="root"/> publish its own content sizes, so the first
    /// one to lay out already knows what the widest of its peers needs.
    /// </summary>
    public void Collect(Visual root, Rect bounds)
    {
        // Cleared first: a grid that has left the tree must stop holding the group open. Only
        // what the walk finds now gets a vote.
        _contributions.Clear();

        foreach (var descendant in Descendants(root))
        {
            if (descendant is Grid grid)
            {
                grid.PublishSharedContributions(this, bounds);
            }
        }
    }

    private static IEnumerable<Visual> Descendants(Visual root)
    {
        var pending = new Stack<Visual>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            yield return current;

            switch (current)
            {
                case Panel panel:
                    foreach (var child in panel.Children)
                    {
                        pending.Push(child);
                    }

                    break;

                case Border { Child: { } child }:
                    pending.Push(child);
                    break;

                // An ItemsControl is a Control, not a Panel, so its generated rows hang off its
                // ItemsPanel rather than off itself. Missing this branch is missing the whole
                // point: a list of rows is exactly what shared sizing is for.
                case ItemsControl { ItemsPanel: { } itemsPanel }:
                    pending.Push(itemsPanel);
                    break;

                case ContentControl { Content: Visual content }:
                    pending.Push(content);
                    break;
            }
        }
    }

    public int Largest(string group)
    {
        var largest = 0;

        foreach (var (_, sizes) in _contributions)
        {
            if (sizes.TryGetValue(group, out var size) && size > largest)
            {
                largest = size;
            }
        }

        return largest;
    }
}
