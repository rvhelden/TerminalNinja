namespace TerminalNinja.Controls;

/// <summary>
/// Provides data for value-changed events on picker controls.
/// </summary>
public sealed class ValueChangedEventArgs<T> : EventArgs
{
    /// <summary>Gets the value before the change.</summary>
    public T OldValue { get; }

    /// <summary>Gets the value after the change.</summary>
    public T NewValue { get; }

    public ValueChangedEventArgs(T oldValue, T newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }
}
