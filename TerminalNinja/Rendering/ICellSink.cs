using TerminalNinja.Primitives;

namespace TerminalNinja.Rendering;

/// <summary>
/// Backend-agnostic destination for cell writes from the renderer.
/// <see cref="Ansi.AnsiWriter"/> is the default ANSI/VT100 implementation;
/// alternative implementations (e.g. a GPU-backed sink) can be plugged in
/// without changing the renderer pipeline.
/// </summary>
public interface ICellSink : IDisposable
{
    /// <summary>
    /// Called once at the start of each frame, before any <see cref="WriteCell"/> calls.
    /// Implementations should invalidate any per-frame state caches (e.g. cursor position).
    /// </summary>
    void BeginFrame();

    /// <summary>
    /// Writes a single cell at the given coordinates. Called by the renderer in
    /// dirty-rect order; the implementation is free to buffer internally.
    /// </summary>
    void WriteCell(int x, int y, Cell cell);

    /// <summary>
    /// Called once at the end of each frame. Implementations must flush any
    /// pending output so the frame is visible to the consumer.
    /// </summary>
    void EndFrame();

    /// <summary>
    /// Resets all style state and emits the implementation-specific reset commands.
    /// Used for terminal/screen cleanup outside of a normal frame.
    /// </summary>
    void Reset();

    /// <summary>
    /// Notifies the sink that the surface dimensions have changed. Implementations
    /// should reset style state and clear/recreate their drawing surface.
    /// </summary>
    void Resize(int width, int height);
}
