using TerminalNinja.Elements;
using TerminalNinja.Primitives;

namespace TerminalNinja.Xaml.Collections;

/// <summary>
/// A collection for Stack children that automatically wraps IElement instances
/// into StackChild by reading attached properties.
/// </summary>
public class StackChildCollection : List<StackChild>
{
    /// <summary>
    /// Adds an element to the collection, automatically wrapping it in a StackChild
    /// by reading the Stack.SizeMode and Stack.FixedSize attached properties.
    /// </summary>
    public void Add(IElement element)
    {
        var sizeMode = Stack.GetSizeMode(element);
        var fixedSize = Stack.GetFixedSize(element);
        
        var stackChild = new StackChild
        {
            Element = element,
            SizeMode = sizeMode,
            FixedSize = fixedSize
        };
        
        Add(stackChild);
    }
}
