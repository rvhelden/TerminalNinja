using TerminalNinja.Xaml.Data;

namespace TerminalNinja.Xaml.Binding;

/// <summary>
/// Stores binding information on a <see cref="Controls.FrameworkElement"/> for deferred activation.
/// When a XAML tree is loaded without a DataContext (e.g., inside a UserControl's InitializeComponent),
/// bindings cannot be activated immediately. Instead, they are stored as <see cref="ElementBinding"/>
/// records and activated later when DataContext is set on the element.
/// </summary>
/// <param name="TargetPropertyName">The CLR property name on the target element to bind to.</param>
/// <param name="Path">The property path on the source (DataContext) object.</param>
/// <param name="Mode">The binding mode (OneWay, TwoWay, OneTime).</param>
/// <param name="Converter">Optional value converter for the binding.</param>
/// <param name="ConverterParameter">Optional parameter passed to the value converter.</param>
/// <param name="RelativeSource">Optional RelativeSource for resolving the binding source relative to the target element.</param>
public sealed record ElementBinding(
    string TargetPropertyName,
    string Path,
    BindingMode Mode,
    IValueConverter? Converter,
    object? ConverterParameter,
    RelativeSource? RelativeSource = null);
