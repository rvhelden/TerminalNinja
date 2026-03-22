using System.Runtime.CompilerServices;

namespace TerminalNinja.Primitives;

/// <summary>
/// Represents a rectangle with position and size (16 bytes).
/// </summary>
public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    /// <summary>Gets the right edge (X + Width).</summary>
    public int Right => X + Width;
    
    /// <summary>Gets the bottom edge (Y + Height).</summary>
    public int Bottom => Y + Height;
    
    /// <summary>
    /// Checks if the rectangle contains the specified point.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(int x, int y) => 
        x >= X && x < Right && y >= Y && y < Bottom;
    
    /// <summary>
    /// Returns the intersection of this rectangle with another.
    /// </summary>
    public Rect Intersect(Rect other)
    {
        var x1 = Math.Max(X, other.X);
        var y1 = Math.Max(Y, other.Y);
        var x2 = Math.Min(Right, other.Right);
        var y2 = Math.Min(Bottom, other.Bottom);
        
        if (x2 <= x1 || y2 <= y1)
        {
            return new Rect(0, 0, 0, 0);
        }

        return new Rect(x1, y1, x2 - x1, y2 - y1);
    }
    
    /// <summary>
    /// Checks if this rectangle overlaps with another.
    /// </summary>
    public bool Overlaps(Rect other) =>
        X < other.Right && Right > other.X &&
        Y < other.Bottom && Bottom > other.Y;
}
