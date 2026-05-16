using TerminalNinja.Primitives;

namespace TerminalNinja.Rendering;

/// <summary>
/// An in-memory <see cref="ICellSink"/> that records every call. Useful for tests
/// that need to assert what the renderer produced without going through ANSI encoding,
/// and for diffing sink-level traces between backends.
/// </summary>
public sealed class MemoryCellSink : ICellSink
{
    private readonly List<CellWrite> _writes = [];

    /// <summary>Recorded cell writes in the order they were received.</summary>
    public IReadOnlyList<CellWrite> Writes => _writes;

    /// <summary>Number of <see cref="BeginFrame"/> calls received.</summary>
    public int BeginFrameCount { get; private set; }

    /// <summary>Number of <see cref="EndFrame"/> calls received.</summary>
    public int EndFrameCount { get; private set; }

    /// <summary>Number of <see cref="Reset"/> calls received.</summary>
    public int ResetCount { get; private set; }

    /// <summary>Dimensions last reported via <see cref="Resize"/>, or null if never resized.</summary>
    public (int Width, int Height)? LastResize { get; private set; }

    /// <inheritdoc />
    public void BeginFrame() => BeginFrameCount++;

    /// <inheritdoc />
    public void WriteCell(int x, int y, Cell cell) => _writes.Add(new CellWrite(x, y, cell));

    /// <inheritdoc />
    public void EndFrame() => EndFrameCount++;

    /// <inheritdoc />
    public void Reset()
    {
        ResetCount++;
        _writes.Clear();
    }

    /// <inheritdoc />
    public void Resize(int width, int height) => LastResize = (width, height);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>A single cell write recorded by <see cref="MemoryCellSink"/>.</summary>
    public readonly record struct CellWrite(int X, int Y, Cell Cell);
}
