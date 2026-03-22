using TerminalNinja.Controls;
using TerminalNinja.Primitives;

namespace TerminalNinja.Xaml.Extensions;

/// <summary>
/// Extension methods for UIElement to support name-based lookup.
/// </summary>
public static class ControlExtensions
{
    /// <summary>
    /// Finds a child control by name within the control tree.
    /// </summary>
    /// <typeparam name="T">The expected type of the control.</typeparam>
    /// <param name="root">The root control to search from.</param>
    /// <param name="name">The name to search for (case-sensitive).</param>
    /// <returns>The found control, or null if not found.</returns>
    public static T? FindByName<T>(this UIElement root, string name) where T : FrameworkElement
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        
        return FindByNameRecursive(root, name) as T;
    }
    
    private static FrameworkElement? FindByNameRecursive(UIElement control, string name)
    {
        // Check if this control matches
        if (control is FrameworkElement fe && fe.Name == name)
        {
            return fe;
        }

        // Use visual tree traversal — works for all container types
        var dummyBounds = new Rect(0, 0, 1000, 1000);
        var myBounds = control.CalculateBounds(dummyBounds);
        foreach (var (child, _) in control.GetChildrenWithBounds(myBounds))
        {
            if (child is UIElement childElement)
            {
                var result = FindByNameRecursive(childElement, name);
                if (result != null)
                {
                    return result;
                }
            }
        }
        
        return null;
    }
}
