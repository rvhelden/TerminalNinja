namespace TerminalNinja.DependencySystem;

/// <summary>
/// Metadata for a dependency property that affects framework-level behavior such as rendering.
/// Use <see cref="AffectsRender"/> to automatically trigger visual invalidation on change.
/// </summary>
public class FrameworkPropertyMetadata : PropertyMetadata
{
    /// <summary>
    /// Gets whether a change to this property requires the element to be re-rendered.
    /// When true, <see cref="DependencyObject.OnPropertyAffectsRender"/> is called automatically by <c>SetValue</c>.
    /// </summary>
    public bool AffectsRender { get; }

    /// <summary>
    /// Gets whether a binding targeting this property binds two-way when its
    /// <c>Mode</c> is left at <see cref="TerminalNinja.Xaml.Binding.BindingMode.Default"/>.
    /// Mirrors WPF's <c>FrameworkPropertyMetadata.BindsTwoWayByDefault</c>; set it for
    /// user-editable state such as a selector's selected item, so a plain
    /// <c>{Binding SelectedItem}</c> writes back to the source without needing an explicit
    /// <c>Mode=TwoWay</c>.
    /// </summary>
    public bool BindsTwoWayByDefault { get; init; }

    public FrameworkPropertyMetadata() { }

    public FrameworkPropertyMetadata(object? defaultValue)
        : base(defaultValue) { }

    public FrameworkPropertyMetadata(object? defaultValue, bool affectsRender)
        : base(defaultValue)
    {
        AffectsRender = affectsRender;
    }

    public FrameworkPropertyMetadata(object? defaultValue, PropertyChangedCallback? propertyChangedCallback)
        : base(defaultValue, propertyChangedCallback) { }

    public FrameworkPropertyMetadata(object? defaultValue, bool affectsRender, PropertyChangedCallback? propertyChangedCallback)
        : base(defaultValue, propertyChangedCallback)
    {
        AffectsRender = affectsRender;
    }

    public FrameworkPropertyMetadata(bool affectsRender, PropertyChangedCallback? propertyChangedCallback = null)
        : base(null, propertyChangedCallback)
    {
        AffectsRender = affectsRender;
    }

    public FrameworkPropertyMetadata(object? defaultValue, bool affectsRender, PropertyChangedCallback? propertyChangedCallback, CoerceValueCallback? coerceValueCallback)
        : base(defaultValue, propertyChangedCallback, coerceValueCallback)
    {
        AffectsRender = affectsRender;
    }
}
