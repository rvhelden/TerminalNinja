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
}
