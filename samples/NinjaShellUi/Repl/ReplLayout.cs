using TerminalNinja.Primitives;

namespace NinjaShellUi;

/// <summary>
/// Per-frame layout for <see cref="ReplView"/>. Replaces the mutable
/// <c>_lastBounds</c> / <c>_lastInputTopY</c> / <c>_lastOutputHeight</c> /
/// <c>_lastInputLines</c> quartet — the layout is recomputed once at the top of
/// each render and threaded through the renderers + handed off to mouse/key
/// paths so they share one source of truth.
/// </summary>
internal readonly record struct ReplLayout(
    Rect Bounds,
    int OutputHeight,
    int InputTopY,
    int InputLines,
    int DiagY,
    int HoverY,
    int PromptWidth)
{
    public int InputBottomY => Bounds.Y + Bounds.Height - 1;
    public int InputX => Bounds.X + PromptWidth;
    public int InputWidth => Math.Max(0, Bounds.Width - PromptWidth);
    public bool IsEmpty => Bounds.Width <= 0 || Bounds.Height <= 0;

    public static ReplLayout Compute(
        Rect bounds,
        int rawInputLines,
        bool hasDiagnostic,
        bool hasHover,
        int promptWidth)
    {
        // The input region grows downward from a baseline near the bottom: it always
        // occupies inputLines rows (>= 1), and the optional hover / diagnostic rows
        // sit above it. Clamp inputLines to half the panel height so a runaway
        // multi-line buffer can't swallow the entire output area.
        var inputLines = Math.Min(rawInputLines, Math.Max(1, bounds.Height / 2));
        var inputBottomY = bounds.Y + bounds.Height - 1;
        var inputTopY = inputBottomY - (inputLines - 1);
        var diagY = hasDiagnostic ? inputTopY - 1 : -1;
        var hoverY = (hasHover && diagY > 0) ? diagY - 1
                   : hasHover ? inputTopY - 1
                   : -1;

        var topReserved = inputLines + (diagY > -1 ? 1 : 0) + (hoverY > -1 ? 1 : 0);
        var outputHeight = Math.Max(0, bounds.Height - topReserved);

        return new ReplLayout(bounds, outputHeight, inputTopY, inputLines, diagY, hoverY, promptWidth);
    }
}
