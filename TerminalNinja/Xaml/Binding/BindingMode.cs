namespace TerminalNinja.Xaml.Binding;

/// <summary>
/// Specifies the direction of data flow in a binding.
/// </summary>
public enum BindingMode
{
    /// <summary>
    /// Uses the default mode of the target dependency property. Most properties default to
    /// <see cref="OneWay"/>; properties registered with
    /// <see cref="TerminalNinja.DependencySystem.FrameworkPropertyMetadata.BindsTwoWayByDefault"/>
    /// (such as <see cref="TerminalNinja.Controls.Primitives.Selector.SelectedItem"/>) default to
    /// <see cref="TwoWay"/>. This is the value a binding takes when no Mode is specified.
    /// </summary>
    Default,

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
