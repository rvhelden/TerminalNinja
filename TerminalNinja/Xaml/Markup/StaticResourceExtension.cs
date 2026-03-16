using Portable.Xaml;
using Portable.Xaml.Markup;

namespace TerminalNinja.Xaml.Markup;

/// <summary>
/// Markup extension for static resource lookup: {StaticResource Key}
/// Resources are resolved after XAML parsing is complete, walking up the visual tree.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public class StaticResourceExtension : MarkupExtension
{
    // Per-execution-context storage for pending resource lookups during XAML parsing.
    private static readonly AsyncLocal<List<PendingResourceLookup>?> _pendingLookups = new();
    
    private static List<PendingResourceLookup> PendingLookups =>
        _pendingLookups.Value ??= new List<PendingResourceLookup>();
    
    /// <summary>
    /// Gets or sets the resource key to look up.
    /// </summary>
    public object? ResourceKey { get; set; }
    
    /// <summary>
    /// Constructor with no arguments (ResourceKey must be set via property).
    /// </summary>
    public StaticResourceExtension()
    {
    }
    
    /// <summary>
    /// Constructor with resource key (for shorthand: {StaticResource MyKey}).
    /// </summary>
    public StaticResourceExtension(object resourceKey)
    {
        ResourceKey = resourceKey;
    }
    
    /// <summary>
    /// Provides the value for the resource lookup.
    /// Since we don't have the full visual tree during XAML parsing,
    /// we defer the actual lookup and return a placeholder.
    /// </summary>
    public override object? ProvideValue(IServiceProvider serviceProvider)
    {
        if (ResourceKey == null)
            throw new InvalidOperationException("StaticResource requires a ResourceKey");
        
        // Get the target object and property from the service provider
        var target = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
        if (target?.TargetObject == null || target.TargetProperty == null)
        {
            throw new InvalidOperationException("Cannot resolve StaticResource target");
        }
        
        // Get property info
        string propertyName;
        Type? propertyType = null;
        
        if (target.TargetProperty is System.Reflection.PropertyInfo propInfo)
        {
            propertyName = propInfo.Name;
            propertyType = propInfo.PropertyType;
        }
        else if (target.TargetProperty is XamlMember xamlMember)
        {
            propertyName = xamlMember.Name;
            propertyType = xamlMember.Type?.UnderlyingType;
        }
        else
        {
            throw new InvalidOperationException($"Unsupported target property type: {target.TargetProperty.GetType()}");
        }
        
        // Store lookup for later resolution
        PendingLookups.Add(new PendingResourceLookup(
            target.TargetObject,
            propertyName,
            propertyType,
            ResourceKey));
        
        // Return default value for property type
        if (propertyType != null)
        {
            if (propertyType == typeof(string))
                return string.Empty;
            if (propertyType.IsValueType)
                return Activator.CreateInstance(propertyType);
        }
        
        return null;
    }
    
    /// <summary>
    /// Gets all pending resource lookups and clears the internal storage.
    /// Called by TerminalXaml after XAML parsing is complete.
    /// </summary>
    internal static List<PendingResourceLookup> GetAndClearPendingLookups()
    {
        var lookups = _pendingLookups.Value ?? new List<PendingResourceLookup>();
        _pendingLookups.Value = null;
        return lookups;
    }
}

/// <summary>
/// Information about a pending static resource lookup.
/// </summary>
internal sealed class PendingResourceLookup
{
    public object TargetObject { get; }
    public string PropertyName { get; }
    public Type? PropertyType { get; }
    public object ResourceKey { get; }
    
    public PendingResourceLookup(object targetObject, string propertyName, Type? propertyType, object resourceKey)
    {
        TargetObject = targetObject;
        PropertyName = propertyName;
        PropertyType = propertyType;
        ResourceKey = resourceKey;
    }
}
