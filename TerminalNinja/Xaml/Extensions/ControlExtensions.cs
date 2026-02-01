using TerminalNinja.Controls;

namespace TerminalNinja.Xaml.Extensions;

/// <summary>
/// Extension methods for IControl to support name-based lookup.
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
    public static T? FindByName<T>(this IControl root, string name) where T : class, IControl
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        
        return FindByNameRecursive(root, name) as T;
    }
    
    private static IControl? FindByNameRecursive(IControl control, string name)
    {
        // Check if this control matches
        if (control.Name == name)
            return control;
        
        // Search children based on control type
        switch (control)
        {
            case Rectangle rect when rect.Child != null:
                var rectResult = FindByNameRecursive(rect.Child, name);
                if (rectResult != null) return rectResult;
                break;
                
            case StackPanel stackPanel:
                foreach (var child in stackPanel.Children)
                {
                    var stackResult = FindByNameRecursive(child, name);
                    if (stackResult != null) return stackResult;
                }
                break;
        }
        
        return null;
    }
}
