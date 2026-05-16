using TerminalNinja.Ansi;
using TerminalNinja.Buffers;
using TerminalNinja.Console;
using TerminalNinja.Controls;
using TerminalNinja.Primitives;

namespace TerminalNinja.Rendering;

/// <summary>
/// Main renderer that orchestrates the rendering pipeline with zero-allocation diffing.
/// </summary>
public sealed class Renderer : IDisposable
{
    private readonly CellBuffer _buffer;
    private readonly ICellSink _sink;
    private readonly TerminalGuard? _guard;
    private readonly ITerminal? _terminal;
    private bool _disposed;

    /// <summary>Gets the width of the rendering viewport.</summary>
    public int Width => _buffer.Width;

    /// <summary>Gets the height of the rendering viewport.</summary>
    public int Height => _buffer.Height;

    /// <summary>Gets the viewport rectangle (full screen).</summary>
    public Rect Viewport => new(0, 0, Width, Height);

    /// <summary>
    /// Creates a new renderer using the system terminal (production use).
    /// </summary>
    public Renderer() : this(SystemTerminal.Instance)
    {
    }

    /// <summary>
    /// Creates a new renderer with dependency injection for testing.
    /// </summary>
    /// <param name="terminal">The terminal abstraction to use.</param>
    public Renderer(ITerminal terminal)
    {
        _terminal = terminal;
        var stdout = terminal.OpenOutput();
        var writer = new AnsiWriter(stdout);
        _sink = writer;
        _guard = TerminalGuard.Enter(writer, terminal);
        _buffer = new CellBuffer(terminal.Width, terminal.Height);
    }

    /// <summary>
    /// Creates a renderer that writes to an arbitrary <see cref="ICellSink"/>.
    /// Used by tests and non-ANSI backends (e.g. a future GPU sink); no terminal interaction.
    /// </summary>
    /// <param name="sink">The destination sink for cell writes.</param>
    /// <param name="width">The viewport width in columns.</param>
    /// <param name="height">The viewport height in rows.</param>
    public Renderer(ICellSink sink, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sink = sink;
        _buffer = new CellBuffer(width, height);
    }

    /// <summary>
    /// Creates an offscreen renderer that writes ANSI sequences to the given stream.
    /// No terminal interaction (no cursor hide/show, no ANSI mode toggling).
    /// Useful for CLI snapshot tools, piping output, and WASM scenarios.
    /// </summary>
    /// <param name="output">The output stream to write ANSI sequences to.</param>
    /// <param name="width">The viewport width in columns.</param>
    /// <param name="height">The viewport height in rows.</param>
    /// <returns>A renderer that writes to the stream.</returns>
    public static Renderer CreateOffscreen(Stream output, int width, int height)
    {
        return new Renderer(output, width, height);
    }

    /// <summary>
    /// Creates a renderer that writes ANSI sequences to an arbitrary stream with explicit
    /// dimensions. Used by <see cref="CreateOffscreen"/> and by tests that need to inspect
    /// the rendered byte stream.
    /// </summary>
    internal Renderer(Stream output, int width, int height)
        : this(new AnsiWriter(output), width, height)
    {
    }
    
    /// <summary>
    /// Clears the entire rendering buffer.
    /// </summary>
    public void Clear()
    {
        _buffer.Clear();
    }
    
    /// <summary>
    /// Draws a UIElement to the buffer (does not display until Present() is called).
    /// Exposes the active sink to controls via <see cref="CellBuffer.ActiveSink"/> for the
    /// duration of the call so capability-aware controls (e.g. <c>TextBlock</c> when an
    /// <see cref="IShapedRunSink"/> is attached) can emit higher-level operations alongside
    /// their per-cell writes.
    /// </summary>
    /// <param name="control">The control to draw.</param>
    public void Draw(UIElement control)
    {
        _buffer.ActiveSink = _sink;
        try
        {
            control.Render(_buffer, Viewport);
        }
        finally
        {
            _buffer.ActiveSink = null;
        }
    }
    
    /// <summary>
    /// Dims the entire buffer to create a backdrop effect for modal overlays.
    /// Should be called after <see cref="Draw"/> and before drawing overlay content.
    /// </summary>
    public void DimBackground()
    {
        _buffer.DimAll();
    }
    
    /// <summary>
    /// Draws an overlay UIElement on top of the existing buffer content.
    /// Unlike <see cref="Draw"/>, this does NOT clear the buffer first —
    /// the overlay paints over whatever is already in the buffer.
    /// </summary>
    /// <param name="overlay">The overlay control to draw.</param>
    public void DrawOverlay(UIElement overlay)
    {
        _buffer.ActiveSink = _sink;
        try
        {
            overlay.Render(_buffer, Viewport);
        }
        finally
        {
            _buffer.ActiveSink = null;
        }
    }
    
    /// <summary>
    /// Presents the rendered frame to the terminal with zero-allocation diffing.
    /// Only changed cells are transmitted to the terminal.
    /// </summary>
    public void Present()
    {
        // BeginFrame invalidates per-frame state (e.g. cursor tracking on the ANSI sink)
        // so the first cell always emits absolute positioning. Without this, stale state
        // from the previous frame can cause rendering corruption (characters at wrong positions).
        _sink.BeginFrame();

        // Zero-allocation iteration using struct enumerator
        foreach (var change in _buffer.GetChanges())
        {
            _sink.WriteCell(change.X, change.Y, change.Cell);
        }

        _sink.EndFrame();
        _buffer.SwapBuffers();
    }
    
    /// <summary>
    /// Resizes the renderer to the specified dimensions.
    /// </summary>
    /// <param name="newWidth">The new width in columns.</param>
    /// <param name="newHeight">The new height in rows.</param>
    public void Resize(int newWidth, int newHeight)
    {
        if (newWidth == Width && newHeight == Height)
        {
            return; // No change needed
        }

        _buffer.Resize(newWidth, newHeight);

        // Resize on the sink resets style state and clears the surface so the
        // diff-based Present() starts from a known-clean baseline. Without this,
        // stale SGR attributes from the previous frame would bleed into the new
        // surface, and unchanged cells outside the content area would not be repainted.
        _sink.Resize(newWidth, newHeight);
    }
    
    /// <summary>
    /// Dumps the current screen buffer contents to a file for debugging.
    /// The dump includes a human-readable representation of the screen with color information.
    /// </summary>
    /// <param name="filePath">The path where the dump file will be saved. If null, uses a timestamped filename.</param>
    /// <returns>The path to the created dump file.</returns>
    public string DumpScreen(string? filePath = null)
    {
        // Generate default filename if not provided
        if (string.IsNullOrEmpty(filePath))
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            filePath = $"screen_dump_{timestamp}.txt";
        }
        
        _buffer.SwapBuffers(); // Ensure we dump the latest buffer
        // Get the dump string from the buffer
        var dumpContent = _buffer.DumpToString();
        _buffer.SwapBuffers(); // Ensure we dump the latest buffer
        
        // Write to file
        File.WriteAllText(filePath, dumpContent);
        
        return filePath;
    }
    
    /// <summary>
    /// Resets the sink's style state and flushes any pending output. For the ANSI
    /// sink this emits the SGR reset sequence; for other sinks it is implementation-defined.
    /// </summary>
    public void WriteReset()
    {
        _sink.Reset();
        _sink.EndFrame();
    }

    /// <summary>
    /// Handles terminal resize events by recreating the buffer.
    /// </summary>
    public void HandleResize()
    {
        if (_terminal == null)
        {
            return; // Test renderer, no resize support
        }

        var newWidth = _terminal.Width;
        var newHeight = _terminal.Height;
        
        Resize(newWidth, newHeight);
    }
    
    /// <summary>
    /// Disposes the renderer and restores the terminal to its original state.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _buffer.Dispose();
        _guard?.Dispose();
        _sink.Dispose();
    }
}
