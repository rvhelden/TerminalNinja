namespace TerminalNinja.Aot;

/// <summary>
/// Excludes a type from <c>ControlFactoryRegistry</c> registration by the source generator.
/// <para>
/// The generator registers every non-abstract type deriving from a bindable base (including
/// <c>ViewModelBase</c>) with a <c>static () =&gt; new T()</c> factory, which forces a
/// parameterless constructor onto types that are never instantiated from XAML. Apply this
/// attribute to view models that take constructor dependencies; property accessors for data
/// binding are still generated.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SkipFactoryRegistrationAttribute : Attribute;
