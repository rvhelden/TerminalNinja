using System.Runtime.CompilerServices;
using TerminalNinja.Primitives;

namespace TerminalNinja.Buffers;

/// <summary>
/// Tracks a dirty rectangle region for optimized rendering.
/// </summary>
public struct DirtyRect
{
    /// <summary>Minimum X coordinate (inclusive).</summary>
    public int MinX;
    
    /// <summary>Minimum Y coordinate (inclusive).</summary>
    public int MinY;
    
    /// <summary>Maximum X coordinate (inclusive).</summary>
    public int MaxX;
    
    /// <summary>Maximum Y coordinate (inclusive).</summary>
    public int MaxY;
    
    /// <summary>Whether any cells have been marked dirty.</summary>
    public bool IsDirty;
    
    /// <summary>
    /// Expands the dirty region to include the specified point.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Expand(int x, int y)
    {
        if (!IsDirty)
        {
            MinX = MaxX = x;
            MinY = MaxY = y;
            IsDirty = true;
        }
        else
        {
            if (x < MinX)
            {
                MinX = x;
            }

            if (x > MaxX)
            {
                MaxX = x;
            }

            if (y < MinY)
            {
                MinY = y;
            }

            if (y > MaxY)
            {
                MaxY = y;
            }
        }
    }
    
    /// <summary>
    /// Resets the dirty region.
    /// </summary>
    public void Reset() => IsDirty = false;
    
    /// <summary>
    /// Converts the dirty region to a rectangle.
    /// </summary>
    public readonly Rect ToRect() => new(MinX, MinY, MaxX - MinX + 1, MaxY - MinY + 1);
}
