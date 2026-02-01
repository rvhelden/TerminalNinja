using TerminalNinja.Core.Elements;

namespace TerminalNinja.Xaml.Internal;

/// <summary>
/// Processes Stack elements after XAML loading to convert attached properties
/// (Stack.SizeMode, Stack.FixedSize) into proper StackChild wrappers.
/// </summary>
internal static class StackChildProcessor
{
    /// <summary>
    /// Processes all Stack elements in the tree, converting children with attached
    /// properties into StackChild wrappers.
    /// </summary>
    public static void ProcessElement(IElement element)
    {
        switch (element)
        {
            case Stack stack:
                ProcessStack(stack);
                break;
                
            case Rectangle rect when rect.Child != null:
                ProcessElement(rect.Child);
                break;
        }
    }
    
    private static void ProcessStack(Stack stack)
    {
        // If Stack already has Children populated, it means they're already wrapped
        // This happens when using programmatic construction
        if (stack.Children.Count > 0)
        {
            // Still recursively process child elements
            foreach (var child in stack.Children)
            {
                ProcessElement(child.Element);
            }
            return;
        }
        
        // In XAML loading, children are typically added via a ContentProperty
        // However, Portable.Xaml might not populate the Children list directly
        // We need to handle this case by wrapping elements that have attached properties
        
        // For now, this is a placeholder - the actual implementation will depend on
        // how Portable.Xaml populates the Stack's Children collection
        // We may need to intercept during object creation or use a custom XamlType
    }
}
