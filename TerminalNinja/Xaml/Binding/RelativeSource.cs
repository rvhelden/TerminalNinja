using System.Windows.Data;
using System.Windows.Markup;

namespace TerminalNinja.Xaml.Binding;

/// <summary>
/// Describes the location of the binding source relative to the position of the binding target.
/// Used with <c>{Binding ... RelativeSource={RelativeSource ...}}</c> markup extension syntax.
/// Mirrors WPF's <c>System.Windows.Data.RelativeSource</c>.
/// </summary>
public sealed class RelativeSource
{
    public static RelativeSource PreviousData { get; set; } = null!;
    public static RelativeSource Self { get; set; } = null!;
    public static RelativeSource TemplatedParent { get; set; } = null!;
    
    [ConstructorArgument("mode")]
    public RelativeSourceMode Mode { get; set; }
    public int AncestorLevel { get; set; }
    public Type? AncestorType { get; set; }

    public RelativeSource()
    {
        // default mode to FindAncestor so that setting Type and Level would be OK
        Mode = RelativeSourceMode.FindAncestor;
    }

    public RelativeSource(RelativeSourceMode mode)
    {
        Mode = mode;
    }

    public RelativeSource(RelativeSourceMode mode, Type ancestorType, int ancestorLevel)
    {
        Mode = mode;
        AncestorType = ancestorType;
        AncestorLevel = ancestorLevel;
    }
}
