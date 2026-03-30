using System.ComponentModel;
using System.Windows.Markup;
using TerminalNinja.Aot;
using TerminalNinja.Documents;
using TerminalNinja.Xaml.Binding;

namespace TerminalNinja.Controls;

/// <summary>
/// Describes the visual structure of a data object.
/// Used primarily with ItemsControl to define how data items should be rendered.
/// </summary>
[ContentProperty("TemplateContent")]
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
    public UIElement? TemplateContent { get; set; }

    /// <summary>
    /// Gets or sets a factory function that creates the control tree.
    /// If both TemplateContent and TemplateFactory are set, TemplateFactory takes precedence.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public Func<UIElement>? TemplateFactory { get; set; }

    /// <summary>
    /// Creates a new instance of the control tree defined by this template.
    /// </summary>
    /// <returns>A new control instance, or null if no template is defined.</returns>
    public UIElement? CreateContent()
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
    /// Property names that should be skipped during cloning.
    /// </summary>
    private static readonly HashSet<string> SkipProperties = new(StringComparer.Ordinal)
    {
        "Parent",
        "DataContext"
    };

    /// <summary>
    /// Creates a deep clone of a control tree using the AOT-compatible registries.
    /// Uses ControlFactoryRegistry for instance creation and PropertyAccessorRegistry for property access.
    /// Also clones pending bindings so that deferred binding activation works on cloned elements.
    /// </summary>
    private static UIElement? CloneControl(UIElement source)
    {
        var sourceType = source.GetType();

        // Create instance via factory registry (AOT-safe, no Activator.CreateInstance)
        if (!ControlFactoryRegistry.TryCreate(sourceType, out var instance))
        {
            throw new InvalidOperationException(
                $"No factory registered for type '{sourceType.FullName}'. " +
                $"Ensure the type is discovered by the source generator.");
        }

        if (instance is not UIElement clone)
        {
            return null;
        }

        // Copy properties via accessor registry (AOT-safe, no reflection)
        foreach (var kvp in PropertyAccessorRegistry.GetAccessors(sourceType))
        {
            var propertyName = kvp.Key;
            var accessor = kvp.Value;

            // Skip special properties that shouldn't be cloned
            if (SkipProperties.Contains(propertyName))
            {
                continue;
            }

            // Skip read-only properties — except InlineCollection which must be
            // cloned into the target (TextBlock.Inlines, Span.Inlines are read-only).
            if (!accessor.CanWrite)
            {
                if (accessor.Getter(source) is InlineCollection { Count: > 0 } sourceInlines
                    && accessor.Getter(clone) is InlineCollection cloneInlines)
                {
                    foreach (var inline in sourceInlines)
                    {
                        var clonedInline = CloneControl(inline);
                        if (clonedInline is Inline clonedInlineTyped)
                        {
                            cloneInlines.Add(clonedInlineTyped);
                        }
                    }
                }
                continue;
            }

            try
            {
                var value = accessor.Getter(source);

                // Handle special cases
                if (value is UIElement childControl)
                {
                    // Recursively clone child controls
                    value = CloneControl(childControl);
                }
                else if (value is IList<UIElement> childrenList)
                {
                    // Clone children in collections (e.g., Panel.Children)
                    var cloneList = accessor.Getter(clone) as IList<UIElement>;
                    if (cloneList != null)
                    {
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
                }

                accessor.Setter!(clone, value);
            }
            catch
            {
                // Skip properties that can't be copied
                // This is expected for some complex types or properties with constraints
            }
        }

        // Clone bindings from the prototype to the clone.
        // Iterate all binding expressions on the source, extract the parent Binding description,
        // and set a fresh binding on the clone's corresponding DependencyProperty.
        if (source is FrameworkElement && clone is FrameworkElement)
        {
            foreach (var (dp, expression) in source.GetAllExpressions())
            {
                if (expression is BindingExpressionBase bindingExpr)
                {
                    var parentBinding = bindingExpr.ParentBindingBase;
                    if (parentBinding != null)
                    {
                        BindingOperations.SetBinding(clone, dp, parentBinding);
                    }
                }
            }
        }

        return clone;
    }
}
