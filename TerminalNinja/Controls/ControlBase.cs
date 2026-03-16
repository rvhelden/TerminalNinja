using System.Runtime.CompilerServices;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Base class for all controls providing dependency property support, INotifyPropertyChanged, and visual invalidation.
/// </summary>
public abstract class ControlBase : DependencyObject, IControl
{
    // IControl properties
    private string? _name;
    public string? Name
    {
        get => _name;
        set => SetProperty(ref _name, value, invalidate: false);
    }

    private object? _dataContext;
    public object? DataContext
    {
        get => _dataContext;
        set => SetProperty(ref _dataContext, value, invalidate: false);
    }

    public IControl? Parent { get; set; }

    public Action? InvalidationCallback { get; set; }

    /// <summary>
    /// Signals that this control needs to be re-rendered.
    /// </summary>
    public void InvalidateVisual()
    {
        InvalidationCallback?.Invoke();
    }

    /// <inheritdoc />
    protected override void OnPropertyAffectsRender(DependencyProperty dp) => InvalidateVisual();

    /// <summary>
    /// Gets the effective DataContext by walking up the parent chain if needed.
    /// </summary>
    public object? GetEffectiveDataContext()
    {
        if (_dataContext != null)
            return _dataContext;

        return Parent switch
        {
            ControlBase cb => cb.GetEffectiveDataContext(),
            IControl c => c.DataContext,
            _ => null
        };
    }

    /// <summary>
    /// Sets a CLR-backed property value and raises PropertyChanged if the value changed.
    /// For dependency properties use <see cref="DependencyObject.SetValue"/> instead.
    /// </summary>
    /// <param name="field">Reference to the backing field.</param>
    /// <param name="value">The new value.</param>
    /// <param name="invalidate">Whether to trigger visual invalidation (default: true).</param>
    /// <param name="propertyName">The property name (automatically captured).</param>
    /// <returns>True if the value changed; otherwise, false.</returns>
    protected bool SetProperty<T>(
        ref T field,
        T value,
        bool invalidate = true,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName!);

        if (invalidate)
            InvalidateVisual();

        return true;
    }

    // Abstract members that derived classes must implement
    public abstract Size2D GetPreferredSize(Rect parent);
    public abstract Rect CalculateBounds(Rect parent);
    public abstract void Render(CellBuffer buffer, Rect parentBounds);
}
