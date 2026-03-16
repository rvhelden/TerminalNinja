using System.Runtime.CompilerServices;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Base class for all UI elements that participate in layout, rendering, and input.
/// Provides invalidation, property change helpers, visibility, and enabled state.
/// </summary>
public abstract class UIElement : Visual
{
    private Visibility _visibility = Visibility.Visible;
    private bool _isEnabled = true;

    /// <summary>
    /// Gets or sets the visibility of this element.
    /// </summary>
    public Visibility Visibility
    {
        get => _visibility;
        set => SetProperty(ref _visibility, value);
    }

    /// <summary>
    /// Gets or sets whether this element is enabled for interaction.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    /// <summary>
    /// Gets or sets the callback invoked when this element needs to be re-rendered.
    /// Set by the Application when the element joins the visual tree.
    /// </summary>
    public Action? InvalidationCallback { get; set; }

    /// <summary>
    /// Signals that this element needs to be re-rendered.
    /// </summary>
    public void InvalidateVisual()
    {
        InvalidationCallback?.Invoke();
    }

    /// <inheritdoc />
    protected override void OnPropertyAffectsRender(DependencyProperty dp) => InvalidateVisual();

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
    /// <summary>
    /// Returns the element's preferred size within the given parent bounds.
    /// Used by layout containers to determine Auto-sized children.
    /// </summary>
    public abstract Size2D GetPreferredSize(Rect parent);

    /// <summary>
    /// Calculates the absolute bounds of this element within the parent bounds.
    /// </summary>
    public abstract Rect CalculateBounds(Rect parent);

    /// <summary>
    /// Renders this element to the specified cell buffer.
    /// </summary>
    public abstract void Render(CellBuffer buffer, Rect parentBounds);
}
