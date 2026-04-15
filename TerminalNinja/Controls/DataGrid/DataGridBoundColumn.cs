using TerminalNinja.Aot;

namespace TerminalNinja.Controls;

/// <summary>
/// Abstract base class for <see cref="DataGrid"/> columns that display data
/// from a bound property. The <see cref="Binding"/> path is resolved via
/// <see cref="PropertyAccessorRegistry"/> for Native AOT compatibility.
/// Corresponds to WPF's System.Windows.Controls.DataGridBoundColumn.
/// </summary>
public abstract class DataGridBoundColumn : DataGridColumn
{
    /// <summary>
    /// Gets or sets the property path on each data item to extract the cell value.
    /// Resolved at runtime via <see cref="PropertyAccessorRegistry"/> (AOT-safe).
    /// </summary>
    public string? Binding { get; set; }

    /// <summary>
    /// Resolves the bound property value from a data item.
    /// </summary>
    protected object? GetBindingValue(object? dataItem)
    {
        if (dataItem == null || string.IsNullOrEmpty(Binding)) return null;
        if (PropertyAccessorRegistry.TryGetAccessor(dataItem.GetType(), Binding, out var accessor))
            return accessor.Value.Getter(dataItem);
        return null;
    }

    /// <inheritdoc />
    internal override object? GetSortValue(object? dataItem)
    {
        // SortMemberPath takes precedence, then fall back to Binding path
        var path = SortMemberPath ?? Binding;
        if (dataItem == null || string.IsNullOrEmpty(path)) return null;
        if (PropertyAccessorRegistry.TryGetAccessor(dataItem.GetType(), path, out var accessor))
            return accessor.Value.Getter(dataItem);
        return null;
    }
}
