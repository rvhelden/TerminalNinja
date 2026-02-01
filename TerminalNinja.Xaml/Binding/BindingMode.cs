namespace TerminalNinja.Xaml.Binding;

/// <summary>
/// Specifies the direction of data flow in a binding.
/// </summary>
public enum BindingMode
{
    /// <summary>
    /// Updates the target property when the binding is created and whenever the source property changes.
    /// </summary>
    OneWay,
    
    /// <summary>
    /// Updates both the target when the source changes and the source when the target changes.
    /// </summary>
    TwoWay,
    
    /// <summary>
    /// Updates the target property only once when the binding is created.
    /// </summary>
    OneTime
}
