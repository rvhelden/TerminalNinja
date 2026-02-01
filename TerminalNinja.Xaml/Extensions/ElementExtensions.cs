using TerminalNinja.Core.Elements;

namespace TerminalNinja.Xaml.Extensions;

/// <summary>
/// Extension methods for IElement to support name-based lookup.
/// </summary>
public static class ElementExtensions
{
    /// <summary>
    /// Finds a child element by name within the element tree.
    /// </summary>
    /// <typeparam name="T">The expected type of the element.</typeparam>
    /// <param name="root">The root element to search from.</param>
    /// <param name="name">The name to search for (case-sensitive).</param>
    /// <returns>The found element, or null if not found.</returns>
    public static T? FindByName<T>(this IElement root, string name) where T : class, IElement
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        
        return FindByNameRecursive(root, name) as T;
    }
    
    private static IElement? FindByNameRecursive(IElement element, string name)
    {
        // Check if this element matches
        if (element.Name == name)
            return element;
        
        // Search children based on element type
        switch (element)
        {
            case Rectangle rect when rect.Child != null:
                var rectResult = FindByNameRecursive(rect.Child, name);
                if (rectResult != null) return rectResult;
                break;
                
            case Stack stack:
                foreach (var child in stack.Children)
                {
                    var stackResult = FindByNameRecursive(child.Element, name);
                    if (stackResult != null) return stackResult;
                }
                break;
        }
        
        return null;
    }
}
