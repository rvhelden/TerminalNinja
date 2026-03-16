using System.Collections.Concurrent;

namespace TerminalNinja.Aot;

/// <summary>
/// AOT-safe registry for attached property setters.
/// Replaces <c>Type.GetMethod("SetXxx")</c> + <c>MethodInfo.Invoke()</c> which are not trim-safe.
/// Maps (ownerType, propertyName) → setter delegate + parameter type.
/// Populated at startup by source-generated <c>[ModuleInitializer]</c> code.
/// </summary>
public static class AttachedPropertySetterRegistry
{
    private static readonly ConcurrentDictionary<(Type OwnerType, string PropertyName), AttachedPropertySetter> Setters = new();

    /// <summary>
    /// Registers an attached property setter.
    /// </summary>
    public static void Register(Type ownerType, string propertyName, Type parameterType, Action<object, object?> setter)
    {
        Setters[(ownerType, propertyName)] = new AttachedPropertySetter(parameterType, setter);
    }

    /// <summary>
    /// Tries to get a setter for the specified attached property.
    /// </summary>
    public static bool TryGetSetter(Type ownerType, string propertyName, out AttachedPropertySetter setter)
    {
        return Setters.TryGetValue((ownerType, propertyName), out setter);
    }
}

/// <summary>
/// Holds the parameter type and setter delegate for an attached property.
/// </summary>
public readonly record struct AttachedPropertySetter(
    Type ParameterType,
    Action<object, object?> Setter);
