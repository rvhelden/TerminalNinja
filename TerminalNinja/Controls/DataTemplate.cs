using System.ComponentModel;
using Portable.Xaml.Markup;
using SWM = System.Windows.Markup;

namespace TerminalNinja.Controls;

/// <summary>
/// Describes the visual structure of a data object.
/// Used primarily with ItemsControl to define how data items should be rendered.
/// </summary>
[ContentProperty("TemplateContent")]
[SWM.ContentProperty("TemplateContent")]
public class DataTemplate
{
    /// <summary>
    /// Gets or sets the data type for which this template is intended.
    /// This is primarily used for implicit DataTemplate selection (future feature).
    /// </summary>
    public Type? DataType { get; set; }

    /// <summary>
    /// Gets or sets the control tree that defines the visual structure.
    /// This control will be cloned for each data item.
    /// </summary>
    public IControl? TemplateContent { get; set; }

    /// <summary>
    /// Gets or sets a factory function that creates the control tree.
    /// If both TemplateContent and TemplateFactory are set, TemplateFactory takes precedence.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public Func<IControl>? TemplateFactory { get; set; }

    /// <summary>
    /// Creates a new instance of the control tree defined by this template.
    /// </summary>
    /// <returns>A new control instance, or null if no template is defined.</returns>
    public IControl? CreateContent()
    {
        // Factory function takes precedence
        if (TemplateFactory != null)
        {
            return TemplateFactory();
        }

        // Clone the template content
        if (TemplateContent != null)
        {
            return CloneControl(TemplateContent);
        }

        return null;
    }

    /// <summary>
    /// Creates a deep clone of a control tree.
    /// This is a simplified implementation that creates a new instance and copies properties.
    /// </summary>
    private static IControl? CloneControl(IControl source)
    {
        // For now, we use a simple reflection-based approach
        // In the future, this could be optimized with XAML re-parsing or compiled expressions
        
        var sourceType = source.GetType();
        var clone = Activator.CreateInstance(sourceType) as IControl;
        
        if (clone == null)
        {
            return null;
        }

        // Copy public properties
        foreach (var prop in sourceType.GetProperties())
        {
            // Skip read-only properties and collections
            if (!prop.CanWrite || !prop.CanRead)
                continue;

            // Skip special properties that shouldn't be cloned
            if (prop.Name is "Parent" or "DataContext")
                continue;

            try
            {
                var value = prop.GetValue(source);
                
                // Handle special cases
                if (value is IControl childControl)
                {
                    // Recursively clone child controls
                    value = CloneControl(childControl);
                }
                else if (value is IList<IControl> childrenList && prop.GetValue(clone) is IList<IControl> cloneList)
                {
                    // Clone children in collections (e.g., Panel.Children)
                    foreach (var child in childrenList)
                    {
                        var clonedChild = CloneControl(child);
                        if (clonedChild != null)
                        {
                            cloneList.Add(clonedChild);
                        }
                    }
                    continue; // Don't set the property again
                }
                
                prop.SetValue(clone, value);
            }
            catch
            {
                // Skip properties that can't be copied
                // This is expected for some complex types or properties with constraints
            }
        }

        return clone;
    }
}
