namespace TerminalNinja.DependencySystem;

/// <summary>
/// Provides data for dependency property changed callbacks.
/// </summary>
public readonly struct DependencyPropertyChangedEventArgs
{
    /// <summary>Gets the dependency property that changed.</summary>
    public DependencyProperty Property { get; }

    /// <summary>Gets the value of the property before the change.</summary>
    public object? OldValue { get; }

    /// <summary>Gets the value of the property after the change.</summary>
    public object? NewValue { get; }

    internal DependencyPropertyChangedEventArgs(DependencyProperty property, object? oldValue, object? newValue)
    {
        Property = property;
        OldValue = oldValue;
        NewValue = newValue;
    }
}
