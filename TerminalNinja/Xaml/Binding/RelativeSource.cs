namespace TerminalNinja.Xaml.Binding;

/// <summary>
/// Describes the location of the binding source relative to the position of the binding target.
/// Used with <c>{Binding ... RelativeSource={RelativeSource ...}}</c> markup extension syntax.
/// Mirrors WPF's <c>System.Windows.Data.RelativeSource</c>.
/// </summary>
public sealed class RelativeSource
{
    /// <summary>
    /// Creates a new <see cref="RelativeSource"/> with the specified mode.
    /// </summary>
    public RelativeSource(RelativeSourceMode mode)
    {
        Mode = mode;
    }

    /// <summary>
    /// Parameterless constructor for XAML parser use.
    /// </summary>
    public RelativeSource() { }

    /// <summary>
    /// Gets or sets the mode that describes the location of the binding source
    /// relative to the binding target.
    /// </summary>
    public RelativeSourceMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the type of ancestor to look for when <see cref="Mode"/>
    /// is <see cref="RelativeSourceMode.FindAncestor"/>.
    /// </summary>
    public Type? AncestorType { get; set; }

    /// <summary>
    /// Gets or sets the level of ancestor to look for (1-based) in
    /// <see cref="RelativeSourceMode.FindAncestor"/> mode. Defaults to 1 (first match).
    /// </summary>
    public int AncestorLevel { get; set; } = 1;

    /// <summary>
    /// Returns a cached <see cref="RelativeSource"/> for <see cref="RelativeSourceMode.Self"/>.
    /// </summary>
    public static RelativeSource Self { get; } = new(RelativeSourceMode.Self);

    /// <summary>
    /// Returns a cached <see cref="RelativeSource"/> for <see cref="RelativeSourceMode.TemplatedParent"/>.
    /// </summary>
    public static RelativeSource TemplatedParent { get; } = new(RelativeSourceMode.TemplatedParent);

    /// <summary>
    /// Resolves the binding source object for a given target element.
    /// Walks the visual tree (via <see cref="Controls.Visual.Parent"/>) to find the appropriate source.
    /// </summary>
    /// <param name="target">The binding target element.</param>
    /// <returns>The resolved source object, or <c>null</c> if not found.</returns>
    public object? ResolveSource(Controls.FrameworkElement target)
    {
        ArgumentNullException.ThrowIfNull(target);

        return Mode switch
        {
            RelativeSourceMode.Self => target,
            RelativeSourceMode.FindAncestor => FindAncestor(target),
            RelativeSourceMode.TemplatedParent => null, // Template system not yet implemented
            _ => null
        };
    }

    /// <summary>
    /// Walks the <see cref="Controls.Visual.Parent"/> chain to find an ancestor
    /// matching <see cref="AncestorType"/> at the specified <see cref="AncestorLevel"/>.
    /// </summary>
    private object? FindAncestor(Controls.Visual target)
    {
        if (AncestorType == null)
            throw new InvalidOperationException(
                "AncestorType must be set when using RelativeSourceMode.FindAncestor.");

        if (AncestorLevel < 1)
            throw new InvalidOperationException(
                $"AncestorLevel must be >= 1, but was {AncestorLevel}.");

        var current = target.Parent;
        var matchCount = 0;

        while (current != null)
        {
            if (AncestorType.IsInstanceOfType(current))
            {
                matchCount++;
                if (matchCount >= AncestorLevel)
                    return current;
            }

            current = current.Parent;
        }

        return null; // Ancestor not found — WPF returns null silently
    }
}
