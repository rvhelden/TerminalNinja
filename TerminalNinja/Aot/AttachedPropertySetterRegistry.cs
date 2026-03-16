using System.Collections.Concurrent;
using TerminalNinja.Controls;

namespace TerminalNinja.Aot;

/// <summary>
/// AOT-safe registry for attached property setters.
/// Replaces <c>Type.GetMethod("SetXxx")</c> + <c>MethodInfo.Invoke()</c> which are not trim-safe.
/// Maps (ownerType, propertyName) → setter delegate + parameter type.
/// </summary>
public static class AttachedPropertySetterRegistry
{
    private static readonly ConcurrentDictionary<(Type OwnerType, string PropertyName), AttachedPropertySetter> Setters = new();

    /// <summary>
    /// Static initializer registers all known attached property setters.
    /// </summary>
    static AttachedPropertySetterRegistry()
    {
        // Grid attached properties
        Register(typeof(Grid), "Row", typeof(int),
            (target, value) => Grid.SetRow(target, (int)value!));
        Register(typeof(Grid), "Column", typeof(int),
            (target, value) => Grid.SetColumn(target, (int)value!));
        Register(typeof(Grid), "RowSpan", typeof(int),
            (target, value) => Grid.SetRowSpan(target, (int)value!));
        Register(typeof(Grid), "ColumnSpan", typeof(int),
            (target, value) => Grid.SetColumnSpan(target, (int)value!));

        // StackPanel attached properties
        Register(typeof(StackPanel), "SizeMode", typeof(ChildSizeMode),
            (target, value) => StackPanel.SetSizeMode(target, (ChildSizeMode)value!));
        Register(typeof(StackPanel), "FixedSize", typeof(int),
            (target, value) => StackPanel.SetFixedSize(target, (int)value!));
    }

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
