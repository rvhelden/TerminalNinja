namespace TerminalNinja.Aot;

/// <summary>
/// Marks a plain data class (POCO) for source-generator discovery so that
/// property accessors are generated at compile time.  This enables AOT-safe
/// data binding when the class is used as a DataContext or item source in
/// XAML DataTemplates.
///
/// Classes that already inherit from <c>ViewModelBase</c>, implement
/// <c>INotifyPropertyChanged</c>, or inherit from a control base class are
/// discovered automatically and do not need this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class BindableObjectAttribute : Attribute;
