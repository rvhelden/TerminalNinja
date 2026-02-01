using System.Runtime.CompilerServices;

namespace TerminalNinja.Primitives;

/// <summary>
/// Represents a size value with a mode (absolute, relative, or stretch).
/// </summary>
public readonly record struct Size
{
    /// <summary>Gets the numeric value of the size.</summary>
    public readonly float Value;
    
    /// <summary>Gets the mode that determines how the value is interpreted.</summary>
    public readonly SizeMode Mode;
    
    private Size(float value, SizeMode mode)
    {
        Value = value;
        Mode = mode;
    }
    
    /// <summary>
    /// Creates an absolute size in cells/characters.
    /// </summary>
    public static Size Absolute(int pixels) => new(pixels, SizeMode.Absolute);
    
    /// <summary>
    /// Creates a relative size as a percentage of the parent (0-100).
    /// </summary>
    public static Size Percent(float percent) => new(percent / 100f, SizeMode.Relative);
    
    /// <summary>
    /// Creates a size that fills all available space.
    /// </summary>
    public static Size Stretch => new(1f, SizeMode.Stretch);
    
    /// <summary>
    /// Resolves this size to an absolute value based on the parent size.
    /// </summary>
    /// <param name="parentSize">The size of the parent container.</param>
    /// <returns>The resolved absolute size in cells.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Resolve(int parentSize) => Mode switch
    {
        SizeMode.Absolute => (int)Value,
        SizeMode.Relative => (int)(Value * parentSize),
        SizeMode.Stretch => parentSize,
        _ => 0
    };
}
