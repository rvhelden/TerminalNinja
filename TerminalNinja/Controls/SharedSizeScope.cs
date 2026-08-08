using System.Runtime.CompilerServices;

namespace TerminalNinja.Controls;

/// <summary>
/// Collects the sizes that grids under one scope have asked for, per shared-size group, so that
/// a column in several different grids can settle on one width.
/// </summary>
/// <remarks>
/// WPF resolves this with a second measure pass over the scope. There is no measure pass here —
/// layout happens during render — so grids publish what they want and read back the largest
/// contribution. A grid that laid out earlier in the frame with a smaller value would be a cell
/// short, so a contribution that moves a group's maximum invalidates the application and the next
/// frame settles it. The maximum only moves when content changes, so this converges rather than
/// looping.
///
/// Contributions are held in a <see cref="ConditionalWeakTable{TKey,TValue}"/>, keyed weakly on
/// the grid. Templated rows come and go constantly — an ItemsControl regenerates its containers —
/// and a strong dictionary would pin every grid that ever rendered, and keep letting dead ones
/// vote on the width.
/// </remarks>
internal sealed class SharedSizeScope
{
    private readonly ConditionalWeakTable<Grid, Dictionary<string, int>> _contributions = new();

    /// <summary>
    /// Records what one grid wants for a group and returns the largest anyone in the scope wants.
    /// </summary>
    /// <param name="grid">The grid casting a vote; held weakly.</param>
    /// <param name="group">The group name, already qualified by axis.</param>
    /// <param name="desired">What this grid's own content needs.</param>
    /// <param name="changed">
    /// True when this call moved the group's maximum, meaning grids already laid out this frame
    /// are now stale and the frame needs drawing again.
    /// </param>
    public int Publish(Grid grid, string group, int desired, out bool changed)
    {
        var before = Largest(group);

        var forGrid = _contributions.GetOrCreateValue(grid);
        forGrid[group] = desired;

        var after = Math.Max(before, desired);

        // Shrinking matters too: the grid that was holding the group wide may have just got
        // narrower, and nothing else recomputes the maximum for us.
        if (desired < before)
        {
            after = Largest(group);
        }

        changed = after != before;
        return after;
    }

    private int Largest(string group)
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
